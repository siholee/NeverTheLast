using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 라운드 보상 선택. 카드 3장 중 하나를 고른다.
    ///
    /// 예전에는 이름 아래에 <c>"한손검 | 중량 3 | STR +3, DEX +2"</c> 같은 문자열 한 줄만 놓았다.
    /// 정보가 다 들어 있어도 세 장을 <b>비교</b>할 수는 없었다 — 같은 항목이 같은 자리에 오지 않으니
    /// 눈이 매번 문장을 다시 읽어야 했기 때문이다.
    ///
    /// 지금은 아이템 데이터를 구조 그대로 받아 <b>등급 → 이름 → 스탯 → 제원</b> 순으로 세우고,
    /// 세 장의 같은 줄에 같은 항목이 오도록 맞췄다. 눈이 가로로 훑으며 비교된다.
    /// </summary>
    public class RewardScreen : ModalScreen
    {
        private const int CardCount = 3;
        private const int MaxStatRows = 4;
        private const int MaxSpecRows = 3;

        protected override string CanvasName => "RewardCanvas";
        protected override int SortingOrder => 70;
        protected override string Title => "보상 선택";
        protected override string Caption => "REWARD";
        protected override Vector2 AnchorMin => new(0.13f, 0.14f);
        protected override Vector2 AnchorMax => new(0.87f, 0.86f);

        private readonly List<Card> _cards = new();
        private List<RewardDef> _rewards = new();

        protected override void Build()
        {
            for (int i = 0; i < CardCount; i++)
            {
                _cards.Add(new Card(Body, i, OnPick));
            }
        }

        public void Show(List<RewardDef> rewards)
        {
            EnsureBuilt();
            _rewards = rewards ?? new List<RewardDef>();

            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(i < _rewards.Count ? _rewards[i] : null);
            }

            Show();
        }

        private void OnPick(int index)
        {
            if (index < 0 || index >= _rewards.Count) return;

            // 보상 대상은 필드 위 첫 아군으로 둔다(기존 동작 유지).
            Unit target = GridManager.Instance?.heroList
                ?.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
            GameManager.Instance?.rewardManager?.ApplyReward(_rewards[index], target);
        }

        // ── 표시할 값 뽑기 ───────────────────────────────────────────

        private readonly struct Row
        {
            public readonly string Label;
            public readonly string Value;
            public readonly Color Color;

            public Row(string label, string value, Color color)
            {
                Label = label;
                Value = value;
                Color = color;
            }
        }

        /// <summary>
        /// 장비의 스탯 보너스. 5스탯 색축을 그대로 입혀 어떤 스탯인지 색으로도 읽히게 한다.
        /// </summary>
        private static List<Row> StatRows(RewardDef reward)
        {
            var rows = new List<Row>();
            if (reward == null) return rows;

            if (reward.item?.statBonuses != null)
            {
                foreach (EquipmentStatBonus bonus in reward.item.statBonuses)
                {
                    if (bonus == null || bonus.amount == 0) continue;

                    Color color = Enum.TryParse(bonus.stat, true, out BaseEnums.PrimaryStat stat)
                        ? UITheme.Stat(stat)
                        : UITheme.TextPrimary;
                    rows.Add(new Row(bonus.stat.ToUpperInvariant(),
                        (bonus.amount > 0 ? "+" : "") + bonus.amount, color));
                }

                return rows;
            }

            // 장비가 아닌 보상. RewardManager.ApplyReward의 우선순위와 Unit.AddRunBonus의
            // 매핑을 그대로 따라간다 — 카드가 실제로 일어날 일만 약속하게 하려는 것이다.
            if (reward.fullHealParty)
            {
                rows.Add(new Row("파티 전체", "완전 회복", UITheme.Hp));
                return rows;
            }

            if (reward.healAmount > 0)
            {
                rows.Add(new Row("파티 회복", "+" + reward.healAmount, UITheme.Hp));
                return rows;
            }

            if (reward.randomTokenAmount > 0)
            {
                rows.Add(new Row("무작위 토큰", reward.randomTokenAmount + "개", UITheme.Accent));
                return rows;
            }

            if (reward.rerollTicketBonus > 0)
            {
                rows.Add(new Row("리롤권", "+" + reward.rerollTicketBonus, UITheme.Accent));
                return rows;
            }

            // 남은 것은 전부 5스탯 강화로 들어간다(Unit.AddRunBonus 참조).
            void AddStat(BaseEnums.PrimaryStat stat, int amount)
            {
                if (amount == 0) return;
                rows.Add(new Row(stat.ToString(), (amount > 0 ? "+" : "") + amount, UITheme.Stat(stat)));
            }

            AddStat(BaseEnums.PrimaryStat.STR, reward.atkBonus + reward.defBonus);
            AddStat(BaseEnums.PrimaryStat.DEX, Mathf.RoundToInt(reward.codeAccelerationBonus * 20f));
            AddStat(BaseEnums.PrimaryStat.CON, reward.hpBonus);
            AddStat(BaseEnums.PrimaryStat.INT, reward.intBonus);
            AddStat(BaseEnums.PrimaryStat.LUK, Mathf.RoundToInt(reward.critChanceBonus));
            return rows;
        }

        /// <summary>장비의 제원. 스탯과 달리 "쓸 수 있는가"를 판단하는 값들이다.</summary>
        private static List<Row> SpecRows(RewardDef reward)
        {
            var rows = new List<Row>();
            ItemData item = reward?.item;
            if (item == null) return rows;

            rows.Add(new Row("중량", Mathf.Max(0, item.weight).ToString(), UITheme.TextPrimary));

            if (item.durability > 0)
            {
                // 내구는 받는 피해에서 고정으로 깎아내는 양이다. 벌점이 아니라 이득이다.
                rows.Add(new Row("받는 피해", "-" + item.durability, UITheme.Shield));
            }

            if (item.RequiredProficiency != EquipmentProficiency.None)
            {
                rows.Add(new Row("요구 숙련", ProficiencyName(item.RequiredProficiency), UITheme.TextPrimary));
            }

            if (item.twoHanded && rows.Count < MaxSpecRows)
            {
                rows.Add(new Row("양손", "보조 슬롯 사용", UITheme.TextMuted));
            }

            if (item.codeGrants is { Count: > 0 } && rows.Count < MaxSpecRows)
            {
                rows.Add(new Row("부여 코드", item.codeGrants.Count + "개", UITheme.Accent));
            }

            return rows;
        }

        /// <summary>장비 부위의 한글 이름. 데이터는 영문이라 여기서만 옮긴다.</summary>
        private static string SlotName(string slot) => slot switch
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

        /// <summary>요구 숙련의 한글 이름.</summary>
        private static string ProficiencyName(EquipmentProficiency proficiency) => proficiency switch
        {
            EquipmentProficiency.Dagger => "단검",
            EquipmentProficiency.Wand => "완드",
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
            _ => proficiency.ToString(),
        };

        /// <summary>등급 · 부위 · 분류를 한 줄로. 세 장이 같은 자리에서 비교된다.</summary>
        private static string MetaLine(RewardDef reward)
        {
            int tier = Mathf.Max(1, reward.tier);
            var parts = new List<string> { $"TIER {tier}" };

            if (!string.IsNullOrWhiteSpace(reward.item?.slot)) parts.Add(SlotName(reward.item.slot));
            if (reward.isRare) parts.Add("RARE");

            return string.Join(" · ", parts);
        }

        private sealed class Card
        {
            private readonly GameObject _root;
            private readonly Image _rarityStrip;
            private readonly TextMeshProUGUI _meta;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _specCaption;
            private readonly Image _specRule;
            private readonly TextMeshProUGUI[] _statLabels = new TextMeshProUGUI[MaxStatRows];
            private readonly TextMeshProUGUI[] _statValues = new TextMeshProUGUI[MaxStatRows];
            private readonly TextMeshProUGUI[] _specLabels = new TextMeshProUGUI[MaxSpecRows];
            private readonly TextMeshProUGUI[] _specValues = new TextMeshProUGUI[MaxSpecRows];

            public Card(Transform parent, int index, Action<int> onPick)
            {
                Image panel = UIBuild.Panel($"Reward{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 12, UITheme.Outline, 1);
                _root = panel.gameObject;

                float width = 1f / CardCount;
                UIBuild.Anchor(panel.rectTransform,
                    new Vector2(index * width, 0f),
                    new Vector2((index + 1) * width, 1f), 14f, 0f);

                // 카드 어디를 눌러도 고를 수 있다. 아래 버튼은 그 사실을 알리는 표지다.
                UIBuild.OnClick(_root, () => onPick(index));

                Transform root = panel.transform;

                _rarityStrip = UIBuild.Solid("Rarity", root, UITheme.Accent);
                UIBuild.Anchor(_rarityStrip.rectTransform, new Vector2(0f, 0.972f), new Vector2(1f, 1f));

                _meta = UIBuild.Label("Meta", root, "", UITheme.FontMicro, UITheme.TextMuted);
                _meta.characterSpacing = 14f;
                UIBuild.Anchor(_meta.rectTransform, new Vector2(0f, 0.905f), new Vector2(1f, 0.965f), 18f, 0f);

                _name = UIBuild.Text("Name", root, "", UITheme.FontTitle, UITheme.TextPrimary,
                    TextAlignmentOptions.TopLeft, wrap: true);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.79f), new Vector2(1f, 0.90f), 18f, 0f);

                Rule(root, "Rule", 0.775f);

                Caption(root, "StatCaption", "STATS", 0.725f);
                for (int i = 0; i < MaxStatRows; i++)
                {
                    float top = 0.70f - i * 0.068f;
                    _statLabels[i] = RowLabel(root, $"StatLabel{i}", top);
                    _statValues[i] = RowValue(root, $"StatValue{i}", top, UITheme.FontHeading);
                }

                _specRule = Rule(root, "Rule2", 0.415f);

                _specCaption = Caption(root, "SpecCaption", "SPEC", 0.355f);
                for (int i = 0; i < MaxSpecRows; i++)
                {
                    float top = 0.33f - i * 0.062f;
                    _specLabels[i] = RowLabel(root, $"SpecLabel{i}", top);
                    _specValues[i] = RowValue(root, $"SpecValue{i}", top, UITheme.FontCaption);
                }

                // 세 장 모두 같은 행동이므로 앰버로 채우지 않고 테두리만 준다.
                // 앰버로 채우는 것은 "지금 눌러야 할 하나"에만 쓰는 표시다.
                Image pick = UIBuild.Panel("Pick", root, Color.clear,
                    UIShapes.Corner.Diagonal, 8, UITheme.Accent, 1);
                UIBuild.Anchor(pick.rectTransform, new Vector2(0f, 0.045f), new Vector2(1f, 0.125f), 18f, 0f);
                UIBuild.OnClick(pick.gameObject, () => onPick(index));

                TextMeshProUGUI pickLabel = UIBuild.Label("PickLabel", pick.transform, "선 택",
                    UITheme.FontBody, UITheme.Accent, TextAlignmentOptions.Center);
                UIBuild.Stretch(pickLabel.rectTransform);
            }

            private static Image Rule(Transform root, string name, float y)
            {
                Image rule = UIBuild.Divider(name, root);
                MoveRule(rule, y);
                return rule;
            }

            private static void MoveRule(Image rule, float y)
            {
                UIBuild.Anchor(rule.rectTransform, new Vector2(0f, y), new Vector2(1f, y), 18f, 0f);
                rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 1f);
            }

            private static void MoveBand(RectTransform rect, float top, float height)
            {
                UIBuild.Anchor(rect, new Vector2(rect.anchorMin.x, top - height),
                    new Vector2(rect.anchorMax.x, top), 18f, 0f);
            }

            private static TextMeshProUGUI Caption(Transform root, string name, string text, float bottom)
            {
                TextMeshProUGUI caption = UIBuild.Label(name, root, text, UITheme.FontMicro, UITheme.TextMuted);
                caption.characterSpacing = 20f;
                UIBuild.Anchor(caption.rectTransform,
                    new Vector2(0f, bottom), new Vector2(1f, bottom + 0.04f), 18f, 0f);
                return caption;
            }

            private static TextMeshProUGUI RowLabel(Transform root, string name, float top)
            {
                TextMeshProUGUI label = UIBuild.Text(name, root, "", UITheme.FontCaption, UITheme.TextMuted);
                UIBuild.Anchor(label.rectTransform,
                    new Vector2(0f, top - 0.058f), new Vector2(0.58f, top), 18f, 0f);
                return label;
            }

            private static TextMeshProUGUI RowValue(Transform root, string name, float top, float fontSize)
            {
                TextMeshProUGUI value = UIBuild.Text(name, root, "", fontSize, UITheme.TextPrimary,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(value.rectTransform,
                    new Vector2(0.58f, top - 0.058f), new Vector2(1f, top), 18f, 0f);
                return value;
            }

            public void Bind(RewardDef reward)
            {
                _root.SetActive(reward != null);
                if (reward == null) return;

                Color rarity = UITheme.Rarity(Mathf.Max(1, reward.tier));
                _rarityStrip.color = rarity;

                _meta.text = MetaLine(reward);
                _meta.color = reward.isRare ? rarity : UITheme.TextMuted;
                _name.text = reward.displayName;

                List<Row> stats = StatRows(reward);
                Fill(_statLabels, _statValues, stats);

                List<Row> specs = SpecRows(reward);
                _specCaption.gameObject.SetActive(specs.Count > 0);
                _specRule.gameObject.SetActive(specs.Count > 0);
                Fill(_specLabels, _specValues, specs);

                // 장비는 대개 스탯이 하나뿐이라 고정 자리에 두면 가운데가 텅 빈다.
                // 실제 줄 수만큼만 쓰고 SPEC 블록을 위로 끌어올린다.
                float statsBottom = 0.70f - Mathf.Max(1, stats.Count) * 0.068f;
                MoveRule(_specRule, statsBottom - 0.015f);
                MoveBand((RectTransform)_specCaption.transform, statsBottom - 0.055f, 0.04f);

                for (int i = 0; i < _specLabels.Length; i++)
                {
                    float top = statsBottom - 0.115f - i * 0.062f;
                    MoveBand(_specLabels[i].rectTransform, top, 0.058f);
                    MoveBand(_specValues[i].rectTransform, top, 0.058f);
                }
            }

            private static void Fill(TextMeshProUGUI[] labels, TextMeshProUGUI[] values, List<Row> rows)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    bool has = i < rows.Count;
                    labels[i].text = has ? rows[i].Label : "";
                    values[i].text = has ? rows[i].Value : "";
                    if (has) values[i].color = rows[i].Color;
                }
            }
        }
    }
}
