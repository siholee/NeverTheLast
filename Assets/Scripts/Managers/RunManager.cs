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
        public static RunManager Instance { get; private set; }

        public bool RunActive { get; private set; }
        public GameMode CurrentMode { get; private set; } = GameMode.Training;

        // 런 범위 상태: 서포트 우정도 (런 시작 시 초기화, 저장/복원 대상)
        public SupportBondState SupportBonds { get; } = new();
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
            _triggeredEventIds.Clear();
            SaveSystem.DeleteSave();
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

            RunActive = true;
            CurrentMode = (GameMode)save.gameMode;
            GameManager.Instance.life = save.life;
            GameManager.Instance.KillCount = save.killCount;
            RestoreInventory(save);
            SupportBonds.Restore(save.supportBonds);
            _triggeredEventIds.Clear();
            foreach (string eventId in save.triggeredEventIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(eventId)) _triggeredEventIds.Add(eventId);
            }

            RoundManager roundManager = GameManager.Instance.RoundManager;
            roundManager.InitializeStage(Mathf.Max(1, save.currentStage));
            RestoreHeroes(save.heroUnits);
            roundManager.LoadRound(Mathf.Max(1, save.currentStage));

            GameManager.Instance.uiManager?.UpdateLifeText();
            GameManager.Instance.EnterNextStageAfterLoad();
            GameManager.Instance.RestorePreparationActionState(save.preparationActionUsed);
            Debug.Log($"[RunManager] 저장 런 복원 - Stage {save.currentStage}, Round {save.currentRound}");
            return true;
        }

        /// <summary>
        /// 스테이지 전진의 단일 진입점. 보상/사건/육성 등 어느 흐름에서 오든
        /// 런 종료 판정(육성 최대 스테이지 도달, 패턴 소진) 후 다음 스테이지를 로드한다.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (!RunActive) return;

            if (CurrentMode == GameMode.Training)
            {
                if (GameManager.Instance.RoundManager.Stage >= GameManager.MaxTrainingStage ||
                    !GameManager.Instance.RoundManager.TryLoadNextRound())
                {
                    CompleteTrainingRun();
                    return;
                }

                SaveCurrentRun();
                GameManager.Instance.EnterNextStageAfterLoad();
                return;
            }

            bool hasNextRound = GameManager.Instance.RoundManager.TryLoadNextRound();
            if (!hasNextRound)
            {
                RunActive = false;
                SaveSystem.DeleteSave();
                GameManager.Instance.gameState = BaseClasses.BaseEnums.GameState.RunComplete;
                GameManager.LoadMainMenuScene();
                return;
            }

            SaveCurrentRun();
            GameManager.Instance.EnterNextStageAfterLoad();
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

            RunActive = false;
            SaveSystem.DeleteSave();
            GameManager.Instance.gameState = GameState.RunComplete;
            GameManager.LoadMainMenuScene();
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
            if (pauseStatus)
            {
                SaveCurrentRun();
            }
        }

        private void OnApplicationQuit()
        {
            SaveCurrentRun();
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
                tokens = BuildTokenSaveData(),
                storedItemIds = GameManager.Instance.inventoryManager?.ItemIdsInHand?.ToList() ?? new List<int>(),
                supportBonds = SupportBonds.BuildSaveData(),
                heroUnits = BuildHeroSaveData(),
                triggeredEventIds = _triggeredEventIds.ToList(),
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
                if (hero == null || !hero.isActive || hero.IsEnemy || hero.currentCell == null) continue;

                result.Add(new UnitSaveData
                {
                    unitId = hero.ID,
                    currentHP = hero.HpCurr,
                    xPos = hero.currentCell.xPos,
                    yPos = hero.currentCell.yPos,
                    isBench = GridManager.Instance.IsBenchCell(hero.currentCell),
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
            inventory.RefreshPanel();
        }

        private static void RestoreHeroes(List<UnitSaveData> savedHeroes)
        {
            foreach (var hero in GridManager.Instance.heroList.ToList())
            {
                if (hero != null && hero.isActive && !hero.IsEnemy)
                {
                    hero.DeactivateUnit();
                }
            }

            if (savedHeroes == null) return;

            foreach (var saved in savedHeroes)
            {
                GridManager.Instance.SpawnUnit(saved.xPos, saved.yPos, false, saved.unitId, saved.isBench);
                Unit restored = GridManager.Instance.heroList
                    .LastOrDefault(hero => hero != null && hero.isActive && hero.ID == saved.unitId);
                restored?.RestoreRunState(saved);
            }
        }
    }
}
