using System;
using System.Collections.Generic;
using Managers.UI.Theme;
using UnityEngine;

namespace Managers.UI.Core
{
    /// <summary>공용 인벤토리 아이템의 일관된 이름표와 설명.</summary>
    public static class ItemTooltip
    {
        public static void Attach(GameObject target, ItemData item, Entities.Unit owner = null)
        {
            if (target == null || item == null) return;
            TooltipTrigger.Attach(target, () => item.name, () => Lines(item),
                UITheme.Rarity(Mathf.Max(1, item.rarity)));
        }

        public static List<UITooltip.Line> Lines(ItemData item)
        {
            var lines = new List<UITooltip.Line>();
            if (item == null) return lines;
            lines.Add(UITooltip.Line.Note(HeadLine(item), UITheme.TextMuted));
            string effect = EffectLine(item);
            if (effect.Length > 0) lines.Add(UITooltip.Line.Note(effect, UITheme.TextPrimary));
            if (item.IsValuable)
                lines.Add(new UITooltip.Line("판매 기본가", $"{item.sellPrice:N0} G", UITheme.Positive));
            lines.Add(UITooltip.Line.Note("파티 공용 보급에 보관된다. 누구의 개인 장비도 아니다.", UITheme.TextSecondary));
            return lines;
        }

        public static string TooltipHead(ItemData item) => HeadLine(item);

        /// <summary>
        /// 쓰면 무엇이 되는지 한 줄. 아이템 데이터에는 설명 문장이 없어서 효과 필드(<c>tonic*</c> · 인장 · 두루마리 · 식량)에서 짓는다.
        /// 공용 보급 화면 · 상점 목록 · 툴팁이 같은 문구를 쓴다.
        /// </summary>
        public static string EffectLine(ItemData item)
        {
            if (item == null) return "";
            if(item.IsNectar && item.nectarFlatStats!=null)
                return "선택 아군에게 런 영구 적용 · "+string.Join(" · ",System.Linq.Enumerable.Select(item.nectarFlatStats,pair=>
                    $"{StatName(pair.Key)} +{pair.Value:0.#}{(IsPointStat(pair.Key)?"%p":"")}"));
            if (item.IsFood) return $"긴 휴식 식량 +{item.foodValue}";
            if (item.IsSeal) return $"보상 코드 하나를 같은 칸에서 한 단계 승급 (최대 T{item.sealMaxTier})";
            if (item.transferScroll) return "Tab에서 무료 드래그 이동 (기존 저장용, 소모 없음)";
            if (item.IsValuable) return "상점에서 골드로 판매";
            if (!item.IsTonic) return "";

            var parts = new List<string>();
            if (item.tonicPercentStats != null)
                foreach (KeyValuePair<string, float> pair in item.tonicPercentStats)
                    parts.Add($"{StatName(pair.Key)} +{pair.Value:0.#}%");
            if (item.tonicFlatStats != null)
                foreach (KeyValuePair<string, float> pair in item.tonicFlatStats)
                    parts.Add(IsPointStat(pair.Key) ? $"{StatName(pair.Key)} +{pair.Value:0.#}%p" : $"{StatName(pair.Key)} +{pair.Value:0.#}");
            if (item.tonicDamageTakenPercent != 0f)
                parts.Add($"받는 피해 {(item.tonicDamageTakenPercent > 0f ? "+" : "")}{item.tonicDamageTakenPercent:0.#}%");
            if (item.tonicStartManaPercent > 0f) parts.Add($"시작 마나 최대치의 {item.tonicStartManaPercent:0.#}%");
            if (item.tonicNegateFirstHarmful) parts.Add("첫 해로운 상태 1회 무효");
            return parts.Count == 0 ? "다음 전투 아군 전원에게 적용" : $"다음 전투 아군 전원 {string.Join(" · ", parts)}";
        }

        private static string StatName(string key)
            => Enum.TryParse(key, true, out BaseClasses.BaseEnums.StatType stat) ? Entities.UnitStats.DisplayName(stat) : key;

        private static bool IsPointStat(string key)
            => string.Equals(key, "CritRate", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "CritDamage", StringComparison.OrdinalIgnoreCase);

        public static string SplitHead(string english, params string[] korean)
        {
            string tail = string.Join(" · ", System.Array.FindAll(korean, part => !string.IsNullOrEmpty(part)));
            return tail.Length == 0 ? english : english + "\n" + tail;
        }

        public static string HeadLine(ItemData item)
            => item == null ? "" : $"TIER {Mathf.Max(1, item.rarity)} · {CategoryName(item.category)}";

        public static string CategoryName(string category) => category switch
        {
            "Food" => "식량",
            "Valuable" => "귀중품",
            "Consumable" => "소모품",
            "Tonic" => "전투 비약",
            "Nectar" => "영구 성장 넥타르",
            "CodeTool" => "코드 도구",
            _ => string.IsNullOrWhiteSpace(category) ? "아이템" : category,
        };

        public static Sprite LoadArt(ItemData item)
            => item == null || string.IsNullOrWhiteSpace(item.icon)
                ? null
                : Resources.Load<Sprite>($"Sprite/Items/{item.icon}");
    }
}
