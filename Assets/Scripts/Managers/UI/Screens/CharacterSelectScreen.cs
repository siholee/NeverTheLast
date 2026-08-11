using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 런 시작 시 출전 캐릭터를 고르는 화면.
    /// 모드에 따라 후보 목록이 달라진다(무한: 육성 완료 캐릭터 / 육성: 메인 + 서포터).
    /// </summary>
    public class CharacterSelectScreen : ModalScreen
    {
        private const int Columns = 5;

        protected override string CanvasName => "CharacterSelectCanvas";
        protected override int SortingOrder => 80;
        protected override string Title => "출전 캐릭터 선택";
        protected override Vector2 AnchorMin => new(0.06f, 0.08f);
        protected override Vector2 AnchorMax => new(0.94f, 0.92f);

        private RectTransform _grid;
        private TextMeshProUGUI _summary;
        private readonly Dictionary<int, Image> _cards = new();
        private readonly Dictionary<int, TextMeshProUGUI> _cardLabels = new();

        protected override void Build()
        {
            _summary = UIBuild.Text("Summary", Body, "", UITheme.FontBody, UITheme.Accent,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_summary.rectTransform, new Vector2(0f, 0.90f), new Vector2(1f, 1f));

            _grid = UIBuild.Container("Grid", Body);
            UIBuild.Anchor(_grid, new Vector2(0f, 0.16f), new Vector2(1f, 0.88f));

            Button start = UIBuild.Button("StartRun", Body, "전투 시작",
                () => CharacterSelectionManager.Instance?.ConfirmSelection(), primary: true);
            UIBuild.Anchor(start.image.rectTransform, new Vector2(0.36f, 0.02f), new Vector2(0.64f, 0.13f));
        }

        public override void Show()
        {
            EnsureCharacterSelectionManager();
            base.Show();
            SetTitle(GameManager.Instance != null && GameManager.Instance.CurrentMode == BaseEnums.GameMode.Infinite
                ? "무한 모드 — 육성 완료 캐릭터 5명 선택"
                : "육성 모드 — 메인 1명, 초기 서포터 최대 3명");
            RebuildGrid();
            RefreshVisuals();
        }

        private void RebuildGrid()
        {
            UIBuild.Clear(_grid);
            _cards.Clear();
            _cardLabels.Clear();

            List<UnitData> units = Candidates();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                int row = i / Columns;
                int column = i % Columns;
                float cellWidth = 1f / Columns;

                Image card = UIBuild.Panel($"Unit{unit.id}", _grid, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
                UIBuild.Anchor(card.rectTransform,
                    new Vector2(column * cellWidth, 0.78f - row * 0.24f),
                    new Vector2((column + 1) * cellWidth, 0.98f - row * 0.24f), 8f, 6f);

                TextMeshProUGUI label = UIBuild.Text("Label", card.transform, DescribeUnit(unit),
                    UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Stretch(label.rectTransform, 6f, 6f);

                _cards[unit.id] = card;
                _cardLabels[unit.id] = label;

                int captured = unit.id;
                UIBuild.OnClick(card.gameObject, () =>
                {
                    CharacterSelectionManager.Instance?.AddHero(captured);
                    RefreshVisuals();
                });
            }
        }

        private static string DescribeUnit(UnitData unit)
        {
            string role = unit.canStartAsMain
                ? "메인"
                : unit.canStartAsSupport
                    ? "서포터"
                    : SaveSystem.IsStarterUnlocked(unit.id)
                        ? "동료"
                        : "";
            return string.IsNullOrWhiteSpace(role) ? unit.name : $"{unit.name}\n<size=80%>{role}</size>";
        }

        private static List<UnitData> Candidates()
        {
            List<UnitData> units = GameManager.Instance?.unitDataList?.units?
                .FindAll(unit => unit.id < 100) ?? new List<UnitData>();

            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == BaseEnums.GameMode.Infinite)
            {
                List<int> trained = SaveSystem.LoadTrainedCharacters().unitIds;
                return units.Where(unit => unit.canUseInInfinite && trained.Contains(unit.id)).ToList();
            }

            return units
                .Where(unit => unit.canStartAsMain || unit.canStartAsSupport || SaveSystem.IsStarterUnlocked(unit.id))
                .OrderByDescending(unit => unit.canStartAsMain)
                .ThenByDescending(unit => unit.canStartAsSupport)
                .ThenBy(unit => unit.id)
                .ToList();
        }

        private void RefreshVisuals()
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            foreach (KeyValuePair<int, Image> pair in _cards)
            {
                bool isSelected = manager.Lineup.Any(entry => entry.UnitId == pair.Key);

                // 선택된 카드는 앰버 테두리 + 체크 표시로 구분한다.
                pair.Value.sprite = UIShapes.CutCorner(8,
                    isSelected ? UITheme.AccentFaint : UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal,
                    isSelected ? UITheme.Accent : UITheme.Outline, 1);

                if (!_cardLabels.TryGetValue(pair.Key, out TextMeshProUGUI label)) continue;
                label.color = isSelected ? UITheme.Accent : UITheme.TextPrimary;
            }

            string mainText = manager.MainUnitId > 0 ? UnitName(manager.MainUnitId) : "미선택";
            IReadOnlyList<int> supports = manager.SupportUnitIds;
            string supportText = supports.Count > 0
                ? string.Join(", ", supports.Select(UnitName))
                : "미선택";
            _summary.text = $"메인  {mainText}      서포터 {supports.Count}명  {supportText}";
        }

        private static string UnitName(int unitId)
        {
            return GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(unit => unit.id == unitId)?.name ?? unitId.ToString();
        }

        private static void EnsureCharacterSelectionManager()
        {
            if (CharacterSelectionManager.Instance != null) return;
            new GameObject("CharacterSelectionManager").AddComponent<CharacterSelectionManager>();
        }
    }
}
