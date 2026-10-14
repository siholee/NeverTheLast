using System;
using System.Collections.Generic;
using Entities;

namespace Combat
{
    /// <summary>
    /// 고유 게이지(전투 자원)의 표시 이름. 코드가 자원을 만들 때 이름을 함께 등록한다.
    /// 카드는 첫 자원을 칸(◆)으로 그리고, 툴팁 · 로그는 여기서 이름을 받아 "창세의 결정 3/7"로 적는다.
    /// </summary>
    public static class CombatResourceLabels
    {
        private static readonly Dictionary<string, string> Labels = new();

        private static readonly Dictionary<string, Func<Unit, string>> Formatters = new();

        public static void Register(string resourceId, string displayName, Func<Unit, string> formatter = null)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;
            Labels[resourceId] = displayName;
            if (formatter != null) Formatters[resourceId] = formatter;
        }

        public static string NameOf(string resourceId)
            => resourceId != null && Labels.TryGetValue(resourceId, out string name) ? name : resourceId;

        /// <summary>"이름  자원 3/7" 꼴의 한 줄. 보여 줄 자원이 없으면 이름만 돌려준다.</summary>
        public static string Compose(Unit unit)
        {
            if (unit == null) return "";

            var parts = new List<string>();
            foreach (string resourceId in unit.CombatResourceIds)
            {
                int maximum = unit.GetCombatResourceMaximum(resourceId);
                if (maximum <= 0) continue;
                parts.Add(Formatters.TryGetValue(resourceId, out var formatter) ? formatter(unit)
                    : $"{NameOf(resourceId)} {unit.GetCombatResource(resourceId)}/{maximum}");
            }
            return parts.Count == 0 ? unit.UnitName : $"{unit.UnitName}  {string.Join("  ", parts)}";
        }
    }
}
