using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;
using System.Collections;
using static BaseEnums;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RoundManager roundManager;
    public GridManager gridManager;
    public UiManager uiManager;
    public SfxManager sfxManager;

    public GameState gameState;
    public List<Unit> currentEnemies;
    public List<Unit> currentHeroes;

    // 게임 데이터 관련
    public DataManager dataManager;
    public UnitDataList unitDataList;

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
                GridManager.Instance.OnRoundStart();
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
        sfxManager = GetComponent<SfxManager>();

        dataManager = GetComponent<DataManager>();
        unitDataList = dataManager.FetchUnitDataList();

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
