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
    /// 그림은 <b>초상화(Portrait)만</b> 쓰고 타일·미리보기 모두 정사각형으로 고정한다.
    /// 초상화 원본이 정사각형이므로 칸이 정사각형이면 여백 없이 딱 맞고 비율도 틀어지지 않는다.
    /// 스탠딩은 세로로 길어 같은 칸에 섞으면 비율이 무너지므로 이 화면에서는 쓰지 않는다.
    ///
    /// 모드에 따라 단계가 다르다.
    ///   육성 모드 — 1단계 메인 1명 → [다음] → 2단계 서포터 4명 → [시작]
    ///   무한 모드 — 메인 단계 없이 서포터 5명을 바로 고른다
    /// </summary>
    public class CharacterSelectScreen : ModalScreen
    {
        private const int Columns = 6;
        private const int MaxParty = 5;

        /// <summary>타일 사이 간격(픽셀).</summary>
        private const float TileGap = 10f;

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
        private SquareGridSizer _sizer;
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

            // 미리보기도 정사각형. AspectRatioFitter가 슬롯 안에서 1:1을 유지하도록 크기를 잡는다.
            RectTransform previewSlot = UIBuild.Container("PreviewSlot", previewPane.transform);
            UIBuild.Anchor(previewSlot, new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.97f));

            _preview = UIBuild.Solid("PreviewArt", previewSlot, Color.white);
            var previewFitter = _preview.gameObject.AddComponent<AspectRatioFitter>();
            previewFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewFitter.aspectRatio = 1f;
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

            // 타일 배치는 GridLayoutGroup에 맡기고, 칸 크기는 SquareGridSizer가 정사각형으로 유지한다.
            var layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.spacing = new Vector2(TileGap, TileGap);
            layout.childAlignment = TextAnchor.UpperCenter;

            _sizer = _grid.gameObject.AddComponent<SquareGridSizer>();
            _sizer.MaxColumns = Columns;
            _sizer.Gap = TileGap;

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

            foreach (UnitData unit in units)
            {
                Image tile = UIBuild.Panel($"Unit{unit.id}", _grid, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 6, UITheme.Outline, 1);

                // 초상화 — 철권식으로 타일 전체를 그림으로 채운다.
                // 타일이 정사각형이고 초상화 원본도 정사각형이라 여백 없이 딱 맞는다.
                Image art = UIBuild.Solid("Art", tile.transform, Color.white);
                UIBuild.Stretch(art.rectTransform, 3f, 3f);
                art.preserveAspect = true;
                Sprite portrait = LoadPortrait(unit.portrait);
                if (portrait != null) art.sprite = portrait;
                else art.enabled = false;
                _tileArts[unit.id] = art;

                // 이름표는 그림 위에 얹는다. 타일을 정사각형으로 유지하기 위해 자리를 따로 빼지 않는다.
                Image nameStrip = UIBuild.Solid("NameStrip", tile.transform, UITheme.Backdrop);
                UIBuild.Anchor(nameStrip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.20f), 3f, 3f);
                nameStrip.raycastTarget = false;

                TextMeshProUGUI label = UIBuild.Text("Name", tile.transform, unit.name,
                    UITheme.FontMicro, UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.20f), 3f, 3f);

                int captured = unit.id;
                UIBuild.OnClick(tile.gameObject, () => OnTileClicked(captured));
                AddHoverPreview(tile.gameObject, captured);

                _tiles[unit.id] = tile;
                if (_hovered == 0) _hovered = unit.id;
            }

            _sizer.Count = units.Count;
            _sizer.Apply();
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
            // ??는 UnityEngine.Object의 수명 검사를 건너뛴다. == 오버로드를 타야 한다.
            EventTrigger existing = target.GetComponent<EventTrigger>();
            EventTrigger trigger = existing != null ? existing : target.AddComponent<EventTrigger>();
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

            // 스탠딩은 세로로 길어 정사각형 슬롯과 맞지 않는다. 이 화면은 초상화만 쓴다.
            Sprite portrait = LoadPortrait(unit.portrait);
            _preview.enabled = portrait != null;
            if (portrait != null) _preview.sprite = portrait;

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

    /// <summary>
    /// <see cref="GridLayoutGroup"/>의 칸을 정사각형으로 유지한다.
    ///
    /// 격자 영역의 실제 픽셀 크기는 캔버스 스케일이 확정된 뒤에야 알 수 있고 창 크기에 따라 또 바뀐다.
    /// 한 번만 재면 첫 프레임에 어긋나므로, 영역 크기가 바뀔 때마다 다시 계산한다.
    /// 가로·세로 어느 쪽으로도 넘치지 않는 변 길이를 골라 후보 수와 무관하게 1:1을 지킨다.
    /// </summary>
    internal sealed class SquareGridSizer : UIBehaviour
    {
        public int MaxColumns = 6;
        public float Gap = 10f;

        /// <summary>
        /// 이번에 배치할 타일 수. 격자를 다시 그리는 쪽이 직접 알려 준다.
        ///
        /// <c>transform.childCount</c>를 세면 안 된다. <see cref="UIBuild.Clear"/>의 Destroy는
        /// 프레임 끝에 처리되므로, 다시 그린 직후에는 <b>지워질 예정인 이전 타일까지 함께 세어져</b>
        /// 열·행 수가 부풀고 칸이 실제보다 작게 잡힌다.
        /// </summary>
        public int Count;

        private GridLayoutGroup _layout;
        private RectTransform _rect;

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            Apply();
        }

        public void Apply()
        {
            if (_layout == null) _layout = GetComponent<GridLayoutGroup>();
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_layout == null || _rect == null) return;

            int count = Count;
            if (count <= 0) return;

            int columns = Mathf.Clamp(count, 1, Mathf.Max(1, MaxColumns));
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

            float width = _rect.rect.width;
            float height = _rect.rect.height;
            if (width <= 1f || height <= 1f) return;

            float side = Mathf.Max(1f, Mathf.Min(
                (width - Gap * (columns - 1)) / columns,
                (height - Gap * (rows - 1)) / rows));

            _layout.constraintCount = columns;
            _layout.spacing = new Vector2(Gap, Gap);
            if (!Mathf.Approximately(_layout.cellSize.x, side))
            {
                _layout.cellSize = new Vector2(side, side);
            }
        }
    }
}
