using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject hexPrefab;     // 육각형 셀 프리팹
    public GameObject unitPrefab;    // 유닛 프리팹 (적과 아군 모두 사용)
    public float hexRadius = 1f;     // 육각형 반지름
    private int gridRadius = 3;      // 그리드 범위 (중앙을 기준으로 반경 3까지)
    public List<Cell> cells = new List<Cell>(); // 생성된 모든 셀을 관리하는 리스트
    private List<CharacterData> allyDataList; // 아군 데이터 리스트
    private List<EnemyData> enemyDataList; // 적 데이터 리스트
    private bool allEnemiesSummoned = false; // 모든 적이 소환되었는지 여부
    public int ROUND = 1; // 라운드 번호

    void Start()
    {
        CreateHexGrid();
        LoadData(); // 데이터 로드
        CreateUnit(1, -2, 2, 0, true); // 아군 유닛 생성 예제
    }

    void Update()
    {
        // Space bar를 눌렀을 때 적을 소환
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!allEnemiesSummoned)
            {
                RoundManager(ROUND);
                ROUND++; // 다음 라운드로 이동
            }
        }
    }

    // 데이터 로드 함수
    void LoadData()
    {
        // 아군 데이터 로드
        TextAsset characterDataFile = Resources.Load<TextAsset>("Data/01_Character");
        if (characterDataFile == null)
        {
            Debug.LogWarning("Character data file not found!");
            return;
        }
        string characterJson = characterDataFile.text;
        CharacterDataWrapper characterDataWrapper = JsonUtility.FromJson<CharacterDataWrapper>(characterJson);
        allyDataList = characterDataWrapper.characters;

        // 적 데이터 로드
        TextAsset enemyDataFile = Resources.Load<TextAsset>("Data/05_Enemy");
        if (enemyDataFile == null)
        {
            Debug.LogWarning("Enemy data file not found!");
            return;
        }
        string enemyJson = enemyDataFile.text;
        EnemyDataWrapper enemyDataWrapper = JsonUtility.FromJson<EnemyDataWrapper>(enemyJson);
        enemyDataList = enemyDataWrapper.enemies;
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
                    cellComponent.IsOccupied = false; // 초기화: 셀을 비어 있는 상태로 설정
                    cells.Add(cellComponent);
                }
            }
        }
    }

    // 유닛 생성 함수
    void CreateUnit(int id, int x, int y, int z, bool side)
    {
        Cell targetCell = cells.Find(cell => cell.xPos == x && cell.yPos == y && cell.zPos == z);
        if (targetCell == null)
        {
            Debug.LogWarning($"Target cell at ({x}, {y}, {z}) not found!");
            return;
        }

        if (targetCell.IsOccupied)
        {
            Debug.LogWarning($"Target cell at ({x}, {y}, {z}) is already occupied!");
            return;
        }

        Vector3 spawnPosition = targetCell.transform.position + new Vector3(0, 1, 0);
        GameObject unitObj = Instantiate(unitPrefab, spawnPosition, Quaternion.identity, targetCell.transform);
        Unit unit = unitObj.GetComponent<Unit>();

        if (unit != null)
        {
            if (side) // 아군 생성
            {
                CharacterData characterData = allyDataList.Find(c => c.ID == id);
                if (characterData != null)
                {
                    unit.ID = characterData.ID;
                    unit.NAME = characterData.NAME;
                    unit.ATK_BASE = characterData.ATK_BASE;
                    unit.DEF_BASE = characterData.DEF_BASE;
                    unit.HP_BASE = characterData.HP_BASE;
                }
                else
                {
                    Debug.LogWarning($"Character data for ID {id} not found in 01_Character.json!");
                }
            }
            else // 적군 생성
            {
                EnemyData enemyData = enemyDataList.Find(e => e.ID == id);
                if (enemyData != null)
                {
                    unit.ID = enemyData.ID;
                    unit.NAME = enemyData.NAME;
                    unit.ATK_BASE = 50; // 예제 값
                    unit.DEF_BASE = 20; // 예제 값
                    unit.HP_BASE = 100; // 예제 값
                }
                else
                {
                    Debug.LogWarning($"Enemy data for ID {id} not found in 05_Enemy.json!");
                }
            }

            targetCell.IsOccupied = true; // 셀이 점유되었음을 표시
        }
    }

    // 라운드 관리 함수
    void RoundManager(int roundNumber)
    {
        // Round data 파일 로드
        TextAsset roundDataFile = Resources.Load<TextAsset>("Data/07_Round");
        if (roundDataFile == null)
        {
            Debug.LogWarning("Round data file not found!");
            return;
        }
        string roundJson = roundDataFile.text;
        RoundDataWrapper roundData = JsonUtility.FromJson<RoundDataWrapper>(roundJson);

        RoundData currentRound = roundData.rounds.Find(r => r.roundNumber == roundNumber);
        if (currentRound != null)
        {
            foreach (var cellData in currentRound.cells)
            {
                int cellIndex = cellData.cellIndex;
                List<int> enemyIds = cellData.enemyIds;

                foreach (int enemyId in enemyIds)
                {
                    if (cellIndex < 0 || cellIndex >= cells.Count) continue;
                    Cell targetCell = cells[cellIndex];

                    if (!targetCell.IsOccupied)
                    {
                        CreateUnit(enemyId, targetCell.xPos, targetCell.yPos, targetCell.zPos, false);
                    }
                }
            }
            allEnemiesSummoned = true;
        }
        else
        {
            Debug.LogWarning("No enemy data found for round " + roundNumber);
        }
    }
}

// Round data 클래스
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

[System.Serializable]
public class RoundDataWrapper
{
    public List<RoundData> rounds;
}

// 아군 데이터 클래스 및 구조
[System.Serializable]
public class CharacterData
{
    public int ID;
    public string NAME;
    public int HP_BASE;
    public int ATK_BASE;
    public int DEF_BASE;
    // 추가적인 속성...
}

[System.Serializable]
public class CharacterDataWrapper
{
    public List<CharacterData> characters;
}

// 적 데이터 클래스 및 구조
[System.Serializable]
public class EnemyData
{
    public int ID;
    public string NAME;
    public SkillData SKILL;
}

[System.Serializable]
public class EnemyDataWrapper
{
    public List<EnemyData> enemies;
}

// 스킬 데이터 클래스
[System.Serializable]
public class SkillData
{
    public string TYPE;
    public float TIMER;
    public string[] CONDITION;
    public string[] EFFECT;
}