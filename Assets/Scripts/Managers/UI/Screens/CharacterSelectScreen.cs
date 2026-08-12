using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 런 시작 시 출전 캐릭터를 고르는 화면. 철권식 캐릭터 셀렉트를 따른다.
    ///   · 후보를 초상화 타일 격자로 늘어놓는다
    ///   · 커서를 올린 캐릭터의 큰 초상화와 요약을 좌측에 띄운다
    ///
    /// 모드에 따라 단계가 다르다.
    ///   육성 모드 — 1단계 메인 1명 → [다음] → 2단계 서포터 4명 → [시작]
    ///   무한 모드 — 메인 단계 없이 서포터 5명을 바로 고른다
    /// </summary>
    public class CharacterSelectScreen : ModalScreen
    {
        private const int Columns = 6;
        private const int MaxParty = 5;

        private enum Phase
        {
            Main,
            Support,
        }

        protected override string CanvasName => "CharacterSelectCanvas";
        protected override int SortingOrder => 80;
        protected override string Title => "출전 캐릭터 선택";
        protected override Vector2 AnchorMin => new(0.05f, 0.06f);
        protected override Vector2 AnchorMax => new(0.95f, 0.94f);

        private RectTransform _grid;
        private Image _preview;
        private TextMeshProUGUI _previewName;
        private TextMeshProUGUI _previewInfo;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _phaseLabel;
        private Button _primaryButton;
        private Button _backButton;

        private readonly Dictionary<int, Image> _tiles = new();
        private readonly Dictionary<int, Image> _tileArts = new();
        private Phase _phase = Phase.Main;
        private int _hovered;

        private static bool IsInfinite =>
            GameManager.Instance != null && GameManager.Instance.CurrentMode == BaseEnums.GameMode.Infinite;

        // ── 조립 ─────────────────────────────────────────────────────

        protected override void Build()
        {
            // 좌: 커서가 올라간 캐릭터의 큰 초상화
            Image previewPane = UIBuild.Panel("PreviewPane", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            UIBuild.Anchor(previewPane.rectTransform, new Vector2(0f, 0.14f), new Vector2(0.30f, 1f), 4f, 4f);

            _preview = UIBuild.Solid("PreviewArt", previewPane.transform, Color.white);
            UIBuild.Anchor(_preview.rectTransform, new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.97f));
            _preview.preserveAspect = true;
            _preview.enabled = false;

            _previewName = UIBuild.Text("PreviewName", previewPane.transform, "",
                UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Center);
            UIBuild.Anchor(_previewName.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.29f));

            _previewInfo = UIBuild.Text("PreviewInfo", previewPane.transform, "",
                UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.Top, wrap: true);
            UIBuild.Anchor(_previewInfo.rectTransform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.19f));

            // 우: 단계 안내 + 후보 격자
            _phaseLabel = UIBuild.Text("Phase", Body, "", UITheme.FontBody, UITheme.Accent);
            UIBuild.Anchor(_phaseLabel.rectTransform, new Vector2(0.32f, 0.93f), new Vector2(1f, 1f));

            _grid = UIBuild.Container("Grid", Body);
            UIBuild.Anchor(_grid, new Vector2(0.32f, 0.22f), new Vector2(1f, 0.92f));

            _summary = UIBuild.Text("Summary", Body, "", UITheme.FontCaption, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(_summary.rectTransform, new Vector2(0.32f, 0.13f), new Vector2(1f, 0.21f));

            _backButton = UIBuild.Button("Back", Body, "← 메인 다시 고르기", GoBackToMain);
            UIBuild.Anchor(_backButton.image.rectTransform, new Vector2(0.32f, 0.02f), new Vector2(0.56f, 0.11f));

            _primaryButton = UIBuild.Button("Primary", Body, "다음", OnPrimary, primary: true);
            UIBuild.Anchor(_primaryButton.image.rectTransform, new Vector2(0.60f, 0.02f), new Vector2(1f, 0.11f));
        }

        public override void Show()
        {
            EnsureCharacterSelectionManager();
            CharacterSelectionManager.Instance?.ClearLineup();

            // 무한 모드에는 메인 단계가 없다.
            _phase = IsInfinite ? Phase.Support : Phase.Main;
            _hovered = 0;

            base.Show();
            RebuildGrid();
            RefreshVisuals();
        }

        // ── 단계 흐름 ────────────────────────────────────────────────

        private void OnPrimary()
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            if (_phase == Phase.Main)
            {
                if (manager.MainUnitId <= 0) return;   // 메인을 골라야 넘어간다
                _phase = Phase.Support;
                RebuildGrid();
                RefreshVisuals();
                return;
            }

            manager.ConfirmSelection();
        }

        private void GoBackToMain()
        {
            if (IsInfinite || _phase != Phase.Support) return;

            CharacterSelectionManager.Instance?.ClearLineup();
            _phase = Phase.Main;
            RebuildGrid();
            RefreshVisuals();
        }

        // ── 격자 ─────────────────────────────────────────────────────

        private void RebuildGrid()
        {
            UIBuild.Clear(_grid);
            _tiles.Clear();
            _tileArts.Clear();

            List<UnitData> units = Candidates();
            int rows = Mathf.Max(1, Mathf.CeilToInt(units.Count / (float)Columns));
            float cellW = 1f / Columns;
            float cellH = 1f / rows;

            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                int row = i / Columns;
                int column = i % Columns;

                Image tile = UIBuild.Panel($"Unit{unit.id}", _grid, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 6, UITheme.Outline, 1);
                UIBuild.Anchor(tile.rectTransform,
                    new Vector2(column * cellW, 1f - (row + 1) * cellH),
                    new Vector2((column + 1) * cellW, 1f - row * cellH), 4f, 4f);

                // 초상화 — 철권식으로 타일 전체를 그림으로 채운다.
                Image art = UIBuild.Solid("Art", tile.transform, Color.white);
                UIBuild.Anchor(art.rectTransform, new Vector2(0.05f, 0.24f), new Vector2(0.95f, 0.96f));
                art.preserveAspect = true;
                Sprite portrait = LoadPortrait(unit.portrait);
                if (portrait != null) art.sprite = portrait;
                else art.enabled = false;
                _tileArts[unit.id] = art;

                TextMeshProUGUI label = UIBuild.Text("Name", tile.transform, unit.name,
                    UITheme.FontMicro, UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.22f));

                int captured = unit.id;
                UIBuild.OnClick(tile.gameObject, () => OnTileClicked(captured));
                AddHoverPreview(tile.gameObject, captured);

                _tiles[unit.id] = tile;
                if (_hovered == 0) _hovered = unit.id;
            }
        }

        private void OnTileClicked(int unitId)
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            _hovered = unitId;

            bool alreadyPicked = manager.Lineup.Any(entry => entry.UnitId == unitId);
            if (alreadyPicked)
            {
                manager.RemoveHero(unitId);
            }
            else if (_phase == Phase.Main)
            {
                // 메인 단계에서는 항상 한 명만 남긴다.
                manager.ClearLineup();
                manager.AddHero(unitId);
            }
            else
            {
                manager.AddHero(unitId);
            }

            RefreshVisuals();
        }

        /// <summary>커서가 타일 위에 올라가면 좌측 큰 초상화를 갈아 끼운다.</summary>
        private void AddHoverPreview(GameObject target, int unitId)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ =>
            {
                _hovered = unitId;
                RefreshPreview();
            });
            trigger.triggers.Add(entry);
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        private void RefreshVisuals()
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            bool mainPhase = _phase == Phase.Main;
            int supportTarget = IsInfinite ? MaxParty : MaxParty - 1;
            int supportCount = manager.SupportUnitIds.Count;

            SetTitle(IsInfinite
                ? "무한 모드 — 서포터 카드 5장 선택"
                : mainPhase ? "육성 모드 — 1단계: 메인 캐릭터" : "육성 모드 — 2단계: 서포터 카드");

            _phaseLabel.text = mainPhase
                ? "여정을 이끌 메인 캐릭터 한 명을 고르세요."
                : $"서포터 카드를 고르세요.   {supportCount} / {supportTarget}";

            foreach (KeyValuePair<int, Image> pair in _tiles)
            {
                bool isSelected = manager.Lineup.Any(entry => entry.UnitId == pair.Key);
                bool isMain = manager.MainUnitId == pair.Key;

                Color fill = isMain ? UITheme.Accent : isSelected ? UITheme.AccentFaint : UITheme.SurfaceRaised;
                Color line = isSelected ? UITheme.Accent : UITheme.Outline;
                pair.Value.sprite = UIShapes.CutCorner(6, fill, UIShapes.Corner.Diagonal, line, isSelected ? 2 : 1);
            }

            string mainText = manager.MainUnitId > 0 ? UnitName(manager.MainUnitId) : "미선택";
            string supportText = supportCount > 0
                ? string.Join(", ", manager.SupportUnitIds.Select(UnitName))
                : "미선택";
            _summary.text = IsInfinite
                ? $"서포터  {supportText}"
                : $"메인  {mainText}\n서포터  {supportText}";

            // 버튼 상태
            bool canProceed = mainPhase ? manager.MainUnitId > 0 : supportCount > 0;
            SetButton(_primaryButton, mainPhase ? "다음 →" : "여정 시작", canProceed);
            SetButtonVisible(_backButton, !IsInfinite && !mainPhase);

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            UnitData unit = Candidates().FirstOrDefault(candidate => candidate.id == _hovered);
            if (unit == null)
            {
                _preview.enabled = false;
                _previewName.text = "";
                _previewInfo.text = "";
                return;
            }

            Sprite standing = LoadStanding(unit.standing) ?? LoadPortrait(unit.portrait);
            _preview.enabled = standing != null;
            if (standing != null) _preview.sprite = standing;

            _previewName.text = unit.name;
            _previewInfo.text =
                $"{unit.element}   주 {unit.mainStat} · 부 {unit.subStat}\n{RoleText(unit)}";
        }

        private static string RoleText(UnitData unit)
        {
            if (unit.canStartAsMain) return "스타터 — 메인으로 시작할 수 있다";
            if (unit.canStartAsSupport) return "서포트 — 서포터 카드 전용";
            return SaveSystem.IsStarterUnlocked(unit.id) ? "해금됨 — 메인으로 쓸 수 있다" : "미해금";
        }

        private static void SetButton(Button button, string label, bool interactable)
        {
            if (button == null) return;
            button.interactable = interactable;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                text.color = interactable ? UITheme.TextOnAccent : UITheme.TextMuted;
            }
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null) button.gameObject.SetActive(visible);
        }

        // ── 후보 ─────────────────────────────────────────────────────

        private List<UnitData> Candidates()
        {
            List<UnitData> units = GameManager.Instance?.unitDataList?.units?
                .FindAll(unit => unit.id < 100) ?? new List<UnitData>();

            if (IsInfinite)
            {
                List<int> trained = SaveSystem.LoadTrainedCharacters().unitIds;
                return units.Where(unit => unit.canUseInInfinite && trained.Contains(unit.id)).ToList();
            }

            // 육성 모드 1단계는 메인이 될 수 있는 캐릭터만, 2단계는 서포터 후보만 보여준다.
            if (_phase == Phase.Main)
            {
                return units
                    .Where(unit => unit.canStartAsMain || SaveSystem.IsStarterUnlocked(unit.id))
                    .OrderBy(unit => unit.id)
                    .ToList();
            }

            return units
                .Where(unit => unit.canStartAsSupport)
                .OrderBy(unit => unit.id)
                .ToList();
        }

        private static Sprite LoadPortrait(string spriteName)
            => string.IsNullOrWhiteSpace(spriteName) ? null : Resources.Load<Sprite>($"Sprite/Portraits/{spriteName}");

        private static Sprite LoadStanding(string spriteName)
            => string.IsNullOrWhiteSpace(spriteName) ? null : Resources.Load<Sprite>($"Sprite/Standings/{spriteName}");

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
