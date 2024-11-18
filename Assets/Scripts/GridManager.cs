using UnityEngine;

public class GridManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public int xMin; // x 좌표 최소값 (아군 영역)
    public int xMax; // x 좌표 최대값 (적 영역)
    public int yMin; // y 좌표 최소값
    public int yMax; // y 좌표 최대값
    public float tileSpacing; // 타일 간의 간격

    // CellManager 배열: x, y 좌표를 기록
    public GameObject[,] CellManager;

    void Start()
    {
        // 값을 Start 함수에서 설정
        xMin = -4;
        xMax = 4;
        yMin = 1;
        yMax = 3;
        tileSpacing = 2.1f; // 타일 간의 간격 설정

        CreateGrid();
    }

    void CreateGrid()
    {
        int rows = yMax - yMin + 1;
        int columns = xMax - xMin + 1;
        CellManager = new GameObject[columns, rows];

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                // 위치 계산
                float xOffset = (x - xMin) * tileSpacing;
                float yOffset = (y - yMin) * tileSpacing;
                Vector3 position = new Vector3(xOffset, yOffset, 0);
                // 타일 생성 및 초기화
                GameObject tile = Instantiate(tilePrefab, position, Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";
                // x가 0일 경우 Sprite none으로 설정
                if (x == 0)
                {
                    tile.GetComponent<SpriteRenderer>().sprite = null;
                }
                // Cell 컴포넌트 설정
                Cell cellComponent = tile.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    cellComponent.xPos = x;
                    cellComponent.yPos = y;
                    cellComponent.isOccupied = false; // 초기화
                }

                // CellManager에 셀 저장
                CellManager[x - xMin, y - yMin] = tile;
            }
        }
    }
    public Cell GetCell(int x, int y)
    {
        // 배열 인덱스 유효성 검사
        if (x - xMin >= 0 && x - xMin < CellManager.GetLength(0) &&
            y - yMin >= 0 && y - yMin < CellManager.GetLength(1))
        {
            return CellManager[x - xMin, y - yMin].GetComponent<Cell>();
        }

        Debug.LogError($"Invalid cell position: ({x}, {y})");
        return null;
    }

    // 포지션 전환: 아군 좌표 <-> 적 좌표
    public Vector3 SwitchPosition(Vector3 position)
    {
        // x 값에 -1을 곱해 포지션 전환
        return new Vector3(position.x * -1, position.y, position.z);
    }
}
