using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>레벨 조건이 붙은 장비 한 칸.</summary>
    [Serializable]
    public class LeveledItemData
    {
        public int itemId;

        /// <summary>이 레벨 이상일 때만 장착하고 나온다.</summary>
        public int minLevel;

        /// <summary>
        /// 이 레벨 이하일 때만 장착하고 나온다. 0이면 상한이 없다.
        /// 초반에만 드는 무뎌진 무기를 뒤 스테이지에서 온전한 무기로 <b>갈아 들게</b> 하려고 둔다.
        /// </summary>
        public int maxLevel;
    }

    [Serializable]
    public class EnemyData
    {
        public int id;
        public string name;
        public int themeId;     // 스테이지 테마 필터 ID
        public int archetype;   // 스폰/배치용 적 분류 ID
        public string element;
        public string tier;     // normal, elite, boss
        public List<int> startingItemIds;
        // 적도 장비 숙련을 데이터로 보유한다. 전용 장비 및 숙련 조건 패시브 판정에 사용한다.
        public List<string> startingProficiencies;
        // Beast / Monster 등 패시브 판정용 분류 태그.
        public List<string> tags;
        // 주/부 스탯. 주스탯이 위력(AttackPower)의 근거이며 ×1.2, 부스탯은 ×1.1 배율을 받는다.
        public string mainStat;
        public string subStat;
        public List<string> subStats;
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
        /// <summary>
        /// 최대 마나. <b>유닛 고유 스탯</b>이라 레벨·강화·훈련·버프가 일절 관여하지 않는다.
        /// 스택형 자원을 쓰는 유닛에게는 의미가 없다(그쪽은 ultimateResourceMax를 본다).
        /// 비워 두면 기본값 100이다.
        /// </summary>
        /// <summary>
        /// 레벨 조건이 붙는 시작 장비. 적의 Level은 현재 스테이지와 같으므로
        /// <b>후반 스테이지에서만 무장하고 나오는 병종</b>을 이것으로 표현한다.
        /// 조건 없이 항상 드는 장비는 <see cref="startingItemIds"/>에 둔다.
        /// </summary>
        public List<LeveledItemData> startingItemsByLevel;

        /// <summary>
        /// 이 적을 <b>처치했을 때</b> 보상 후보에 오르는 장비·귀중품 ID. 전리품이라 대개 들고 있던 장비다.
        /// 한 전투에서 처치한 적들의 드랍 + 공용 드랍 + 소모품이 한 풀로 합쳐진 뒤 티어 확률로 3장이 뽑힌다.
        /// </summary>
        public List<int> drops;

        public int manaMax;
        public string ultimateResourceType;
        public string ultimateResourceName;
        public int ultimateResourceMax;
        public Dictionary<string, int> codes;
        public Dictionary<string, int> codeStages;
        // 레벨 해금 패시브 (아군 UnitData와 동일 구조). 적도 초기 패시브 + 레벨 해금 패시브를 가질 수 있다.
        public List<LevelPassiveData> levelPassives;
        public string portrait;
        public string standing;
    }

    [Serializable]
    public class EnemyDataList
    {
        public List<EnemyData> enemies;
    }
}
