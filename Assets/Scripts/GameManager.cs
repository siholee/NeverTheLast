using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject GridManagerPrefab; // 그리드 매니저 프리팹
    public GameObject HeroPrefab; // 아군 프리팹
    public GameObject EnemyPrefab; // 적 프리팹
    private GridManager grid; // Grid 클래스 참조
    private bool isRoundInProgress; // 라운드 진행 여부
    public int ROUND; // 라운드 번호
    private Dictionary<int, Queue<int>> enemiesToSummon; // Queue로 수정하여 적 소환 관리
    private List<GameObject> activeEnemies; // 현재 활성화된 적 목록

    void Start()
    {
        grid = GridManagerPrefab.GetComponent<GridManager>();
        if (grid == null)
        {
            Debug.LogError("Grid component not found on GridManagerPrefab.");
            return;
        }

        ROUND = 1;
        isRoundInProgress = false;
        activeEnemies = new List<GameObject>(); // 활성화된 적 리스트 초기화
        CreateUnit(1, true, 1, 1);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRoundInProgress)
        {
            RoundManager();
        }

        // 모든 적이 처치되었는지 확인
        if (isRoundInProgress && enemiesToSummon.Count == 0 && activeEnemies.Count == 0)
        {
            isRoundInProgress = false;
            Debug.Log("Round complete!");
        }
    }

    public void RoundManager()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Data/07_Round");
        if (jsonFile == null)
        {
            Debug.LogError("Data/07_Round.json not found.");
            return;
        }

        RoundDataWrapper roundDataWrapper = JsonUtility.FromJson<RoundDataWrapper>(jsonFile.text);
        RoundData currentRound = roundDataWrapper.rounds.Find(r => r.roundNumber == ROUND);
        if (currentRound == null)
        {
            Debug.LogError($"No data found for round {ROUND}.");
            return;
        }
        Debug.Log($"Round {ROUND} data loaded successfully.");
        isRoundInProgress = true;
        enemiesToSummon = new Dictionary<int, Queue<int>>();

        foreach (var cell in currentRound.cells)
        {
            enemiesToSummon[cell.cellIndex] = new Queue<int>(cell.enemyIds);
            Debug.Log($"Cell {cell.cellIndex} has {enemiesToSummon[cell.cellIndex].Count} enemies.");
        }

        // 적 소환 시작
        StartCoroutine(SummonEnemies());
    }

    private System.Collections.IEnumerator SummonEnemies()
    {
        while (isRoundInProgress)
        {
            foreach (var entry in enemiesToSummon)
            {
                int cellIndex = entry.Key;
                Queue<int> enemyQueue = entry.Value;

                while (enemyQueue.Count > 0)
                {
                    Cell emptyCell = SearchForEmptyCell(cellIndex);
                    if (emptyCell != null)
                    {
                        int enemyId = enemyQueue.Dequeue();
                        CreateUnit(enemyId, false, emptyCell.xPos, emptyCell.yPos);
                    }
                    else
                    {
                        yield return null; // 빈 셀이 없을 경우 다음 프레임까지 대기
                    }
                }
            }

            yield return null; // 다음 프레임까지 대기
        }
    }

    public Cell SearchForEmptyCell(int x)
    {
        x = x + 3;
        for (int y = 1; y < 4; y++) // y 값은 1부터 3까지
        {
            Cell cellToSearch = grid.GetCell(x, y);
            if (cellToSearch != null && !cellToSearch.isOccupied)
            {
                Debug.Log($"Empty cell found at ({cellToSearch.xPos}, {cellToSearch.yPos}).");
                return cellToSearch;
            }
        }
        return null; // 빈 셀이 없을 경우
    }

    public void CreateUnit(int id, bool isHero, int x, int y)
    {
        GameObject unit = Instantiate(isHero ? HeroPrefab : EnemyPrefab, grid.CellManager[x - grid.xMin, y - grid.yMin].transform.position, Quaternion.identity);
        Cell cell = grid.GetCell(x, y);
        if (cell != null)
        {
            cell.isOccupied = true;
            cell.unit = unit;
        }

        if (isHero)
        {
            unit.GetComponent<Hero>().ID = id;
             grid.CellManager[x, y].GetComponent<Cell>().GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>($"Sprite/Portraits/centurion_portrait");
        }
        else
        {
            unit.GetComponent<Enemy>().ID = id;
            grid.CellManager[x, y].GetComponent<Cell>().GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>($"Sprite/Portraits/ADAM_PORTRAIT");
            activeEnemies.Add(unit); // 활성화된 적 리스트에 추가
        }
    }
}

// JSON 데이터 클래스 정의
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
