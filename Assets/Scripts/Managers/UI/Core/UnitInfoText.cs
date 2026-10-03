using System;
using System.Linq;
using Entities;

namespace Managers.UI.Core
{
    /// <summary>
    /// 유닛 소개에 쓰는 짧은 한글 표기(원소 · 전투 성향 · 주/부 스탯 · 한 줄 소개).
    /// 전장 카드 툴팁이 캐릭터 선택창과 같은 말로 유닛을 소개하게 한 곳에 모았다.
    /// </summary>
    public static class UnitInfoText
    {
        public static string ElementName(string element) => element switch
        {
            "Pyro" => "불",
            "Hydro" => "물",
            "Anemo" => "바람",
            "Electro" => "번개",
            "Dendro" => "풀",
            "Cryo" => "얼음",
            "Geo" => "바위",
            "Void" => "공허",
            _ => string.IsNullOrWhiteSpace(element) || element == "None" ? "무속성" : element,
        };

        /// <summary>"공격 120 · 방어 90 · 속도 110" 꼴의 한 줄. 카드 툴팁이 쓴다.</summary>
        public static string Stats(Unit unit)
        {
            if (unit == null) return "";
            return $"공격 {unit.AtkCurr} · 방어 {unit.DefCurr} · 특방 {unit.SpDefCurr} · 속도 {unit.SpeedCurr:0}";
        }
        /// <summary>캐릭터 선택창의 한 줄 소개. 적과 소환수는 없다.</summary>
        public static string Tagline(Unit unit)
        {
            if (unit == null || unit.IsEnemy) return "";
            return GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(data => data.id == unit.ID)?.tagline ?? "";
        }

        /// <summary>아군의 전투 성향 UI 태그. 종족·진영 판정용 tags와는 별개다.</summary>
        public static System.Collections.Generic.IReadOnlyList<UnitTagCatalog.Definition> ArchetypeTags(Unit unit)
        {
            if (unit == null || unit.IsEnemy) return Array.Empty<UnitTagCatalog.Definition>();
            UnitData data = GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(candidate => candidate.id == unit.ID);
            return UnitTagCatalog.Resolve(data?.archetypeTags);
        }
    }
}
