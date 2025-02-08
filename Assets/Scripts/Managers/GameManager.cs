using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RoundManager roundManager;
    public SkillManager skillManager;
    public UnitManager unitManager;
    public GridManager gridManager;
    public UiManager uiManager;
    public SfxManager sfxManager;


    // 현재 진행중인 게임 상태
    public enum GameState
    {
        Preperation,
        RoundInProgress,
        RoundEnd,
        GameOver
    }
    public GameState gameState;
    public List<Unit> currentEnemies;
    public List<Unit> currentHeroes;

    // 게임 데이터 관련
    public DataManager dataManager;
    public EnemyDataList enemyDataList;
    public HeroDataList heroDataList;

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
            if (gameState == GameState.Preperation)
            {
                gameState = GameState.RoundInProgress;
            }
            else if (gameState == GameState.RoundInProgress)
            {
                gameState = GameState.RoundEnd;
            }
            else if (gameState == GameState.RoundEnd)
            {
                gameState = GameState.Preperation;
            }
        }
    }

    private void Start()
    {
        gameState = GameState.Preperation;
        skillManager = GetComponent<SkillManager>();
        unitManager = GetComponent<UnitManager>();
        sfxManager = GetComponent<SfxManager>();
        Debug.LogWarning("GameManager Start");

        dataManager = GetComponent<DataManager>();
        enemyDataList = dataManager.FetchEnemyDataList();
        heroDataList = dataManager.FetchHeroDataList();

        unitManager.gameManager = this;
        unitManager.heroCooldowns = new CooldownController[4, 3];
        unitManager.enemyCooldowns = new CooldownController[4, 3];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                unitManager.heroCooldowns[i, j] = new CooldownController();
                unitManager.enemyCooldowns[i, j] = new CooldownController();
            }
        }

        gridManager.gameManager = this;
        gridManager.InitializeComponent();

        roundManager = new RoundManager(dataManager);
        roundManager.gameManager = this;
        roundManager.LoadRound(1); // 첫 번째 라운드 시작
    }

    private void Update()
    {
        if (roundManager.IsRoundInProgress)
        {
            roundManager.UpdateRound();
        }
    }
}
