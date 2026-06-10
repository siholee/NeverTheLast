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

            RoundManager roundManager = GameManager.Instance.RoundManager;
            roundManager.InitializeStage(Mathf.Max(1, save.currentStage));
            RestoreHeroes(save.heroUnits);
            roundManager.LoadRound(Mathf.Max(1, save.currentStage));

            GameManager.Instance.uiManager?.UpdateLifeText();
            Debug.Log($"[RunManager] 저장 런 복원 - Stage {save.currentStage}, Round {save.currentRound}");
            return true;
        }

        public void AdvanceAfterReward()
        {
            if (!RunActive) return;

            if (CurrentMode == GameMode.Training)
            {
                if (GameManager.Instance.RoundManager.Stage >= GameManager.MaxTrainingStage)
                {
                    CompleteTrainingRun();
                    return;
                }

                GameManager.Instance.EnterTrainingPhase();
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
            GameManager.Instance.EnterPreparationAfterReward();
        }

        public void AdvanceAfterTrainingPhase()
        {
            if (!RunActive) return;

            bool hasNextRound = GameManager.Instance.RoundManager.TryLoadNextRound();
            if (!hasNextRound)
            {
                CompleteTrainingRun();
                return;
            }

            SaveCurrentRun();
            GameManager.Instance.EnterPreparationAfterReward();
        }

        private void CompleteTrainingRun()
        {
            int mainUnitId = CharacterSelectionManager.Instance?.MainUnitId ?? 0;
            if (mainUnitId <= 0)
            {
                mainUnitId = GridManager.Instance.heroList
                    .FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy)?.ID ?? 0;
            }

            SaveSystem.AddTrainedCharacter(mainUnitId);
            RunActive = false;
            SaveSystem.DeleteSave();
            GameManager.Instance.gameState = GameState.RunComplete;
            GameManager.LoadMainMenuScene();
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
                tokens = BuildTokenSaveData(),
                heroUnits = BuildHeroSaveData(),
            };
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
                    strUpgrade = hero.StrUpgrade,
                    dexUpgrade = hero.DexUpgrade,
                    conUpgrade = hero.ConUpgrade,
                    intUpgrade = hero.IntUpgrade,
                    lukUpgrade = hero.LukUpgrade,
                    hpUpgrade = hero.HpUpgrade,
                    atkUpgrade = hero.AtkUpgrade,
                    defUpgrade = hero.DefUpgrade,
                    critChanceUpgrade = hero.CritChanceUpgrade,
                    critMultiplierUpgrade = hero.CritMultiplierUpgrade,
                    codeAccelerationBonus = hero.CodeAccelerationRunBonus,
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
