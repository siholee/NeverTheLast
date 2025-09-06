using System;
using System.Collections.Generic;
using BaseClasses;
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
                        gameState = GameState.RoundInProgress;
                        GridManager.Instance.OnRoundStart();
                        break;
                    case GameState.RoundInProgress:
                        gameState = GameState.RoundEnd;
                        break;
                    case GameState.RoundEnd:
                        gameState = GameState.Preparation;
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
        }

        private void Update()
        {
            if (_roundManager.IsRoundInProgress)
            {
                _roundManager.UpdateRound();
            }
        }
    }
}
