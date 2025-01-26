using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RoundManager roundManager;
    public ObjectPoolManager poolManager;

    // 게임 데이터 관련
    public DataManager dataManager;
    public EnemyDataList enemyDataList;

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
        roundManager = new RoundManager();
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
