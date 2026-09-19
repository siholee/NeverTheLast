using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;

namespace Managers.UI.Core
{
    /// <summary>
    /// 유닛 소개에 쓰는 짧은 한글 표기(원소 · 숙련 · 주/부 스탯 · 한 줄 소개).
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

        /// <summary>숙련 목록. 없으면 "없음"(잔처럼 맨손 캐릭터).</summary>
        public static string Proficiencies(Unit unit)
        {
            IReadOnlyList<string> keys = unit?.StartingProficiencies;
            if (keys == null || keys.Count == 0) return "없음";
            return string.Join(" · ", keys.Where(key => !string.IsNullOrWhiteSpace(key)).Select(key =>
                Enum.TryParse(key, true, out EquipmentProficiency proficiency)
                    ? ItemTooltip.ProficiencyName(proficiency)
                    : key));
        }

        /// <summary>"주 DEX · 부 LUK" 꼴. 부스탯이 없으면 주스탯만 적는다.</summary>
        public static string Stats(Unit unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.MainStat)) return "";
            List<string> subs = unit.SubStats?.Where(stat => !string.IsNullOrWhiteSpace(stat)).ToList()
                                ?? new List<string>();
            return subs.Count == 0
                ? $"주 {unit.MainStat}"
                : $"주 {unit.MainStat} · 부 {string.Join(" · ", subs)}";
        }

        /// <summary>캐릭터 선택창의 한 줄 소개. 적과 소환수는 없다.</summary>
        public static string Tagline(Unit unit)
        {
            if (unit == null || unit.IsEnemy) return "";
            return GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(data => data.id == unit.ID)?.tagline ?? "";
        }
    }
}
