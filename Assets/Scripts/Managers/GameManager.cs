using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        private RoundManager _roundManager;
        public GridManager gridManager;
        public UIManager uiManager;
        public SfxManager sfxManager;
        public ShopManager shopManager;
        public InventoryManager inventoryManager;
        public DragAndDropManager dragAndDropManager;
        public RunManager runManager;
        public RewardManager rewardManager;
        public RoundManager RoundManager => _roundManager;
        public const int MaxTrainingStage = 100;
        public GameMode CurrentMode => runManager != null ? runManager.CurrentMode : GameMode.Training;

        public static void LoadMainMenuScene()
        {
            CleanupRunContext();
            SceneManager.LoadScene("MainMenu");
        }

        public static void LoadBattleScene()
        {
            if (Instance != null)
            {
                CleanupRunContext();
            }

            SceneManager.LoadScene("Game");
        }

        public GameState gameState;
        public int KillCount;
        public Dictionary<int, SynergyInfo> SynergyCounts;

        // 준비 단계 타이머 관련
        public float preparationTime = 30f; // 준비 시간 (초)
        private float currentPreparationTime;
        private bool isPreparationTimerActive = false;

        // 라운드 진행 타이머 관련
        public float roundProgressTime = 60f; // 라운드 진행 시간 (초)
        private float currentRoundProgressTime;
        private bool isRoundProgressTimerActive = false;
        private float trainingPhaseEnteredAt;

        // 생명력 시스템
        public int life; // 현재 생명력

        // 아군 필드 상태 저장 (라운드 복원용)
        private List<(int xPos, int yPos, int unitId)> allyFieldSnapshot;

        // 게임 데이터 관련
        public DataManager dataManager;
        public UnitDataList unitDataList;
        public SynergyDataList synergyDataList;
        public ResourceTokenDataList resourceTokenDataList;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                KillCount = 0;
                life = 20; // 생명력 초기화
                
                // 런타임에만 DontDestroyOnLoad 적용
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }

                EnsurePersistentManagers();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void NextGameState(bool isGameOver)
        {
            if (isGameOver)
            {
                gameState = GameState.GameOver;
            }
            else
            {
                switch (gameState)
                {
                    case GameState.CharacterSelection:
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                        break;
                    case GameState.Preparation:
                        StartRound();
                        break;
                    case GameState.RoundInProgress:
                        gameState = GameState.RoundEnd;
                        isPreparationTimerActive = false;
                        isRoundProgressTimerActive = false;
                        break;
                    case GameState.RoundEnd:
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                        break;
                    case GameState.RewardSelection:
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                        break;
                    case GameState.TrainingPhase:
                        runManager?.AdvanceAfterTrainingPhase();
                        break;
                    case GameState.RunComplete:
                        SaveSystem.DeleteSave();
                        LoadMainMenuScene();
                        break;
                    case GameState.GameOver:
                        SaveSystem.DeleteSave();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void CalculateSynergies()
        {
            SynergyCounts.Clear();
            foreach (var synergyData in synergyDataList.synergies)
            {
                SynergyCounts.Add(synergyData.id, new SynergyInfo(synergyData, new List<UnitInfo>()));
            }
            foreach (var hero in GridManager.Instance.heroList)
            {
                if (!hero.isActive) continue;
                foreach (var synergyId in hero.Synergies)
                {
                    SynergyCounts[synergyId].Count++;
                    SynergyCounts[synergyId].Units.Add(new UnitInfo(hero.UnitName, hero.PortraitPath));
                }
            }
            uiManager.SetSynergyText(SynergyCounts);
        }

        private void StartPreparationTimer()
        {
            currentPreparationTime = preparationTime;
            isPreparationTimerActive = true;
        }

        public void StartRound()
        {
            gameState = GameState.RoundInProgress;
            isPreparationTimerActive = false;
            
            // 아군 필드 상태 저장 (라운드 종료 후 복원용)
            SaveAllyFieldState();
            runManager?.SaveCurrentRun();
            
            // 라운드 진행 타이머 시작
            currentRoundProgressTime = roundProgressTime;
            isRoundProgressTimerActive = true;
            
            Debug.LogWarning("[GameManager] 🔥 라운드 시작 - GridManager.OnRoundStart() 호출");
            GridManager.Instance.OnRoundStart();
        }

        public void OnKillEnemy()
        {
            KillCount++;
            
            // 3의 배수 킬마다 토큰 보상 지급
            if (KillCount % 3 == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    int randomTokenId = UnityEngine.Random.Range(1, 9); // 1-8 사이의 무작위 tokenId
                    inventoryManager.AddToken(randomTokenId, 1);
                }
                Debug.Log($"킬 {KillCount}번째 달성! 무작위 토큰 5개를 지급했습니다.");
            }
        }

        private void Start()
        {
            SynergyCounts = new Dictionary<int, SynergyInfo>();
            gameState = GameState.Preparation;
            allyFieldSnapshot = new List<(int xPos, int yPos, int unitId)>();
            sfxManager = GetComponent<SfxManager>();

            dataManager = GetComponent<DataManager>();
            unitDataList = dataManager.FetchUnitDataList();
            synergyDataList = dataManager.FetchSynergyDataList();
            resourceTokenDataList = dataManager.FetchTokenDataList();
            foreach (var synergyData in synergyDataList.synergies)
            {
                SynergyCounts.Add(synergyData.id, new SynergyInfo(synergyData, new List<UnitInfo>()));
            }

            gridManager.gameManager = this;
            gridManager.InitializeComponent();
            shopManager = GetComponent<ShopManager>();
            inventoryManager = GetComponent<InventoryManager>();
            for (int i = 1; i <= 3; i++)
            {
                shopManager.RerollShopItems(i);
            }
            shopManager.ShowShopItems(1);
            inventoryManager.Initialize();

            _roundManager = new RoundManager(dataManager);
            StartFromIntent();
        }

        private void Update()
        {
            if (_roundManager != null && _roundManager.IsRoundInProgress)
            {
                _roundManager.UpdateRound();
            }
            
            // 준비 단계 타이머 처리
            if (gameState == GameState.Preparation && isPreparationTimerActive)
            {
                currentPreparationTime -= Time.deltaTime;
                
                // UI 업데이트 (음수가 되지 않도록 보정)
                int displayTime = Mathf.Max(0, Mathf.CeilToInt(currentPreparationTime));
                if (uiManager != null)
                {
                    uiManager.UpdateGameStatus(gameState, displayTime);
                }
                
                // 시간이 다 되면 자동 시작
                if (currentPreparationTime <= 0)
                {
                    StartRound();
                }
            }
            // 라운드 진행 중 타이머 처리
            else if (gameState == GameState.RoundInProgress && isRoundProgressTimerActive)
            {
                currentRoundProgressTime -= Time.deltaTime;
                
                // 남은 적 수 계산 (필드의 적 + 스폰 대기 중인 적)
                int remainingEnemies = GetRemainingEnemyCount();
                
                // 아군 전멸 체크
                if (AreAllAlliesDefeated())
                {
                    EndRoundByAllyDefeat();
                    return;
                }
                
                // UI 업데이트
                int displayTime = Mathf.Max(0, Mathf.CeilToInt(currentRoundProgressTime));
                if (uiManager != null)
                {
                    uiManager.UpdateGameStatusWithEnemyCount(gameState, displayTime, remainingEnemies);
                }
                
                // 시간이 다 되면 라운드 종료 (시간 초과)
                if (currentRoundProgressTime <= 0)
                {
                    EndRoundByTimeout();
                }
            }
            else if (uiManager != null)
            {
                // 다른 상태일 때는 기본 상태만 표시
                if (gameState == GameState.RoundInProgress)
                {
                    // 라운드 진행 중이지만 타이머가 비활성화된 경우 (적 수만 표시)
                    int remainingEnemies = GetRemainingEnemyCount();
                    uiManager.UpdateGameStatusWithEnemyCount(gameState, 0, remainingEnemies);
                }
                else
                {
                    uiManager.UpdateGameStatus(gameState, 0);
                }
            }

            if (gameState == GameState.TrainingPhase &&
                Time.unscaledTime - trainingPhaseEnteredAt > 0.15f &&
                (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                uiManager?.HideTrainingPhasePanel();
                runManager?.AdvanceAfterTrainingPhase();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginRunAfterCharacterSelection()
        {
            if (_roundManager == null)
            {
                _roundManager = new RoundManager(dataManager);
                _roundManager.InitializeStage(1);
            }

            _roundManager.LoadRound(1);
            gameState = GameState.Preparation;
            StartPreparationTimer();
            runManager?.SaveCurrentRun();
        }

        public void EnterPreparationAfterReward()
        {
            gameState = GameState.Preparation;
            StartPreparationTimer();
            uiManager?.UpdateLifeText();
        }

        public void EnterTrainingPhase()
        {
            gameState = GameState.TrainingPhase;
            isPreparationTimerActive = false;
            isRoundProgressTimerActive = false;
            trainingPhaseEnteredAt = Time.unscaledTime;
            uiManager?.ShowTrainingPhasePanel();
        }

        private int GetRemainingEnemyCount()
        {
            int fieldEnemies = 0;
            int queuedEnemies = 0;
            
            // 필드에 있는 적 수 계산 (벤치에 있는 적은 제외)
            foreach (Unit enemy in GridManager.Instance.enemyList)
            {
                if (enemy != null && enemy.isActive)
                {
                    // 벤치에 있는 유닛은 제외 (적이 벤치에 있을 가능성은 낮지만 안전을 위해)
                    if (GridManager.Instance.IsBenchCell(enemy.currentCell))
                    {
                        continue;
                    }
                    fieldEnemies++;
                }
            }
            
            // 스폰 대기 중인 적 수 계산
            if (_roundManager != null)
            {
                queuedEnemies = _roundManager.GetTotalQueuedEnemies();
            }
            
            return fieldEnemies + queuedEnemies;
        }

        private bool AreAllAlliesDefeated()
        {
            // 필드에 있는 아군 수 계산 (벤치 제외)
            foreach (Unit ally in GridManager.Instance.heroList)
            {
                if (ally != null && ally.isActive)
                {
                    // 벤치에 있는 유닛은 제외, 필드에 있는 아군만 체크
                    if (!GridManager.Instance.IsBenchCell(ally.currentCell))
                    {
                        return false; // 필드에 살아있는 아군이 있으면 false
                    }
                }
            }
            return true; // 필드에 살아있는 아군이 없으면 true
        }

        private void EndRoundByTimeout()
        {
            // 라운드 진행 타이머 정지
            isRoundProgressTimerActive = false;
            
            // 남은 적 수만큼 생명력 차감
            int remainingEnemies = GetRemainingEnemyCount();
            TakeDamage(remainingEnemies);
            
            Debug.Log($"라운드 시간 초과! 남은 적 {remainingEnemies}마리만큼 생명력 차감. 현재 생명력: {life}");
            
            // 라운드 종료 처리
            EndRound(false);
        }

        public void EndRoundByEnemyDefeat()
        {
            // 적 전멸로 인한 라운드 종료 (생명력 차감 없음)
            isRoundProgressTimerActive = false;
            Debug.Log("모든 적을 처치했습니다! 라운드 승리!");
            EndRound(true);
        }

        private void EndRoundByAllyDefeat()
        {
            // 아군 전멸로 인한 라운드 종료 (패배 처리)
            isRoundProgressTimerActive = false;
            
            // 남은 적 수만큼 생명력 차감 (패배 페널티)
            int remainingEnemies = GetRemainingEnemyCount();
            TakeDamage(remainingEnemies);
            
            Debug.Log($"아군이 전멸했습니다! 남은 적 {remainingEnemies}마리만큼 생명력 차감. 현재 생명력: {life}");
            
            // 라운드 종료 처리
            EndRound(false);
        }

        private void EndRound(bool victory)
        {
            _roundManager?.StopRound();
            gameState = GameState.RoundEnd;
            
            // 아군 필드 상태 복원 (게임 오버가 아닌 경우에만)
            if (life > 0)
            {
                RestoreAllyFieldState();
            }
            
            if (life <= 0)
            {
                SaveSystem.DeleteSave();
                return;
            }

            if (victory)
            {
                gameState = GameState.RewardSelection;
                var rewards = rewardManager != null
                    ? rewardManager.GenerateRewards(3, _roundManager?.Round ?? 1, CurrentMode)
                    : new List<RewardDef>();
                uiManager?.ShowRewardPanel(rewards);
            }
            else
            {
                runManager?.SaveCurrentRun();
                NextGameState(false);
            }
        }

        private void TakeDamage(int damage)
        {
            life = Mathf.Max(0, life - damage);
            
            // UI 업데이트
            if (uiManager != null)
            {
                uiManager.UpdateLifeText();
            }
            
            // 생명력이 0이 되면 게임 오버
            if (life <= 0)
            {
                gameState = GameState.GameOver;
                Debug.Log("생명력이 0이 되었습니다. 게임 오버!");
                SaveSystem.DeleteSave();
            }
        }

        private void SaveAllyFieldState()
        {
            allyFieldSnapshot.Clear();
            
            // 현재 필드에 있는 아군 유닛들의 위치와 ID 저장
            foreach (Unit ally in GridManager.Instance.heroList)
            {
                if (ally != null && ally.isActive && ally.currentCell != null)
                {
                    // 벤치가 아닌 필드에 있는 유닛만 저장
                    if (!GridManager.Instance.IsBenchCell(ally.currentCell))
                    {
                        allyFieldSnapshot.Add((ally.currentCell.xPos, ally.currentCell.yPos, ally.ID));
                    }
                }
            }
            
            Debug.Log($"아군 필드 상태 저장됨: {allyFieldSnapshot.Count}개 유닛");
        }

        public void RestoreAllyFieldState()
        {
            if (gameState == GameState.GameOver) return; // 게임 오버 시에는 복원하지 않음
            
            // 현재 필드에 있는 모든 아군 유닛 제거 (벤치는 유지)
            var fieldAllies = GridManager.Instance.heroList.Where(ally => 
                ally != null && ally.isActive && ally.currentCell != null && 
                !GridManager.Instance.IsBenchCell(ally.currentCell)).ToList();
            
            foreach (Unit ally in fieldAllies)
            {
                ally.DeactivateUnit();
            }
            
            // 저장된 상태로 아군 필드 복원
            foreach (var (xPos, yPos, unitId) in allyFieldSnapshot)
            {
                GridManager.Instance.SpawnUnit(xPos, yPos, false, unitId, false);
            }
            
            Debug.Log($"아군 필드 상태 복원됨: {allyFieldSnapshot.Count}개 유닛");
        }

        private void EnsurePersistentManagers()
        {
            if (SettingsManager.Instance == null)
            {
                new GameObject("SettingsManager").AddComponent<SettingsManager>();
            }

            if (RunManager.Instance == null)
            {
                runManager = new GameObject("RunManager").AddComponent<RunManager>();
            }
            else
            {
                runManager = RunManager.Instance;
            }

            if (RewardManager.Instance == null)
            {
                rewardManager = new GameObject("RewardManager").AddComponent<RewardManager>();
            }
            else
            {
                rewardManager = RewardManager.Instance;
            }
        }

        private void StartFromIntent()
        {
            switch (GameStartIntent.Current)
            {
                case GameStartIntent.Intent.NewGame:
                    runManager.StartRun(GameMode.Training);
                    _roundManager.InitializeStage(1);
                    gameState = GameState.CharacterSelection;
                    uiManager?.ShowCharacterSelection();
                    break;
                case GameStartIntent.Intent.InfiniteMode:
                    runManager.StartRun(GameMode.Infinite);
                    _roundManager.InitializeStage(1);
                    gameState = GameState.CharacterSelection;
                    uiManager?.ShowCharacterSelection();
                    break;
                case GameStartIntent.Intent.Continue:
                    if (runManager.LoadSavedRun())
                    {
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                    }
                    else
                    {
                        _roundManager.InitializeStage(1);
                        gameState = GameState.CharacterSelection;
                        uiManager?.ShowCharacterSelection();
                    }
                    break;
                case GameStartIntent.Intent.DirectStart:
                default:
                    runManager.StartRun(GameMode.Training);
                    _roundManager.InitializeStage(1);
                    _roundManager.LoadRound(1);
                    gameState = GameState.Preparation;
                    StartPreparationTimer();
                    break;
            }

            GameStartIntent.Current = GameStartIntent.Intent.DirectStart;
        }

        private static void CleanupRunContext()
        {
            CharacterSelectionManager.DestroyInstance();
            RunManager.DestroyInstance();
            RewardManager.DestroyInstance();

            if (Instance != null)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
        }
    }
}
