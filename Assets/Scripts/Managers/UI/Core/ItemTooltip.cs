using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers.UI.Theme;
using UnityEngine;

namespace Managers.UI.Core
{
    /// <summary>
    /// 장비 한 점의 툴팁 내용.
    ///
    /// 보상 카드와 도감 슬롯이 <b>같은 장비를 다른 문장으로 설명하면</b> 플레이어가 둘을
    /// 대조하며 읽게 된다. 한글 이름표(부위·숙련)와 줄 구성을 여기 한 곳에 모아 둔다.
    /// </summary>
    public static class ItemTooltip
    {
        /// <summary>호버하면 이 장비의 제원이 뜨도록 붙인다.</summary>
        public static void Attach(GameObject target, ItemData item, Unit owner)
        {
            if (target == null || item == null) return;

            TooltipTrigger.Attach(target,
                () => item.name,
                () => Lines(item, owner),
                UITheme.Rarity(Mathf.Max(1, item.rarity)));
        }

        /// <summary>제원 줄. 카드에 적기에는 길고, 고를 때는 필요한 값들이다.</summary>
        public static List<UITooltip.Line> Lines(ItemData item, Unit owner = null)
        {
            var lines = new List<UITooltip.Line>();
            if (item == null) return lines;

            lines.Add(UITooltip.Line.Note(TooltipHead(item), UITheme.TextMuted));

            // 못 쓰는 장비인지가 가장 먼저 보여야 한다. 중량만 먹고 효과는 없다.
            if (owner != null && !owner.CanUseEquipmentEffects(item))
            {
                lines.Add(UITooltip.Line.Note("숙련 없음 — 중량만 적용된다", UITheme.Danger));
            }

            if (item.statBonuses != null)
            {
                foreach (EquipmentStatBonus bonus in item.statBonuses)
                {
                    if (bonus == null || bonus.amount == 0) continue;

                    bool isPrimary = bonus.TryGetPrimary(out BaseEnums.PrimaryStat stat);
                    Color color = isPrimary ? UITheme.Stat(stat) : UITheme.TextPrimary;
                    string label = isPrimary
                        ? bonus.stat.ToUpperInvariant()
                        : EquipmentStatKeys.DisplayName(bonus.stat);
                    lines.Add(new UITooltip.Line(label,
                        (bonus.amount > 0 ? "+" : "") + bonus.amount + EquipmentStatKeys.DisplaySuffix(bonus.stat),
                        color));
                }
            }

            lines.Add(new UITooltip.Line("중량", Mathf.Max(0, item.weight).ToString(), UITheme.TextPrimary));

            // 내구는 받는 피해에서 고정으로 깎아내는 값이다. 벌점이 아니라 이득이다.
            if (item.durability > 0)
            {
                lines.Add(new UITooltip.Line("받는 피해", "-" + item.durability, UITheme.Shield));
            }

            if (item.RequiredProficiency != EquipmentProficiency.None)
            {
                lines.Add(new UITooltip.Line("요구 숙련",
                    ProficiencyName(item.RequiredProficiency), UITheme.TextPrimary));
            }

            // 개수만 적으면 무엇을 주는지 알려고 장착해 봐야 했다. 이름과 한 줄 설명을 함께 싣는다.
            foreach (EquipmentCodeGrant grant in item.codeGrants ?? new List<EquipmentCodeGrant>())
            {
                if (grant == null) continue;
                CodeCatalog.Entry entry = CodeCatalog.Find(CodeCatalog.ParseSlot(grant.slot), grant.codeId);
                lines.Add(new UITooltip.Line("부여 코드", entry?.verbalName ?? $"#{grant.codeId}", UITheme.Accent));

                string summary = CodeCatalog.Summary(entry?.description);
                if (summary.Length > 0) lines.Add(UITooltip.Line.Note(summary, UITheme.TextSecondary));
            }

            if (item.twoHanded)
            {
                lines.Add(UITooltip.Line.Note("두 손으로 든다 — 보조 슬롯을 함께 쓴다", UITheme.TextMuted));
            }

            return lines;
        }

