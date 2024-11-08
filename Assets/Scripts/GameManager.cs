using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject hexPrefab;     // 육각형 셀 프리팹
    public GameObject unitPrefab;    // 생성할 유닛 프리팹
    public float hexRadius = 1f;     // 육각형 반지름
    private int gridRadius = 3;      // 그리드 범위 (중앙을 기준으로 반경 3까지)
    public List<Cell> cells = new List<Cell>(); // 생성된 모든 셀을 관리하는 리스트

    void Start()
    {
        CreateHexGrid();
        CreateUnit(1,0,0,0);
    }

    // 육각형 그리드 생성 함수
    void CreateHexGrid()
    {
        float hexWidth = Mathf.Sqrt(3) * hexRadius;
        float hexHeight = 2f * hexRadius;
        float xOffset = hexWidth * 0.75f;
        float yOffset = hexHeight * 0.5f;
        cells.Clear();

        for (int x = -gridRadius; x <= gridRadius; x++)
        {
            for (int y = -gridRadius; y <= gridRadius; y++)
            {
                int z = -x - y;
                if (z < -gridRadius || z > gridRadius) continue;
                if ((x == -3 && y == 3 && z == 0) || (x == 3 && y == -3 && z == 0)) continue;

                float xPos = x * xOffset;
                if (y % 2 != 0) xPos += hexWidth * 0.5f;
                float yPos = y * (hexHeight * 0.75f);
                Vector3 position = new Vector3(xPos, 0, yPos);
                Quaternion rotation = Quaternion.Euler(90, 0, 0);
                GameObject hex = Instantiate(hexPrefab, position, rotation, transform);

                Cell cellComponent = hex.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    cellComponent.xPos = x;
                    cellComponent.yPos = y;
                    cellComponent.zPos = z;
                    cells.Add(cellComponent);
                }
            }
        }
    }

    // ID를 기반으로 유닛 생성 함수
    void CreateUnit(int id, int x, int y, int z)
    {
        // 특정 셀 위치를 기반으로 유닛 배치
        Cell targetCell = cells.Find(cell => cell.xPos == x && cell.yPos == y && cell.zPos == z);
        Vector3 spawnPosition = targetCell != null ? targetCell.transform.position + new Vector3(0, 1, 0) : Vector3.zero;

        // 유닛 생성 및 ID 설정
        GameObject unitObj = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
        unitObj.GetComponent<Unit>().ID = id;
    }
}
