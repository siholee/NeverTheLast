using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Helpers;
using Managers.UI.Core;
using PartyTonicState = Core.PartyTonicState;
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
    /// 지금은 <b>등급 → 이름 → 일러스트 → 요약</b> 순으로 세운다. 장비에 일러스트가 붙으면서
    /// 카드가 제원을 다 이고 있을 자리가 없어졌고, 무엇보다 <b>그림이 먼저 눈에 들어와야</b>
    /// 세 장을 훑는 속도가 빨라진다. 숫자와 조건은 마우스를 올렸을 때 툴팁으로 보인다.
    ///
    /// 장비가 아닌 보상도 일러스트 경로가 있으면 같은 카드 구조를 사용한다.
    /// </summary>
    public class RewardScreen : ModalScreen
    {
        /// <summary>한 줄에 놓는 카드 수. 넷 이상이면 두 줄로 나눈다.</summary>
        private const int CardsPerRow = 3;
        private const int MaxStatRows = 4;
        private const int MaxSpecRows = 4;

        protected override string CanvasName => "RewardCanvas";
        protected override int SortingOrder => 70;
        protected override string Title => "보상 선택";
        protected override string Caption => "REWARD";
        protected override Vector2 AnchorMin => new(0.13f, 0.14f);
        protected override Vector2 AnchorMax => new(0.87f, 0.86f);

        private readonly List<Card> _cards = new();
        private List<RewardDef> _rewards = new();
        private RectTransform _cardsRoot;
        private UnitTargetPicker _targetPicker;
        private RewardDef _pendingTargetReward;

        protected override void Build()
        {
            _cardsRoot = UIBuild.Container("RewardCards", Body);
            UIBuild.Stretch(_cardsRoot);
            for (int i = 0; i < RewardManager.MaxRewardCount; i++)
            {
                _cards.Add(new Card(_cardsRoot, i, OnPick));
            }

            _targetPicker = new UnitTargetPicker(Body, OnTargetPicked, ShowRewardCards, "보상 카드로 돌아가기");
        }

        public void Show(List<RewardDef> rewards)
        {
            EnsureBuilt();
            _rewards = rewards ?? new List<RewardDef>();

            // 보상은 스테이지가 오를수록 3장에서 6장까지 는다. 셋까지는 한 줄, 넷부터는 3열 두 줄이다.
            int shown = Mathf.Min(_rewards.Count, _cards.Count);
            int columns = Mathf.Clamp(shown, 1, CardsPerRow);
            int rows = Mathf.Max(1, Mathf.CeilToInt(shown / (float)CardsPerRow));
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i < shown) _cards[i].Layout(i % CardsPerRow, i / CardsPerRow, columns, rows);
                _cards[i].Bind(i < shown ? _rewards[i] : null);
            }

            ShowRewardCards();
            Show();
        }

        private void OnPick(int index)
        {
            if (index < 0 || index >= _rewards.Count) return;

            RewardDef reward = _rewards[index];
            if (reward.RequiresTargetSelection)
            {
                ShowRewardTargets(reward);
                return;
            }

            // 장비 등 기존 단일 대상 보상은 기존 동작대로 필드 위 첫 아군을 사용한다.
            Unit target = GridManager.Instance?.heroList
                ?.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
            GameManager.Instance?.rewardManager?.ApplyReward(reward, target);
        }

        private void ShowRewardTargets(RewardDef reward)
        {
            List<Unit> candidates = TargetCandidates(reward);
            if (candidates.Count == 0) return;

            _pendingTargetReward = reward;
            _cardsRoot.gameObject.SetActive(false);
            SetTitle(reward.displayName + " — 대상 선택");
            _targetPicker.Show(candidates, TargetGuide(reward), unit => TargetPreview(reward, unit));
        }

        /// <summary>대상 선택 판의 안내문. 상점도 같은 문구를 쓴다.</summary>
        public static string TargetGuide(RewardDef reward)
        {
            if (reward.RequiresLevelTargetSelection)
                return "레벨을 올릴 아군을 선택하세요. 오른 뒤의 레벨을 함께 표시합니다.";

            return reward.RequiresReviveTargetSelection
                ? "부활시킬 아군을 선택하세요. 부활 후 체력을 함께 표시합니다."
                : "회복할 아군을 선택하세요. 현재 체력과 회복 후 체력을 함께 표시합니다.";
        }

        /// <summary>카드 아래 한 줄. 쓰고 나면 체력이 어떻게 되는지를 미리 보여 준다.</summary>
        public static string TargetPreview(RewardDef reward, Unit unit)
        {
            if (reward.RequiresLevelTargetSelection)
            {
                return $"Lv.{unit.Level}\n→ Lv.{unit.Level + reward.levelGrant}";
            }

            if (reward.RequiresReviveTargetSelection)
            {
                int after = RewardManager.CalculateReviveHp(reward, unit);
                return $"전투 불능\n→ HP {after:N0} / {unit.HpMax:N0}";
            }

            int amount = RewardManager.CalculateHealingAmount(reward, unit);
            int healed = Mathf.Min(unit.HpMax, unit.HpCurr + amount);
            return $"HP {unit.HpCurr:N0} / {unit.HpMax:N0}\n→ {healed:N0} / {unit.HpMax:N0}";
        }

        /// <summary>회복·부활 대상 후보. 상점도 같은 조건을 쓴다.</summary>
        public static List<Unit> TargetCandidates(RewardDef reward)
        {
            if (reward.RequiresLevelTargetSelection) return RewardManager.LevelGrantCandidates();

            return GridManager.Instance?.heroList?
                .Where(hero => reward.RequiresReviveTargetSelection
                    ? RewardManager.IsValidReviveTarget(hero)
                    : RewardManager.IsValidHealingTarget(hero) && hero.HpCurr < hero.HpMax)
                .ToList() ?? new List<Unit>();
        }

        private void OnTargetPicked(Unit target)
        {
            if (_pendingTargetReward == null) return;
            bool valid = _pendingTargetReward.RequiresLevelTargetSelection
                ? RewardManager.IsValidLevelTarget(target)
                : _pendingTargetReward.RequiresReviveTargetSelection
                    ? RewardManager.IsValidReviveTarget(target)
                    : RewardManager.IsValidHealingTarget(target);
            if (!valid) return;
            GameManager.Instance?.rewardManager?.ApplyReward(_pendingTargetReward, target);
            _pendingTargetReward = null;
        }

        private void ShowRewardCards()
        {
            _pendingTargetReward = null;
            SetTitle(Title);
            if (_cardsRoot != null) _cardsRoot.gameObject.SetActive(true);
            _targetPicker?.SetActive(false);
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

                    bool isPrimary = bonus.TryGetPrimary(out BaseEnums.PrimaryStat stat);
                    Color color = isPrimary ? UITheme.Stat(stat) : UITheme.TextPrimary;
                    string label = isPrimary
                        ? bonus.stat.ToUpperInvariant()
                        : EquipmentStatKeys.DisplayName(bonus.stat);
                    rows.Add(new Row(label,
                        (bonus.amount > 0 ? "+" : "") + bonus.amount + EquipmentStatKeys.DisplaySuffix(bonus.stat),
                        color));
                }

                return rows;
            }

            // 장비가 아닌 보상. RewardManager.ApplyReward의 우선순위와 Unit.AddRunBonus의
            // 매핑을 그대로 따라간다 — 카드가 실제로 일어날 일만 약속하게 하려는 것이다.
            if (reward.IsTonic && reward.TryGetTonicStat(out BaseEnums.PrimaryStat tonicStat))
            {
                // 합연산과 곱연산은 읽는 법이 다르다. 기호를 그대로 보여 줘야 둘을 헷갈리지 않는다.
                string amount = reward.tonicMultiplier > 1f
                    ? $"×{reward.tonicMultiplier:0.00}"
                    : $"+{reward.tonicFlat}";
                rows.Add(new Row($"파티 {tonicStat}", amount, UITheme.Stat(tonicStat)));
                rows.Add(new Row("지속", $"{PartyTonicState.BattleDuration}전투", UITheme.TextSecondary));
                return rows;
            }

            if (reward.fullHealParty)
            {
                rows.Add(new Row("파티 전체", "완전 회복", UITheme.Hp));
                return rows;
            }

            if (reward.fullReviveParty)
            {
                rows.Add(new Row("파티 전체", "체력 100%로 부활", UITheme.Hp));
                return rows;
            }

            if (reward.revivePercent > 0f)
            {
                rows.Add(new Row("지정 아군", $"체력 {Mathf.RoundToInt(reward.revivePercent * 100f)}%로 부활", UITheme.Hp));
                return rows;
            }

            if (reward.fullHealTarget)
            {
                rows.Add(new Row("지정 아군", "완전 회복", UITheme.Hp));
                return rows;
            }

            if (reward.healPercent > 0f)
            {
                rows.Add(new Row("지정 아군", $"+{Mathf.RoundToInt(reward.healPercent * 100f)}%", UITheme.Hp));
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
                // 누가 효과를 받는지가 고를지 말지를 가른다. 숙련이 없으면 중량만 진다.
                // 행 수가 카드 높이에 묶여 있어 같은 줄에 보유자를 붙인다.
                var holders = GridManager.Instance?.heroList?
                    .Where(unit => unit != null && unit.isActive && !unit.IsEnemy &&
                                   unit.HasProficiency(item.RequiredProficiency))
                    .Select(unit => unit.UnitName)
                    .Distinct()
                    .ToList() ?? new List<string>();
                string proficiency = ItemTooltip.ProficiencyName(item.RequiredProficiency);
                rows.Add(new Row("요구 숙련",
                    holders.Count > 0 ? $"{proficiency} · {string.Join(", ", holders)}" : $"{proficiency} · 보유자 없음",
                    holders.Count > 0 ? UITheme.TextPrimary : UITheme.Danger));
            }

            // 부여 코드가 이 장비를 고를 가장 큰 이유다. 양손 여부보다 먼저 적는다.
            if (item.codeGrants is { Count: > 0 } && rows.Count < MaxSpecRows)
            {
                // 개수만 적으면 무엇을 주는지 알 수 없다. 첫 코드의 이름을 싣는다(T3+는 하나뿐이다).
                var grant = item.codeGrants[0];
                string grantName = Codes.Base.CodeCatalog.Find(Codes.Base.CodeCatalog.ParseSlot(grant.slot), grant.codeId)?.verbalName;
                rows.Add(new Row("부여 코드", string.IsNullOrWhiteSpace(grantName) ? item.codeGrants.Count + "개" : grantName, UITheme.Accent));
            }

            if (item.twoHanded && rows.Count < MaxSpecRows)
            {
                rows.Add(new Row("양손", "보조 슬롯 사용", UITheme.TextMuted));
            }

            return rows;
        }

        /// <summary>등급 · 부위 · 분류를 한 줄로. 세 장이 같은 자리에서 비교된다.</summary>
        private static string MetaLine(RewardDef reward)
        {
            int tier = Mathf.Max(1, reward.tier);
            var parts = new List<string> { $"TIER {tier}" };

            if (!string.IsNullOrWhiteSpace(reward.item?.slot)) parts.Add(ItemTooltip.SlotName(reward.item.slot));
            if (reward.isRare) parts.Add("RARE");

            return string.Join(" · ", parts);
        }

        private sealed class Card
        {
            private readonly GameObject _root;
            private readonly RectTransform _rect;
            private readonly Image _rarityStrip;
            private readonly TextMeshProUGUI _meta;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _specCaption;
            private readonly Image _specRule;
            private readonly TextMeshProUGUI _statCaption;
            private readonly TextMeshProUGUI[] _statLabels = new TextMeshProUGUI[MaxStatRows];
            private readonly TextMeshProUGUI[] _statValues = new TextMeshProUGUI[MaxStatRows];
            private readonly TextMeshProUGUI[] _specLabels = new TextMeshProUGUI[MaxSpecRows];
            private readonly TextMeshProUGUI[] _specValues = new TextMeshProUGUI[MaxSpecRows];
            private readonly Image _art;
            private readonly TextMeshProUGUI _summary;

            public Card(Transform parent, int index, Action<int> onPick)
            {
                Image panel = UIBuild.Panel($"Reward{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 12, UITheme.Outline, 1);
                _root = panel.gameObject;

                _rect = panel.rectTransform;
                Layout(index, 0, CardsPerRow, 1);

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

                _statCaption = Caption(root, "StatCaption", "STATS", 0.725f);
                for (int i = 0; i < MaxStatRows; i++)
                {
                    float top = 0.70f - i * 0.068f;
                    _statLabels[i] = RowLabel(root, $"StatLabel{i}", top);
                    _statValues[i] = RowValue(root, $"StatValue{i}", top, UITheme.FontHeading);
                }

                // 일러스트. 카드 가운데를 통째로 쓴다. 없으면(장비가 아닌 보상) 꺼지고
                // 그 자리에 예전처럼 스탯 줄이 들어온다.
                var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(root, false);
                _art = artObject.GetComponent<Image>();
                _art.preserveAspect = true;
                _art.raycastTarget = false;
                UIBuild.Anchor(_art.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.755f), 22f, 0f);

                // 그림 아래 한 줄 요약. 세 장을 비교할 때 가장 자주 보는 값만 남긴다.
                _summary = UIBuild.Text("Summary", root, "", UITheme.FontCaption, UITheme.TextSecondary,
                    TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_summary.rectTransform, new Vector2(0f, 0.17f), new Vector2(1f, 0.29f), 16f, 0f);

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

            /// <summary>격자 칸에 맞춰 카드를 놓는다. 위 줄부터 채운다.</summary>
            public void Layout(int column, int row, int columns, int rows)
            {
                float width = 1f / Mathf.Max(1, columns);
                float height = 1f / Mathf.Max(1, rows);
                float top = 1f - row * height;
                UIBuild.Anchor(_rect,
                    new Vector2(column * width, top - height),
                    new Vector2((column + 1) * width, top), 14f, rows > 1 ? 7f : 0f);
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
                List<Row> specs = SpecRows(reward);

                Sprite art = LoadRewardArt(reward);
                _art.sprite = art;
                _art.enabled = art != null;

                // 카드 어디에 마우스를 올려도 제원이 뜬다. 그림에 자리를 내준 값들이 여기 있다.
                TooltipTrigger.Attach(_root, () => reward.displayName,
                    () => TooltipLines(reward, stats, specs), rarity);

                BindArtLayout(art != null, stats, specs);
            }

            private static Sprite LoadRewardArt(RewardDef reward)
            {
                if (reward?.item != null) return ItemTooltip.LoadArt(reward.item);
                return string.IsNullOrWhiteSpace(reward?.artPath)
                    ? null
                    : Resources.Load<Sprite>(reward.artPath);
            }

            /// <summary>그림이 있으면 요약 한 줄, 없으면 예전처럼 표를 세운다.</summary>
            private void BindArtLayout(bool hasArt, List<Row> stats, List<Row> specs)
            {
                _summary.gameObject.SetActive(hasArt);
                // 이름 아래 구분선은 그림이 있을 때도 남긴다. 제목과 본문을 가르는 선이다.
                _statCaption.gameObject.SetActive(!hasArt);
                _specCaption.gameObject.SetActive(!hasArt && specs.Count > 0);
                _specRule.gameObject.SetActive(!hasArt && specs.Count > 0);

                for (int i = 0; i < _statLabels.Length; i++)
                {
                    _statLabels[i].gameObject.SetActive(!hasArt);
                    _statValues[i].gameObject.SetActive(!hasArt);
                }
                for (int i = 0; i < _specLabels.Length; i++)
                {
                    _specLabels[i].gameObject.SetActive(!hasArt);
                    _specValues[i].gameObject.SetActive(!hasArt);
                }

                if (hasArt)
                {
                    _summary.text = SummaryLine(stats);
                    return;
                }

                Fill(_statLabels, _statValues, stats);
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

            /// <summary>그림 아래 한 줄. 스탯 보너스만 색을 입혀 이어 붙인다.</summary>
            private static string SummaryLine(List<Row> stats)
            {
                if (stats.Count == 0) return "";

                var parts = new List<string>();
                foreach (Row row in stats)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(row.Color);
                    parts.Add($"<color=#{hex}>{row.Label} {row.Value}</color>");
                }
                return string.Join("   ", parts);
            }

            private static List<UITooltip.Line> TooltipLines(
                RewardDef reward, List<Row> stats, List<Row> specs)
            {
                var lines = new List<UITooltip.Line>
                {
                    UITooltip.Line.Note(MetaLine(reward), UITheme.TextMuted),
                };

                if (!string.IsNullOrWhiteSpace(reward.description))
                {
                    lines.Add(UITooltip.Line.Note(reward.description, UITheme.TextSecondary));
                }

                foreach (Row row in stats) lines.Add(new UITooltip.Line(row.Label, row.Value, row.Color));
                foreach (Row row in specs) lines.Add(new UITooltip.Line(row.Label, row.Value, row.Color));

                if (reward.item?.twoHanded == true)
                {
                    lines.Add(UITooltip.Line.Note("두 손으로 든다 — 보조 슬롯을 함께 쓴다", UITheme.TextMuted));
                }

                return lines;
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
