using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class UnitData
    {
        public int id;
        public string name;
        public string element;
        public List<int> startingItemIds;
        public bool canStartAsMain;
        public bool canStartAsSupport;
        public bool canUseInInfinite;
        public string mainStat;
        public string subStat;
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
        // 레벨 해금 패시브: codes.passive 외에 레벨과 INT 코드 용량을 만족하면 해금되는 목록.
        public List<LevelPassiveData> levelPassives;
        public string portrait;
        public List<int> cost;
        public int costAmount;
        public int tier;
    }

    // 레벨에 따라 해금되는 추가 패시브 정의.
    // codeId: 20_codes.yaml passive 코드 ID / unlockLevel: 해금되는 유닛 레벨 / stage: 코드 단계(1 또는 3)
    [Serializable]
    public class LevelPassiveData
    {
        public int codeId;
        public int unlockLevel;
        public int stage = 1;
    }

    [Serializable]
    public class UnitDataList
    {
        public List<UnitData> units;
    }
}
