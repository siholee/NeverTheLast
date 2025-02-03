using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RoundManager roundManager;
    public SkillManager skillManager;

    // 현재 진행중인 게임 상태
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

    private void Start()
    {
        dataManager = GetComponent<DataManager>();
        enemyDataList = dataManager.FetchEnemyDataList();
        heroDataList = dataManager.FetchHeroDataList();
        roundManager = new RoundManager(dataManager);
        skillManager = GetComponent<SkillManager>();
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
