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

        public void StartRun(bool clearSavedRun = true)
        {
            RunActive = true;
            _triggeredEventIds.Clear();
            if (clearSavedRun) SaveSystem.DeleteSave();
        }

        /// <summary>
        /// 플레이어가 직접 저장하거나 게임을 끌 때 지금 상태를 저장해도 되는지.
        /// 전투 중에는 쓰러진 아군이 저장본에서 빠지고, 보상 선택 중에는 경험치를 받은 채
        /// 같은 전투로 되돌아가며, 캐릭터 선택 중에는 파티가 비어 있다.
        /// 그때는 전투 직전·사건 때 찍어 둔 자동 저장을 그대로 둔다.
        /// </summary>
        public bool CanSaveNow
        {
            get
            {
                if (!RunActive || GameManager.Instance == null || GridManager.Instance == null) return false;
                GameState state = GameManager.Instance.gameState;
                return state == GameState.Preparation
                    || state == GameState.EventStage;
            }
        }

        public void SaveCurrentRun()
        {
            if (!RunActive || GameManager.Instance == null || GridManager.Instance == null) return;
            SaveSystem.SaveRun(BuildSaveData());
        }

        public bool LoadSavedRun()
        {
            RunSaveData save = SaveSystem.LoadRun();
            if (save == null)
            {
                StartRun(clearSavedRun: false);
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
            GameManager.Instance.life = save.life;
            GameManager.Instance.KillCount = save.killCount;
            RestoreInventory(save);
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

            // 같은 씬의 준비 상태에서 복원해도 이전 적 미리보기가 저장 층의 스폰 자리를 점유하지 않게 한다.
            GridManager.Instance.ClearActiveEnemies();
            RestoreHeroes(save.heroUnits);
            roundManager.LoadRound(Mathf.Max(1, save.currentStage));

            GameManager.Instance.uiManager?.UpdateLifeText();
            GameManager.Instance.EnterNextStageAfterLoad();
            GameManager.Instance.RestorePreparationState(
                save.restUsedThisPreparation,
                save.shortRestCharges,
                save.shopPurchaseCount);
            GameManager.Instance.RestorePendingTonic(save.pendingTonicItemId);
            Debug.Log($"[RunManager] 저장 런 복원 - Stage {save.currentStage}, Round {save.currentRound}");
        }

        /// <summary>
        /// 스테이지 전진의 단일 진입점. 보상/사건 어느 흐름에서 오든 여기를 지난다.
        /// 게임 모드는 무한 모드 하나라 스테이지 상한이 없고, 라운드 데이터가 바닥날 때만 런이 끝난다.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (!RunActive) return;

            int previousRound = GameManager.Instance.RoundManager.Round;

            bool hasNextRound = GameManager.Instance.RoundManager.TryLoadNextRound();
            if (!hasNextRound)
            {
                RunActive = false;
                SaveSystem.DeleteSave();
                GameManager.Instance.gameState = BaseClasses.BaseEnums.GameState.RunComplete;
                GameManager.LoadMainMenuScene();
                return;
            }

            ResetUltimateResourcesOnThemeTransition(previousRound);
            GameManager.Instance.EnterNextStageAfterLoad();
            // 새 준비/사건 상태와 초기화된 휴식·상점 횟수를 함께 저장한다.
            SaveCurrentRun();
        }

        /// <summary>1-8 → 2-1처럼 8라운드 테마 라운드가 바뀔 때 궁극기 자원을 완전히 비운다.</summary>
        private static void ResetUltimateResourcesOnThemeTransition(int previousRound)
        {
            if (GameManager.Instance.RoundManager.Round == previousRound) return;

            foreach (Unit hero in GridManager.Instance.heroList)
            {
                if (hero != null && hero.isActive && !hero.IsEnemy) hero.ClearUltimateResource();
            }
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
                life = GameManager.Instance.life,
                killCount = GameManager.Instance.KillCount,
                rerollTicketCount = GameManager.Instance.inventoryManager?.rerollTicketCount ?? 0,
                gold = GameManager.Instance.inventoryManager?.Gold ?? 0,
                restUsedThisPreparation = GameManager.Instance.RestUsedThisPreparation,
                shortRestCharges = GameManager.Instance.ShortRestCharges,
                shopPurchaseCount = GameManager.Instance.ShopPurchaseCount,
                pendingTonicItemId = GameManager.Instance.PendingTonicItemId,
                tokens = BuildTokenSaveData(),
                storedItemIds = GameManager.Instance.inventoryManager?.ItemIdsInHand?.ToList() ?? new List<int>(),
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
                if (hero == null || hero.IsEnemy || hero.currentCell == null) continue;
                if (hero.ID <= 0 && hero.LastActiveId <= 0) continue;

                var saved = new UnitSaveData
                {
                    unitId = hero.ID > 0 ? hero.ID : hero.LastActiveId,
                    currentHP = hero.isActive ? hero.HpCurr : 0,
                    xPos = hero.currentCell.xPos,
                    yPos = hero.currentCell.yPos,
                    isBench = GridManager.Instance.IsBenchCell(hero.currentCell),
                    ultimateResource = hero.ManaCurr,
                };
                hero.WriteRunState(saved);
                result.Add(saved);
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
