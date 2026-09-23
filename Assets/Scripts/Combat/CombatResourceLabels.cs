using System.Collections.Generic;
using Entities;

namespace Combat
{
    /// <summary>
    /// 카드 이름표에 띄울 전투 자원의 이름.
    ///
    /// 이름표는 원래 "먼저 등록된 자원 하나"를 ◆로만 보여 줬다. 가우디의 스택처럼 캐릭터가
    /// 하나씩만 들고 있을 때는 충분했지만, 천공 전선의 소환체는 <b>무적 횟수와 카운트다운을
    /// 함께</b> 보여 줘야 우선순위를 읽을 수 있다. 이름이 붙은 자원은 전부 띄우고,
    /// 이름이 없는 자원은 예전처럼 첫 하나만 ◆로 띄운다.
    /// </summary>
    public static class CombatResourceLabels
    {
        private sealed class Label
        {
            public string Text;
            public bool ShowMaximum;

            /// <summary>켜져 있는지만 알리면 되는 자원. 숫자 없이 이름만 띄운다.</summary>
            public bool NameOnly;
        }

        private static readonly Dictionary<string, Label> Labels = new()
        {
            { SkyFrontlineResources.Ward, new Label { Text = "무적", ShowMaximum = false } },
            { SkyFrontlineResources.Countdown, new Label { Text = "발동까지", ShowMaximum = false } },
            { SkyFrontlineResources.Satiety, new Label { Text = "포만", ShowMaximum = false } },
            { CoastFrontlineResources.Armor, new Label { Text = "갑주", NameOnly = true } },
            { CoastFrontlineResources.Charge, new Label { Text = "준비", NameOnly = true } },
            { CoastFrontlineResources.Phase, new Label { Text = "구간", ShowMaximum = true } },
            { Codes.Passive.ShakespeareCombat.LanguageResource, new Label { Text = "언어", ShowMaximum = false } },
            { Codes.Passive.FestinaLenteEffect.VirtualManaResource, new Label { Text = "가상 마나", ShowMaximum = true } },
        };

        /// <summary>이름표 문자열. 보여 줄 자원이 없으면 이름만 돌려준다.</summary>
        public static string Compose(Unit unit)
        {
            if (unit == null) return "";

            var parts = new List<string>();
            bool unnamedShown = false;
            foreach (string resourceId in unit.CombatResourceIds)
            {
                int maximum = unit.GetCombatResourceMaximum(resourceId);
                if (maximum <= 0) continue;
                int current = unit.GetCombatResource(resourceId);

                if (Labels.TryGetValue(resourceId, out Label label))
                {
                    // 다 쓴 무적·비어 있는 포만은 숨긴다. 이름표가 길어지면 이름이 먼저 잘린다.
                    if (current <= 0) continue;
                    if (label.NameOnly) { parts.Add(label.Text); continue; }
                    parts.Add(label.ShowMaximum ? $"{label.Text} {current}/{maximum}" : $"{label.Text} {current}");
                    continue;
                }

                if (unnamedShown) continue;
                unnamedShown = true;
                parts.Add($"◆{current}/{maximum}");
            }

            return parts.Count == 0 ? unit.UnitName : $"{unit.UnitName}  {string.Join("  ", parts)}";
        }
    }
}
