using System.Collections.Generic;
using UnityEngine;
using YamlDotNet.Serialization;

public class DataManager : MonoBehaviour
{
    // 다른 데이터 불러오는것도 전부 여기에 정리할 것
    public HeroDataList FetchHeroDataList()
    {
        TextAsset heroData = Resources.Load<TextAsset>("Data/10_heroes");
        var deserializer = new DeserializerBuilder().Build();
        if (heroData == null)
        {
            Debug.LogError("Data/10_heroes.yaml not found.");
            return null;
        }

        HeroDataList heroDataList = deserializer.Deserialize<HeroDataList>(heroData.text);
        return heroDataList;
    }

    public EnemyDataList FetchEnemyDataList()
    {
        TextAsset enemyData = Resources.Load<TextAsset>("Data/20_enemies");
        var deserializer = new DeserializerBuilder().Build();
        if (enemyData == null)
        {
            Debug.LogError("Data/20_enemies.yaml not found.");
            return null;
        }

        EnemyDataList enemyDataList = deserializer.Deserialize<EnemyDataList>(enemyData.text);
        return enemyDataList;
    }

    public CodeDataList FetchCodeDataList()
    {
        TextAsset codeData = Resources.Load<TextAsset>("Data/30_codes");
        var deserializer = new DeserializerBuilder().Build();
        if (codeData == null)
        {
            Debug.LogError("Data/30_codes.yaml not found.");
            return null;
        }

        CodeDataList codeDataList = deserializer.Deserialize<CodeDataList>(codeData.text);
        return codeDataList;
    }
    public RoundDataList FetchRoundDataList()
    {
        TextAsset roundData = Resources.Load<TextAsset>("Data/70_rounds");
        var deserializer = new DeserializerBuilder().Build();
        if (roundData == null)
        {
            Debug.LogError("Data/70_rounds.yaml not found.");
            return null;
        }

        RoundDataList roundDataList = deserializer.Deserialize<RoundDataList>(roundData.text);
        return roundDataList;
    }
}



[System.Serializable]
public class RoundDataList
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

[System.Serializable]
public class HeroData
{
    public int id;
    public string name;
    public int[] synergies;
    public int hp;
    public int hp_increase;
    public int atk;
    public int atk_increase;
    public int def;
    public int def_increase;
    public int crit_chance;
    public int crit_chance_increase;
    public int crit_damage;
    public int crit_damage_increase;
    public int mana;
    public Dictionary<string, int> codes;
    public string portrait;
}

[System.Serializable]
public class HeroDataList
{
    public List<HeroData> heroes;
}

[System.Serializable]
public class EnemyData
{
    public int id;
    public string name;
    public int hp_base;
    public int hp_increment;
    public int atk_base;
    public int atk_increment;
    public int def_base;
    public int def_increment;
    public int crit_chance_base;
    public int crit_damage_base;
    public Dictionary<string, int> codes;
    public string portrait;
}

[System.Serializable]
public class EnemyDataList
{
    public List<EnemyData> enemies;
}

[System.Serializable]
public class CodeData
{
    public int id;           // 코드 ID
    public string name;      // 코드 이름
    public float cooldown;   // 기본 쿨타임
    public bool isPassive;   // 패시브 여부
    public int counter;      // 최대 사용 횟수
    public int attribute;    // Attribute (HP, ATK, DEF 등)
    public float coefficient; // 스킬 계수
    public int elementId;    // 속성 ID
}

[System.Serializable]
public class CodeDataList
{
    public List<CodeData> codes; // 코드 목록
}