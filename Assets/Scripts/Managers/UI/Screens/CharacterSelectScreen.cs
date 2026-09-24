using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseClasses;
using Codes.Base;
using Core;
using Helpers;
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
    ///   · 커서를 올린 캐릭터의 큰 스탠딩과 요약을 좌측에 띄운다
    ///
    /// 로스터 타일은 정사각형 초상화를, 좌측 쇼케이스는 세로 스탠딩을 우선 사용한다.
    ///
    /// 모드에 따라 단계가 다르다.
    ///   육성 모드 — 1단계 메인 1명 → [다음] → 2단계 서포터 4명 → [시작]
    ///   무한 모드 — 메인 단계 없이 서포터 5명을 바로 고른다
    ///
    /// 좌측 미리보기는 두 모드를 오간다(버튼 · Q · 패드 Y).
    ///   요약 — 큰 초상화 + 전투 성향 태그 · 한 줄 소개 · 원소와 스탯
    ///   기술 — 고유 패시브 · 일반행동 · (특수행동) · 궁극기 설명과 해금 패시브 목록
    /// 한 번 켜면 다른 캐릭터로 커서를 옮겨도 유지된다. 여러 캐릭터의 기술을 견줘 보는 용도다.
    ///
    /// 마우스 없이도 고를 수 있다(<see cref="UINavInput"/>). 커서는 격자와 아래 버튼 줄을 오가며,
    /// 격자 맨 아랫줄에서 아래로 내리면 버튼 줄로 넘어간다. 결정은 Space · Enter · 패드 A,
    /// 메인 다시 고르기는 Backspace · 패드 B, 패드 Start는 [다음]/[여정 시작]이다.
    /// </summary>
    public class CharacterSelectScreen : ModalScreen
    {
        private const int Columns = 5;
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
        private ScrollRect _rosterScroll;
        private SquareGridSizer _sizer;
        private Image _preview;
        private TextMeshProUGUI _previewName;
        private TextMeshProUGUI _previewInfo;
        private RectTransform _tagStrip;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _phaseLabel;
        private TextMeshProUGUI _stepLabel;
        private Button _primaryButton;
        private Button _backButton;
        private Button _recommendButton;
        private Button _detailButton;
        private Button _previousCardButton;
        private Button _nextCardButton;
        private TextMeshProUGUI _cardLabel;

        private RectTransform _previewSlot;
        private GameObject _detailView;
        private RectTransform _detailContent;
        private TextMeshProUGUI _detailText;
        private ScrollRect _detailScroll;
        private float _detailWidth;
        private float _detailScale;
        private bool _detailMode;
        private readonly List<Image> _partySlots = new();
        private readonly List<Image> _partyArts = new();
        private readonly List<TextMeshProUGUI> _partyNames = new();

        /// <summary>키보드·패드 커서. 한 장을 격자 타일이나 버튼 위로 옮겨 다닌다.</summary>
        private Image _cursor;
        private bool _cursorOnButtons;
        private int _buttonIndex;
        private readonly UINavInput _nav = new();

        /// <summary>격자에 그린 순서. 방향키 이동은 이 순서의 인덱스로 계산한다.</summary>
        private readonly List<int> _order = new();

        /// <summary>
        /// 고른 메인에게 추천하는 서포터(우선순위 순). 30_synergies.yaml의 고점 조합에서
        /// 지금 고를 수 있는 사람을 먼저 담고, 모자라면 대체 조합으로 채운다.
        /// </summary>
        private readonly List<int> _recommended = new();
        private string _recommendReason = "";
        private string _recommendArchetype = "";

        private readonly Dictionary<int, Image> _tiles = new();
        private readonly Dictionary<int, Image> _tileArts = new();
        private readonly Dictionary<int, int> _cardIndexByUnit = new();
        private Phase _phase = Phase.Main;
        private int _hovered;

        private static bool IsInfinite =>
            GameManager.Instance != null && GameManager.Instance.CurrentMode == BaseEnums.GameMode.Infinite;

        // ── 조립 ─────────────────────────────────────────────────────

        protected override void Build()
        {
            // 좌 40%: 선택 후보를 한 명씩 크게 보여 주는 캐릭터 쇼케이스.
            Image previewPane = UIBuild.Panel("PreviewPane", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            UIBuild.Anchor(previewPane.rectTransform, Vector2.zero, new Vector2(0.39f, 1f));
            previewPane.rectTransform.offsetMin = new Vector2(4f, 128f);
            previewPane.rectTransform.offsetMax = new Vector2(-4f, -4f);

            TextMeshProUGUI showcaseEyebrow = UIBuild.Text("ShowcaseEyebrow", previewPane.transform,
                "CHARACTER / PROFILE", UITheme.FontMicro, UITheme.Accent, TextAlignmentOptions.Left);
            UIBuild.Anchor(showcaseEyebrow.rectTransform, new Vector2(0f, 1f), Vector2.one);
            showcaseEyebrow.rectTransform.offsetMin = new Vector2(24f, -40f);
            showcaseEyebrow.rectTransform.offsetMax = new Vector2(-24f, -16f);

            // 설명과 버튼은 고정된 하단 구역에 두고 원화가 남은 높이를 쓴다.
            RectTransform previewSlot = UIBuild.Container("PreviewSlot", previewPane.transform);
            UIBuild.Stretch(previewSlot);
            previewSlot.offsetMin = new Vector2(24f, 244f);
            previewSlot.offsetMax = new Vector2(-24f, -48f);
            _previewSlot = previewSlot;

            _preview = UIBuild.Solid("PreviewArt", previewSlot, Color.white);
            _preview.type = Image.Type.Simple;
            UIBuild.Stretch(_preview.rectTransform);
            _preview.preserveAspect = true;
            _preview.enabled = false;

            _previewName = UIBuild.Text("PreviewName", previewPane.transform, "",
                UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Center);
            AnchorFooter(_previewName.rectTransform, 202f, 236f);
            _previewName.alignment = TextAlignmentOptions.Left;

            _tagStrip = UIBuild.Container("ArchetypeTags", previewPane.transform);
            AnchorFooter(_tagStrip, 160f, 192f);

            // 소개와 기본 정보만 보여 준다. 편성 추천은 오른쪽 서포터 영역에서만 다룬다.
            _previewInfo = UIBuild.Text("PreviewInfo", previewPane.transform, "",
                UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, wrap: true);
            _previewInfo.richText = true;
            AnchorFooter(_previewInfo.rectTransform, 108f, 158f);

            _previousCardButton = UIBuild.Button("PreviousCard", previewPane.transform, "‹", () => CycleCard(-1));
            UIBuild.Anchor(_previousCardButton.image.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f));
            _previousCardButton.image.rectTransform.offsetMin = new Vector2(24f, 70f);
            _previousCardButton.image.rectTransform.offsetMax = new Vector2(70f, 104f);

            _cardLabel = UIBuild.Text("SelectedCard", previewPane.transform, "", UITheme.FontMicro,
                UITheme.TextSecondary, TextAlignmentOptions.Center, wrap: true);
            AnchorFooter(_cardLabel.rectTransform, 70f, 104f);
            _cardLabel.rectTransform.offsetMin = new Vector2(76f, 70f);
            _cardLabel.rectTransform.offsetMax = new Vector2(-76f, 104f);

            _nextCardButton = UIBuild.Button("NextCard", previewPane.transform, "›", () => CycleCard(1));
            UIBuild.Anchor(_nextCardButton.image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f));
            _nextCardButton.image.rectTransform.offsetMin = new Vector2(-70f, 70f);
            _nextCardButton.image.rectTransform.offsetMax = new Vector2(-24f, 104f);

            // 기술 상세. 코드 설명 네 덩어리는 요약 칸에 들어가지 않으므로 초상화 자리까지 쓰고 굴린다.
            _detailContent = UIBuild.ScrollArea("Detail", previewPane.transform, out ScrollRect detailScroll);
            _detailView = detailScroll.gameObject;
            _detailScroll = detailScroll;
            UIBuild.Stretch(detailScroll.viewport);
            detailScroll.viewport.offsetMin = new Vector2(24f, 84f);
            detailScroll.viewport.offsetMax = new Vector2(-24f, -106f);
            _detailText = UIBuild.Text("DetailText", _detailContent, "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            _detailText.richText = true;
            _detailText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _detailText.rectTransform.anchoredPosition = new Vector2(-8f, -8f);
            _detailView.SetActive(false);

            // 빈 문자열로 만들면 UIBuild.Button이 라벨 자체를 만들지 않아 이후 갱신할 글자가 없다.
            _detailButton = UIBuild.Button("DetailToggle", previewPane.transform, "기술 보기  [Q]", ToggleDetail);
            AnchorFooter(_detailButton.image.rectTransform, 18f, 66f);

            // 우 60%: 명확한 단계 표시, 후보 로스터, 필요할 때만 읽는 추천 근거.
            Image rosterPane = UIBuild.Panel("RosterPane", Body, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            UIBuild.Anchor(rosterPane.rectTransform, new Vector2(0.398f, 0f), Vector2.one);
            rosterPane.rectTransform.offsetMin = new Vector2(4f, 128f);
            rosterPane.rectTransform.offsetMax = new Vector2(-4f, -4f);

            _stepLabel = UIBuild.Text("Step", rosterPane.transform, "", UITheme.FontMicro, UITheme.Accent,
                TextAlignmentOptions.Left);
            UIBuild.Anchor(_stepLabel.rectTransform, new Vector2(0f, 1f), Vector2.one);
            _stepLabel.rectTransform.offsetMin = new Vector2(20f, -38f);
            _stepLabel.rectTransform.offsetMax = new Vector2(-20f, -14f);

            _phaseLabel = UIBuild.Text("Phase", rosterPane.transform, "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.Left);
            UIBuild.Anchor(_phaseLabel.rectTransform, new Vector2(0f, 1f), Vector2.one);
            _phaseLabel.rectTransform.offsetMin = new Vector2(20f, -74f);
            _phaseLabel.rectTransform.offsetMax = new Vector2(-20f, -42f);

            _grid = UIBuild.ScrollArea("RosterScroll", rosterPane.transform, out ScrollRect rosterScroll);
            _rosterScroll = rosterScroll;
            UIBuild.Stretch(rosterScroll.viewport);
            rosterScroll.viewport.offsetMin = new Vector2(16f, 16f);
            rosterScroll.viewport.offsetMax = new Vector2(-16f, -88f);

            // 타일 배치는 GridLayoutGroup에 맡기고, 칸 크기는 SquareGridSizer가 정사각형으로 유지한다.
            var layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.spacing = new Vector2(TileGap, TileGap);
            layout.childAlignment = TextAnchor.UpperCenter;
            // 커서 외곽 4px와 오른쪽 스크롤 막대가 마스크에 잘리지 않도록 비운다.
            layout.padding = new RectOffset(6, 18, 6, 6);

            _sizer = _grid.gameObject.AddComponent<SquareGridSizer>();
            _sizer.MaxColumns = Columns;
            _sizer.Gap = TileGap;
            _sizer.ScrollContent = true;

            _summary = UIBuild.Text("Summary", rosterPane.transform, "", UITheme.FontCaption, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            AnchorFooter(_summary.rectTransform, 16f, 78f);

            _backButton = UIBuild.Button("Back", rosterPane.transform, "← 메인 변경", GoBackToMain);
            UIBuild.Anchor(_backButton.image.rectTransform, Vector2.zero, new Vector2(0.35f, 0f));
            _backButton.image.rectTransform.offsetMin = new Vector2(24f, 88f);
            _backButton.image.rectTransform.offsetMax = new Vector2(-6f, 132f);

            _recommendButton = UIBuild.Button("Recommend", rosterPane.transform, "★ 추천 4명 채우기", ApplyRecommendation);
            UIBuild.Anchor(_recommendButton.image.rectTransform, new Vector2(0.35f, 0f), new Vector2(0.78f, 0f));
            _recommendButton.image.rectTransform.offsetMin = new Vector2(6f, 88f);
            _recommendButton.image.rectTransform.offsetMax = new Vector2(-6f, 132f);

            _primaryButton = UIBuild.Button("Primary", Body, "다음", OnPrimary, primary: true);
            UIBuild.Anchor(_primaryButton.image.rectTransform, new Vector2(0.80f, 0f), new Vector2(1f, 0f));
            _primaryButton.image.rectTransform.offsetMin = new Vector2(4f, 4f);
            _primaryButton.image.rectTransform.offsetMax = new Vector2(-4f, 112f);

            BuildPartyRail();

            _cursor = UIBuild.Solid("NavCursor", Body, Color.white);
            _cursor.sprite = UIShapes.CutCorner(8, new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal,
                UITheme.TextPrimary, 3);
            _cursor.type = Image.Type.Sliced;
            _cursor.raycastTarget = false;
            _cursor.gameObject.SetActive(false);
        }

        private static void AnchorFooter(RectTransform rect, float bottom, float top)
        {
            UIBuild.Anchor(rect, Vector2.zero, new Vector2(1f, 0f));
            rect.offsetMin = new Vector2(24f, bottom);
            rect.offsetMax = new Vector2(-24f, top);
        }

        private void BuildPartyRail()
        {
            Image rail = UIBuild.Panel("PartyRail", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            UIBuild.Anchor(rail.rectTransform, Vector2.zero, new Vector2(0.79f, 0f));
            rail.rectTransform.offsetMin = new Vector2(4f, 4f);
            rail.rectTransform.offsetMax = new Vector2(-4f, 112f);

            TextMeshProUGUI label = UIBuild.Text("PartyLabel", rail.transform, "PARTY  /  출전 편성",
                UITheme.FontMicro, UITheme.Accent, TextAlignmentOptions.Left);
            UIBuild.Anchor(label.rectTransform, new Vector2(0.025f, 0.7f), new Vector2(0.2f, 0.94f));

            for (int i = 0; i < MaxParty; i++)
            {
                float left = 0.205f + i * 0.153f;
                Image slot = UIBuild.Panel($"PartySlot{i + 1}", rail.transform, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 5, UITheme.Outline, 1);
                UIBuild.Anchor(slot.rectTransform, new Vector2(left, 0.12f), new Vector2(left + 0.14f, 0.88f));

                Image art = UIBuild.Solid("Portrait", slot.transform, Color.white);
                UIBuild.Anchor(art.rectTransform, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.96f));
                art.preserveAspect = true;
                art.type = Image.Type.Simple;
                art.raycastTarget = false;

                TextMeshProUGUI name = UIBuild.Text("Name", slot.transform, $"{i + 1}", UITheme.FontMicro,
                    UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Anchor(name.rectTransform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.25f));

                _partySlots.Add(slot);
                _partyArts.Add(art);
                _partyNames.Add(name);
            }
        }

        public override void Show()
        {
            EnsureCharacterSelectionManager();
            CharacterSelectionManager.Instance?.ClearLineup();

            // 무한 모드에는 메인 단계가 없다.
            _phase = IsInfinite ? Phase.Support : Phase.Main;
            _hovered = 0;
            _recommended.Clear();
            _cardIndexByUnit.Clear();
            _cursorOnButtons = false;

            base.Show();
            _nav.Reset();
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
                _hovered = 0;
                _cursorOnButtons = false;
                BuildRecommendation(manager.MainUnitId);
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
            _recommended.Clear();
            _cursorOnButtons = false;
            RebuildGrid();
            RefreshVisuals();
        }

        // ── 격자 ─────────────────────────────────────────────────────

        private void RebuildGrid()
        {
            // 커서가 타일의 자식이면 타일과 함께 지워진다. 지우기 전에 빼 둔다.
            _cursor.transform.SetParent(Body, false);
            _cursor.gameObject.SetActive(false);

            UIBuild.Clear(_grid);
            _tiles.Clear();
            _tileArts.Clear();
            _order.Clear();

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
                Image nameStrip = UIBuild.Solid("NameStrip", tile.transform, UITheme.Surface);
                UIBuild.Anchor(nameStrip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.20f), 3f, 3f);
                nameStrip.raycastTarget = false;

                TextMeshProUGUI label = UIBuild.Text("Name", tile.transform, IsLavoisierLocked(unit.id) ? $"미해금 · {unit.name}" : unit.name,
                    UITheme.FontMicro, UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.20f), 3f, 3f);

                int rank = _recommended.IndexOf(unit.id);
                // 서포터 단계의 추천 편성 순위만 표시한다.
                if (rank >= 0)
                {
                    Image badge = UIBuild.Solid("RecommendBadge", tile.transform, UITheme.Accent);
                    UIBuild.Anchor(badge.rectTransform, new Vector2(0f, 0.82f), new Vector2(0.62f, 1f), 3f, 3f);
                    badge.raycastTarget = false;
                    TextMeshProUGUI badgeLabel = UIBuild.Text("RecommendLabel", badge.transform, $"★ 추천 {rank + 1}",
                        UITheme.FontMicro, UITheme.TextOnAccent, TextAlignmentOptions.Center);
                    UIBuild.Stretch(badgeLabel.rectTransform);
                    badgeLabel.raycastTarget = false;
                }

                int cardCount = SaveSystem.GetTrainedCharacterRecords(unit.id).Count;
                if (cardCount > 0)
                {
                    Image countBadge = UIBuild.Solid("CardCountBadge", tile.transform, UITheme.Surface);
                    UIBuild.Anchor(countBadge.rectTransform, new Vector2(0.58f, 0.82f), Vector2.one, 3f, 3f);
                    countBadge.raycastTarget = false;
                    TextMeshProUGUI countLabel = UIBuild.Text("CardCount", countBadge.transform, $"육성 ×{cardCount}",
                        UITheme.FontMicro, UITheme.Accent, TextAlignmentOptions.Center);
                    UIBuild.Stretch(countLabel.rectTransform);
                    countLabel.raycastTarget = false;
                }

                int captured = unit.id;
                UIBuild.OnClick(tile.gameObject, () => OnTileClicked(captured));
                AddHoverPreview(tile.gameObject, captured);

                _tiles[unit.id] = tile;
                _order.Add(unit.id);
                if (_hovered == 0) _hovered = unit.id;
            }

            _sizer.Count = units.Count;
            _sizer.Apply();
            _rosterScroll.verticalNormalizedPosition = 1f;
        }

        private void OnTileClicked(int unitId)
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            _hovered = unitId;

            if (IsLavoisierLocked(unitId))
            {
                RefreshPreview();
                return;
            }

            bool alreadyPicked = manager.Lineup.Any(entry => entry.UnitId == unitId);
            if (alreadyPicked)
            {
                manager.RemoveHero(unitId);
            }
            else if (_phase == Phase.Main)
            {
                // 메인 단계에서는 항상 한 명만 남긴다.
                manager.ClearLineup();
                manager.AddHero(unitId, SelectedSupportId(unitId));
            }
            else
            {
                manager.AddHero(unitId, SelectedSupportId(unitId));
            }

            RefreshVisuals();
        }

        /// <summary>커서가 타일 위에 올라가면 좌측 큰 초상화를 갈아 끼운다.</summary>
        private void AddHoverPreview(GameObject target, int unitId)
        {
            // EventTrigger를 붙이면 타일이 휠까지 먹어, 커서가 초상화 위에 있을 때 명단이 굴러가지 않았다.
            UIPointerEvents.On(target).Entered += () =>
            {
                _hovered = unitId;
                _cursorOnButtons = false;
                RefreshPreview();
                PlaceCursor();
            };
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        private void RefreshVisuals()
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            bool mainPhase = _phase == Phase.Main;
            int supportTarget = IsInfinite ? MaxParty : MaxParty - 1;
            int supportCount = IsInfinite ? manager.Lineup.Count : manager.SupportUnitIds.Count;

            SetTitle(IsInfinite
                ? "무한 모드 — 서포터 카드 5장 선택"
                : mainPhase ? "육성 모드 — 1단계: 메인 캐릭터" : "육성 모드 — 2단계: 서포터 카드");

            _phaseLabel.text = mainPhase
                ? "여정을 이끌 메인 캐릭터를 선택하세요"
                : $"메인을 보완할 서포터를 선택하세요   {supportCount} / {supportTarget}";
            _stepLabel.text = IsInfinite
                ? "PARTY SETUP  ·  서포터 카드 5장"
                : mainPhase ? "STEP 01  /  02  ·  메인 선택" : "STEP 02  /  02  ·  서포터 선택";

            foreach (KeyValuePair<int, Image> pair in _tiles)
            {
                bool isSelected = manager.Lineup.Any(entry => entry.UnitId == pair.Key);
                bool isMain = manager.MainUnitId == pair.Key;
                if (_tileArts.TryGetValue(pair.Key, out Image tileArt))
                    tileArt.color = IsLavoisierLocked(pair.Key) ? new Color(.35f, .35f, .35f) : Color.white;

                Color fill = isMain ? UITheme.Accent : isSelected ? UITheme.AccentFaint : UITheme.SurfaceRaised;
                Color line = isSelected ? UITheme.Accent : UITheme.Outline;
                pair.Value.sprite = UIShapes.CutCorner(6, fill, UIShapes.Corner.Diagonal, line, isSelected ? 2 : 1);
            }

            string recommendText = RecommendationSummary();
            _summary.text = recommendText.Length > 0
                    ? $"{Colored("추천 조합", UITheme.Accent)}  {recommendText}"
                    : $"{Colored("편성 가이드", UITheme.Accent)}  서로 다른 역할을 조합해 빈틈을 보완하세요.";
            _summary.gameObject.SetActive(!mainPhase);
            // 메인 단계에는 비어 있는 추천/버튼 공간까지 로스터에 돌려준다.
            _rosterScroll.viewport.offsetMin = new Vector2(16f, mainPhase ? 16f : IsInfinite ? 88f : 144f);
            _sizer.Apply();

            RefreshPartyRail(manager);

            // 버튼 상태
            // 무한 모드는 카드 5장이 모두 차야 시작된다(ConfirmSelection이 그렇게 막는다).
            // 버튼이 먼저 켜지면 눌러도 아무 일이 없어 멈춘 것처럼 보인다.
            bool canProceed = mainPhase ? manager.MainUnitId > 0
                : IsInfinite ? supportCount >= supportTarget : supportCount > 0;
            SetButton(_primaryButton, mainPhase ? "다음 →" : "여정 시작", canProceed);
            SetButtonVisible(_backButton, !IsInfinite && !mainPhase);
            SetButtonVisible(_recommendButton, !mainPhase && _recommended.Count > 0);
            SetButton(_recommendButton, "★ 추천 4명 채우기", !IsRecommendationApplied(manager), onAccent: false);

            RefreshPreview();
            PlaceCursor();
        }

        private void RefreshPartyRail(CharacterSelectionManager manager)
        {
            List<CharacterSelectionManager.LineupEntry> entries = manager.Lineup.Take(MaxParty).ToList();
            for (int i = 0; i < _partySlots.Count; i++)
            {
                bool filled = i < entries.Count;
                CharacterSelectionManager.LineupEntry entry = filled ? entries[i] : null;
                int id = entry?.UnitId ?? 0;
                UnitData unit = filled
                    ? GameManager.Instance?.unitDataList?.units?.FirstOrDefault(candidate => candidate.id == id)
                    : null;

                Sprite portrait = unit != null ? LoadPortrait(unit.portrait) : null;
                _partyArts[i].enabled = portrait != null;
                if (portrait != null) _partyArts[i].sprite = portrait;
                string variant = filled ? CardVariantLabel(id, entry.SupportId) : "";
                _partyNames[i].text = filled
                    ? (manager.MainUnitId == id ? $"MAIN · {UnitName(id)}{variant}" : $"{UnitName(id)}{variant}")
                    : $"{i + 1}  비어 있음";
                _partyNames[i].color = filled ? UITheme.TextPrimary : UITheme.TextMuted;
                Color fill = manager.MainUnitId == id ? UITheme.AccentFaint : UITheme.SurfaceRaised;
                Color line = filled ? UITheme.Accent : UITheme.Outline;
                _partySlots[i].sprite = UIShapes.CutCorner(5, fill, UIShapes.Corner.Diagonal, line, filled ? 2 : 1);
            }
        }

        private void RefreshPreview()
        {
            ApplyPreviewMode();

            UnitData unit = Candidates().FirstOrDefault(candidate => candidate.id == _hovered);
            if (unit == null)
            {
                _preview.enabled = false;
                _previewName.text = "";
                _previewInfo.text = "";
                UIBuild.Clear(_tagStrip);
                _detailText.text = "";
                RefreshCardSelector(null);
                return;
            }

            if (_detailMode)
            {
                _previewName.text = unit.name;
                RefreshCardSelector(unit);
                ShowDetail(unit);
                return;
            }

            // 큰 쇼케이스는 세로 스탠딩을 우선하고, 없는 캐릭터만 초상화로 대체한다.
            Sprite portrait = SpriteResource.LoadStanding(unit.standing);
            if (portrait == null) portrait = LoadPortrait(unit.portrait);
            _preview.enabled = portrait != null;
            if (portrait != null) _preview.sprite = portrait;

            _previewName.text = unit.name;
            UnitTagCatalog.BuildChips(_tagStrip,
                IsLavoisierLocked(unit.id)
                    ? System.Array.Empty<UnitTagCatalog.Definition>()
                    : UnitTagCatalog.Resolve(unit.archetypeTags),
                UITheme.Accent);
            List<string> subStats = unit.subStats?
                .Where(stat => !string.IsNullOrWhiteSpace(stat))
                .ToList() ?? new List<string>();
            if (subStats.Count == 0 && !string.IsNullOrWhiteSpace(unit.subStat))
                subStats.Add(unit.subStat);
            string statText = subStats.Count == 0
                ? $"주 {unit.mainStat} (단일)"
                : $"주 {unit.mainStat} · 부 {string.Join(" · ", subStats)}";

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(unit.tagline) && !IsLavoisierLocked(unit.id))
                lines.Add(Colored(unit.tagline, UITheme.TextPrimary));
            lines.Add($"{ElementName(unit.element)}   {statText}");

            if (IsLavoisierLocked(unit.id))
                lines.Add(Colored(RoleText(unit), UITheme.TextMuted));
            _previewInfo.text = string.Join("\n", lines);
            RefreshCardSelector(unit);
        }

        private void CycleCard(int direction)
        {
            List<TrainedCharacterRecord> records = SaveSystem.GetTrainedCharacterRecords(_hovered);
            if (records.Count <= 1) return;

            int current = SelectedCardIndex(_hovered, records.Count);
            _cardIndexByUnit[_hovered] = (current + direction + records.Count) % records.Count;
            CharacterSelectionManager.Instance?.SetSelectedSupportCard(_hovered, SelectedSupportId(_hovered));
            RefreshVisuals();
        }

        private void RefreshCardSelector(UnitData unit)
        {
            bool visible = !_detailMode && unit != null && !IsLavoisierLocked(unit.id);
            _cardLabel.gameObject.SetActive(visible);
            if (!visible)
            {
                _previousCardButton.gameObject.SetActive(false);
                _nextCardButton.gameObject.SetActive(false);
                return;
            }

            List<TrainedCharacterRecord> records = SaveSystem.GetTrainedCharacterRecords(unit.id);
            bool multiple = records.Count > 1;
            _previousCardButton.gameObject.SetActive(multiple);
            _nextCardButton.gameObject.SetActive(multiple);
            if (records.Count == 0)
            {
                _cardLabel.text = _phase == Phase.Main ? "신규 육성" : "기본 지원 카드";
                return;
            }

            int index = SelectedCardIndex(unit.id, records.Count);
            SupportCardSaveData card = records[index].supportCard;
            _cardLabel.text = $"육성 카드 {index + 1} / {records.Count} · 특기 {card.specialtyTraining} · 훈련 +{card.trainingBonus}";
        }

        private int SelectedCardIndex(int unitId, int count)
        {
            if (count <= 0) return -1;
            if (!_cardIndexByUnit.TryGetValue(unitId, out int index))
            {
                string selected = CharacterSelectionManager.Instance?.Lineup
                    .FirstOrDefault(entry => entry.UnitId == unitId)?.SupportId;
                List<TrainedCharacterRecord> records = SaveSystem.GetTrainedCharacterRecords(unitId);
                index = records.FindIndex(record => record.supportCard?.supportId == selected);
                if (index < 0) index = count - 1;
            }
            index = Mathf.Clamp(index, 0, count - 1);
            _cardIndexByUnit[unitId] = index;
            return index;
        }

        private string SelectedSupportId(int unitId)
        {
            List<TrainedCharacterRecord> records = SaveSystem.GetTrainedCharacterRecords(unitId);
            int index = SelectedCardIndex(unitId, records.Count);
            return index >= 0 ? records[index].supportCard?.supportId : "";
        }

        private static string CardVariantLabel(int unitId, string supportId)
        {
            List<TrainedCharacterRecord> records = SaveSystem.GetTrainedCharacterRecords(unitId);
            if (records.Count == 0 || string.IsNullOrWhiteSpace(supportId)) return "";
            int index = records.FindIndex(record => record.supportCard?.supportId == supportId);
            return index >= 0 ? $" · #{index + 1}" : "";
        }

        // ── 기술 상세 ────────────────────────────────────────────────

        private void ToggleDetail()
        {
            _detailMode = !_detailMode;
            RefreshPreview();
        }

        /// <summary>요약은 초상화 + 짧은 글, 기술은 이름을 위로 올리고 나머지 자리를 설명에 준다.</summary>
        private void ApplyPreviewMode()
        {
            _previewSlot.gameObject.SetActive(!_detailMode);
            _previewInfo.gameObject.SetActive(!_detailMode);
            _tagStrip.gameObject.SetActive(!_detailMode);
            _cardLabel.gameObject.SetActive(!_detailMode);
            _previousCardButton.gameObject.SetActive(!_detailMode);
            _nextCardButton.gameObject.SetActive(!_detailMode);
            _detailView.SetActive(_detailMode);

            if (_detailMode)
            {
                UIBuild.Anchor(_previewName.rectTransform, new Vector2(0f, 1f), Vector2.one);
                _previewName.rectTransform.offsetMin = new Vector2(24f, -94f);
                _previewName.rectTransform.offsetMax = new Vector2(-24f, -48f);
            }
            else AnchorFooter(_previewName.rectTransform, 202f, 236f);

            TextMeshProUGUI label = _detailButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = _detailMode ? "요약 보기  [Q]" : "기술 보기  [Q]";
        }

        /// <summary>
        /// 고유 패시브 · 일반행동 · 특수행동 · 궁극기를 설명과 함께. 해금 패시브는 이름과 레벨만 —
        /// 전부 풀어 쓰면 칸이 넘치고, 자세한 설명은 자료실에 있다.
        /// </summary>
        private void ShowDetail(UnitData unit)
        {
            var sb = new StringBuilder();

            if (IsLavoisierLocked(unit.id))
            {
                sb.Append(CodeText.Paint(RoleText(unit), UITheme.TextMuted));
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(unit.tagline))
                    sb.Append($"<size={UITheme.FontCaption}>{CodeText.Paint(CodeText.Plain(unit.tagline), UITheme.TextSecondary)}</size>\n\n");

                if (unit.codes != null)
                {
                    if (unit.codes.TryGetValue("passive", out int passive))
                        CodeText.AppendBlock(sb, CodeCatalog.Slot.Passive, passive, "고유 패시브");
                    if (unit.codes.TryGetValue("normal", out int normal))
                        CodeText.AppendBlock(sb, CodeCatalog.Slot.Normal, normal, "일반행동");
                    if (unit.codes.TryGetValue("special", out int special))
                        CodeText.AppendBlock(sb, CodeCatalog.Slot.Special, special, "특수행동");
                    if (unit.codes.TryGetValue("ultimate", out int ultimate))
                        CodeText.AppendBlock(sb, CodeCatalog.Slot.Ultimate, ultimate, "궁극기");
                }

                List<LevelPassiveData> unlocks = unit.levelPassives?
                    .Where(passive => passive != null)
                    .OrderBy(passive => passive.unlockLevel)
                    .ToList() ?? new List<LevelPassiveData>();
                if (unlocks.Count > 0)
                {
                    sb.Append($"<b>{CodeText.Paint($"해금 패시브 {unlocks.Count}개", UITheme.Accent)}</b>\n");
                    IEnumerable<string> names = unlocks.Select(passive =>
                        $"Lv.{passive.unlockLevel} " +
                        (CodeCatalog.Find(CodeCatalog.Slot.Passive, passive.codeId)?.verbalName ?? $"#{passive.codeId}"));
                    sb.Append($"<size={UITheme.FontCaption}>{CodeText.Paint(CodeText.Plain(string.Join(" · ", names)), UITheme.TextSecondary)}</size>\n");
                    sb.Append($"<size={UITheme.FontMicro}>{CodeText.Paint("각 코드의 설명은 ESC 메뉴 › 자료실에서 볼 수 있다.", UITheme.TextMuted)}</size>");
                }
            }

            string text = sb.ToString();
            _detailText.text = text;

            // 처음 여는 프레임엔 캔버스 크기가 덜 잡혀 폭이 틀리게 읽힌다. 틀리면 줄 수를 적게 재 끝이 잘린다.
            Canvas.ForceUpdateCanvases();
            ResizeDetail();
            _detailScroll.StopMovement();
            _detailContent.anchoredPosition = Vector2.zero;
        }

        private void ResizeDetail()
        {
            _detailWidth = _detailContent.rect.width;
            _detailScale = UITheme.TextScale;
            float width = Mathf.Max(1f, _detailWidth - 16f);
            // 스크롤 본문은 글자를 축소하지 않고 전체 높이를 확보한다.
            _detailText.enableAutoSizing = false;
            _detailText.fontSize = UITheme.FontBody * _detailScale;
            float height = Mathf.Ceil(_detailText.GetPreferredValues(_detailText.text, width, 0f).y);
            _detailText.rectTransform.sizeDelta = new Vector2(-16f, height);
            _detailContent.sizeDelta = new Vector2(0f, height + 40f);
        }

        // ── 키보드 · 패드 ────────────────────────────────────────────

        /// <summary>UIManager가 매 프레임 부른다. 이 화면은 MonoBehaviour가 아니다.</summary>
        public void Tick()
        {
            if (!IsVisible) return;
            if (_detailMode && (Mathf.Abs(_detailWidth - _detailContent.rect.width) > 0.5f ||
                                !Mathf.Approximately(_detailScale, UITheme.TextScale)))
                ResizeDetail();

            // 메뉴·캐릭터 창이 위에 떠 있으면 그쪽 입력이다. 뒤에서 커서가 움직이면 안 된다.
            if (GameManager.Instance?.uiManager?.IsOverlayOpen == true) return;

            // 마우스로 버튼을 누르면 EventSystem이 그 버튼을 '선택'해 두고, 이후 Space·Enter·패드 A를
            // 그 버튼의 클릭으로 보낸다. 여기서도 결정을 처리하므로 두 번 눌리게 된다. 선택을 늘 비운다.
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null &&
                events.currentSelectedGameObject.transform.IsChildOf(Body))
            {
                events.SetSelectedGameObject(null);
            }

            _nav.Poll();
            if (!_nav.Any) return;

            if (_nav.Info)
            {
                ToggleDetail();
            }

            if (_nav.Cancel)
            {
                GoBackToMain();
                return;
            }

            if (_nav.Start)
            {
                if (_primaryButton.interactable) OnPrimary();
                return;
            }

            if (_nav.Move != Vector2Int.zero)
            {
                if (_cursorOnButtons) MoveOnButtons(_nav.Move);
                else MoveOnGrid(_nav.Move);
                RefreshPreview();
                PlaceCursor();
            }

            if (_nav.Submit)
            {
                if (_cursorOnButtons)
                {
                    List<Button> buttons = NavButtons();
                    if (_buttonIndex < buttons.Count && buttons[_buttonIndex].interactable)
                        buttons[_buttonIndex].onClick.Invoke();
                }
                else if (_hovered != 0)
                {
                    OnTileClicked(_hovered);
                }
            }
        }

        /// <summary>
        /// 격자 안의 이동. 좌우는 줄을 넘어 이어지고, 맨 아랫줄에서 더 내리면 버튼 줄로 간다.
        /// 아랫줄이 덜 찬 격자에서 그 위 칸이 내려갈 자리가 없으면 마지막 타일로 붙는다.
        /// </summary>
        private void MoveOnGrid(Vector2Int move)
        {
            if (_order.Count == 0)
            {
                _cursorOnButtons = true;
                return;
            }

            int columns = Mathf.Clamp(_order.Count, 1, Columns);
            int index = Mathf.Max(0, _order.IndexOf(_hovered));
            int rows = Mathf.CeilToInt(_order.Count / (float)columns);

            if (move.x != 0)
            {
                index = Mathf.Clamp(index + move.x, 0, _order.Count - 1);
            }
            else if (move.y > 0)
            {
                if (index - columns >= 0) index -= columns;
            }
            else if (move.y < 0)
            {
                if (index / columns >= rows - 1)
                {
                    _cursorOnButtons = true;
                    _buttonIndex = DefaultButtonIndex();
                    return;
                }

                index = Mathf.Min(index + columns, _order.Count - 1);
            }

            _hovered = _order[index];
            EnsureHoveredVisible(index, columns, rows);
        }

        /// <summary>패드/키보드로 화면 밖 행에 도달하면 해당 행이 보이도록 목록도 함께 움직인다.</summary>
        private void EnsureHoveredVisible(int index, int columns, int rows)
        {
            if (_rosterScroll == null || rows <= 1) return;
            int row = index / Mathf.Max(1, columns);
            _rosterScroll.verticalNormalizedPosition = 1f - row / (float)(rows - 1);
        }

        private void MoveOnButtons(Vector2Int move)
        {
            List<Button> buttons = NavButtons();
            if (move.y > 0 || buttons.Count == 0)
            {
                _cursorOnButtons = false;
                return;
            }

            if (move.x != 0) _buttonIndex = Mathf.Clamp(_buttonIndex + move.x, 0, buttons.Count - 1);
        }

        /// <summary>지금 보이는 버튼, 화면 왼쪽부터. 기술 보기 버튼도 같은 줄이다.</summary>
        private List<Button> NavButtons() =>
            new[] { _detailButton, _backButton, _recommendButton, _primaryButton }
                .Where(button => button != null && button.gameObject.activeSelf)
                .ToList();

        /// <summary>버튼 줄에 내려오면 [다음]/[여정 시작]에 먼저 선다. 가장 자주 누를 버튼이다.</summary>
        private int DefaultButtonIndex() => Mathf.Max(0, NavButtons().IndexOf(_primaryButton));

        /// <summary>커서 테두리를 지금 가리키는 타일이나 버튼 위로 옮긴다.</summary>
        private void PlaceCursor()
        {
            if (_cursor == null) return;

            RectTransform target = null;
            if (_cursorOnButtons)
            {
                List<Button> buttons = NavButtons();
                if (buttons.Count == 0) _cursorOnButtons = false;
                else target = buttons[Mathf.Clamp(_buttonIndex, 0, buttons.Count - 1)].image.rectTransform;
            }

            if (!_cursorOnButtons && _tiles.TryGetValue(_hovered, out Image tile))
            {
                target = tile.rectTransform;
            }

            if (target == null)
            {
                _cursor.gameObject.SetActive(false);
                return;
            }

            _cursor.transform.SetParent(target, false);
            UIBuild.Stretch(_cursor.rectTransform, -4f, -4f);
            _cursor.transform.SetAsLastSibling();
            _cursor.gameObject.SetActive(true);
        }

        private static string RoleText(UnitData unit)
            => CharacterSelectionManager.UnlockConditionText(unit);

        private static void SetButton(Button button, string label, bool interactable, bool onAccent = true)
        {
            if (button == null) return;
            button.interactable = interactable;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                text.color = !interactable ? UITheme.TextMuted
                    : onAccent ? UITheme.TextOnAccent : UITheme.TextPrimary;
            }
        }

        // ── 추천 조합 ────────────────────────────────────────────────

        /// <summary>
        /// 메인의 추천 서포터 넷을 정한다. 고점 조합(전 캐릭터 해금 가정)에서 지금 격자에 오른 사람을
        /// 먼저 담고, 빈자리는 최초 로스터만 쓰는 대체 조합으로 채운다. 해금이 덜 된 계정에서도
        /// 네 자리가 비지 않게 하려는 것이다.
        /// </summary>
        private void BuildRecommendation(int mainUnitId)
        {
            _recommended.Clear();
            _recommendReason = "";
            _recommendArchetype = "";

            SynergyRecommendationData entry = SynergyCatalog.RecommendationFor(mainUnitId);
            if (entry == null) return;

            var available = new HashSet<int>(Candidates()
                .Where(unit => !IsLavoisierLocked(unit.id))
                .Select(unit => unit.id));

            IEnumerable<int> Pick(SynergyLineupData lineup) =>
                lineup?.members?.Where(available.Contains) ?? Enumerable.Empty<int>();

            foreach (int id in Pick(entry.best).Concat(Pick(entry.basic)))
            {
                if (_recommended.Count >= MaxParty - 1) break;
                if (!_recommended.Contains(id)) _recommended.Add(id);
            }

            // 고점 조합이 통째로 들어갔으면 그 이유를, 아니면 대체 조합의 이유를 보여 준다.
            bool bestComplete = entry.best?.members != null && entry.best.members.All(available.Contains);
            SynergyLineupData shown = bestComplete ? entry.best : entry.basic ?? entry.best;
            _recommendReason = shown?.reason?.Trim() ?? "";
            _recommendArchetype = string.IsNullOrWhiteSpace(shown?.archetype)
                ? ""
                : SynergyCatalog.ArchetypeName(shown.archetype);
        }

        /// <summary>추천 서포터로 서포터 칸을 통째로 다시 채운다. 메인은 그대로 둔다.</summary>
        private void ApplyRecommendation()
        {
            CharacterSelectionManager manager = CharacterSelectionManager.Instance;
            if (manager == null || _recommended.Count == 0) return;

            foreach (int id in manager.SupportUnitIds.ToList()) manager.RemoveHero(id);
            foreach (int id in _recommended) manager.AddHero(id);
            RefreshVisuals();
        }

        private bool IsRecommendationApplied(CharacterSelectionManager manager) =>
            manager != null && _recommended.Count > 0 &&
            manager.SupportUnitIds.Count == _recommended.Count &&
            _recommended.All(id => manager.SupportUnitIds.Contains(id));

        private string RecommendationSummary()
        {
            if (_phase != Phase.Support || _recommended.Count == 0) return "";
            string names = string.Join(" · ", _recommended.Select(UnitName));
            string archetype = string.IsNullOrWhiteSpace(_recommendArchetype) ? "" : $" ({_recommendArchetype})";
            string reason = string.IsNullOrWhiteSpace(_recommendReason) ? "" : $" — {FirstSentence(_recommendReason)}";
            return names + archetype + reason;
        }

        /// <summary>추천 이유는 여러 문장이라 요약 칸에는 첫 문장만 싣는다.</summary>
        private static string FirstSentence(string text)
        {
            string flat = text.Replace("\r", "").Replace("\n", " ").Trim();
            int end = flat.IndexOf(". ", System.StringComparison.Ordinal);
            return end > 0 ? flat.Substring(0, end + 1) : flat;
        }

        private static string Colored(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        private static string ElementName(string element) => element switch
        {
            "Pyro" => "불",
            "Hydro" => "물",
            "Anemo" => "바람",
            "Electro" => "번개",
            "Dendro" => "풀",
            "Cryo" => "얼음",
            "Geo" => "바위",
            _ => string.IsNullOrWhiteSpace(element) ? "무속성" : element,
        };

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null) button.gameObject.SetActive(visible);
        }

        // ── 후보 ─────────────────────────────────────────────────────

        private List<UnitData> Candidates()
        {
            // 예전에는 id < 100만 띄웠다. 유닛 ID가 진영별 20칸 블록으로 넓어진 뒤로는
            // 로마(100~)·이집트(120~)·메히코(140~)·갈리아(160~)가 통째로 빠져, 후반 ID 블록의 스타터를 고를 수 없었다.
            List<UnitData> units = GameManager.Instance?.unitDataList?.units ?? new List<UnitData>();

            if (IsInfinite)
                return units.Where(CharacterSelectionManager.IsInfiniteEligible).ToList();

            // 육성 모드 1단계는 메인이 될 수 있는 캐릭터만, 2단계는 서포터 후보만 보여준다.
            if (_phase == Phase.Main)
            {
                return units
                    // 초기 서포트 카드는 육성이 끝나 기록이 남아도 메인 격자에 오르지 않는다.
                    // 스스로 얻어 낸 스타팅 해금(니콜·프레이아·마리)만 예외로 올라온다 — IsSupportOnly가 판단한다.
                    .Where(unit => !CharacterSelectionManager.IsSupportOnly(unit))
                    .Where(unit => unit.canStartAsMain ||
                                   CharacterSelectionManager.IsTemporarilyUnlocked(unit) ||
                                   CharacterSelectionManager.HasNoUnlockPath(unit) ||
                                   SaveSystem.IsStarterUnlocked(unit.id) ||
                                   CharacterSelectionManager.IsPendingUnlock(unit))
                    .OrderBy(unit => unit.id)
                    .ToList();
            }

            // 유닛 중복 출전 금지 — 메인으로 고른 캐릭터는 서포터 격자에 아예 띄우지 않는다.
            // 육성이 끝난 스타터가 서포터 카드로도 등장하기 시작하면 같은 유닛이 양쪽에 오를 수 있다.
            int mainUnitId = CharacterSelectionManager.Instance?.MainUnitId ?? 0;

            return units
                .Where(unit => unit.canStartAsSupport ||
                               CharacterSelectionManager.HasNoUnlockPath(unit) ||
                               SaveSystem.IsStarterUnlocked(unit.id) ||
                               SaveSystem.IsCharacterTrained(unit.id))
                .Where(unit => unit.id != mainUnitId)
                .OrderBy(unit => unit.id)
                .ToList();
        }

        private static Sprite LoadPortrait(string spriteName)
            => SpriteResource.LoadPortrait(spriteName);

        /// <summary>해금 조건이 남아 있는 유닛인가. 판정은 CharacterSelectionManager가 갖는다.</summary>
        private static bool IsLavoisierLocked(int id) => CharacterSelectionManager.IsPendingUnlock(id);

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
    /// 스크롤 목록은 가로폭을 채우고, 고정 영역은 가로·세로에 맞춰 1:1을 지킨다.
    /// </summary>
    internal sealed class SquareGridSizer : UIBehaviour
    {
        public int MaxColumns = 6;
        public float Gap = 10f;
        public bool ScrollContent;

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
        private float _appliedWidth = -1f;
        private float _appliedViewportHeight = -1f;
        private int _appliedCount = -1;

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

        private void LateUpdate()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_rect == null) return;

            float width = _rect.rect.width;
            float viewportHeight = _rect.parent is RectTransform viewport
                ? viewport.rect.height
                : _rect.rect.height;

            // ScrollRect의 앵커 높이는 화면을 만든 프레임의 끝에서 확정된다. Content 자체는
            // 위쪽 고정 높이라 부모만 변할 때 OnRectTransformDimensionsChange가 오지 않을 수 있다.
            if (_appliedCount != Count ||
                Mathf.Abs(_appliedWidth - width) > 0.5f ||
                Mathf.Abs(_appliedViewportHeight - viewportHeight) > 0.5f)
            {
                Apply();
            }
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
            if (width <= 1f || (!ScrollContent && height <= 1f)) return;

            float viewportHeight = _rect.parent is RectTransform viewport
                ? viewport.rect.height
                : height;

            float widthSide = (width - _layout.padding.horizontal - Gap * (columns - 1)) / columns;
            float side;
            if (ScrollContent)
            {
                // 스크롤 목록은 가로폭을 모두 쓴다. 행 수 때문에 초상화를 축소하지 않는다.
                side = Mathf.Max(1f, widthSide);
            }
            else
            {
                side = Mathf.Max(1f, Mathf.Min(widthSide,
                    (height - _layout.padding.vertical - Gap * (rows - 1)) / rows));
            }

            if (ScrollContent)
            {
                float contentHeight = rows * side + Gap * (rows - 1) + _layout.padding.vertical;
                if (!Mathf.Approximately(_rect.sizeDelta.y, contentHeight))
                    _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, contentHeight);
            }

            _layout.constraintCount = columns;
            _layout.spacing = new Vector2(Gap, Gap);
            if (!Mathf.Approximately(_layout.cellSize.x, side))
            {
                _layout.cellSize = new Vector2(side, side);
            }


            _appliedWidth = width;
            _appliedViewportHeight = viewportHeight;
            _appliedCount = Count;
        }
    }
}
