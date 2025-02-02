using UnityEngine;
using UnityEngine.Pool;

public class GridManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static GridManager Instance { get; private set; }

    public ObjectPoolManager poolManager;
    public GameObject CellPrefab;
    public int xMin = -4;
    public int xMax = 4;
    public int yMin = 1;
    public int yMax = 3;
    public float tileSpacing = 2.1f;

    private Cell[,] CellManager; // 셀 관리 배열

    private void Awake()
    {
        // 싱글톤 인스턴스 초기화
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 변경 시에도 유지
        }
        else
        {
            Destroy(gameObject); // 이미 인스턴스가 존재하면 제거
        }
    }

    private void Start()
    {
        CreateGrid();
    }

    private void CreateGrid()
    {
        int rows = yMax - yMin + 1;
        int columns = xMax - xMin + 1;
        CellManager = new Cell[columns, rows];

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                // 위치 계산
                float xOffset = (x - xMin) * tileSpacing;
                float yOffset = (y - yMin) * tileSpacing;
                Vector3 position = new Vector3(xOffset, yOffset, 0);

                // Cell 생성 및 초기화
                GameObject cellObject = Instantiate(CellPrefab, position, Quaternion.identity, transform);
                cellObject.name = $"Cell_{x}_{y}";

                Cell cellComponent = cellObject.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    cellComponent.xPos = x;
                    cellComponent.yPos = y;
                    cellComponent.isOccupied = false; // 초기화
                }

                // xPos가 0일 경우 SpriteRenderer의 sprite를 null로 설정
                SpriteRenderer spriteRenderer = cellObject.GetComponent<SpriteRenderer>();
                if (x == 0 && spriteRenderer != null)
                {
                    spriteRenderer.sprite = null;
                }

                // 배열에 셀 저장
                CellManager[x - xMin, y - yMin] = cellComponent;
            }
        }
    }

    public bool IsCellAvailable(int xPos, int yPos)
    {
        int adjustedX = xPos - xMin;
        int adjustedY = yPos - yMin;

        if (adjustedX >= 0 && adjustedX < CellManager.GetLength(0) &&
            adjustedY >= 0 && adjustedY < CellManager.GetLength(1))
        {
            Cell cell = CellManager[adjustedX, adjustedY];
            return cell != null && !cell.isOccupied;
        }

        return false; // 셀이 없거나 이미 점유됨
    }

    public void SpawnEnemy(int xPos, int yPos, int enemyId)
    {
        int adjustedX = xPos - xMin;
        int adjustedY = yPos - yMin;

        if (adjustedX >= 0 && adjustedX < CellManager.GetLength(0) &&
            adjustedY >= 0 && adjustedY < CellManager.GetLength(1))
        {
            Cell cell = CellManager[adjustedX, adjustedY];
            if (cell != null)
            {
                Debug.Log($"Spawning enemy {enemyId} at Cell ({xPos}, {yPos}).");
                cell.isOccupied = true; // 셀을 점유 상태로 변경
                Enemy enemyUnit = poolManager.ActivateUnit(false).GetComponent<Enemy>();
                enemyUnit.currentCell = cell;
                enemyUnit.InitProcess(false, enemyId);
            }
        }
    }
}
