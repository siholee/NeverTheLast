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
    /// TAB으로 여는 파티 캐릭터 시트. 장비와 개인 인벤토리는 없고 스탯 · 코드만 다룬다.
    /// </summary>
    public sealed class CodexScreen
    {
        private enum Tab { Sheet, Codes }

        private static readonly BaseClasses.BaseEnums.StatType[] StatOrder =
        {
            BaseClasses.BaseEnums.StatType.MaxHp,
            BaseClasses.BaseEnums.StatType.Attack,
            BaseClasses.BaseEnums.StatType.SpecialAttack,
            BaseClasses.BaseEnums.StatType.Defense,
            BaseClasses.BaseEnums.StatType.SpecialDefense,
            BaseClasses.BaseEnums.StatType.Speed,
            BaseClasses.BaseEnums.StatType.CritRate,
            BaseClasses.BaseEnums.StatType.CritDamage,
        };

        private GameObject _rootObject;
        private RectTransform _columns;
        private Button _sheetTab;
        private Button _codeTab;
        private Tab _tab = Tab.Sheet;

        public bool IsOpen => _rootObject != null && _rootObject.activeSelf;

        public void Show(Unit focus = null)
        {
            EnsureBuilt();
            _rootObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_rootObject != null) _rootObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_rootObject != null) return;
            Canvas canvas = UIBuild.Canvas("CodexCanvas", UITheme.LayerCharacterSheet);
            _rootObject = new GameObject("CodexScreen", typeof(RectTransform));
            _rootObject.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_rootObject.GetComponent<RectTransform>());

            Image backdrop = UIBuild.Backdrop("Backdrop", _rootObject.transform);
            UIBuild.OnClick(backdrop.gameObject, Hide);

            Image panel = UIBuild.Panel("Panel", _rootObject.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            UIBuild.Anchor(panel.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));
            UIBuild.Elevate(panel, 40, 0.6f, 14f);
            _rootObject.AddComponent<UIPopIn>().Panel = panel.rectTransform;

            BuildHeader(panel.transform);
            _columns = UIBuild.Container("Columns", panel.transform);
            UIBuild.Anchor(_columns, Vector2.zero, new Vector2(1f, 0.90f), 18f, 18f);
            _rootObject.SetActive(false);
        }

        private void BuildHeader(Transform panel)
        {
            Image bar = UIBuild.Solid("HeaderBar", panel, UITheme.Accent);
            UIBuild.Pin(bar.rectTransform, new Vector2(0f, 1f), new Vector2(3f, 26f), new Vector2(18f, -20f));
            TextMeshProUGUI caption = UIBuild.Label("Caption", panel, "CHARACTER SHEET",
                UITheme.FontMicro, UITheme.TextMuted);
            caption.characterSpacing = 20f;
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 13f), new Vector2(30f, -16f));
            TextMeshProUGUI title = UIBuild.Label("Title", panel, "캐릭터", UITheme.FontTitle, UITheme.TextPrimary);
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 30f), new Vector2(30f, -32f));

            _sheetTab = MakeTab(panel, "캐릭터 시트", -78f, () => SetTab(Tab.Sheet));
            _codeTab = MakeTab(panel, "코드", 78f, () => SetTab(Tab.Codes));
            Button close = UIBuild.Button("Close", panel, "×", Hide);
            UIBuild.Pin(close.image.rectTransform, new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(-18f, -18f));
        }

        private static Button MakeTab(Transform panel, string text, float x, System.Action action)
        {
            Button button = UIBuild.Button($"Tab{text}", panel, text, action, false, UITheme.FontCaption);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, 34f), new Vector2(x, -18f));
            return button;
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            Refresh();
        }

        private void Refresh()
        {
            if (_columns == null) return;
            UIBuild.Clear(_columns);
            Tint(_sheetTab, _tab == Tab.Sheet);
            Tint(_codeTab, _tab == Tab.Codes);

            List<Unit> allies = GridManager.Instance?.heroList?
                .Where(unit => unit != null && !unit.IsEnemy && (unit.ID > 0 || unit.LastActiveId > 0))
                .GroupBy(unit => unit.ID > 0 ? unit.ID : unit.LastActiveId)
                .Select(group => group.First())
                .Take(5)
                .ToList() ?? new List<Unit>();

            if (allies.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _columns, "표시할 아군이 없습니다.",
                    UITheme.FontBody, UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Stretch(empty.rectTransform);
                return;
            }

            float width = 1f / allies.Count;
            for (int i = 0; i < allies.Count; i++) BuildColumn(allies[i], i * width, (i + 1) * width);
        }

        private static void Tint(Button button, bool active)
        {
            if (button == null) return;
            button.image.sprite = UIShapes.CutCorner(8,
                active ? UITheme.Accent : UITheme.SurfaceRaised, UIShapes.Corner.Diagonal);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = active ? UITheme.TextOnAccent : UITheme.TextSecondary;
        }

        private void BuildColumn(Unit unit, float left, float right)
        {
            Image column = UIBuild.Panel($"Col_{unit.UnitName}", _columns, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            UIBuild.Anchor(column.rectTransform, new Vector2(left, 0f), new Vector2(right, 1f), 6f, 0f);

            TextMeshProUGUI name = UIBuild.Text("Name", column.transform, unit.UnitName,
                UITheme.FontHeading, UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(name.rectTransform, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.98f));
            TextMeshProUGUI meta = UIBuild.Text("Meta", column.transform,
                $"Lv.{unit.Level}   ·   EXP {unit.Exp} / {unit.ExpToNextLevel}", UITheme.FontMicro,
                UITheme.TextMuted, TextAlignmentOptions.Center);
            UIBuild.Anchor(meta.rectTransform, new Vector2(0.04f, 0.87f), new Vector2(0.96f, 0.92f));

            float y = 0.84f;
            if (_tab == Tab.Sheet) BuildSheet(column.transform, unit, ref y);
            else BuildCodes(column.transform, unit, ref y);
        }

        private static void BuildSheet(Transform parent, Unit unit, ref float y)
        {
            AddHeading(parent, "스탯  (기초 → 최종)", ref y);
            foreach (BaseClasses.BaseEnums.StatType stat in StatOrder)
            {
                float baseValue = unit.BaseStat(stat);
                float finalValue = unit.StatValue(stat);
                string value = Mathf.Approximately(baseValue, finalValue)
                    ? UnitStats.Format(stat, finalValue)
                    : $"{UnitStats.Format(stat, baseValue)} → {UnitStats.Format(stat, finalValue)}";
                AddRow(parent, UnitStats.DisplayName(stat), value, ref y);
            }
            y -= 0.02f;
            AddHeading(parent, "전투 자원", ref y);
            AddRow(parent, "체력", $"{unit.HpCurr} / {unit.HpMax}", ref y);
            AddRow(parent, "마나", $"{unit.ManaCurr} / {unit.ManaMax}", ref y);
        }

        private static void BuildCodes(Transform parent, Unit unit, ref float y)
        {
            AddHeading(parent, "고유 특성 · 일반행동 · 초월기", ref y);
            foreach (PassiveCode passive in unit.ActivePassiveCodes) AddCode(parent, "고유 특성", passive, ref y);
            AddCode(parent, "일반행동", unit.ActiveNormalCode, ref y);
            AddCode(parent, "초월기", unit.ActiveUltimateCode, ref y);
        }

        private static void AddCode(Transform parent, string kind, Code code, ref float y)
        {
            if (code == null) return;
            CodeCatalog.Entry entry = CodeCatalog.Find(code);
            string name = !string.IsNullOrWhiteSpace(code.CodeName) ? code.CodeName : entry?.verbalName ?? "이름 없음";
            string description = CodeCatalog.Summary(entry?.description);
            AddRow(parent, kind, name, ref y, UITheme.CodeUnique);

            TextMeshProUGUI text = UIBuild.Text("Description", parent, description,
                UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(text.rectTransform, new Vector2(0.06f, y - 0.10f), new Vector2(0.94f, y));
            y -= 0.115f;
        }

        private static void AddHeading(Transform parent, string text, ref float y)
        {
            TextMeshProUGUI label = UIBuild.Text("Heading", parent, text, UITheme.FontCaption, UITheme.Accent);
            UIBuild.Anchor(label.rectTransform, new Vector2(0.05f, y - 0.045f), new Vector2(0.95f, y));
            y -= 0.055f;
        }

        private static void AddRow(Transform parent, string left, string right, ref float y, Color? tint = null)
        {
            TextMeshProUGUI l = UIBuild.Text("Label", parent, left, UITheme.FontMicro, UITheme.TextSecondary);
            UIBuild.Anchor(l.rectTransform, new Vector2(0.06f, y - 0.045f), new Vector2(0.54f, y));
            TextMeshProUGUI r = UIBuild.Text("Value", parent, right, UITheme.FontMicro,
                tint ?? UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(r.rectTransform, new Vector2(0.54f, y - 0.045f), new Vector2(0.94f, y));
            y -= 0.05f;
        }
    }
}
