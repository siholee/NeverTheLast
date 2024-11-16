using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject GridManagerPrefab; // 그리드 매니저 프리팹
    public GameObject HeroPrefab; // 아군 프리팹
    public GameObject EnemyPrefab; // 적 프리팹
    private GridManager grid; // Grid 클래스 참조
    private bool isRoundInProgress = false; // 라운드 진행 여부
    public int ROUND; // 라운드 번호
    private Queue<int> enemiesToSummon = new Queue<int>(); // 적 소환 대기열

    void Start()
    {
        // GridManagerPrefab에서 Grid 컴포넌트 가져오기
        grid = GridManagerPrefab.GetComponent<GridManager>();
        if (grid == null)
        {
            Debug.LogError("Grid component not found on GridManagerPrefab.");
            return;
        }

        ROUND = 1;
    }

    void Update()
    {
        // If press space key, call RoundManager
        if (Input.GetKeyDown(KeyCode.Space) && !isRoundInProgress)
        {
            RoundManager();
        }
    }

    public void RoundManager()
    {
        Debug.Log("RoundManager called."); // 디버그 로그 추가

        // Load JSON data from Resources/Data/07_Round.json
        TextAsset jsonFile = Resources.Load<TextAsset>("Data/07_Round");
        if (jsonFile == null)
        {
            Debug.LogError("Data/07_Round.json not found.");
            return;
        }

        // Parse JSON data
        RoundDataWrapper roundDataWrapper = JsonUtility.FromJson<RoundDataWrapper>(jsonFile.text);
        RoundData currentRound = roundDataWrapper.rounds.Find(r => r.roundNumber == ROUND);

        if (currentRound == null)
        {
            Debug.LogError($"No data found for round {ROUND}.");
            return;
        }

        Debug.Log($"Round {ROUND} data loaded successfully."); // 디버그 로그 추가

        // 적 소환 대기열 초기화
        enemiesToSummon.Clear();
        isRoundInProgress = true;

        // 셀 데이터를 순회하며 적 소환 대기열에 추가
        foreach (var cellData in currentRound.cells)
        {
            int cellIndex = cellData.cellIndex;
            if (cellIndex < grid.yMin || cellIndex > grid.yMax) // y 값 검사
            {
                Debug.LogWarning($"Invalid cellIndex {cellIndex} for round {ROUND}.");
                continue;
            }

            Debug.Log($"Valid cellIndex {cellIndex} for round {ROUND}."); // 디버그 로그 추가

            // 적 ID를 대기열에 추가
            foreach (int enemyId in cellData.enemyIds)
            {
                enemiesToSummon.Enqueue(enemyId);
            }
        }

        // 적 소환 시작
        StartCoroutine(SummonEnemies());
    }

    private System.Collections.IEnumerator SummonEnemies()
    {
        while (enemiesToSummon.Count > 0)
        {
            bool enemySummoned = false;

            // 적 셀 영역 (x 값이 grid.xMin ~ grid.xMax) 및 유효한 y 범위 (grid.yMin ~ grid.yMax)에서 적을 소환할 수 있는지 확인
            for (int x = grid.xMin; x <= grid.xMax; x++)
            {
                for (int y = grid.yMin; y <= grid.yMax; y++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (cell != null && !cell.isOccupied)
                    {
                        int enemyId = enemiesToSummon.Dequeue();
                        CreateUnit(enemyId, cell.xPos, cell.yPos, false); // 적 생성
                        cell.isOccupied = true;

                        // 셀의 스프라이트를 None으로 설정
                        SpriteRenderer spriteRenderer = cell.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            spriteRenderer.sprite = null; // 스프라이트를 None으로 설정
                        }

                        Debug.Log($"Enemy {enemyId} spawned at ({x}, {y})."); // 디버그 로그 추가
                        enemySummoned = true;
                        break;
                    }
                }

                if (enemySummoned)
                {
                    break;
                }
            }

            // 만약 소환할 수 있는 셀이 없다면 대기
            if (!enemySummoned)
            {
                Debug.Log("Waiting for an available cell..."); // 디버그 로그 추가
                yield return null; // 다음 프레임까지 대기
            }
        }

        // 적이 모두 소환된 후 적 영역이 비어있는지 확인
        while (true)
        {
            bool allCellsCleared = true;
            for (int x = grid.xMin; x <= grid.xMax; x++)
            {
                for (int y = grid.yMin; y <= grid.yMax; y++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (cell != null && cell.isOccupied)
                    {
                        allCellsCleared = false;
                        break;
                    }
                }

                if (!allCellsCleared)
                {
                    break;
                }
            }

            if (allCellsCleared)
            {
                Debug.Log("All enemy cells cleared. Ending round."); // 디버그 로그 추가
                isRoundInProgress = false; // 라운드 종료
                ROUND++; // 다음 라운드로 이동
                break;
            }

            yield return null; // 다음 프레임까지 대기
        }
    }

    // 유닛 생성 함수
    void CreateUnit(int id, int x, int y, bool side)
    {
        Vector3 position = new Vector3(x, y, 0); // 2D 포지션 설정
        if (side == true)
        {
            GameObject unit = Instantiate(HeroPrefab, position, Quaternion.identity, transform);
            unit.name = $"Unit_{id}";
            Hero heroComponent = unit.GetComponent<Hero>();
            heroComponent.ID = id;
        }
        else if (side == false)
        {
            GameObject unit = Instantiate(EnemyPrefab, position, Quaternion.identity, transform);
            unit.name = $"Unit_{id}";
            Enemy enemyComponent = unit.GetComponent<Enemy>();
            enemyComponent.ID = id;
            enemyComponent.LEVEL = ROUND;
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
