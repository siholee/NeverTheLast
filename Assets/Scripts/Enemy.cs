using System.Collections.Generic;
using UnityEngine;

public class Enemy : Unit
{
    public int LEVEL; // 적 레벨
    void Start()
    {
        LoadCharacterData();
        StatusUpdate();
    }
    // 캐릭터 데이터를 로드하는 함수
    public void LoadCharacterData()
    {
        // Resources 폴더에서 JSON 파일 로드
        TextAsset characterDataFile = Resources.Load<TextAsset>("Data/05_Enemy");
        if (characterDataFile == null)
        {
            Debug.LogWarning("Character data file not found!");
            return;
        }
        // JSON 데이터를 문자열로 가져오기
        string json = characterDataFile.text;
        CharacterDataWrapper characterWrapper = JsonUtility.FromJson<CharacterDataWrapper>(json);
        // 데이터 유효성 검사
        if (characterWrapper == null || characterWrapper.characters == null || characterWrapper.characters.Count == 0)
        {
            Debug.LogWarning("Character data is empty or invalid!");
            return;
        }

        // ID에 해당하는 캐릭터 데이터 찾기
        CharacterData character = System.Array.Find(characterWrapper.characters.ToArray(), c => c.ID == ID);
        if (character != null)
        {
            // 캐릭터 데이터를 Enemy 속성에 할당
            NAME = character.NAME;
            HP_BASE = SetBase(character.HP, LEVEL, character.HP_INCREASE);
            ATK_BASE = SetBase(character.ATK, LEVEL, character.ATK_INCREASE);
            DEF_BASE = SetBase(character.DEF, LEVEL, character.DEF_INCREASE);
            CRT_POS_BASE = character.CRT_POS;
            CRT_DMG_BASE = character.CRT_DMG;
            CT_BASE = character.CT;
        }
        else
        {
            Debug.LogWarning($"Character with ID {ID} not found in character data!");
        }
    }
   
    public int SetBase(int n, int lv, int increase){
        return n + lv * increase;
    }
}
// CharacterDataWrapper 클래스 정의
[System.Serializable]
public class CharacterDataWrapper
{
    public List<CharacterData> characters;
}

// CharacterData 클래스 정의
[System.Serializable]
public class CharacterData
{
    public int ID;
    public string NAME;
    public int HP;
    public int HP_INCREASE;
    public int ATK;
    public int ATK_INCREASE;
    public int DEF;
    public int DEF_INCREASE;
    public int CRT_POS;
    public int CRT_DMG;
    public int CT;
}