        /// <summary>
        /// 툴팁 머리말. <b>영문(TIER · RARE)은 윗줄, 한글(부위 · 분류)은 아랫줄</b>로 나눈다.
        /// 한 줄에 <c>TIER 3 · 주무기 · 장궁</c>으로 이어 붙였더니 영문과 한글이 섞여 읽혔다.
        /// </summary>
        public static string TooltipHead(ItemData item)
        {
            string slot = string.IsNullOrWhiteSpace(item.slot) ? "" : SlotName(item.slot);
            string category = string.IsNullOrWhiteSpace(item.category) ? "" : CategoryName(item.category);
            return SplitHead($"TIER {Mathf.Max(1, item.rarity)}", slot, category);
        }

        /// <summary>영문 줄 아래에 비어 있지 않은 한글 조각을 <c> · </c>으로 이어 한 줄 더 붙인다.</summary>
        public static string SplitHead(string english, params string[] korean)
        {
            string tail = string.Join(" · ", System.Array.FindAll(korean, part => !string.IsNullOrEmpty(part)));
            return tail.Length == 0 ? english : english + "\n" + tail;
        }

        /// <summary>등급 · 부위 · 분류를 한 줄로.</summary>
        public static string HeadLine(ItemData item)
        {
            var parts = new List<string> { $"TIER {Mathf.Max(1, item.rarity)}" };
            if (!string.IsNullOrWhiteSpace(item.slot)) parts.Add(SlotName(item.slot));
            if (!string.IsNullOrWhiteSpace(item.category)) parts.Add(CategoryName(item.category));
            return string.Join(" · ", parts);
        }

        /// <summary>장비 부위의 한글 이름. 데이터는 영문이라 여기서만 옮긴다.</summary>
        public static string SlotName(string slot) => slot switch
        {
            "MainHand" => "주무기",
            "OffHand" => "보조",
            "Armor" => "갑옷",
            "Head" => "머리",
            "Necklace" => "목걸이",
            "Ring" => "반지",
            "Shoes" => "신발",
            _ => slot,
        };

        /// <summary>
        /// 장비 분류의 한글 이름. 데이터(40_items.yaml)는 영문 키라 여기서만 옮긴다.
        ///
        /// 예전에는 상점 · 캐릭터 창이 키를 그대로 찍어 <c>Shoes</c> · <c>HeavyArmor</c> · <c>Longbow</c>가
        /// 한글 사이에 섞여 나왔다(QA). 무기·방어구 분류는 숙련 이름과 같으므로 숙련 표를 그대로 쓴다.
        /// </summary>
        public static string CategoryName(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "";
            if (Enum.TryParse(category, true, out EquipmentProficiency proficiency) &&
                proficiency != EquipmentProficiency.None)
            {
                return ProficiencyName(proficiency);
            }

            return category switch
            {
                "Clothing" => "의복",
                "Helmet" => "투구",
                "Necklace" => "목걸이",
                "Ring" => "반지",
                "Shoes" => "신발",
                "Valuable" => "귀중품",
                _ => category,
            };
        }

        /// <summary>요구 숙련의 한글 이름.</summary>
        public static string ProficiencyName(EquipmentProficiency proficiency) => proficiency switch
        {
            EquipmentProficiency.Dagger => "단검",
            EquipmentProficiency.Orb => "보주",
            EquipmentProficiency.Greatsword => "대검",
            EquipmentProficiency.Longbow => "장궁",
            EquipmentProficiency.Shortbow => "단궁",
            EquipmentProficiency.Crossbow => "쇠뇌",
            EquipmentProficiency.LightArmor => "경갑",
            EquipmentProficiency.MediumArmor => "평갑",
            EquipmentProficiency.HeavyArmor => "중갑",
            EquipmentProficiency.Shield => "방패",
            EquipmentProficiency.Longsword => "한손검",
            EquipmentProficiency.Mace => "둔기",
            EquipmentProficiency.Spear => "장창",
            EquipmentProficiency.Shortspear => "단창",
            _ => proficiency.ToString(),
        };

        /// <summary>장비 일러스트. 없으면 null.</summary>
        public static Sprite LoadArt(ItemData item)
            => item == null || string.IsNullOrWhiteSpace(item.icon)
                ? null
                : Resources.Load<Sprite>($"Sprite/Items/{item.icon}");
    }
}
