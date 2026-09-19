using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>
    /// `30_synergies.yaml` — 역할군·아키타입·메인별 추천 편성.
    /// 기획 원본은 <c>Assets/Docs/Design/Detail_15_Party_Synergy.md</c>다.
    /// </summary>
    [Serializable]
    public class SynergyDataList
    {
        public List<SynergyRoleData> roles;
        public List<SynergyUnitData> units;
        public List<SynergyArchetypeData> archetypes;
        public List<SynergyRecommendationData> recommendations;
    }

    [Serializable]
    public class SynergyRoleData
    {
        public string id;
        public string name;
        public string description;
    }

    [Serializable]
    public class SynergyUnitData
    {
        public int id;
        public string name;

        /// <summary>Starter / Support / Locked. 10_units.yaml의 characterType과 같은 값이다.</summary>
        public string type;
        public string element;
        public string mainStat;

        /// <summary>성장 후 실제 최고 스탯. 서포트 카드의 특기가 될 값이다.</summary>
        public string topStat;

        /// <summary>권장 열. Front = x−1, Rear = x−2.</summary>
        public string row;

        public List<string> roles;
        public SynergyValueData supportValue;
        public List<SynergyProvideData> provides;
        public List<string> wants;
        public string caution;
    }

    [Serializable]
    public class SynergyValueData
    {
        public int combat;
        public int training;
    }

    [Serializable]
    public class SynergyProvideData
    {
        /// <summary>로컬라이즈 키로 쓸 수 있는 안정적인 문자열.</summary>
        public string key;
        public string text;
    }

    [Serializable]
    public class SynergyArchetypeData
    {
        public string id;
        public string name;
        public List<int> core;
        public string description;
        public List<int> partners;
        public string counterplay;
    }

    [Serializable]
    public class SynergyRecommendationData
    {
        public int mainId;
        public string mainName;

        /// <summary>이 메인이 무엇으로 이기는가. 한 줄 요약.</summary>
        public string axis;

        /// <summary>처음 하는 사람에게 권하는 메인인가. 캐릭터 선택 1단계 타일에 추천 띠가 붙는다.</summary>
        public bool starterRecommended;

        /// <summary>전 캐릭터 해금 + 코드 전수 완료 가정.</summary>
        public SynergyLineupData best;

        /// <summary>최초 로스터(Support 유형)만, 전수 코드 없음 가정.</summary>
        public SynergyLineupData basic;

        public string warning;
    }

    [Serializable]
    public class SynergyLineupData
    {
        public string archetype;

        /// <summary>서포트 4명. 순서가 곧 우선순위다.</summary>
        public List<int> members;

        /// <summary>메인 포함 다섯 명의 권장 열.</summary>
        public Dictionary<int, string> rows;

        public string reason;
    }
}
