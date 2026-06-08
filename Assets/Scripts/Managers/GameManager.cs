using System;
using System.Collections.Generic;
using BaseClasses;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BaseClasses.BaseEnums;
using static Core.GameStartIntent;

namespace Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public RoundManager roundManager;
        public GridManager gridManager;
        public UIManager uiManager;
        public SfxManager sfxManager;
        public BattleManager battleManager;

        // Phase 2: 씬 전환 헬퍼 ("Game"은 Assets/Scenes/Game.unity의 씬 이름)
        public static void LoadMainMenuScene() => SceneManager.LoadScene("MainMenu");
        public static void LoadBattleScene()   => SceneManager.LoadScene("Game");

        public GameState gameState;
        public Dictionary<int, SynergyInfo> SynergyCounts;

        // Phase 3: SP 시스템
        public SPManager spManager;

        // Phase 7: 로그라이트 런 관리자
        public RunManager    runManager;
        public RewardManager rewardManager;

        // 게임 데이터 관련
        public DataManager dataManager;
        public UnitDataList unitDataList;
        public SynergyDataList synergyDataList;
        public ElementDataList elementDataList;
        public ItemDataList itemDataList;  // Phase 4

        private void Awake()
        {
            // GameManager는 DontDestroyOnLoad하지 않는다.
            // Game.unity가 로드될 때마다 새로 생성되며, Inspector 참조(gridManager 등)도 새로 연결됨.
            // DontDestroyOnLoad 싱글톤은 SettingsManager / RunManager / RewardManager 만 사용.
            Instance = this;

            // Phase 3: SPManager는 Awake에서 초기화 (UIManager.Start()의 구독보다 먼저)
            spManager = new SPManager();

            // SettingsManager — MainMenu.unity에서 생성되어 영속함; 없으면 fallback 생성
            if (SettingsManager.Instance == null)
            {
                var settingsGO = new GameObject("SettingsManager");
                settingsGO.AddComponent<SettingsManager>();
            }

            // Phase 7: RunManager / RewardManager (DontDestroyOnLoad 싱글톤)
            if (RunManager.Instance == null)
            {
                var go = new GameObject("RunManager");
                runManager = go.AddComponent<RunManager>();
            }
            else
            {
                runManager = RunManager.Instance;
            }

            if (RewardManager.Instance == null)
            {
                var go = new GameObject("RewardManager");
                rewardManager = go.AddComponent<RewardManager>();
            }
            else
            {
                rewardManager = RewardManager.Instance;
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
                        battleManager.StartBattle();
                        break;
                    case GameState.CharacterSelection:
                        // 캐릭터 선택 완료 → Preparation
                        gameState = GameState.Preparation;
                        break;
                    case GameState.RoundInProgress:
                        gameState = GameState.RoundEnd;
                        break;
                    case GameState.RoundEnd:
                        // Phase 7: 보상 선택 단계로 전환
                        gameState = GameState.RewardSelection;
                        if (rewardManager != null)
                        {
                            var rewards = rewardManager.GenerateRewards(3);
                            uiManager?.ShowRewardPanel(rewards);
                        }
                        else
                        {
                            // RewardManager 없으면 바로 Preparation 복귀
                            gameState = GameState.Preparation;
                        }
                        break;
                    case GameState.RewardSelection:
                        // 보상 선택 완료 후 Preparation (RunManager.AdvanceEncounter가 라운드 로드)
                        gameState = GameState.Preparation;
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
        }

        private void Start()
        {
            SynergyCounts = new Dictionary<int, SynergyInfo>();
            gameState = GameState.Preparation;
            sfxManager = GetComponent<SfxManager>();

            dataManager = GetComponent<DataManager>();
            unitDataList = dataManager.FetchUnitDataList();
            synergyDataList = dataManager.FetchSynergyDataList();
            elementDataList = dataManager.FetchElementDataList();
            itemDataList = dataManager.FetchItemDataList();  // Phase 4
            foreach (var synergyData in synergyDataList.synergies)
            {
                SynergyCounts.Add(synergyData.id, new SynergyInfo(synergyData, new List<UnitInfo>()));
            }

            gridManager.gameManager = this;
            gridManager.InitializeComponent();

            roundManager = new RoundManager(dataManager);

            // GameStartIntent로 메인메뉴의 의도를 전달받아 런 초기화
            switch (GameStartIntent.Current)
            {
                case Intent.NewGame:
                    // 메인메뉴 "새로운 여정" → 런 초기화 후 캐릭터 선택
                    RunManager.Instance?.StartRun();
                    gameState = GameState.CharacterSelection;
                    uiManager?.ShowCharacterSelection();
                    break;

                case Intent.Continue:
                    // 메인메뉴 "이어하기" → 저장 데이터에서 팀/라운드 복원 (아군 자동 소환 포함)
                    RunManager.Instance?.LoadSavedRun();
                    // 저장된 팀이 이미 소환됨 → Preparation → RoundInProgress 진입
                    gameState = GameState.Preparation;
                    NextGameState(false); // Preparation → RoundInProgress
                    break;

                default:
                    // DirectStart: 에디터에서 Game 씬 직접 플레이 → 기본 팀 자동 구성
                    if (RunManager.Instance == null || !RunManager.Instance.RunActive)
                    {
                        // CharacterSelectionManager 없으면 생성
                        if (CharacterSelectionManager.Instance == null)
                        {
                            var go = new GameObject("CharacterSelectionManager");
                            go.AddComponent<CharacterSelectionManager>();
                        }
                        // 기본 팀 구성
                        CharacterSelectionManager.Instance.UseDefaultLineup();
                        // 적 큐 로드
                        roundManager.LoadRound(1);
                        // 기본 팀 소환
                        foreach (var entry in CharacterSelectionManager.Instance.Lineup)
                        {
                            bool isFrontline = entry.Line == GridLine.Frontline;
                            gridManager.SpawnHero(entry.UnitId, isFrontline, entry.SlotIndex);
                        }
                        // Preparation → RoundInProgress
                        gameState = GameState.Preparation;
                        NextGameState(false);
                    }
                    break;
            }
            // 의도 소비 후 리셋
            GameStartIntent.Current = Intent.DirectStart;
        }

        // 턴제 전투에서는 BattleManager가 전투 루프를 관리하므로 Update() 실시간 처리 제거
    }
}