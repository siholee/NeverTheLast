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
        private static System.Collections.Generic.Dictionary<int, string> _enemyIntros;
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
        /// <summary>캐릭터 선택창의 한 줄 소개. 적은 적 데이터의 전투 소개를 쓴다.</summary>
        public static string Tagline(Unit unit)
        {
            if (unit == null) return "";
            if (unit.IsEnemy)
            {
                _enemyIntros ??= GameManager.Instance?.dataManager?.FetchEnemyDataList()?.enemies?
                    .ToDictionary(data => data.id, data => data.tagline);
                return _enemyIntros != null && _enemyIntros.TryGetValue(unit.ID, out string intro) && !string.IsNullOrWhiteSpace(intro)
                    ? intro : "전장을 위협하는 공허의 생물.";
            }
            return GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(data => data.id == unit.ID)?.tagline
                ?? (unit.IsSummon ? "주인의 명령에 따라 전투를 지원하는 소환수." : "고유 능력으로 전투에 참여하는 아군.");
        }

        public static System.Collections.Generic.IReadOnlyList<UITooltip.Line> IdentityLines(Unit unit)
            => new[]
            {
                new UITooltip.Line("원소", ElementName(unit.Element), Theme.UITheme.TextPrimary),
                UITooltip.Line.Note(CardIntroduction(unit), Theme.UITheme.TextSecondary),
            };

        /// <summary>카드에는 긴 스킬 요약 대신 소개의 첫 문장만 보여 준다.</summary>
        public static string CardIntroduction(Unit unit)
        {
            string intro = Tagline(unit).Trim();
            int end = intro.IndexOf('.');
            if (end >= 0) intro = intro.Substring(0, end + 1);
            return intro;
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
