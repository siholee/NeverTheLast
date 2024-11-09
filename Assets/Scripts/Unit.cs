using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public int ID;
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
        // Load character data from Resources
        TextAsset characterDataFile = Resources.Load<TextAsset>("Data/01_Character");
        if (characterDataFile == null)
        {
            Debug.LogWarning("Character data file not found!");
            return;
        }

        string json = characterDataFile.text;
        CharacterDataWrapper characterWrapper = JsonUtility.FromJson<CharacterDataWrapper>(json);
        CharacterData character = characterWrapper.characters.Find(c => c.ID == ID);

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

            // Create character-specific ARTS
            CreateArts(character.ARTS);
        }
        else
        {
            Debug.LogWarning("Character data for ID " + ID + " not found!");
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
            // Create position-specific ARTS
            CreateArts(position.ARTS);
        }
        else
        {
            Debug.LogWarning("Position data for ID " + posID + " not found!");
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
            // 새 빈 GameObject를 생성하고 Arts 컴포넌트를 추가
            GameObject artsObject = new GameObject("Arts_" + artsID);
            Arts newArts = artsObject.AddComponent<Arts>();

            // Arts 데이터를 설정
            newArts.NAME = arts.NAME;
            newArts.TYPE = arts.SKILL.TYPE;
            newArts.COUNTER = arts.SKILL.COUNTER;
            newArts.CONDITIONS = arts.SKILL.CONDITION;
            newArts.EFFECTS = arts.SKILL.EFFECT;
            
            newArts.CT = 0f;
            newArts.CURRENT_CT = 0f;
            newArts.MAX_CT = 0f;

            // 스킬 소유자 설정
            newArts.OWNER = this; // 'this'는 현재 Unit 객체를 참조

            // ARTS_MANAGER 리스트에 추가
            ARTS_MANAGER.Add(newArts);

            // Arts 객체를 Unit 객체의 하위 자식으로 설정
            artsObject.transform.SetParent(this.transform);
        }
        else
        {
            Debug.LogWarning("Arts data for ID " + artsID + " not found!");
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
        // 데미지를 임시로 출력
        Debug.Log("Received Damage: " + damage);

        // 태그를 바탕으로 ARTS_MANAGER 내에 있는 조건을 검색
        foreach (Arts arts in ARTS_MANAGER)
        {
            if (arts.TYPE == "COUNT")
            {
                // CONDITIONS 배열과 태그 비교
                foreach (string condition in arts.CONDITIONS)
                {
                    foreach (string tag in tags)
                    {
                        if (condition == tag)
                        {
                            // 조건이 태그와 일치할 경우 COUNTER 증가
                            arts.COUNTER++;
                            Debug.Log("Arts: " + arts.NAME + " - Counter increased to: " + arts.COUNTER);
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
