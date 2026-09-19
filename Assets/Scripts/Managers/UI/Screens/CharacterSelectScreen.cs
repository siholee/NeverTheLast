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
    ///   · 커서를 올린 캐릭터의 큰 초상화와 요약을 좌측에 띄운다
    ///
    /// 그림은 <b>초상화(Portrait)만</b> 쓰고 타일·미리보기 모두 정사각형으로 고정한다.
    /// 초상화 원본이 정사각형이므로 칸이 정사각형이면 여백 없이 딱 맞고 비율도 틀어지지 않는다.
    /// 스탠딩은 세로로 길어 같은 칸에 섞으면 비율이 무너지므로 이 화면에서는 쓰지 않는다.
    ///
    /// 모드에 따라 단계가 다르다.
    ///   육성 모드 — 1단계 메인 1명 → [다음] → 2단계 서포터 4명 → [시작]
    ///   무한 모드 — 메인 단계 없이 서포터 5명을 바로 고른다
    ///
    /// 좌측 미리보기는 두 모드를 오간다(버튼 · Q · 패드 Y).
    ///   요약 — 큰 초상화 + 한 줄 소개 · 원소와 스탯 · 운용 축
    ///   기술 — 고유 패시브 · 일반행동 · (특수행동) · 궁극기 설명과 해금 패시브 목록
    /// 한 번 켜면 다른 캐릭터로 커서를 옮겨도 유지된다. 여러 캐릭터의 기술을 견줘 보는 용도다.
    ///
    /// 마우스 없이도 고를 수 있다(<see cref="UINavInput"/>). 커서는 격자와 아래 버튼 줄을 오가며,
    /// 격자 맨 아랫줄에서 아래로 내리면 버튼 줄로 넘어간다. 결정은 Space · Enter · 패드 A,
    /// 메인 다시 고르기는 Backspace · 패드 B, 패드 Start는 [다음]/[여정 시작]이다.
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
        private Button _recommendButton;
        private Button _detailButton;

        private RectTransform _previewSlot;
        private GameObject _detailView;
        private RectTransform _detailContent;
        private TextMeshProUGUI _detailText;
        private bool _detailMode;

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
            UIBuild.Anchor(previewSlot, new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.97f));
            _previewSlot = previewSlot;

            _preview = UIBuild.Solid("PreviewArt", previewSlot, Color.white);
            var previewFitter = _preview.gameObject.AddComponent<AspectRatioFitter>();
            previewFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewFitter.aspectRatio = 1f;
            _preview.preserveAspect = true;
            _preview.enabled = false;

            _previewName = UIBuild.Text("PreviewName", previewPane.transform, "",
                UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Center);
            UIBuild.Anchor(_previewName.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 0.41f));

            // 한 줄 소개 · 원소와 스탯 · 운용 축 · 편성 자격을 차례로 싣는다. 예전에는 원소와 스탯뿐이라
            // "이 캐릭터가 뭘 하는가"를 알 수 없다는 제보가 있었다.
            _previewInfo = UIBuild.Text("PreviewInfo", previewPane.transform, "",
                UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.Top, wrap: true);
            _previewInfo.richText = true;
            UIBuild.Anchor(_previewInfo.rectTransform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.33f));

            // 기술 상세. 코드 설명 네 덩어리는 요약 칸에 들어가지 않으므로 초상화 자리까지 쓰고 굴린다.
            _detailContent = UIBuild.ScrollArea("Detail", previewPane.transform, out ScrollRect detailScroll);
            _detailView = detailScroll.gameObject;
            UIBuild.Anchor((RectTransform)detailScroll.transform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.89f));
            _detailText = UIBuild.Text("DetailText", _detailContent, "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            _detailText.richText = true;
            _detailText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detailText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _detailText.rectTransform.anchoredPosition = Vector2.zero;
            _detailView.SetActive(false);

            _detailButton = UIBuild.Button("DetailToggle", Body, "", ToggleDetail);
            UIBuild.Anchor(_detailButton.image.rectTransform, new Vector2(0f, 0.02f), new Vector2(0.30f, 0.11f), 4f, 0f);

            // 우: 단계 안내 + 후보 격자
            _phaseLabel = UIBuild.Text("Phase", Body, "", UITheme.FontBody, UITheme.Accent);
            UIBuild.Anchor(_phaseLabel.rectTransform, new Vector2(0.32f, 0.93f), new Vector2(1f, 1f));

            _grid = UIBuild.Container("Grid", Body);
            UIBuild.Anchor(_grid, new Vector2(0.32f, 0.26f), new Vector2(1f, 0.92f));

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
            UIBuild.Anchor(_summary.rectTransform, new Vector2(0.32f, 0.12f), new Vector2(1f, 0.25f));

            _backButton = UIBuild.Button("Back", Body, "← 메인 다시 고르기", GoBackToMain);
            UIBuild.Anchor(_backButton.image.rectTransform, new Vector2(0.32f, 0.02f), new Vector2(0.52f, 0.11f));

            _recommendButton = UIBuild.Button("Recommend", Body, "★ 추천 편성", ApplyRecommendation);
            UIBuild.Anchor(_recommendButton.image.rectTransform, new Vector2(0.54f, 0.02f), new Vector2(0.74f, 0.11f));

            _primaryButton = UIBuild.Button("Primary", Body, "다음", OnPrimary, primary: true);
            UIBuild.Anchor(_primaryButton.image.rectTransform, new Vector2(0.76f, 0.02f), new Vector2(1f, 0.11f));

            _cursor = UIBuild.Solid("NavCursor", Body, Color.white);
            _cursor.sprite = UIShapes.CutCorner(8, new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal,
                UITheme.TextPrimary, 3);
            _cursor.type = Image.Type.Sliced;
            _cursor.raycastTarget = false;
            _cursor.gameObject.SetActive(false);
        }

        public override void Show()
        {
            EnsureCharacterSelectionManager();
            CharacterSelectionManager.Instance?.ClearLineup();

            // 무한 모드에는 메인 단계가 없다.
            _phase = IsInfinite ? Phase.Support : Phase.Main;
            _hovered = 0;
            _recommended.Clear();
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
                Image nameStrip = UIBuild.Solid("NameStrip", tile.transform, UITheme.Backdrop);
                UIBuild.Anchor(nameStrip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.20f), 3f, 3f);
                nameStrip.raycastTarget = false;

                TextMeshProUGUI label = UIBuild.Text("Name", tile.transform, IsLavoisierLocked(unit.id) ? "미해금 · 라부아지에" : unit.name,
                    UITheme.FontMicro, UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.20f), 3f, 3f);

                int rank = _recommended.IndexOf(unit.id);
                // 1단계에서는 입문 추천 메인(30_synergies.yaml의 starterRecommended)에 띠를 붙인다.
                bool starterPick = _phase == Phase.Main && !IsInfinite &&
                                   SynergyCatalog.RecommendationFor(unit.id)?.starterRecommended == true;
                if (starterPick)
                {
                    Image badge = UIBuild.Solid("RecommendBadge", tile.transform, UITheme.Accent);
                    UIBuild.Anchor(badge.rectTransform, new Vector2(0f, 0.82f), new Vector2(0.62f, 1f), 3f, 3f);
                    badge.raycastTarget = false;
                    TextMeshProUGUI badgeLabel = UIBuild.Text("RecommendLabel", badge.transform, "★ 입문 추천",
                        UITheme.FontMicro, UITheme.TextOnAccent, TextAlignmentOptions.Center);
                    UIBuild.Stretch(badgeLabel.rectTransform);
                    badgeLabel.raycastTarget = false;
                }
                else if (rank >= 0)
                {
                    Image badge = UIBuild.Solid("RecommendBadge", tile.transform, UITheme.Accent);
                    UIBuild.Anchor(badge.rectTransform, new Vector2(0f, 0.82f), new Vector2(0.62f, 1f), 3f, 3f);
                    badge.raycastTarget = false;
                    TextMeshProUGUI badgeLabel = UIBuild.Text("RecommendLabel", badge.transform, $"★ 추천 {rank + 1}",
                        UITheme.FontMicro, UITheme.TextOnAccent, TextAlignmentOptions.Center);
                    UIBuild.Stretch(badgeLabel.rectTransform);
                    badgeLabel.raycastTarget = false;
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
                _cursorOnButtons = false;
                RefreshPreview();
                PlaceCursor();
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
                if (_tileArts.TryGetValue(pair.Key, out Image tileArt))
                    tileArt.color = IsLavoisierLocked(pair.Key) ? new Color(.35f, .35f, .35f) : Color.white;

                Color fill = isMain ? UITheme.Accent : isSelected ? UITheme.AccentFaint : UITheme.SurfaceRaised;
                Color line = isSelected ? UITheme.Accent : UITheme.Outline;
                pair.Value.sprite = UIShapes.CutCorner(6, fill, UIShapes.Corner.Diagonal, line, isSelected ? 2 : 1);
            }

            string mainText = manager.MainUnitId > 0 ? UnitName(manager.MainUnitId) : "미선택";
            string supportText = supportCount > 0
                ? string.Join(", ", manager.SupportUnitIds.Select(UnitName))
                : "미선택";
            string recommendText = RecommendationSummary();
            _summary.text = IsInfinite
                ? $"서포터  {supportText}"
                : $"메인  {mainText}\n서포터  {supportText}" +
                  (recommendText.Length > 0 ? $"\n{Colored("추천", UITheme.Accent)}  {recommendText}" : "");

            // 버튼 상태
            // 무한 모드는 카드 5장이 모두 차야 시작된다(ConfirmSelection이 그렇게 막는다).
            // 버튼이 먼저 켜지면 눌러도 아무 일이 없어 멈춘 것처럼 보인다.
            bool canProceed = mainPhase ? manager.MainUnitId > 0
                : IsInfinite ? supportCount >= supportTarget : supportCount > 0;
            SetButton(_primaryButton, mainPhase ? "다음 →" : "여정 시작", canProceed);
            SetButtonVisible(_backButton, !IsInfinite && !mainPhase);
            SetButtonVisible(_recommendButton, !mainPhase && _recommended.Count > 0);
            SetButton(_recommendButton, "★ 추천 편성", !IsRecommendationApplied(manager), onAccent: false);

            RefreshPreview();
            PlaceCursor();
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
                _detailText.text = "";
                return;
            }

            if (_detailMode)
            {
                _previewName.text = unit.name;
                ShowDetail(unit);
                return;
            }

            // 스탠딩은 세로로 길어 정사각형 슬롯과 맞지 않는다. 이 화면은 초상화만 쓴다.
            Sprite portrait = LoadPortrait(unit.portrait);
            _preview.enabled = portrait != null;
            if (portrait != null) _preview.sprite = portrait;

            _previewName.text = unit.name;
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

            // 메인 단계에서는 "이 캐릭터로 어떻게 이기는가"를, 서포터 단계에서는 추천 여부를 붙인다.
            SynergyRecommendationData recommendation = SynergyCatalog.RecommendationFor(unit.id);
            if (_phase == Phase.Main && !IsInfinite && !string.IsNullOrWhiteSpace(recommendation?.axis))
                lines.Add($"운용  {recommendation.axis}");
            if (_phase == Phase.Main && !IsInfinite && recommendation?.starterRecommended == true)
                lines.Add(Colored("★ 입문 추천 — 처음이라면 이 캐릭터로 시작해 보세요", UITheme.Accent));
            if (_phase == Phase.Support && _recommended.Contains(unit.id))
                lines.Add(Colored($"★ {UnitName(CharacterSelectionManager.Instance?.MainUnitId ?? 0)}의 추천 서포터", UITheme.Accent));

            lines.Add(Colored(RoleText(unit), UITheme.TextMuted));
            _previewInfo.text = string.Join("\n", lines);
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
            _detailView.SetActive(_detailMode);

            UIBuild.Anchor(_previewName.rectTransform,
                _detailMode ? new Vector2(0f, 0.905f) : new Vector2(0f, 0.34f),
                _detailMode ? new Vector2(1f, 0.985f) : new Vector2(1f, 0.41f));

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
            float width = Mathf.Max(160f, _detailContent.rect.width);
            float height = _detailText.GetPreferredValues(text, width, 0f).y;
            _detailText.rectTransform.sizeDelta = new Vector2(0f, height);
            _detailContent.sizeDelta = new Vector2(0f, height + 12f);
            _detailContent.anchoredPosition = Vector2.zero;
        }

        // ── 키보드 · 패드 ────────────────────────────────────────────

        /// <summary>UIManager가 매 프레임 부른다. 이 화면은 MonoBehaviour가 아니다.</summary>
        public void Tick()
        {
            if (!IsVisible) return;

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
        {
            if (IsLavoisierLocked(unit.id)) return "육성 모드를 처음 클리어하면 해금";
            if (unit.id == Entities.LavoisierChemistry.UnitId) return "해금됨 — 메인·서포터 / 시약 배합형 고급 딜러";
            if (unit.canStartAsMain) return "스타터 — 메인으로 시작할 수 있다";
            if (unit.canStartAsSupport) return "서포트 — 서포터 카드 전용";
            if (CharacterSelectionManager.IsTemporarilyUnlocked(unit)) return "테스트 해금 — 메인으로 시작할 수 있다";
            // 영입 사건이 아직 없는 Locked. 잠가 두면 영영 쓸 수 없어 열어 둔다.
            if (CharacterSelectionManager.HasNoUnlockPath(unit)) return "영입 사건 준비 중 — 지금은 바로 쓸 수 있다";
            return SaveSystem.IsStarterUnlocked(unit.id) ? "해금됨 — 메인으로 쓸 수 있다" : "미해금";
        }

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
            // 로마(100~)·이집트(120~)·메히코(140~)·갈리아(160~)가 통째로 빠져, Starter인 아누비스조차 고를 수 없었다.
            List<UnitData> units = GameManager.Instance?.unitDataList?.units ?? new List<UnitData>();

            if (IsInfinite)
                return units.Where(CharacterSelectionManager.IsInfiniteEligible).ToList();

            // 육성 모드 1단계는 메인이 될 수 있는 캐릭터만, 2단계는 서포터 후보만 보여준다.
            if (_phase == Phase.Main)
            {
                return units
                    // 초기 서포트 카드는 육성이 끝나 해금 기록이 남아도 메인 격자에 오르지 않는다.
                    .Where(unit => !CharacterSelectionManager.IsSupportOnly(unit))
                    .Where(unit => unit.canStartAsMain ||
                                   CharacterSelectionManager.IsTemporarilyUnlocked(unit) ||
                                   CharacterSelectionManager.HasNoUnlockPath(unit) ||
                                   SaveSystem.IsStarterUnlocked(unit.id) || unit.id == Entities.LavoisierChemistry.UnitId)
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

        private static bool IsLavoisierLocked(int id) => id == Entities.LavoisierChemistry.UnitId &&
            !SaveSystem.IsStarterUnlocked(id) && !SaveSystem.IsCharacterTrained(id);

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
