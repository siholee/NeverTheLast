using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    // 다른 데이터 불러오는것도 전부 여기에 정리할 것
    public EnemyDataList FetchEnemyDataList()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Data/05_Enemy");
        EnemyDataList enemyDataWrapper = JsonUtility.FromJson<EnemyDataList>(jsonFile.text);

        if (enemyDataWrapper == null || enemyDataWrapper.enemies == null || enemyDataWrapper.enemies.Count == 0)
        {
            Debug.LogWarning("Character data is empty or invalid!");
            return null;
        }
        return enemyDataWrapper;
    }
}



[System.Serializable]
public class RoundDataWrapper
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
public class EnemyData
{
    public int ID;
    public string NAME;
    public int HP_BASE;
    public int HP_INCREASE;
    public int ATK_BASE;
    public int ATK_INCREASE;
    public int DEF_BASE;
    public int DEF_INCREASE;
    public int ARTS;
    public string PORTRAIT;
}

[System.Serializable]
public class EnemyDataList
{
    public List<EnemyData> enemies;
}