using UnityEngine;
using System.Collections.Generic; 

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RoundManager roundManager;

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

[System.Serializable]
public class RoundDataWrapper
{
    public List<RoundData> rounds;
}

[System.Serializable]
public class RoundData
{
    public int roundNumber;
    public List<CellData> cells;
}

[System.Serializable]
public class CellData
{
    public int cellIndex;
    public List<int> enemyIds;
}
