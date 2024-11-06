using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class GameManager : MonoBehaviour
{
    public GameObject hexPrefab;     // 육각형 셀 프리팹
    public GameObject unitPrefab;    // 생성할 유닛 프리팹
    public float hexRadius = 1f;     // 육각형 반지름
    private int gridRadius = 3;      // 그리드 범위 (중앙을 기준으로 반경 3까지)
    public Material[] materials;     // 다양한 Material 배열로 준비
    public List<Cell> cells = new List<Cell>(); // 생성된 모든 셀을 관리하는 리스트

    [System.Serializable]
    public class CharacterData
    {
        public int ID;
        public string NAME;
        public string POS;
        public int HP_BASE;
        public int ATK_BASE;
        public int DEF_BASE;
        public SkillData ARTS;
        public SkillData PASSIVE;
    }

    [System.Serializable]
    public class SkillData
    {
        public string NAME;
        public string TYPE;
        public int COUNTER;
        public float TIMER;
        public string[] CONDITION;
        public object[] EFFECT; // 다양한 형식의 효과를 받을 수 있게 object 배열로 설정
    }

    [System.Serializable]
    public class CharacterDataArray
    {
        public CharacterData[] array;
    }


    void Start()
    {
        CreateHexGrid();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CreateUnit(1, 0, 0, 0); // 예시로 ID 1번 캐릭터를 생성
        }
    }

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

                if (materials.Length > 0)
                {
                    int randomIndex = Random.Range(0, materials.Length);
                    hex.GetComponent<Renderer>().material = materials[randomIndex];
                }

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

    void CreateUnit(int id, int x, int y, int z)
    {
        CharacterData characterData = LoadCharacterData(id);
        if (characterData == null)
        {
            Debug.LogWarning("Character data not found!");
            return;
        }

        Cell targetCell = cells.Find(cell => cell.xPos == x && cell.yPos == y && cell.zPos == z);
        Vector3 spawnPosition = targetCell != null ? targetCell.transform.position + new Vector3(0, 1, 0) : Vector3.zero;

        GameObject unitObj = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
        Unit unit = unitObj.GetComponent<Unit>();
        if (unit != null)
        {
            unit.ID = characterData.ID;
            unit.NAME = characterData.NAME;
            unit.POS = characterData.POS;
            unit.HP_BASE = characterData.HP_BASE;
            unit.ATK_BASE = characterData.ATK_BASE;
            unit.DEF_BASE = characterData.DEF_BASE;
            unit.HP_CURRENT = characterData.HP_BASE;
            unit.HP_MAX = characterData.HP_BASE;
            unit.ATK = characterData.ATK_BASE;
            unit.DEF = characterData.DEF_BASE;
            
            // 스킬 추가
            Skill artsSkill = unitObj.AddComponent<Skill>();
            artsSkill.SKILL_NAME = characterData.ARTS.NAME;
            artsSkill.SKILL_TYPE = characterData.ARTS.TYPE;
            artsSkill.SKILL_COUNTER = characterData.ARTS.COUNTER;
            artsSkill.SKILL_TIMER = characterData.ARTS.TIMER;
            unit.SKILL_MANAGER.Add(artsSkill);

            Skill passiveSkill = unitObj.AddComponent<Skill>();
            passiveSkill.SKILL_NAME = characterData.PASSIVE.NAME;
            passiveSkill.SKILL_TYPE = characterData.PASSIVE.TYPE;
            passiveSkill.SKILL_COUNTER = characterData.PASSIVE.COUNTER;
            unit.SKILL_MANAGER.Add(passiveSkill);
        }
    }

    CharacterData LoadCharacterData(int id)
    {
        TextAsset dataFile = Resources.Load<TextAsset>("Data/01_Character");
        if (dataFile == null) return null;

        // JSON 데이터를 래핑된 객체로 파싱
        CharacterDataArray characterArray = JsonUtility.FromJson<CharacterDataArray>($"{{\"array\":{dataFile.text}}}");
        foreach (CharacterData character in characterArray.array)
        {
            if (character.ID == id)
            {
                return character;
            }
        }
        return null;
    }


}
