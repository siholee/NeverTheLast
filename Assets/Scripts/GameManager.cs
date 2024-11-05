using UnityEngine;
using System.Collections.Generic; // 리스트를 사용하기 위해 필요

public class GameManager : MonoBehaviour
{
    public GameObject hexPrefab;     // 육각형 셀 프리팹
    public GameObject unitPrefab;    // 생성할 유닛 프리팹
    public float hexRadius = 1f;     // 육각형 반지름
    private int gridRadius = 3;      // 그리드 범위 (중앙을 기준으로 반경 3까지)
    public Material[] materials;     // 다양한 Material 배열로 준비
    public List<Cell> cells = new List<Cell>(); // 생성된 모든 셀을 관리하는 리스트

    void Start()
    {
        CreateHexGrid();
    }

    void Update()
    {
        // 스페이스 바를 누르면 유닛 생성
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CreateUnit();
        }
    }

    void CreateHexGrid()
    {
        // 육각형의 정확한 가로 및 세로 간격 계산
        float hexWidth = Mathf.Sqrt(3) * hexRadius; // sqrt(3) * 반지름 = 육각형의 가로 길이
        float hexHeight = 2f * hexRadius;           // 2 * 반지름 = 육각형의 세로 길이
        float xOffset = hexWidth * 0.75f;           // 육각형을 x 방향으로 이동할 때의 오프셋
        float yOffset = hexHeight * 0.5f;           // 홀수 줄에서의 세로 오프셋

        // 리스트 초기화
        cells.Clear();

        for (int x = -gridRadius; x <= gridRadius; x++)
        {
            for (int y = -gridRadius; y <= gridRadius; y++)
            {
                int z = -x - y;

                // 좌표 조건 확인: x + y + z = 0 이어야 하고, 특정 좌표는 제외
                if (z < -gridRadius || z > gridRadius) continue;
                if ((x == -3 && y == 3 && z == 0) || (x == 3 && y == -3 && z == 0)) continue;

                // 홀수 줄에서는 X 위치에 오프셋 추가 (지그재그 배치)
                float xPos = x * xOffset;
                if (y % 2 != 0) // y가 홀수일 경우에만 xPos에 오프셋을 추가
                {
                    xPos += hexWidth * 0.5f;
                }

                float yPos = y * (hexHeight * 0.75f); // y 방향으로 약간씩 겹쳐지도록 배치

                // 육각형 셀 인스턴스 생성 (회전값 적용)
                Vector3 position = new Vector3(xPos, 0, yPos); // 2D에서 보면 z가 y와 같은 역할을 함
                Quaternion rotation = Quaternion.Euler(90, 0, 0); // X축을 기준으로 90도 회전
                GameObject hex = Instantiate(hexPrefab, position, rotation, transform);

                // 무작위로 Material 할당
                if (materials.Length > 0)
                {
                    int randomIndex = Random.Range(0, materials.Length);
                    hex.GetComponent<Renderer>().material = materials[randomIndex];
                }

                // Cell 클래스에 xPos, yPos, zPos 값 설정 및 리스트에 추가
                Cell cellComponent = hex.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    cellComponent.xPos = x;
                    cellComponent.yPos = y;
                    cellComponent.zPos = z;
                    cells.Add(cellComponent); // 생성된 셀을 리스트에 추가
                }
            }
        }
    }

    // 주어진 x, y, z 좌표에 유닛을 배치하는 함수
    public void PlaceUnitAtCell(int x, int y, int z)
    {
        // 해당하는 셀을 찾음
        foreach (Cell cell in cells)
        {
            if (cell.xPos == x && cell.yPos == y && cell.zPos == z)
            {
                // 유닛 생성 (셀의 위치에 바로 배치)
                Vector3 unitPosition = cell.transform.position + new Vector3(0, 1, 0); // 셀 위에 배치
                GameObject unit = Instantiate(unitPrefab, unitPosition, Quaternion.identity);
                unit.transform.SetParent(cell.transform); // 유닛을 셀의 자식으로 설정
                break;
            }
        }
    }

    // 스페이스 바를 눌렀을 때 임의의 셀에 유닛을 생성하는 함수
    void CreateUnit()
    {
        // 유닛을 배치할 셀을 무작위로 선택
        int randomIndex = Random.Range(0, cells.Count);
        Cell selectedCell = cells[randomIndex];

        // 유닛을 선택한 셀에 배치
        PlaceUnitAtCell(selectedCell.xPos, selectedCell.yPos, selectedCell.zPos);
    }
}
