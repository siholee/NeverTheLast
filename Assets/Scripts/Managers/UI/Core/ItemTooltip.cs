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
            if (item.IsFood)
                lines.Add(new UITooltip.Line("식량", $"+{item.foodValue}", UITheme.Accent));
            if (item.IsValuable)
                lines.Add(new UITooltip.Line("판매 기본가", $"{item.sellPrice:N0} G", UITheme.Positive));
            lines.Add(UITooltip.Line.Note("파티 공용 인벤토리에 보관된다.", UITheme.TextSecondary));
            return lines;
        }

        public static string TooltipHead(ItemData item) => HeadLine(item);

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
            _ => string.IsNullOrWhiteSpace(category) ? "아이템" : category,
        };

        public static Sprite LoadArt(ItemData item)
            => item == null || string.IsNullOrWhiteSpace(item.icon)
                ? null
                : Resources.Load<Sprite>($"Sprite/Items/{item.icon}");
    }
}
