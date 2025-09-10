using System;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;
using UnityEngine.Serialization;
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

        // 생명력 시스템
        public int life; // 현재 생명력

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
                DontDestroyOnLoad(gameObject);
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
                    case GameState.GameOver:
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
            
            // 라운드 진행 타이머 시작
            currentRoundProgressTime = roundProgressTime;
            isRoundProgressTimerActive = true;
            
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
            _roundManager.LoadRound(1); // 첫 번째 라운드 시작
            
            // 첫 번째 준비 타이머 시작
            StartPreparationTimer();
        }

        private void Update()
        {
            if (_roundManager.IsRoundInProgress)
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
        }

        private int GetRemainingEnemyCount()
        {
            int fieldEnemies = 0;
            int queuedEnemies = 0;
            
            // 필드에 있는 적 수 계산
            foreach (Unit enemy in GridManager.Instance.enemyList)
            {
                if (enemy != null && enemy.isActive)
                {
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

        private void EndRoundByTimeout()
        {
            // 라운드 진행 타이머 정지
            isRoundProgressTimerActive = false;
            
            // 남은 적 수만큼 생명력 차감
            int remainingEnemies = GetRemainingEnemyCount();
            TakeDamage(remainingEnemies);
            
            Debug.Log($"라운드 시간 초과! 남은 적 {remainingEnemies}마리만큼 생명력 차감. 현재 생명력: {life}");
            
            // 라운드 종료 처리
            EndRound();
        }

        public void EndRoundByEnemyDefeat()
        {
            // 적 전멸로 인한 라운드 종료 (생명력 차감 없음)
            isRoundProgressTimerActive = false;
            Debug.Log("모든 적을 처치했습니다! 라운드 승리!");
            EndRound();
        }

        private void EndRound()
        {
            gameState = GameState.RoundEnd;
            
            // 잠시 후 다음 준비 단계로 전환
            if (life > 0)
            {
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
            }
        }
    }
}
