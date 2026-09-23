using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class UnitData
    {
        public int id;
        public string name;

        /// <summary>캐릭터 선택 화면의 한 줄 소개. "뭐 하는 캐릭터인가"만 적는다.</summary>
        public string tagline;
        public string element;
        public List<int> startingItemIds;
        // 시작 숙련은 무기 1종을 원칙으로 하며 방어구 숙련은 캐릭터별 사양이다. 추가 숙련은 패시브가 연다.
        public List<string> startingProficiencies;
        // 사냥꾼 등 분류 조건에 사용하는 데이터 태그(예: Beast, Monster).
        public List<string> tags;
        /// <summary>
        /// 플레이어에게 전투 성향을 설명하는 UI 태그(예: Burst, Support, Sturdy).
        /// 종족·진영 판정에 쓰는 tags와 섞지 않는다.
        /// </summary>
        public List<string> archetypeTags;
        /// <summary>Starter / Support / Locked. 편성 자격의 단일 출처다.</summary>
        public string characterType;
        public bool canStartAsMain;
        public bool canStartAsSupport;

        /// <summary>
        /// <b>서포트로 출전한 런을 완주하면 스타팅(메인 후보)으로 열리는가.</b>
        ///
        /// 우마무스메의 육성마와 같은 결이다 — 곁에서 한 런을 끝까지 본 서포트가 다음 런의 주인공이 된다.
        /// 영입 사건(<c>80_stages.yaml</c>)이 없는 초기 서포트 카드에게 주는 해금 경로라,
        /// 조건을 코드가 아니라 여기에 적는다. 해금되면 <c>canStartAsMain</c>이 false여도
        /// 메인 격자에 오른다(<see cref="Managers.CharacterSelectionManager.IsSupportOnly"/>).
        /// </summary>
        public bool unlocksAsStarterOnClear;
        /// <summary>계정 누적 전투 소환 횟수가 이 값에 닿으면 스타팅 후보로 해금된다. 0이면 사용하지 않는다.</summary>
        public int unlockAfterSummonCount;
        /// <summary>
        /// 이 유닛이 서포트로 참가한 육성 모드를 완주했을 때 영구 해금할 다른 유닛 ID.
        /// 서포터 자신을 스타팅으로 여는 <see cref="unlocksAsStarterOnClear"/>와 구분한다.
        /// </summary>
        public List<int> unlocksUnitIdsOnClear;
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
        /// <summary>
        /// 정수 성장치 사이를 세밀하게 조정하는 선택 배율. 0 또는 미지정은 1배다.
        /// 레벨 성장분에만 적용되며 기초·강화·훈련·장비에는 적용하지 않는다.
        /// </summary>
        public float strLevelGrowthScale;
        public float dexLevelGrowthScale;
        public float conLevelGrowthScale;
        public float intLevelGrowthScale;
        public float lukLevelGrowthScale;
        /// <summary>
        /// 최대 마나. <b>유닛 고유 스탯</b>이라 레벨·강화·훈련·버프가 일절 관여하지 않는다.
        /// 스택형 자원을 쓰는 유닛에게는 의미가 없다(그쪽은 ultimateResourceMax를 본다).
        /// 비워 두면 기본값 100이다.
        /// </summary>
        public int manaMax;
        public string ultimateResourceType;
        public string ultimateResourceName;
        public int ultimateResourceMax;
        public Dictionary<string, int> codes;
        public Dictionary<string, int> codeStages;
        // 레벨 해금 패시브: codes.passive 외에 레벨 조건을 만족하면 해금되는 목록.
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
