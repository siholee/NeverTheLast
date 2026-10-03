using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>
    /// 아군 유닛 정의(<c>10_units.yaml</c>). 스탯 · 코드 · 초상화는 <see cref="CombatantData"/>에 있다.
    /// </summary>
    [Serializable]
    public class UnitData : CombatantData
    {
        /// <summary>캐릭터 선택 화면의 한 줄 소개. "뭐 하는 캐릭터인가"만 적는다.</summary>
        public string tagline;
        /// <summary>
        /// 플레이어에게 전투 성향을 설명하는 UI 태그(예: Swift, Support, Shielding).
        /// 종족 · 진영 판정에 쓰는 tags와 섞지 않는다. 표시 이름은 <c>UnitTagCatalog</c>가 정한다.
        /// </summary>
        public List<string> archetypeTags;
        /// <summary>잔처럼 행동 전에 소모하는 반격 기회의 기초 최대치. 0이면 해당 자원을 만들지 않는다.</summary>
        public int counterUsesBase;
        /// <summary>
        /// Unlocked / Locked. 편성 자격의 단일 출처다. Unlocked는 처음부터 쓸 수 있고, Locked는 계정 해금
        /// (영입 사건 등)이 있어야 쓴다. 메인 · 서포트 구분은 없다 — 해금된 캐릭터는 누구나 무한 모드에 선다.
        /// </summary>
        public string characterType;
        /// <summary>
        /// 아직 못 얻은 동안 선택 화면에 띄울 해금 조건 한 줄. 비어 있으면 잠금 표시를 하지 않는다.
        /// </summary>
        public string unlockCondition;
    }

    [Serializable]
    public class UnitDataList
    {
        public List<UnitData> units;
    }
}
