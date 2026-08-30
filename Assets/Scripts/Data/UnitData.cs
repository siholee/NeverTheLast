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
        // 시작 숙련은 무기 1종을 원칙으로 하며 방어구 숙련은 캐릭터별 사양이다. 추가 숙련은 패시브가 연다.
        public List<string> startingProficiencies;
        // 사냥꾼 등 분류 조건에 사용하는 데이터 태그(예: Beast, Monster).
        public List<string> tags;
        /// <summary>Starter / Support / Locked. 편성 자격의 단일 출처다.</summary>
        public string characterType;
        public bool canStartAsMain;
        public bool canStartAsSupport;
        public bool canUseInInfinite;
        public string mainStat;
        public string subStat;
        public List<string> subStats;
        public float mainStatTrainingBonus;
        public float subStatTrainingBonus;
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
        public string ultimateResourceType;
        public string ultimateResourceName;
        public int ultimateResourceMax;
        public Dictionary<string, int> codes;
        public Dictionary<string, int> codeStages;
        // 레벨 해금 패시브: codes.passive 외에 레벨과 INT 코드 용량을 만족하면 해금되는 목록.
        public List<LevelPassiveData> levelPassives;
        public string portrait;
        // 비주얼 노벨/캐릭터 소개용 세로 전신 일러스트. 없으면 portrait로 폴백한다.
        public string standing;
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
