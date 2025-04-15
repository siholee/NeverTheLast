using System;
using System.Collections.Generic;
using BaseClasses;
using Managers.UI;
using UnityEngine;
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

        public GameState gameState;
        public Dictionary<int, SynergyInfo> SynergyCounts;

        // 게임 데이터 관련
        public DataManager dataManager;
        public UnitDataList unitDataList;
        public SynergyDataList synergyDataList;

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
        }

        private void Start()
{
    SynergyCounts = new Dictionary<int, SynergyInfo>();
    gameState = GameState.Preparation;
    sfxManager = GetComponent<SfxManager>();

    // UIManager 참조 확인
    if (uiManager == null)
    {
        uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
            Debug.LogWarning("UIManager not found!");
        else
            Debug.Log("UIManager reference found");
    }

    dataManager = GetComponent<DataManager>();
    unitDataList = dataManager.FetchUnitDataList();
    synergyDataList = dataManager.FetchSynergyDataList();
    foreach (var synergyData in synergyDataList.synergies)
    {
        SynergyCounts.Add(synergyData.id, new SynergyInfo(synergyData, new List<UnitInfo>()));
    }

    gridManager.gameManager = this;
    if (gridManager.uiManager == null)
        gridManager.uiManager = uiManager; // GridManager에 UIManager 참조 전달
    gridManager.InitializeComponent();

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
