using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class RunManager : MonoBehaviour
    {
        public const int YamaUnitId = 61;
        public static RunManager Instance { get; private set; }

        public bool RunActive { get; private set; }
        public GameMode CurrentMode { get; private set; } = GameMode.Training;

        // 런 범위 상태: 서포트 우정도 (런 시작 시 초기화, 저장/복원 대상)
        public SupportBondState SupportBonds { get; } = new();

        // 런 범위 상태: 훈련 체력 · 훈련 레벨 · 스킬 Pt · 컨디션 (저장/복원 대상)
        public TrainingState Training { get; } = new();

        // 런 범위 상태: 강화제. 전투가 끝날 때마다 남은 전투 수가 하나씩 줄어든다.
        public PartyTonicState PartyTonics { get; } = new();

        // 런 범위 상태: 스킬 힌트. 훈련에서 쌓고 준비 페이즈에서 스킬 Pt로 배운다.
        public SkillHintState SkillHints { get; } = new();
        private readonly HashSet<string> _triggeredEventIds = new();

        public static void DestroyInstance()
        {
            if (Instance == null) return;

            var instance = Instance;
            Instance = null;
            Destroy(instance.gameObject);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void StartRun(GameMode mode = GameMode.Training)
        {
            CurrentMode = mode;
            RunActive = true;
            SupportBonds.Clear();
            Training.Reset();
            PartyTonics.Clear();
            SkillHints.Clear();
            _triggeredEventIds.Clear();
            SaveSystem.DeleteSave();
        }

        /// <summary>
        /// 플레이어가 직접 저장하거나 게임을 끌 때 지금 상태를 저장해도 되는지.
        /// 전투 중에는 쓰러진 아군이 저장본에서 빠지고, 보상 선택 중에는 경험치를 받은 채
        /// 같은 전투로 되돌아가며, 캐릭터 선택 중에는 파티가 비어 있다.
        /// 그때는 전투 직전·사건·훈련 때 찍어 둔 자동 저장을 그대로 둔다.
        /// </summary>
        public bool CanSaveNow
        {
            get
            {
                if (!RunActive || GameManager.Instance == null) return false;
                GameState state = GameManager.Instance.gameState;
                return state == GameState.Preparation
                    || state == GameState.TrainingPhase
                    || state == GameState.EventStage;
            }
        }

        public void SaveCurrentRun()
        {
            if (!RunActive || GameManager.Instance == null) return;
            SaveSystem.SaveRun(BuildSaveData());
        }

        public bool LoadSavedRun()
        {
            RunSaveData save = SaveSystem.LoadRun();
            if (save == null)
            {
                StartRun();
                return false;
            }

            RestoreRun(save);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public RunSaveData CaptureDebugSnapshot() => BuildSaveData();
        public void RestoreDebugSnapshot(RunSaveData save) => RestoreRun(save);
#endif

        private void RestoreRun(RunSaveData save)
        {
            RunActive = true;
            CurrentMode = (GameMode)save.gameMode;
            GameManager.Instance.life = save.life;
            GameManager.Instance.KillCount = save.killCount;
            RestoreInventory(save);
            SupportBonds.Restore(save.supportBonds);
            Training.Restore(save.training);
            PartyTonics.Restore(save.partyTonics);
            SkillHints.Restore(save.skillHints);
            SkillHints.RestoreDryTrainings(save.hintDryTrainings);
            _triggeredEventIds.Clear();
            foreach (string eventId in save.triggeredEventIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(eventId)) _triggeredEventIds.Add(eventId);
            }

            RoundManager roundManager = GameManager.Instance.RoundManager;
            roundManager.InitializeStage(Mathf.Max(1, save.currentStage));

            // 편성을 짜기 전에 테마부터 되돌린다. LoadRound가 테마를 보고 적을 세우므로
            // 여기서 심어 두지 않으면 불러올 때마다 추첨이 다시 굴러 라운드 도중에 테마가 갈린다.
            roundManager.RestoreTheme(save.currentThemeId);
            GameManager.Instance.EventScheduler?.Restore(save.pendingEventIds, roundManager.GetEventById);

            RestoreHeroes(save.heroUnits);
            roundManager.LoadRound(Mathf.Max(1, save.currentStage));

            GameManager.Instance.uiManager?.UpdateLifeText();
            GameManager.Instance.EnterNextStageAfterLoad();
            GameManager.Instance.RestorePreparationActionState(save.preparationActionUsed, save.shopPurchaseCount);
            Debug.Log($"[RunManager] 저장 런 복원 - Stage {save.currentStage}, Round {save.currentRound}");
        }

        /// <summary>
        /// 스테이지 전진의 단일 진입점. 보상/사건/육성 등 어느 흐름에서 오든
        /// 런 종료 판정(육성 최대 스테이지 도달, 패턴 소진) 후 다음 스테이지를 로드한다.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (!RunActive) return;

            int previousRound = GameManager.Instance.RoundManager.Round;

            if (CurrentMode == GameMode.Training)
            {
                if (GameManager.Instance.RoundManager.Stage >= GameManager.MaxTrainingStage ||
                    !GameManager.Instance.RoundManager.TryLoadNextRound())
                {
                    CompleteTrainingRun();
                    return;
                }

                ResetUltimateResourcesOnThemeTransition(previousRound);
                // 스테이지 사이의 드문 컨디션 사건. 저장 전에 굴려야 불러오기로 결과를 물릴 수 없다.
                GameManager.Instance.RollStageConditionEvent();
                SaveCurrentRun();
                GameManager.Instance.EnterNextStageAfterLoad();
                return;
            }

            bool hasNextRound = GameManager.Instance.RoundManager.TryLoadNextRound();
            if (!hasNextRound)
            {
                GrantAnyRunCompletionUnlocks();
                RunActive = false;
                SaveSystem.DeleteSave();
                GameManager.Instance.gameState = BaseClasses.BaseEnums.GameState.RunComplete;
                GameManager.LoadMainMenuScene();
                return;
            }

            ResetUltimateResourcesOnThemeTransition(previousRound);
            SaveCurrentRun();
            GameManager.Instance.EnterNextStageAfterLoad();
        }

        /// <summary>1-10 → 2-1처럼 10스테이지 테마 라운드가 바뀔 때 궁극기 자원을 완전히 비운다.</summary>
        private static void ResetUltimateResourcesOnThemeTransition(int previousRound)
        {
            if (GameManager.Instance.RoundManager.Round == previousRound) return;

            foreach (Unit hero in GridManager.Instance.heroList)
            {
                if (hero != null && hero.isActive && !hero.IsEnemy) hero.ClearUltimateResource();
            }
        }

        private void CompleteTrainingRun()
        {
            int mainUnitId = CharacterSelectionManager.Instance?.MainUnitId ?? 0;
            Unit mainUnit = null;
            if (mainUnitId <= 0)
            {
                mainUnit = GridManager.Instance.heroList
                    .FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
                mainUnitId = mainUnit?.ID ?? 0;
            }
            else
            {
                mainUnit = GridManager.Instance.heroList
                    .FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.ID == mainUnitId);
            }

            if (mainUnit != null)
            {
                SaveSystem.AddTrainedCharacterRecord(BuildTrainedCharacterRecord(mainUnit));
            }
            else
            {
                SaveSystem.AddTrainedCharacter(mainUnitId);
            }

            GrantSupportStarterUnlocks(mainUnitId);
            GrantAnyRunCompletionUnlocks();

            RunActive = false;
            SaveSystem.DeleteSave();
            GameManager.Instance.gameState = GameState.RunComplete;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 실제 런을 끝까지 돌리는 검증 도구는 결과를 기록할 한 프레임이 더 필요하다.
            // 검증 세션에서만 Game 씬을 유지하고, 일반 플레이는 종전대로 메인 메뉴로 돌아간다.
            if (DebugMode.SuiteRunning) return;
#endif
            GameManager.LoadMainMenuScene();
        }

        /// <summary>
        /// 성공·실패나 모드와 무관하게 한 번의 런이 끝났을 때 열리는 영구 해금.
        /// 현재는 야마가 이 경로를 사용한다.
        /// </summary>
        internal static void GrantAnyRunCompletionUnlocks()
        {
            if (!SaveSystem.IsStarterUnlocked(YamaUnitId))
                SaveSystem.AddStarterUnlock(YamaUnitId, announce: true);
        }

        /// <summary>
        /// 완주한 런에 <b>서포트로 함께 선</b> 동료 중 <c>unlocksAsStarterOnClear</c>가 붙은 이를
        /// 스타팅 후보로 연다. 우마무스메의 육성마와 같은 결 — 곁에서 한 런을 끝까지 본 서포트가
        /// 다음 런의 주인공이 된다. <c>unlocksUnitIdsOnClear</c>가 있으면
        /// 서포터와 연결된 별도의 메인 캐릭터도 함께 연다.
        ///
        /// <b>메인은 세지 않는다</b> — 메인은 육성 기록 자체가 자격이다.
        /// 편성 기록(<c>SupportUnitIds</c>)과 실제 로스터를 함께 본다. 불러오기로 이어 온 런은
        /// 편성 기록이 비어 있고, 반대로 쓰러진 채 끝난 아군은 <c>ID</c>가 0이라 서로를 메운다.
        /// </summary>
        private static void GrantSupportStarterUnlocks(int mainUnitId)
        {
            var partyIds = new HashSet<int>();
            foreach (int unitId in CharacterSelectionManager.Instance?.SupportUnitIds ?? new List<int>())
            {
                if (unitId > 0) partyIds.Add(unitId);
            }
            foreach (Unit hero in GridManager.Instance?.heroList ?? new List<Unit>())
            {
                if (hero == null || hero.IsEnemy || hero.IsSummon) continue;
                int unitId = hero.ID > 0 ? hero.ID : hero.LastActiveId;
                if (unitId > 0) partyIds.Add(unitId);
            }
            partyIds.Remove(mainUnitId);

            List<UnitData> units = GameManager.Instance?.unitDataList?.units;
            if (units == null) return;

            foreach (int unitId in partyIds)
            {
                UnitData data = units.FirstOrDefault(unit => unit != null && unit.id == unitId);
                if (data == null) continue;

                if (data.unlocksAsStarterOnClear && !SaveSystem.IsStarterUnlocked(unitId))
                {
                    SaveSystem.AddStarterUnlock(unitId, announce: true);
                    Debug.Log($"[RunManager] 서포트 완주 해금 — {data.name}({unitId})이 스타팅 후보가 되었다");
                }

                foreach (int targetId in data.unlocksUnitIdsOnClear ?? new List<int>())
                {
                    if (targetId <= 0 || SaveSystem.IsStarterUnlocked(targetId)) continue;
                    UnitData target = units.FirstOrDefault(unit => unit != null && unit.id == targetId);
                    SaveSystem.AddStarterUnlock(targetId, announce: true);
                    Debug.Log($"[RunManager] 연계 완주 해금 — {data.name}과 함께 완주하여 " +
                              $"{target?.name ?? targetId.ToString()}({targetId})이 스타팅 후보가 되었다");
                }
            }
        }

        private static TrainedCharacterRecord BuildTrainedCharacterRecord(Unit mainUnit)
        {
            long createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PrimaryStatSaveData finalStats = BuildPrimaryStatSnapshot(mainUnit);
            List<LearnedPassiveSaveData> passiveRecords = mainUnit.LearnedPassiveRecords
                .Where(record => record != null && record.codeId > 0)
                .Select(record => new LearnedPassiveSaveData
                {
                    codeId = record.codeId,
                    stage = Mathf.Max(1, record.stage),
                    transferable = record.transferable,
                })
                .ToList();

            return new TrainedCharacterRecord
            {
                version = 1,
                unitId = mainUnit.ID,
                unitName = mainUnit.UnitName,
                finalTrainingLevel = mainUnit.TrainingLevel,
                finalPrimaryStats = finalStats,
                titleIds = BuildTitleIds(mainUnit, finalStats, passiveRecords),
                ownedPassiveCodes = passiveRecords,
                supportCard = BuildSupportCard(mainUnit, finalStats, passiveRecords, createdAt),
                createdAtUnixSeconds = createdAt,
            };
        }

        private static PrimaryStatSaveData BuildPrimaryStatSnapshot(Unit unit)
        {
            return new PrimaryStatSaveData
            {
                str = unit.GetBaseStr(),
                dex = unit.GetBaseDex(),
                con = unit.GetBaseCon(),
                intStat = unit.GetBaseInt(),
                luk = unit.GetBaseLuk(),
            };
        }

        private static SupportCardSaveData BuildSupportCard(
            Unit unit,
            PrimaryStatSaveData stats,
            List<LearnedPassiveSaveData> passiveRecords,
            long createdAt)
        {
            BaseEnums.PrimaryStat specialty = RollSupportSpecialty(stats);
            int specialtyValue = GetSavedPrimaryStat(stats, specialty);
            int sourcePower = stats.str + stats.dex + stats.con + stats.intStat + stats.luk
                + unit.TrainingLevel * 3
                + passiveRecords.Sum(record => Mathf.Max(1, record.stage) * 10);

            int transferableSkillCount = passiveRecords.Count(record => record.transferable);
            int specialtyRate = Mathf.Clamp(UnityEngine.Random.Range(35, 61) + unit.TrainingLevel / 8, 20, 85);
            int specialtyBonus = Mathf.Clamp(1 + specialtyValue / 35 + UnityEngine.Random.Range(0, 2), 1, 8);
            int trainingBonus = Mathf.Clamp(UnityEngine.Random.Range(0, 3) + sourcePower / 250, 0, 6);
            int skillTransferRate = Mathf.Clamp(10 + transferableSkillCount * 6 + stats.luk / 8 + UnityEngine.Random.Range(0, 11), 5, 70);
            int initialBond = Mathf.Clamp(20 + unit.TrainingLevel / 3 + UnityEngine.Random.Range(0, 16), 0, 75);
            int bondGainRate = Mathf.Clamp(1 + UnityEngine.Random.Range(0, 3) + unit.TrainingLevel / 50, 1, 6);
            int friendshipBonus = Mathf.Clamp(1 + specialtyBonus / 2 + UnityEngine.Random.Range(0, 3), 1, 8);

            return new SupportCardSaveData
            {
                supportId = $"{unit.ID}_{createdAt}",
                sourceUnitId = unit.ID,
                sourceUnitName = unit.UnitName,
                specialtyTraining = specialty.ToString(),
                specialtyRate = specialtyRate,
                specialtyBonus = specialtyBonus,
                trainingBonus = trainingBonus,
                skillTransferRate = skillTransferRate,
                initialBond = initialBond,
                bondGainRate = bondGainRate,
                friendshipBonus = friendshipBonus,
                sourcePower = sourcePower,
            };
        }

        private static List<string> BuildTitleIds(
            Unit unit,
            PrimaryStatSaveData stats,
            List<LearnedPassiveSaveData> passiveRecords)
        {
            var titles = new List<string> { "training_complete" };
            titles.Add($"specialist_{GetHighestPrimaryStat(stats).ToString().ToLowerInvariant()}");

            if (unit.TrainingLevel >= 50)
            {
                titles.Add("veteran_training");
            }
            if (unit.TrainingLevel >= 90)
            {
                titles.Add("century_training");
            }
            if (passiveRecords.Count >= 3)
            {
                titles.Add("skill_collector");
            }

            return titles;
        }

        private static BaseEnums.PrimaryStat RollSupportSpecialty(PrimaryStatSaveData stats)
        {
            BaseEnums.PrimaryStat highestStat = GetHighestPrimaryStat(stats);
            int roll = UnityEngine.Random.Range(0, 100);

            if (roll < 80)
            {
                return highestStat;
            }

            BaseEnums.PrimaryStat[] statsPool =
            {
                BaseEnums.PrimaryStat.STR,
                BaseEnums.PrimaryStat.DEX,
                BaseEnums.PrimaryStat.CON,
                BaseEnums.PrimaryStat.INT,
                BaseEnums.PrimaryStat.LUK,
            };
            return statsPool[UnityEngine.Random.Range(0, statsPool.Length)];
        }

        private static BaseEnums.PrimaryStat GetHighestPrimaryStat(PrimaryStatSaveData stats)
        {
            BaseEnums.PrimaryStat highest = BaseEnums.PrimaryStat.STR;
            int highestValue = stats.str;

            Consider(BaseEnums.PrimaryStat.DEX, stats.dex);
            Consider(BaseEnums.PrimaryStat.CON, stats.con);
            Consider(BaseEnums.PrimaryStat.INT, stats.intStat);
            Consider(BaseEnums.PrimaryStat.LUK, stats.luk);
            return highest;

            void Consider(BaseEnums.PrimaryStat stat, int value)
            {
                if (value <= highestValue) return;

                highest = stat;
                highestValue = value;
            }
        }

        private static int GetSavedPrimaryStat(PrimaryStatSaveData stats, BaseEnums.PrimaryStat stat)
        {
            return stat switch
            {
                BaseEnums.PrimaryStat.STR => stats.str,
                BaseEnums.PrimaryStat.DEX => stats.dex,
                BaseEnums.PrimaryStat.CON => stats.con,
                BaseEnums.PrimaryStat.INT => stats.intStat,
                BaseEnums.PrimaryStat.LUK => stats.luk,
                _ => 0,
            };
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && CanSaveNow)
            {
                SaveCurrentRun();
            }
        }

        private void OnApplicationQuit()
        {
            if (CanSaveNow) SaveCurrentRun();
        }

        private RunSaveData BuildSaveData()
        {
            var roundManager = GameManager.Instance.RoundManager;
            return new RunSaveData
            {
                currentStage = roundManager?.Stage ?? 1,
                currentRound = roundManager?.Round ?? 1,
                gameMode = (int)CurrentMode,
                life = GameManager.Instance.life,
                killCount = GameManager.Instance.KillCount,
                rerollTicketCount = GameManager.Instance.inventoryManager?.rerollTicketCount ?? 0,
                gold = GameManager.Instance.inventoryManager?.Gold ?? 0,
                preparationActionUsed = GameManager.Instance.PreparationActionUsed,
                shopPurchaseCount = GameManager.Instance.ShopPurchaseCount,
                tokens = BuildTokenSaveData(),
                storedItemIds = GameManager.Instance.inventoryManager?.ItemIdsInHand?.ToList() ?? new List<int>(),
                supportBonds = SupportBonds.BuildSaveData(),
                partyTonics = PartyTonics.BuildSaveData(),
                skillHints = SkillHints.BuildSaveData(),
                hintDryTrainings = SkillHints.DryTrainings,
                training = Training.BuildSaveData(),
                heroUnits = BuildHeroSaveData(),
                triggeredEventIds = _triggeredEventIds.ToList(),
                currentThemeId = roundManager?.CurrentThemeId ?? 0,
                pendingEventIds = GameManager.Instance.EventScheduler?.BuildSaveData() ?? new List<string>(),
                valuables = GameManager.Instance.inventoryManager?.Valuables?.ToList() ?? new List<ValuableHolding>(),
            };
        }

        public bool HasTriggeredEvent(string eventId)
        {
            return !string.IsNullOrWhiteSpace(eventId) && _triggeredEventIds.Contains(eventId);
        }

        public void MarkEventTriggered(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return;
            _triggeredEventIds.Add(eventId);
            SaveCurrentRun();
        }

        private static List<TokenSaveData> BuildTokenSaveData()
        {
            var result = new List<TokenSaveData>();
            var inventory = GameManager.Instance.inventoryManager;
            if (inventory?.TokensInHand == null) return result;

            foreach (var pair in inventory.TokensInHand)
            {
                result.Add(new TokenSaveData { tokenId = pair.Key, amount = pair.Value });
            }

            return result;
        }

        private static List<UnitSaveData> BuildHeroSaveData()
        {
            var result = new List<UnitSaveData>();
            foreach (var hero in GridManager.Instance.heroList)
            {
                // 쓰러진 아군도 남긴다. 빼면 불러오기에서 그 유닛이 통째로 사라진다.
                if (hero == null || hero.IsEnemy || hero.IsSummon || hero.currentCell == null) continue;
                if (hero.ID <= 0 && hero.LastActiveId <= 0) continue;

                result.Add(new UnitSaveData
                {
                    unitId = hero.ID > 0 ? hero.ID : hero.LastActiveId,
                    selectedSupportId = hero.SelectedSupportCardId,
                    currentHP = hero.isActive ? hero.HpCurr : 0,
                    xPos = hero.currentCell.xPos,
                    yPos = hero.currentCell.yPos,
                    isBench = GridManager.Instance.IsBenchCell(hero.currentCell),
                    ultimateResource = hero.ManaCurr,
                    level = hero.Level,
                    exp = hero.Exp,
                    trainingLevel = hero.TrainingLevel,
                    strUpgrade = hero.StrUpgrade,
                    dexUpgrade = hero.DexUpgrade,
                    conUpgrade = hero.ConUpgrade,
                    intUpgrade = hero.IntUpgrade,
                    lukUpgrade = hero.LukUpgrade,
                    codeAccelerationBonus = hero.CodeAccelerationRunBonus,
                    equippedItemIds = hero.EquippedItemIds.ToList(),
                    carriedItemIds = hero.CarriedItemIds.ToList(),
                    equipmentStacks = hero.PersistentEquipmentStacks
                        .Where(pair => pair.Value > 0 && hero.EquippedItemIds.Contains(pair.Key))
                        .Select(pair => new EquipmentStackSaveData { itemId = pair.Key, stacks = pair.Value })
                        .ToList(),
                    grantedPassiveCodeIds = hero.GrantedPassiveCodeIds.ToList(),
                });
            }

            return result;
        }

        private static void RestoreInventory(RunSaveData save)
        {
            var inventory = GameManager.Instance.inventoryManager;
            if (inventory == null) return;

            if (inventory.TokensInHand == null)
            {
                inventory.TokensInHand = new IntIntDictionary();
            }

            inventory.TokensInHand.Clear();
            foreach (var token in save.tokens ?? new List<TokenSaveData>())
            {
                inventory.TokensInHand[token.tokenId] = token.amount;
            }

            inventory.rerollTicketCount = save.rerollTicketCount;
            inventory.RestoreGold(save.gold);
            inventory.ItemIdsInHand = save.storedItemIds?.ToList() ?? new List<int>();
            inventory.RestoreValuables(save.valuables);
            inventory.RefreshPanel();
        }

        private static void RestoreHeroes(List<UnitSaveData> savedHeroes)
        {
            // 비활성화만 하면 리스트에 사본이 남는다. 불러오기 전에 기존 아군을 완전히 물린다.
            foreach (var hero in GridManager.Instance.heroList.ToList())
            {
                if (hero != null && !hero.IsEnemy)
                {
                    GridManager.Instance.RetireUnit(hero);
                }
            }
            GridManager.Instance.PruneUnitLists();

            if (savedHeroes == null) return;

            foreach (var saved in savedHeroes)
            {
                // 스폰이 돌려준 인스턴스를 그대로 쓴다. ID로 되찾으면 자리를 못 잡았을 때
                // 같은 ID의 앞선 유닛에 남의 저장 상태를 덮어쓴다.
                Unit restored = GridManager.Instance.SpawnUnit(
                    saved.xPos, saved.yPos, false, saved.unitId, saved.isBench);
                if (restored == null)
                {
                    Debug.LogWarning($"[RunManager] 유닛 {saved.unitId} 복원 실패 — 배치할 자리가 없다");
                    continue;
                }
                restored.RestoreRunState(saved);
                // 체력 0으로 저장된 아군은 쓰러진 채였다. 그대로 눕혀 부활 대상으로 남긴다.
                if (saved.currentHP <= 0) restored.DeactivateUnit();
            }
        }
    }
}
