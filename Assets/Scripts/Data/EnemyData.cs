using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class EnemyData
    {
        public int id;
        public string name;
        public int themeId;     // 스테이지 테마 필터 ID
        public int archetype;   // 스폰/배치용 적 분류 ID
        public string element;
        public string tier;     // normal, elite, boss
        public int strBase;
        public int strIncrementLvl;
        public int strIncrementUpgrade;
        public int dexBase;
        public int dexIncrementLvl;
        public int dexIncrementUpgrade;
        public int conBase;
        public int conIncrementLvl;
        public int conIncrementUpgrade;
        public int intBase;
        public int intIncrementLvl;
        public int intIncrementUpgrade;
        public int lukBase;
        public int lukIncrementLvl;
        public int lukIncrementUpgrade;
        public int atkBase;
        public int atkIncrementLvl;
        public int defBase;
        public int defIncrementLvl;
        public string ultimateResourceType;
        public string ultimateResourceName;
        public int ultimateResourceMax;
        public Dictionary<string, int> codes;
        public Dictionary<string, int> codeStages;
        // 레벨 해금 패시브 (아군 UnitData와 동일 구조). 적도 초기 패시브 + 레벨 해금 패시브를 가질 수 있다.
        public List<LevelPassiveData> levelPassives;
        public string portrait;
    }

    [Serializable]
    public class EnemyDataList
    {
        public List<EnemyData> enemies;
    }
}
