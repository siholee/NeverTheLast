using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public int ID;
    public bool SIDE; // true: 아군, false: 적군
    public string NAME;
    public string POS;
    public int HP_CURRENT;
    public int HP_MAX;
    public int ATK;
    public int DEF;
    public float CRT_POS;
    public float CRT_DMG;
    public float CT;

    public int HP_BASE;
    public float HP_MULBUFF;
    public float HP_SUMBUFF;

    public int ATK_BASE;
    public float ATK_MULBUFF;
    public float ATK_SUMBUFF;

    public int DEF_BASE;
    public float DEF_MULBUFF;
    public float DEF_SUMBUFF;

    public float CRT_POS_BASE;
    public float CRT_POS_BUFF;

    public float CRT_DMG_BASE;
    public float CRT_DMG_BUFF;

    public float CT_BASE;
    public float CT_MULBUFF;
    public float CT_SUMBUFF;

    public List<Status> STATUS_MANAGER = new List<Status>();
    public List<Arts> ARTS_MANAGER = new List<Arts>();

    void Start()
    {
        LoadCharacterData();
        StatusUpdate();
    }

    void LoadCharacterData()
    {
        // Load ally and enemy data from Resources
        TextAsset characterDataFile = Resources.Load<TextAsset>("Data/01_Character");
        TextAsset enemyDataFile = Resources.Load<TextAsset>("Data/05_Enemy");

        if (characterDataFile == null || enemyDataFile == null)
        {
            Debug.LogWarning("Character or Enemy data file not found!");
            return;
        }

        string characterJson = characterDataFile.text;
        string enemyJson = enemyDataFile.text;

        CharacterDataWrapper characterWrapper = JsonUtility.FromJson<CharacterDataWrapper>(characterJson);
        List<CharacterData> allyDataList = characterWrapper.characters;

        EnemyDataWrapper enemyWrapper = JsonUtility.FromJson<EnemyDataWrapper>(enemyJson);
        List<EnemyData> enemyDataList = enemyWrapper.enemies;

        if (SIDE) // 아군인 경우
        {
            CharacterData character = allyDataList.Find(c => c.ID == ID);
            if (character != null)
            {
                NAME = character.NAME;
                HP_BASE = character.HP_BASE;
                ATK_BASE = character.ATK_BASE;
                DEF_BASE = character.DEF_BASE;
                HP_CURRENT = HP_BASE;
                HP_MAX = HP_BASE;
                ATK = ATK_BASE;
                DEF = DEF_BASE;

                // Load position data to get POS and ARTS
                LoadPositionData(character.POS);
                CreateArts(character.ARTS);
            }
            else
            {
                Debug.LogWarning($"Character data for ID {ID} not found in 01_Character.json!");
            }
        }
        else // 적군인 경우
        {
            EnemyData enemy = enemyDataList.Find(e => e.ID == ID);
            if (enemy != null)
            {
                NAME = enemy.NAME;
                HP_BASE = 100; // 예제 값
                ATK_BASE = 50; // 예제 값
                DEF_BASE = 20; // 예제 값
                HP_CURRENT = HP_BASE;
                HP_MAX = HP_BASE;
                ATK = ATK_BASE;
                DEF = DEF_BASE;

                // 적군의 스킬이나 추가 설정이 필요할 경우
                // Example: enemy.SKILL
            }
            else
            {
                Debug.LogWarning($"Enemy data for ID {ID} not found in 05_Enemy.json!");
            }
        }
    }

    void LoadPositionData(int posID)
    {
        // Load position data from Resources
        TextAsset positionDataFile = Resources.Load<TextAsset>("Data/02_Position");
        if (positionDataFile == null)
        {
            Debug.LogWarning("Position data file not found!");
            return;
        }

        string json = positionDataFile.text;
        PositionDataWrapper positionWrapper = JsonUtility.FromJson<PositionDataWrapper>(json);
        PositionData position = positionWrapper.positions.Find(p => p.ID == posID);

        if (position != null)
        {
            POS = position.NAME;
            CreateArts(position.ARTS);
        }
        else
        {
            Debug.LogWarning($"Position data for ID {posID} not found!");
        }
    }

    void CreateArts(int artsID)
    {
        // Load Arts data from Resources
        TextAsset artsDataFile = Resources.Load<TextAsset>("Data/03_Arts");
        if (artsDataFile == null)
        {
            Debug.LogWarning("Arts data file not found!");
            return;
        }

        string json = artsDataFile.text;
        ArtsDataWrapper artsWrapper = JsonUtility.FromJson<ArtsDataWrapper>(json);
        ArtsData arts = System.Array.Find(artsWrapper.arts.ToArray(), a => a.ID == artsID);

        if (arts != null)
        {
            GameObject artsObject = new GameObject("Arts_" + artsID);
            Arts newArts = artsObject.AddComponent<Arts>();

            newArts.NAME = arts.NAME;
            newArts.TYPE = arts.SKILL.TYPE;
            newArts.COUNTER = 0;
            newArts.MAX_COUNTER = arts.SKILL.COUNTER;
            newArts.CONDITIONS = arts.SKILL.CONDITION;
            newArts.EFFECTS = arts.SKILL.EFFECT;
            newArts.CT = 0f;
            newArts.MAX_CT = 0f;
            newArts.OWNER = this;

            ARTS_MANAGER.Add(newArts);
            artsObject.transform.SetParent(this.transform);
        }
        else
        {
            Debug.LogWarning($"Arts data for ID {artsID} not found!");
        }
    }

    public void StatusUpdate()
    {
        HP_MAX = (int)(HP_BASE * (1 + (HP_MULBUFF * 0.01f)) + HP_SUMBUFF);
        ATK = (int)(ATK_BASE * (1 + (ATK_MULBUFF * 0.01f)) + ATK_SUMBUFF);
        DEF = (int)(DEF_BASE * (1 + (DEF_MULBUFF * 0.01f)) + DEF_SUMBUFF);
        CRT_POS = CRT_POS_BASE + CRT_POS_BUFF;
        CRT_DMG = CRT_DMG_BASE + CRT_DMG_BUFF;
    }

    public void EffectHandler(float damage, string[] tags)
    {
        Debug.Log("Received Damage: " + damage);

        foreach (Arts arts in ARTS_MANAGER)
        {
            if (arts.TYPE == "COUNT")
            {
                foreach (string condition in arts.CONDITIONS)
                {
                    foreach (string tag in tags)
                    {
                        if (condition == tag)
                        {
                            arts.COUNTER++;
                            Debug.Log($"Arts: {arts.NAME} - Counter increased to: {arts.COUNTER}");
                        }
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class CharacterDataWrapper
    {
        public List<CharacterData> characters;
    }

    [System.Serializable]
    public class PositionDataWrapper
    {
        public List<PositionData> positions;
    }

    [System.Serializable]
    public class ArtsDataWrapper
    {
        public List<ArtsData> arts;
    }

    [System.Serializable]
    public class CharacterData
    {
        public int ID;
        public string NAME;
        public int POS;
        public int HP_BASE;
        public int ATK_BASE;
        public int DEF_BASE;
        public int ARTS;
    }

    [System.Serializable]
    public class PositionData
    {
        public int ID;
        public string NAME;
        public int ARTS;
    }

    [System.Serializable]
    public class ArtsData
    {
        public int ID;
        public string NAME;
        public SkillData SKILL;
    }

    [System.Serializable]
    public class SkillData
    {
        public string TYPE;
        public int COUNTER;
        public string[] CONDITION;
        public string[] EFFECT;
    }
}