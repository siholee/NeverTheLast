using System.Collections.Generic;
using System.Linq;
using Codes.Base;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// TAB으로 여는 중앙 패널. 발더스 게이트 3의 인벤토리 레이아웃을 따른다.
    ///   좌 — 파티 목록(누르면 그 캐릭터 기준으로 전환)
    ///   중 — 선택한 캐릭터의 장비/보관함 격자, 상단에 탭
    ///   우 — 선택한 항목의 상세. 아무것도 안 골랐으면 캐릭터 요약을 보여준다.
    /// </summary>
    public class CodexScreen
    {
        private enum Tab
        {
            Equipment,
            Skills,
            Sheet,
        }

        private const int GridColumns = 5;
        private const float SlotSize = 76f;
        private const float SlotGap = 8f;

        private GameObject _rootObject;
        private RectTransform _partyColumn;
        private RectTransform _gridArea;
        private RectTransform _detailArea;
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _weightLabel;
        private Button _equipmentTab;
        private Button _skillTab;
        private Button _sheetTab;
        private RectTransform _statStrip;

        private Unit _selected;
        private Tab _tab = Tab.Equipment;

        public bool IsOpen => _rootObject != null && _rootObject.activeSelf;

        public void Show(Unit focus = null)
        {
            EnsureBuilt();
            if (focus != null) _selected = focus;
            _selected ??= FirstAlly();

            _rootObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_rootObject != null) _rootObject.SetActive(false);
        }

        // ── 조립 ─────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_rootObject != null) return;

            Canvas canvas = UIBuild.Canvas("CodexCanvas", 60);
            _rootObject = new GameObject("CodexScreen", typeof(RectTransform));
            _rootObject.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_rootObject.GetComponent<RectTransform>());

            // 뒤 전투 화면을 눌러 어둡게. 배경 클릭으로 닫는다.
            Image backdrop = UIBuild.Backdrop("Backdrop", _rootObject.transform);
            UIBuild.OnClick(backdrop.gameObject, Hide);

            Image panel = UIBuild.Panel("Panel", _rootObject.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            UIBuild.Anchor(panel.rectTransform, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.92f));

            BuildHeader(panel.transform);

            // ── 좌: 파티 열 ──
            Image partyPane = UIBuild.Panel("PartyPane", panel.transform, UITheme.SurfaceSunken,
                UIShapes.Corner.None, 4);
            UIBuild.Anchor(partyPane.rectTransform, new Vector2(0f, 0f), new Vector2(0.19f, 0.88f),
                UITheme.PanelPad, UITheme.PanelPad);
            _partyColumn = UIBuild.Container("PartyList", partyPane.transform);
            UIBuild.Stretch(_partyColumn, 8f, 8f);

            // ── 중: 격자 ──
            var gridPane = UIBuild.Container("GridPane", panel.transform);
            UIBuild.Anchor(gridPane, new Vector2(0.19f, 0f), new Vector2(0.63f, 0.88f),
                UITheme.PanelPad, UITheme.PanelPad);
            BuildTabs(gridPane);

            // BG3 레퍼런스의 '근접 +N / 명중 보너스 / 피해' 줄 자리에 주스탯을 놓는다.
            Image statPane = UIBuild.Panel("StatStrip", gridPane, UITheme.SurfaceSunken,
                UIShapes.Corner.None, 4);
            UIBuild.Anchor(statPane.rectTransform, new Vector2(0f, 0.80f), new Vector2(1f, 0.90f), 0f, 2f);
            _statStrip = UIBuild.Container("StatRow", statPane.transform);
            UIBuild.Stretch(_statStrip, 8f, 4f);

            _gridArea = UIBuild.Container("Grid", gridPane);
            UIBuild.Anchor(_gridArea, new Vector2(0f, 0f), new Vector2(1f, 0.79f));

            // ── 우: 상세 ──
            Image detailPane = UIBuild.Panel("DetailPane", panel.transform, UITheme.SurfaceSunken,
                UIShapes.Corner.None, 4);
            UIBuild.Anchor(detailPane.rectTransform, new Vector2(0.63f, 0f), new Vector2(1f, 0.88f),
                UITheme.PanelPad, UITheme.PanelPad);
            _detailArea = UIBuild.Container("Detail", detailPane.transform);
            UIBuild.Stretch(_detailArea, 16f, 16f);

            _rootObject.SetActive(false);
        }

        private void BuildHeader(Transform parent)
        {
            _headerLabel = UIBuild.Label("Header", parent, "장비 및 코드", UITheme.FontTitle,
                UITheme.TextPrimary);
            UIBuild.Anchor(_headerLabel.rectTransform, new Vector2(0f, 0.88f), new Vector2(0.5f, 1f),
                UITheme.PanelPad, 8f);

            _weightLabel = UIBuild.Text("Weight", parent, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_weightLabel.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(0.93f, 1f),
                0f, 8f);

            Button close = UIBuild.Button("Close", parent, "✕", Hide);
            UIBuild.Pin(close.image.rectTransform, new Vector2(1f, 1f), new Vector2(40f, 40f),
                new Vector2(-UITheme.PanelPad, -UITheme.PanelPad));

            Image rule = UIBuild.Divider("HeaderRule", parent);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.88f),
                UITheme.PanelPad, 0f);
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 1f);
        }

        private void BuildTabs(Transform parent)
        {
            _equipmentTab = UIBuild.Button("EquipTab", parent, "장비", () => SetTab(Tab.Equipment));
            UIBuild.Anchor(_equipmentTab.image.rectTransform, new Vector2(0f, 0.92f),
                new Vector2(0.24f, 1f));

            _skillTab = UIBuild.Button("SkillTab", parent, "코드", () => SetTab(Tab.Skills));
            UIBuild.Anchor(_skillTab.image.rectTransform, new Vector2(0.26f, 0.92f),
                new Vector2(0.50f, 1f));

            _sheetTab = UIBuild.Button("SheetTab", parent, "캐릭터 시트", () => SetTab(Tab.Sheet));
            UIBuild.Anchor(_sheetTab.image.rectTransform, new Vector2(0.52f, 0.92f),
                new Vector2(0.82f, 1f));
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            Refresh();
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_rootObject == null) return;

            RefreshTabVisuals();
            RefreshParty();
            RefreshGrid();
            ShowUnitSummary();

            _headerLabel.text = _selected != null ? $"{_selected.UnitName}" : "장비 및 코드";
            _weightLabel.text = _selected != null
                ? $"중량  {_selected.CarryWeightCurrent} / {_selected.CarryWeightFirstCap} · {_selected.CarryWeightSecondCap} · 3차 {_selected.CarryWeightMax}{(_selected.IsOverCarryWeightMax ? "  [초과 — 휴대품 정리 필요]" : "")}      코드  {_selected.LearnedCodeCount} / {_selected.MaxCodeCount}"
                : "";
            _weightLabel.color = _selected?.IsOverCarryWeightMax == true ? UITheme.Danger : UITheme.TextSecondary;
        }

        private void RefreshTabVisuals()
        {
            // 선택된 탭만 앰버로 칠해 현재 위치를 알린다.
            Tint(_equipmentTab, _tab == Tab.Equipment);
            Tint(_skillTab, _tab == Tab.Skills);
            Tint(_sheetTab, _tab == Tab.Sheet);
        }

        private static void Tint(Button button, bool active)
        {
            if (button == null) return;
            button.image.sprite = UIShapes.CutCorner(8,
                active ? UITheme.Accent : UITheme.SurfaceRaised, UIShapes.Corner.Diagonal);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = active ? UITheme.TextOnAccent : UITheme.TextSecondary;
        }

        private void RefreshParty()
        {
            UIBuild.Clear(_partyColumn);

            List<Unit> allies = Allies();
            for (int i = 0; i < allies.Count; i++)
            {
                Unit unit = allies[i];
                bool isSelected = ReferenceEquals(unit, _selected);

                Image card = UIBuild.Panel($"Ally{i}", _partyColumn,
                    isSelected ? UITheme.AccentFaint : UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 6,
                    isSelected ? UITheme.Accent : default, isSelected ? 1 : 0);
                UIBuild.Pin(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 56f),
                    new Vector2(0f, -i * 62f));
                // 가로는 부모에 맞춰 늘린다.
                card.rectTransform.anchorMin = new Vector2(0f, 1f);
                card.rectTransform.anchorMax = new Vector2(1f, 1f);
                card.rectTransform.sizeDelta = new Vector2(0f, 56f);

                TextMeshProUGUI name = UIBuild.Text("Name", card.transform, unit.UnitName,
                    UITheme.FontBody, isSelected ? UITheme.Accent : UITheme.TextPrimary);
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), 10f, 0f);

                TextMeshProUGUI meta = UIBuild.Text("Meta", card.transform,
                    $"Lv.{unit.Level}   HP {unit.HpCurr}/{unit.HpMax}",
                    UITheme.FontMicro, UITheme.TextMuted);
                UIBuild.Anchor(meta.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), 10f, 0f);

                Unit captured = unit;
                UIBuild.OnClick(card.gameObject, () =>
                {
                    _selected = captured;
                    Refresh();
                });
            }
        }

        private void RefreshGrid()
        {
            UIBuild.Clear(_gridArea);
            if (_selected == null) return;

            RefreshStatStrip();
            if (_tab == Tab.Equipment) BuildEquipmentGrid();
            else if (_tab == Tab.Skills) BuildSkillList();
            else BuildCharacterSheet();
        }

        /// <summary>
        /// 소유 아이템 격자 바로 위에 5주스탯을 가로로 늘어놓는다.
        /// 발더스 게이트의 '근접/원거리 명중·피해' 줄을 대체하는 자리다.
        /// </summary>
        private void RefreshStatStrip()
        {
            UIBuild.Clear(_statStrip);
            if (_selected == null) return;

            var stats = new[]
            {
                BaseClasses.BaseEnums.PrimaryStat.STR,
                BaseClasses.BaseEnums.PrimaryStat.DEX,
                BaseClasses.BaseEnums.PrimaryStat.CON,
                BaseClasses.BaseEnums.PrimaryStat.INT,
                BaseClasses.BaseEnums.PrimaryStat.LUK,
            };

            float step = 1f / stats.Length;
            for (int i = 0; i < stats.Length; i++)
            {
                var stat = stats[i];
                bool isMain = _selected.MainPrimaryStat == stat;
                bool isSub = string.Equals(_selected.SubStat, stat.ToString(),
                    System.StringComparison.OrdinalIgnoreCase);

                var cell = UIBuild.Container($"Stat{stat}", _statStrip);
                UIBuild.Anchor(cell, new Vector2(i * step, 0f), new Vector2((i + 1) * step, 1f));

                Color tone = isMain ? UITheme.Accent : isSub ? UITheme.TextPrimary : UITheme.TextSecondary;
                string mark = isMain ? " ★" : isSub ? " ◆" : "";

                TextMeshProUGUI name = UIBuild.Text("Name", cell, stat + mark,
                    UITheme.FontMicro, tone, TextAlignmentOptions.Center);
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f));

                TextMeshProUGUI value = UIBuild.Text("Value", cell,
                    _selected.GetBasePrimaryStat(stat).ToString(),
                    UITheme.FontBody, tone, TextAlignmentOptions.Center);
                UIBuild.Anchor(value.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            }
        }

        /// <summary>
        /// 캐릭터 시트 — 기초 스탯 → 상세(파생) 스탯 → 고유 코드 → 그 외 보유 코드 순으로 쌓는다.
        /// </summary>
        private void BuildCharacterSheet()
        {
            float y = 0f;

            AddSheetHeading("기초 스탯", ref y);
            AddSheetRow("STR", $"{_selected.GetBasePrimaryStat(BaseClasses.BaseEnums.PrimaryStat.STR)}", ref y);
            AddSheetRow("DEX", $"{_selected.GetBasePrimaryStat(BaseClasses.BaseEnums.PrimaryStat.DEX)}", ref y);
            AddSheetRow("CON", $"{_selected.GetBasePrimaryStat(BaseClasses.BaseEnums.PrimaryStat.CON)}", ref y);
            AddSheetRow("INT", $"{_selected.GetBasePrimaryStat(BaseClasses.BaseEnums.PrimaryStat.INT)}", ref y);
            AddSheetRow("LUK", $"{_selected.GetBasePrimaryStat(BaseClasses.BaseEnums.PrimaryStat.LUK)}", ref y);
            y += 10f;

            AddSheetHeading("상세 스탯", ref y);
            AddSheetRow("최대 체력", $"{_selected.HpCurr} / {_selected.HpMax}", ref y);
            AddSheetRow("방어력", $"{_selected.DefCurr}   (받는 피해 −{(1f - _selected.DamageTakenMultiplierFromArmor) * 100f:0}%)", ref y);
            AddSheetRow("내구", $"{_selected.DurabilityCurr}   (피해 고정 경감)", ref y);
            AddSheetRow("위력 100 기준 피해", $"{_selected.SkillDamage(100)}", ref y);
            AddSheetRow("치명타", $"{_selected.CritChanceCurr * 100f:0.#}%   ×{_selected.CritMultiplierCurr:0.##}", ref y);
            AddSheetRow("행동 속도", $"×{_selected.ActionSpeedCurr:0.##}", ref y);
            AddSheetRow("마나 효율", $"×{_selected.ManaEfficiencyCurr:0.##}", ref y);
            if (_selected.ShieldCurr > 0) AddSheetRow("방어막", $"{_selected.ShieldCurr}", ref y);
            y += 10f;

            AddSheetHeading("고유 코드", ref y);
            var unique = _selected.ActivePassiveCodes.Where(c => c != null && c.IsUniquePassive).ToList();
            foreach (Code code in unique)
            {
                AddSheetRow($"패시브  {code.CodeName}", StageText(code), ref y);
            }
            if (_selected.ActiveNormalCode != null)
            {
                AddSheetRow($"일반  {_selected.ActiveNormalCode.CodeName}", StageText(_selected.ActiveNormalCode), ref y);
            }
            if (_selected.ActiveUltimateCode != null)
            {
                AddSheetRow($"궁극기  {_selected.ActiveUltimateCode.CodeName}", StageText(_selected.ActiveUltimateCode), ref y);
            }
            y += 10f;

            AddSheetHeading($"그 외 보유 코드   {_selected.LearnedCodeCount} / {_selected.MaxCodeCount}", ref y);
            var others = _selected.ActivePassiveCodes.Where(c => c != null && !c.IsUniquePassive).ToList();
            if (others.Count == 0 && _selected.ActiveItemPassiveCodes.Count == 0)
            {
                AddSheetRow("—", "없음", ref y);
            }
            foreach (Code code in others)
            {
                AddSheetRow(code.CodeName, StageText(code), ref y);
            }
            foreach (Code code in _selected.ActiveItemPassiveCodes)
            {
                if (code == null) continue;
                AddSheetRow(code.CodeName, "장비", ref y);
            }
        }

        private static string StageText(Code code)
            => code != null && code.MaxStage > 1 ? $"{code.CurrentStage}단계" : "";

        private void AddSheetHeading(string text, ref float y)
        {
            TextMeshProUGUI label = UIBuild.Text("Heading", _gridArea, text, UITheme.FontBody, UITheme.Accent);
            UIBuild.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(0f, -y));
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 24f);
            y += 26f;
        }

        private void AddSheetRow(string left, string right, ref float y)
        {
            var row = UIBuild.Container("Row", _gridArea);
            UIBuild.Pin(row, new Vector2(0f, 1f), new Vector2(0f, 20f), new Vector2(0f, -y));
            row.anchorMax = new Vector2(1f, 1f);
            row.sizeDelta = new Vector2(0f, 20f);

            TextMeshProUGUI l = UIBuild.Text("L", row, left, UITheme.FontCaption, UITheme.TextPrimary);
            UIBuild.Anchor(l.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), 8f, 0f);

            TextMeshProUGUI r = UIBuild.Text("R", row, right, UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(r.rectTransform, new Vector2(0.6f, 0f), new Vector2(1f, 1f), 8f, 0f);

            y += 22f;
        }

        private void BuildEquipmentGrid()
        {
            ItemDataList itemData = GameManager.Instance?.itemDataList;
            List<ItemData> equipped = _selected.EquippedItems.ToList();

            int index = 0;

            // 장착 중인 장비를 먼저, 앰버 테두리로 구분해 배치한다.
            foreach (ItemData item in equipped)
            {
                CreateSlot(index++, item, isEquipped: true, isCarried: false);
            }

            foreach (int itemId in _selected.CarriedItemIds)
            {
                ItemData item = itemData?.items?.FirstOrDefault(entry => entry.id == itemId);
                if (item == null) continue;
                CreateSlot(index++, item, isEquipped: false, isCarried: true);
            }

            foreach (int itemId in GameManager.Instance?.inventoryManager?.ItemIdsInHand ?? new List<int>())
            {
                ItemData item = itemData?.items?.FirstOrDefault(entry => entry.id == itemId);
                if (item == null) continue;
                CreateSlot(index++, item, isEquipped: false, isCarried: false);
            }

            if (index == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _gridArea, "보유한 장비가 없습니다.",
                    UITheme.FontBody, UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Stretch(empty.rectTransform);
            }
        }

        private void CreateSlot(int index, ItemData item, bool isEquipped, bool isCarried)
        {
            int row = index / GridColumns;
            int column = index % GridColumns;

            Color border = isEquipped ? UITheme.Accent : UITheme.Rarity(item.rarity);
            Image slot = UIBuild.Panel($"Slot{index}", _gridArea, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 6, border, isEquipped ? 2 : 1);
            UIBuild.Pin(slot.rectTransform, new Vector2(0f, 1f), new Vector2(SlotSize, SlotSize),
                new Vector2(column * (SlotSize + SlotGap), -row * (SlotSize + SlotGap)));

            TextMeshProUGUI label = UIBuild.Text("Name", slot.transform, item.name,
                UITheme.FontMicro, UITheme.TextPrimary, TextAlignmentOptions.Center, wrap: true);
            UIBuild.Stretch(label.rectTransform, 4f, 14f);
            label.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI weight = UIBuild.Text("Weight", slot.transform, $"{item.weight}",
                UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.BottomRight);
            UIBuild.Pin(weight.rectTransform, new Vector2(1f, 0f), new Vector2(20f, 14f),
                new Vector2(-4f, 2f));

            if (isEquipped)
            {
                TextMeshProUGUI mark = UIBuild.Text("Equipped", slot.transform, "E",
                    UITheme.FontMicro, UITheme.Accent, TextAlignmentOptions.BottomLeft);
                UIBuild.Pin(mark.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 14f),
                    new Vector2(4f, 2f));
            }

            UIBuild.OnClick(slot.gameObject, () => ShowItemDetail(item, isEquipped, isCarried));
        }

        private void BuildSkillList()
        {
            var entries = new List<(string Kind, string Name, string Detail)>();

            Code normal = _selected.ActiveNormalCode;
            if (normal != null)
            {
                entries.Add(("일반", normal.CodeName, $"쿨다운 {normal.Cooldown:0.##}초"));
            }

            Code ultimate = _selected.ActiveUltimateCode;
            if (ultimate != null)
            {
                entries.Add(("궁극", ultimate.CodeName,
                    $"쿨다운 {ultimate.Cooldown:0.##}초 · {_selected.UltimateResourceName} {_selected.UltimateResourceMax}"));
            }

            foreach (PassiveCode passive in _selected.ActivePassiveCodes)
            {
                if (passive == null) continue;
                entries.Add(("패시브", passive.CodeName, $"단계 {passive.CurrentStage}/{passive.MaxStage}"));
            }

            foreach (PassiveCode passive in _selected.ActiveItemPassiveCodes)
            {
                if (passive == null) continue;
                entries.Add(("장비", passive.CodeName, "장비로 부여됨"));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                (string kind, string name, string detail) = entries[i];

                Image row = UIBuild.Panel($"Code{i}", _gridArea, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 6);
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.sizeDelta = new Vector2(0f, 54f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -i * 60f);

                TextMeshProUGUI kindLabel = UIBuild.Label("Kind", row.transform, kind,
                    UITheme.FontMicro, UITheme.Accent);
                UIBuild.Anchor(kindLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.14f, 1f), 12f, 0f);

                TextMeshProUGUI nameLabel = UIBuild.Text("Name", row.transform, name,
                    UITheme.FontBody, UITheme.TextPrimary);
                UIBuild.Anchor(nameLabel.rectTransform, new Vector2(0.14f, 0.45f), new Vector2(1f, 1f), 0f, 0f);

                TextMeshProUGUI detailLabel = UIBuild.Text("Detail", row.transform, detail,
                    UITheme.FontMicro, UITheme.TextMuted);
                UIBuild.Anchor(detailLabel.rectTransform, new Vector2(0.14f, 0f), new Vector2(1f, 0.45f), 0f, 0f);
            }

            if (entries.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _gridArea, "보유한 코드가 없습니다.",
                    UITheme.FontBody, UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Stretch(empty.rectTransform);
            }
        }

        // ── 우측 상세 ────────────────────────────────────────────────

        private void ShowUnitSummary()
        {
            UIBuild.Clear(_detailArea);
            if (_selected == null) return;

            float y = 0f;
            AddDetailTitle(_selected.UnitName, ref y);
            string expLine = _selected.IsEnemy ? "" : $"  ·  EXP {_selected.Exp}/{_selected.ExpToNextLevel}";
            AddDetailLine($"레벨 {_selected.Level} · 육성 Lv.{_selected.TrainingLevel}{expLine}", ref y, UITheme.TextSecondary);
            y += 8f;

            AddDetailLine($"체력      {_selected.HpCurr} / {_selected.HpMax}", ref y);
            AddDetailLine($"주스탯    {_selected.MainPrimaryStat}  {_selected.GetBasePrimaryStat(_selected.MainPrimaryStat)}", ref y);
            AddDetailLine($"위력 100 기준 피해  {_selected.SkillDamage(100)}", ref y);
            AddDetailLine($"치명타    {_selected.CritChanceCurr:0.#}%  ×{_selected.CritMultiplierCurr:0.##}", ref y);
            AddDetailLine($"회피      {_selected.EvasionChanceCurr:0.#}%", ref y);
            if (_selected.ShieldCurr > 0) AddDetailLine($"방어막    {_selected.ShieldCurr}", ref y, UITheme.Shield);
            y += 12f;

            AddDetailLine("기본 스탯", ref y, UITheme.Accent);
            AddDetailLine($"STR {_selected.StrBase}   DEX {_selected.DexBase}   CON {_selected.ConBase}", ref y, UITheme.TextSecondary);
            AddDetailLine($"INT {_selected.IntBase}   LUK {_selected.LukBase}", ref y, UITheme.TextSecondary);
        }

        private void ShowItemDetail(ItemData item, bool isEquipped, bool isCarried)
        {
            UIBuild.Clear(_detailArea);

            float y = 0f;
            AddDetailTitle(item.name, ref y, UITheme.Rarity(item.rarity));
            AddDetailLine($"{item.slot} · {item.category} · 중량 {item.weight}", ref y, UITheme.TextSecondary);
            if (_selected != null && !_selected.CanUseEquipmentEffects(item))
            {
                AddDetailLine("숙련 없음: 중량만 적용되고 장비 효과는 비활성", ref y, UITheme.Danger);
            }
            y += 10f;

            if (item.statBonuses != null && item.statBonuses.Count > 0)
            {
                AddDetailLine("스탯 보정", ref y, UITheme.Accent);
                foreach (var bonus in item.statBonuses)
                {
                    AddDetailLine($"{bonus.stat}  {(bonus.amount >= 0 ? "+" : "")}{bonus.amount}", ref y);
                }

                y += 10f;
            }

            if (item.codeGrants != null && item.codeGrants.Count > 0)
            {
                AddDetailLine("부여 코드", ref y, UITheme.Accent);
                foreach (var grant in item.codeGrants)
                {
                    AddDetailLine($"{grant.slot}  #{grant.codeId} (단계 {grant.stage})", ref y);
                }

                y += 10f;
            }

            if (isEquipped)
            {
                AddDetailLine("장착 중", ref y, UITheme.Positive);
                return;
            }

            // 보관 중인 장비는 여기서 바로 장착할 수 있다.
            Button equip = UIBuild.Button("Equip", _detailArea, "장착", () => TryEquip(item), primary: true);
            UIBuild.Pin(equip.image.rectTransform, new Vector2(0f, 0f), new Vector2(140f, 40f),
                isCarried ? new Vector2(0f, 48f) : Vector2.zero);
            equip.image.rectTransform.anchorMax = new Vector2(1f, 0f);
            equip.image.rectTransform.sizeDelta = new Vector2(0f, 40f);

            if (isCarried)
            {
                Button discard = UIBuild.Button("Discard", _detailArea, "폐기", () => Discard(item));
                UIBuild.Pin(discard.image.rectTransform, new Vector2(0f, 0f), new Vector2(140f, 40f), Vector2.zero);
                discard.image.rectTransform.anchorMax = new Vector2(1f, 0f);
                discard.image.rectTransform.sizeDelta = new Vector2(0f, 40f);
            }
        }

        private void Discard(ItemData item)
        {
            if (_selected == null || item == null || !_selected.RemoveCarriedItem(item.id)) return;
            GameManager.Instance?.runManager?.SaveCurrentRun();
            GameManager.Instance?.RefreshPreparationForCarryWeight();
            Refresh();
        }

        private void TryEquip(ItemData item)
        {
            if (_selected == null) return;

            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            if (inventory == null) return;

            if (inventory.TryEquipStoredItem(_selected, item.id, out string reason))
            {
                GameManager.Instance.runManager?.SaveCurrentRun();
                Refresh();
                return;
            }

            // 실패 사유는 상세 패널 하단에 그대로 띄운다.
            float y = 999f;
            AddDetailLine(string.IsNullOrWhiteSpace(reason) ? "장착할 수 없습니다." : reason,
                ref y, UITheme.Danger);
        }

        private void AddDetailTitle(string text, ref float y, Color? color = null)
        {
            TextMeshProUGUI label = UIBuild.Text("Title", _detailArea, text, UITheme.FontHeading,
                color ?? UITheme.TextPrimary);
            UIBuild.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 28f), new Vector2(0f, -y));
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 28f);
            y += 30f;
        }

        private void AddDetailLine(string text, ref float y, Color? color = null)
        {
            TextMeshProUGUI label = UIBuild.Text("Line", _detailArea, text, UITheme.FontCaption,
                color ?? UITheme.TextPrimary);
            UIBuild.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 20f), new Vector2(0f, -y));
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 20f);
            y += 22f;
        }

        // ── 데이터 ───────────────────────────────────────────────────

        private static List<Unit> Allies()
        {
            GridManager grid = GridManager.Instance;
            if (grid?.heroList == null) return new List<Unit>();

            return grid.heroList
                .Where(unit => unit != null && !unit.IsEnemy)
                .OrderByDescending(unit => unit.currentCell != null && unit.currentCell.yPos > 0)
                .ThenBy(unit => unit.UnitName)
                .ToList();
        }

        private static Unit FirstAlly() => Allies().FirstOrDefault();
    }
}
