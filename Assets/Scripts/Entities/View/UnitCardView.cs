using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BaseClasses;
using Effects;
using Effects.Vfx;
using Entities.Status;
using Managers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Entities.View
{
    /// <summary>
    /// 전장(과 대기석)의 유닛 한 명을 <b>자유롭게 선 원화 + 가벼운 전투 HUD</b>로 그린다.
    ///
    /// 한 장에 담기는 것:
    ///   · 자유 실루엣 — 카드 프레임 없이 원화와 접지 그림자만 무대 위에 선다
    ///   · 원화 — 칸 안에 들어가는 크기로 가운데 선다(마스크로 자르지 않는다)
    ///   · 전투 HUD(월드 캔버스) — 중성 금속 에너지 오브와 원화 위 한 장의 하단 리퀴드 글라스
    ///
    /// 2026-09 개편 때 하스스톤식 판 · 보석 카드로 바꿨다가 이 디자인으로 되돌렸다(사용자 판단 — 원래 쪽이 낫다).
    /// 대신 <b>움직임은 카드 자체가 한다</b>는 개편은 그대로다.
    ///   · 근접 공격 — 카드가 들려 대상에게 날아가 부딪히고 돌아온다(<see cref="Charge"/>)
    ///   · 원거리 — 카드가 살짝 물러났다 앞으로 튀며 정중앙에서 투사체를 낸다(<see cref="PlayThrow"/>)
    ///   · 피격 — 하얗게 번쩍이며 밀리고, 부딪힌 공격이면 떤다
    ///   · 등장 — 세로 광선 안에서 원화가 복원되고 얇은 파동이 닫힌다(<see cref="PlayEntry"/>)
    ///   · 파괴 — <see cref="CardLifecycleFx"/>가 원화를 떠서 깨뜨린다
    ///
    /// 움직임은 세 층이다. 루트가 <b>월드 이동</b>(돌진), 몸체가 <b>들림 · 기울기 · 찌그러짐</b>,
    /// 그 위에 피격 반응(밀림 · 떨림)이 얹힌다. 그림자는 루트에 남아 들린 높이를 말해 준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitCardView : MonoBehaviour
    {
        /// <summary>칸 한 변 대비 카드 한 변의 비율.</summary>
        public const float CardRatio = 0.96f;

        /// <summary>캔버스 안의 작업 단위. 카드 한 변이 100이 되도록 잡는다.</summary>
        private const float CanvasWidth = 100f;
        private const float CanvasHeight = 134f;

        // HUD 캔버스 좌표(좌상단 기준). 상태가 있을 때만 셋째 행까지 열고, 평소에는 이름·HP·행동선에 맞춰 접는다.
        private const float RingSize = 48f;
        private const float RingEdge = 3.1f;
        private const float OrbCenterX = 75f;
        private const float OrbCenterY = 28f;
        private const float GlassLeft = 2.5f;
        private const float GlassTop = 72.5f;
        private const float GlassWidth = 95f;
        private const float GlassHeight = 27.5f;
        private const float GlassEmptyHeight = 18.7f;
        private const float GlassCenterY = GlassTop + GlassHeight * 0.5f;
        private const float HpTop = 84.4f;
        private const float HpHeight = 1.55f;
        private const float ActionTop = 87.6f;
        private const float ActionHeight = 0.7f;
        private const float DotTop = 89.5f;
        private const float DotSize = 7.5f;
        private const float DotGap = 9.1f;
        private const int MaxDots = 6;
        private const float DotIconRatio = 0.68f;
        private const float DotCountSize = 4.7f;
        private const int MaxElementBadges = 3;
        private const int CompactStatusIconCount = 3;

        /// <summary>원화가 칸 안에서 차지하는 한 변(카드 한 변 대비). 옛 <c>Cell.PortraitFitSize</c>다.</summary>
        private const float PortraitFit = 1.03f;

        private static readonly Color RingWell = new(0.69f, 0.82f, 0.91f, 0.22f);
        private static readonly Color DotBacking = new(0.09f, 0.145f, 0.204f, 0.10f);
        private static readonly Color MarkIdle = new(0.973f, 0.984f, 1f, 0.94f);
        private static readonly Color MarkShadow = new(0.075f, 0.125f, 0.20f, 0.94f);
        private static readonly Color GroundShadow = new(0.255f, 0.36f, 0.45f, 0.43f);
        private const float InactiveAlpha = 0.38f;

        /// <summary>오브가 카드 안에 들어오므로 별도 상단 여백은 없다.</summary>
        public static float HudAbove(float cardSize) => 0f;

        /// <summary>카드 아래로 붙는 HUD(체력 · 행동 · 상태 아이콘)의 높이. 카메라가 이만큼 아래를 비운다.</summary>
        public static float HudBelow(float cardSize) => cardSize * Mathf.Max(0f, GlassTop + GlassHeight - CanvasWidth) / CanvasWidth;

        // ── 반응 시간 ───────────────────────────────────────────────
        private const float FlashTime = 0.14f;
        private const float PunchTime = 0.18f;
        private const float PunchAmount = 0.06f;
        private const float RecoilDistance = 0.075f;     // 카드 한 변 대비
        private const float ShakeTime = 0.22f;
        private const float ShakeDistance = 0.062f;
        private const float ShakeFrequency = 30f;
        private const float LungeTime = 0.26f;
        private const float LungeDistance = 0.10f;
        private const float VfxKickTime = 0.16f;
        private const float VfxKickDistance = 0.040f;
        private const float AirborneLiftHeight = 0.13f;

        // ── 구성 요소 ───────────────────────────────────────────────
        private Unit _unit;
        private bool _combatHud;
        private float _cardSize;
        private int _baseOrder;
        private int _layerId;
        private int _sortBoost;

        private Transform _body;
        private SpriteRenderer _shadow;
        private SpriteRenderer _auraGlow;
        private SpriteRenderer _portrait;
        private TextMeshPro _nameLabel;
        private TextMeshPro _hpLabel;
        private TextMeshPro _resourceBadgeText;
        private TextMeshPro _strongShotText;
        private SpriteRenderer _suryaResourcePlate;
        private Image _actingOutline;

        private Canvas _orbCanvas;
        private CanvasGroup _orbGroup;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _bottomHudRoot;
        private RectTransform _ultRoot;
        private Image _ultRearFill;
        private Image _ultFill;
        private Image _ultFillLight;
        private Image _ultFillShade;
        private Image _ultRearWave;
        private Image _ultWave;
        private Image _ultOutline;
        private Image _ultMarkShadow;
        private Image _ultMark;
        private Image _ultGlass;
        private Image _ultReadyGlow;
        private Image _ultReadyRing;
        private Image _resourceBadge;
        private Image _resourceBadgeDigit;
        private Image[] _treasureBeads = new Image[8];
        private GameObject _bottomGlass;
        private GameObject _bottomGlassShadow;
        private Image _glassTint;
        private Image _hpTrack;
        private Image _hpFill;
        private Image _shieldFill;
        private Image _actionTrack;
        private Image _actionFill;
        private Image[] _dots = new Image[MaxDots];
        private Image[] _dotBacks = new Image[MaxDots];
        private Image[] _dotRims = new Image[MaxDots];
        private Image[] _dotArrows = new Image[MaxDots];
        private Image[] _dotCountBacks = new Image[MaxDots];
        private Image[] _dotCounts = new Image[MaxDots];
        private Image[] _elementBadges = new Image[MaxElementBadges];
        private List<UnitStatus> _dotStatuses = new();
        private List<int> _dotStackCounts = new();
        private RectTransform _supportHudPulseTarget;
        private Image _supportHudPulseImage;
        private Color _supportHudPulseBaseColor;
        private float _supportHudPulseAge = -1f;
        private const float SupportHudPulseDuration = 0.34f;
        private float _resourcePulseAge = -1f;
        private float _resourcePulseStrength;

        public void PulseResourceBadge(float strength = 1f)
        {
            _resourcePulseAge = 0f;
            _resourcePulseStrength = Mathf.Clamp01(strength);
        }

        private string _ringElementName;
        private Color _ringColor = UITheme.Mana;
        private float _ultFillAmount;
        private bool _ultReady;
        private bool _compactHud;
        private bool _statusRowVisible;
        private bool _glassHeightInitialized;
        private bool _hudLodInitialized;
        private int _visibleDotSlots = MaxDots;
        private float _hudPixelScale = 1f;

        private static Sprite _orbGlassSprite;
        private static Sprite _geoGlyphSprite;
        private static Sprite _voidGlyphSprite;
        private static Sprite _neutralGlyphSprite;
        private static readonly Dictionary<Sprite, Sprite> FadedPortraits = new();
        private static readonly Dictionary<Sprite, Sprite> PortraitSources = new();

        // 패널이 시작되는 높이부터 실제 밑변까지 지우고, 하단에서 긴 직선으로 잘린 원화만
        // 좌우 모서리도 함께 녹인다. 짧은 발·꼬리 끝은 절단선으로 오인하지 않는다.
        private const float PortraitFadeFraction = 0.30f;
        private const float PortraitClearFraction = 0.025f;
        private const float PortraitSideFadeFraction = 0.07f;
        private const float PortraitSideRunFraction = 0.028f;
        private const byte PortraitOpaqueThreshold = 16;
        private const byte PortraitBleedThreshold = 224;
        private const int PortraitBleedRadius = 3;
        // 원화는 화면에서 원본보다 2~4배 작게 보인다. 런타임 사본에도 밉 체인을 만들어
        // 축소 시 한 밉의 bilinear 표본만 반복하는 점선 계단 대신 연속 커버리지를 적분한다.
        private const float PortraitMipBias = -0.25f;
        private const byte PortraitDiagnosticLowAlphaThreshold = 48;
        private const int PortraitDiagnosticCoreRadius = 3;

        // ── 움직임 상태 ─────────────────────────────────────────────
        private float _flash;
        private float _punch;
        private float _recoil;
        private float _hitSquash;
        private float _shake;
        private float _shakeStrength;
        private float _shakeSeed;
        private Color _vfxLightColor = Color.white;
        private float _vfxLightPeak;
        private float _vfxLightAge;
        private float _vfxLightDuration;
        private float _lunge;
        private Vector3 _lungeDirection = Vector3.right;
        private float _vfxKick;
        private float _vfxKickStrength;
        private Vector3 _vfxKickDirection = Vector3.right;
        private float _airborneLift;
        private int _facing = 1;
        private int _lastHp = -1;

        /// <summary>돌진 등으로 카드가 떠나 있는 거리(월드).</summary>
        private Vector3 _motionOffset;
        /// <summary>들린 정도 0~1. 크기 · 높이 · 그림자 · 정렬이 함께 움직인다.</summary>
        private float _lift;
        private float _tilt;
        private Vector2 _squash = Vector2.one;
        private float _alpha = 1f;
        private float _entryDelay = -1f;
        private float _entryAge = -1f;
        private float _landingAge = -1f;
        private bool _entryFxStarted;

        private const float EntryDropTime = 0.38f;
        private const float LandingTime = 0.18f;

        private void OnEnable()
        {
            // Play 중 스크립트 재컴파일 뒤에는 런타임 생성 계층은 복원되지만, 새 배열 필드의
            // 원소는 복원되지 않을 수 있다. 완성된 캔버스처럼 보이는 반쪽 HUD를 여기서 바로 수선한다.
            if (_body != null && !HudIsComplete()) RebuildHud();
        }

        /// <summary>돌진처럼 카드가 스스로 움직이는 중인가. 이 동안에는 들린 상태를 반응이 건드리지 않는다.</summary>
        public bool IsPerformingMotion { get; private set; }

        /// <summary>카드 몸체의 월드 중심. 투사체의 출발점과 착탄점이다.</summary>
        public Vector3 CenterWorld => _body != null ? _body.position : transform.position;

        /// <summary>하네스가 카드와 전투 로그를 같은 유닛 ID로 묶을 때 쓴다.</summary>
        public Unit BoundUnit => _unit;

        /// <summary>
        /// 피격 중심 검수용 얼굴~상체 화면 박스. 전신 원화의 하단 28%는 발·패널 접점이므로 제외한다.
        /// Unity 화면 좌표라 원점은 좌하단이며, PNG 분석기는 높이에서 y를 빼서 사용한다.
        /// </summary>
        /// <summary>자세·피격 모션이 바뀌어도 실제 원화 상단 72% 중심에 피격 앵커를 놓는다.</summary>
        public Vector3 PortraitFocusWorldPoint => _portrait != null
            ? _portrait.bounds.center + Vector3.up * (_portrait.bounds.size.y * 0.14f)
            : transform.position;

        public Rect PortraitFocusScreenRect(Camera camera)
        {
            if (camera == null || _portrait == null || !_portrait.enabled) return default;
            Bounds bounds = _portrait.bounds;
            float focusBottom = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.28f);
            Vector3 bottomLeft = camera.WorldToScreenPoint(new Vector3(bounds.min.x, focusBottom, bounds.center.z));
            Vector3 topRight = camera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
            return Rect.MinMaxRect(Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y));
        }

        /// <summary>칸에 카드 뷰를 붙인다(이미 있으면 그대로 쓴다).</summary>
        public static UnitCardView Attach(Transform cell, float cellSize)
        {
            UnitCardView view = cell.GetComponentInChildren<UnitCardView>(true);
            if (view == null)
            {
                var go = new GameObject("UnitCard");
                go.transform.SetParent(cell, false);
                go.transform.localPosition = Vector3.zero;
                view = go.AddComponent<UnitCardView>();
            }

            view.Build(cellSize * CardRatio);
            return view;
        }

        // ══════════════════════════════════════════════════════
        // 조립
        // ══════════════════════════════════════════════════════

        private void Build(float cardSize)
        {
            if (_body != null && _canvas != null)
            {
                if (!HudIsComplete()) RebuildHud();
                Resize(cardSize);
                return;
            }

            _cardSize = cardSize;

            // 발밑 그림자 — 바닥(루트)에 남아 들린 높이를 말한다.
            _shadow = VfxSpace.NewSprite(transform, "Shadow", VfxSpace.SoftDot, GroundShadow, 0, 0);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // 상태 오라도 카드 크기의 면을 만들지 않는다. 검증된 원형 텍스처를 발밑에 눕혀 짧은 표식으로만 쓴다.
            _auraGlow = VfxSpace.NewSprite(_body, "AuraGlow", VfxSpace.SoftDot, Color.clear, 0, 0);

            _nameLabel = NewLabel("Name", FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _nameLabel.color = new Color(0.973f, 0.984f, 1f, 1f);
            _nameLabel.outlineColor = new Color(0.075f, 0.125f, 0.20f, 0.96f);
            _nameLabel.outlineWidth = 0.16f;
            _nameLabel.overflowMode = TextOverflowModes.Truncate;

            // 월드 캔버스의 TextMeshProUGUI는 이 카드에서 렌더되지 않아 체력 수도 월드 TMP로 둔다.
            // 어두운 유리판 위에서 한눈에 읽히도록 밝은 숫자에 짙은 외곽을 둔다.
            _hpLabel = NewLabel("HpValue", FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            _hpLabel.color = new Color(0.96f, 0.99f, 1f, 1f);
            _hpLabel.outlineColor = new Color(0.075f, 0.125f, 0.20f, 0.98f);
            _hpLabel.outlineWidth = 0.16f;
            _hpLabel.overflowMode = TextOverflowModes.Truncate;

            EnsureResourceLabels();

            BuildHud();
            LayOut(cardSize);
            Show(false);
        }

        private void EnsureResourceLabels()
        {
            if (_resourceBadgeText == null)
            {
                _resourceBadgeText = NewLabel("ResourceValue", FontStyles.Bold, TextAlignmentOptions.Center);
                _resourceBadgeText.color = Color.white;
                _resourceBadgeText.outlineColor = new Color(0.075f, 0.125f, 0.20f, 0.98f);
                _resourceBadgeText.outlineWidth = 0.18f;
                _resourceBadgeText.enabled = false;
            }
            if (_strongShotText == null)
            {
                _strongShotText = NewLabel("StrongShot", FontStyles.Bold, TextAlignmentOptions.Center);
                _strongShotText.text = "강탄";
                _strongShotText.color = new Color(1f, 0.85f, 0.57f);
                _strongShotText.outlineColor = new Color(0.075f, 0.125f, 0.20f, 0.98f);
                _strongShotText.outlineWidth = 0.18f;
                _strongShotText.enabled = false;
            }
            if (_suryaResourcePlate == null)
            {
                _suryaResourcePlate = VfxSpace.NewSprite(_body, "SuryaResourcePlate",
                    UIShapes.RoundedRect(6, Color.white, new Color(0.75f, 0.86f, 0.93f, 0.7f), 1),
                    new Color(0.12f, 0.19f, 0.25f, 0.88f), 0, 0);
                _suryaResourcePlate.enabled = false;
            }
        }

        private TextMeshPro NewLabel(string name, FontStyles style, TextAlignmentOptions alignment)
        {
            // 캔버스 밖의 월드 TextMeshPro다. 코드로 붙인 월드 캔버스 위에서는 TextMeshProUGUI가
            // 그려지지 않았다 — MeshRenderer로 그리면 정렬도 카드 렌더러와 같은 축에서 다룬다.
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            go.transform.SetParent(_body, false);
            var label = go.GetComponent<TextMeshPro>();
            UIBuild.ApplyFont(label);
            label.color = Color.white;
            label.alignment = alignment;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.enableAutoSizing = false;
            return label;
        }

        private void BuildHud()
        {
            var orbCanvasObject = new GameObject("OrbHud", typeof(RectTransform), typeof(Canvas));
            orbCanvasObject.transform.SetParent(_body, false);
            _orbCanvas = orbCanvasObject.GetComponent<Canvas>();
            _orbCanvas.renderMode = RenderMode.WorldSpace;
            _orbCanvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
            var orbCanvasRect = (RectTransform)orbCanvasObject.transform;
            orbCanvasRect.pivot = new Vector2(0.5f, 1f);
            orbCanvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _orbGroup = UIBuild.Group(orbCanvasObject);
            _orbGroup.blocksRaycasts = false;

            var canvasObject = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(_body, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            // TextMeshPro SDF가 쓰는 UV2 · 법선 채널을 코드로 붙인 캔버스는 버린다. 켜 둔다.
            _canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.pivot = new Vector2(0.5f, 1f);
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _group = UIBuild.Group(canvasObject);
            _group.blocksRaycasts = false;
            Transform root = canvasObject.transform;

            // 하단 정보판만 카드 슬롯 폭에 맞춰 줄일 수 있게 별도 묶음으로 둔다. 오브와 원소 표식은
            // 원화 크기를 따라가되, 이름·HP·상태는 큰 카드끼리 붙어도 이 묶음 밖으로 번지지 않는다.
            _bottomHudRoot = UIBuild.Container("BottomHud", root);
            _bottomHudRoot.anchorMin = new Vector2(0f, 1f);
            _bottomHudRoot.anchorMax = new Vector2(0f, 1f);
            _bottomHudRoot.pivot = new Vector2(0f, 1f);
            _bottomHudRoot.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _bottomHudRoot.anchoredPosition = Vector2.zero;

            // 지금 행동 중인 유닛은 정보판을 하나 더 만들지 않고 같은 글라스의 바깥 림만 밝힌다.
            _actingOutline = NewImage(_bottomHudRoot, "ActingOutline", Color.white);
            _actingOutline.sprite = UIShapes.RoundedRect(8, Color.clear, new Color(0.82f, 0.66f, 0.35f, 0.78f), 2);
            _actingOutline.type = Image.Type.Sliced;
            Place(_actingOutline.rectTransform, GlassLeft - 0.8f, GlassTop - 0.8f, GlassWidth + 1.6f, GlassHeight + 1.6f);
            _actingOutline.enabled = false;

            // ── 부착 원소 표식(좌상단) ──
            for (int i = 0; i < MaxElementBadges; i++)
            {
                Image badge = NewImage(root, $"Element{i}", Color.white);
                badge.sprite = UIShapes.Disc(64, Color.white);
                Place(badge.rectTransform, 3f + i * 9f, 3f, 8f, 8f);
                badge.enabled = false;
                _elementBadges[i] = badge;
            }

            // ── 궁극기 오브(원화 우상단 뒤에서 원화와 겹친다) ──
            // 액체는 원형 마스크 안에서 아래→위로 차고, 생성 에셋은 광택만 맡아 원소색을 가리지 않는다.
            // 하단 HUD와 캔버스를 나눠야 SpriteRenderer 원화가 오브 앞, 글자는 원화 앞에 놓일 수 있다.
            _ultRoot = UIBuild.Container("UltGauge", orbCanvasObject.transform);
            _ultRoot.anchorMin = new Vector2(0f, 1f);
            _ultRoot.anchorMax = new Vector2(0f, 1f);
            _ultRoot.pivot = new Vector2(0.5f, 0.5f);
            _ultRoot.sizeDelta = new Vector2(RingSize, RingSize);
            _ultRoot.anchoredPosition = new Vector2(OrbCenterX, -OrbCenterY);
            _ultReadyGlow = NewImage(_ultRoot, "ReadyGlow", Color.clear);
            _ultReadyGlow.sprite = UIShapes.Disc(96, Color.white);
            // 바깥으로 번진 원판의 아래쪽 호만 금색 막대처럼 보였으므로 준비광도 오브 안에서 끝낸다.
            UIBuild.Stretch(_ultReadyGlow.rectTransform, 0.55f, 0.55f);

            Image well = NewImage(_ultRoot, "Well", RingWell);
            well.sprite = UIShapes.Disc(96, Color.white);
            UIBuild.Stretch(well.rectTransform, RingEdge, RingEdge);

            RectTransform liquidMask = UIBuild.Container("LiquidMask", _ultRoot);
            // 낮은 마나 수위가 크롬 하단에 붙으면 오브 밖의 막대처럼 읽힌다. 액체를 웰 안쪽에
            // 한 번 더 물려 10~20% 수위도 독립된 초승달로 보이게 한다.
            const float liquidInset = RingEdge + 2.4f;
            UIBuild.Stretch(liquidMask, liquidInset, liquidInset);
            Image maskGraphic = NewImage(liquidMask, "Mask", Color.white);
            maskGraphic.sprite = UIShapes.Disc(96, Color.white);
            var mask = maskGraphic.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // 뒤쪽 액체는 실제 수위보다 아주 조금 늦거나 앞서며 좌우로 흔들린다. 앞층과 수면이
            // 겹쳐질 때 한 장짜리 세로 채움이 아니라 오브 안쪽에 깊이가 있는 것처럼 읽힌다.
            _ultRearFill = NewImage(liquidMask, "LiquidRear", UITheme.Mana);
            _ultRearFill.type = Image.Type.Filled;
            _ultRearFill.fillMethod = Image.FillMethod.Vertical;
            _ultRearFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            UIBuild.Stretch(_ultRearFill.rectTransform, -0.7f, -0.2f);

            _ultRearWave = NewImage(liquidMask, "LiquidRearSurface", new Color(1f, 1f, 1f, 0.36f));
            _ultRearWave.sprite = UIShapes.Disc(64, Color.white);
            _ultRearWave.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _ultRearWave.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _ultRearWave.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _ultRearWave.rectTransform.sizeDelta = new Vector2(RingSize - liquidInset * 2f + 0.4f, 2.2f);

            _ultFill = NewImage(liquidMask, "LiquidFront", UITheme.Mana);
            _ultFill.type = Image.Type.Filled;
            _ultFill.fillMethod = Image.FillMethod.Vertical;
            _ultFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            UIBuild.Stretch(_ultFill.rectTransform);

            _ultFillShade = NewImage(liquidMask, "LiquidDepth", Color.white);
            _ultFillShade.sprite = UIShapes.VerticalGradient(64,
                new Color(0.03f, 0.08f, 0.12f, 0.01f), new Color(0.02f, 0.06f, 0.10f, 0.30f));
            _ultFillShade.type = Image.Type.Filled;
            _ultFillShade.fillMethod = Image.FillMethod.Vertical;
            _ultFillShade.fillOrigin = (int)Image.OriginVertical.Bottom;
            UIBuild.Stretch(_ultFillShade.rectTransform);

            _ultFillLight = NewImage(liquidMask, "LiquidGradient", Color.white);
            _ultFillLight.sprite = UIShapes.VerticalGradient(64,
                new Color(1f, 1f, 1f, 0.72f), new Color(1f, 1f, 1f, 0.05f));
            _ultFillLight.type = Image.Type.Filled;
            _ultFillLight.fillMethod = Image.FillMethod.Vertical;
            _ultFillLight.fillOrigin = (int)Image.OriginVertical.Bottom;
            UIBuild.Stretch(_ultFillLight.rectTransform);

            _ultWave = NewImage(liquidMask, "LiquidSurface", new Color(1f, 1f, 1f, 0.82f));
            _ultWave.sprite = UIShapes.Disc(64, Color.white);
            _ultWave.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _ultWave.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _ultWave.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _ultWave.rectTransform.sizeDelta = new Vector2(RingSize - liquidInset * 2f, 1.7f);

            // 밝은 액체 위에서도 흰 글리프가 사라지지 않도록 같은 실루엣을 한 겹 크게 깐다.
            // 외곽선 셰이더 대신 스프라이트 사본을 써 생성 글리프의 가장자리를 그대로 보존한다.
            _ultMarkShadow = NewImage(_ultRoot, "MarkOutline", MarkShadow);
            _ultMarkShadow.sprite = StatusIcons.Get(StatusGlyph.Focus);
            _ultMarkShadow.preserveAspect = true;
            UIBuild.Stretch(_ultMarkShadow.rectTransform, RingEdge * 3.15f, RingEdge * 3.15f);
            _ultMarkShadow.rectTransform.anchoredPosition = new Vector2(0.18f, -0.22f);

            _ultMark = NewImage(_ultRoot, "Mark", Color.white);
            _ultMark.sprite = StatusIcons.Get(StatusGlyph.Focus);
            _ultMark.preserveAspect = true;
            UIBuild.Stretch(_ultMark.rectTransform, RingEdge * 3.5f, RingEdge * 3.5f);

            _ultGlass = NewImage(_ultRoot, "Glass", new Color(1f, 1f, 1f, 0.82f));
            _ultGlass.sprite = HudSprite("energy_orb_glass_bright", UIShapes.Disc(96, Color.white, 0.76f));
            _ultGlass.preserveAspect = true;
            UIBuild.Stretch(_ultGlass.rectTransform, -0.2f, -0.2f);

            _ultOutline = NewImage(_ultRoot, "Outline", new Color(0.78f, 0.89f, 0.96f, 0.98f));
            _ultOutline.sprite = UIShapes.Disc(96, Color.white, 0.84f);
            UIBuild.Stretch(_ultOutline.rectTransform);

            _ultReadyRing = NewImage(_ultRoot, "ReadyRing", Color.clear);
            _ultReadyRing.sprite = UIShapes.Disc(96, Color.white, 0.88f);
            _ultReadyRing.preserveAspect = true;
            UIBuild.Stretch(_ultReadyRing.rectTransform, 0.85f, 0.85f);

            // 고유 자원은 별도 원 배지를 달지 않고 오브 윗링에 작은 숫자 각인으로 붙인다.
            _resourceBadge = NewImage(_ultRoot, "ResourceBadge", new Color(0.075f, 0.125f, 0.20f, 0.82f));
            _resourceBadge.sprite = UIShapes.RoundedRect(5, Color.white,
                new Color(0.91f, 0.97f, 1f, 0.74f), 1);
            _resourceBadge.type = Image.Type.Sliced;
            _resourceBadge.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _resourceBadge.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _resourceBadge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _resourceBadge.rectTransform.sizeDelta = new Vector2(9.2f, 7.2f);
            _resourceBadge.rectTransform.anchoredPosition = new Vector2(0f, -2.1f);
            _resourceBadgeDigit = NewImage(_resourceBadge.transform, "Value", Color.white);
            _resourceBadgeDigit.preserveAspect = true;
            UIBuild.Stretch(_resourceBadgeDigit.rectTransform, 2.6f, 1.45f);
            _resourceBadge.enabled = false;
            _resourceBadgeDigit.enabled = false;

            // 보물은 마나로 저장되는 기존 자원이다. 표시만 8칸 구슬로 바꾸고 0에서도 빈 칸을 남긴다.
            for (int i = 0; i < _treasureBeads.Length; i++)
            {
                Image bead = NewImage(_ultRoot, $"TreasureBead{i}", Color.clear);
                bead.sprite = UIShapes.Disc(32, Color.white, 0.82f);
                bead.rectTransform.anchorMin = bead.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                float angle = (-90f + i * 45f) * Mathf.Deg2Rad;
                bead.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 14.2f;
                bead.rectTransform.sizeDelta = new Vector2(3.8f, 3.8f);
                bead.enabled = false;
                _treasureBeads[i] = bead;
            }

            // ── 하단 A안 리퀴드 글라스 ──
            // 원화 사본을 패널 안에 다시 그리면 밝은 전장에서도 다리·옷자락이 검은 얼룩으로 남는다.
            // 배경과 무관하게 일정한 스모크 한 장만 써 정보 대비와 원화 실루엣을 함께 보존한다.
            Image glassDrop = NewImage(_bottomHudRoot, "BottomGlassShadow", new Color(0.075f, 0.125f, 0.20f, 0.20f));
            glassDrop.sprite = UIShapes.SoftShadow(10, 8, 0.18f);
            glassDrop.type = Image.Type.Sliced;
            Place(glassDrop.rectTransform, GlassLeft, GlassTop + 0.8f, GlassWidth, GlassEmptyHeight);
            _bottomGlassShadow = glassDrop.gameObject;

            Image glassBase = NewImage(_bottomHudRoot, "BottomGlass", Color.white);
            glassBase.sprite = UIShapes.RoundedRect(10, Color.clear);
            glassBase.type = Image.Type.Sliced;
            Place(glassBase.rectTransform, GlassLeft, GlassTop, GlassWidth, GlassEmptyHeight);
            _bottomGlass = glassBase.gameObject;

            // 밝은 전장에서도 양 진영을 같은 대비로 묶는다. 흰 원화와 백색 결정 모두에서
            // 읽혀야 하므로 아군/적 색 분기 대신 안 2의 공통 스모크 면을 쓴다.
            _glassTint = NewImage(glassBase.transform, "Tint", new Color(0.149f, 0.22f, 0.29f, 0.72f));
            _glassTint.sprite = UIShapes.RoundedRect(10, Color.white);
            _glassTint.type = Image.Type.Sliced;
            UIBuild.Stretch(_glassTint.rectTransform);

            Image glassShade = NewImage(glassBase.transform, "InnerShade", Color.white);
            glassShade.sprite = UIShapes.VerticalGradient(32,
                new Color(0.48f, 0.72f, 0.82f, 0.01f), new Color(0.075f, 0.125f, 0.20f, 0.10f));
            UIBuild.Stretch(glassShade.rectTransform, 0.8f, 0.8f);

            Image specular = NewImage(glassBase.transform, "UpperSpecular", new Color(1f, 1f, 1f, 0.28f));
            specular.sprite = UIShapes.RoundedRect(2, Color.white);
            specular.type = Image.Type.Sliced;
            Place(specular.rectTransform, 13f, 1.15f, GlassWidth - 26f, 0.45f);

            Image glassRim = NewImage(glassBase.transform, "BrightRim", Color.white);
            glassRim.sprite = UIShapes.RoundedRect(10, Color.clear, new Color(0.97f, 0.985f, 1f, 0.72f), 1);
            glassRim.type = Image.Type.Sliced;
            UIBuild.Stretch(glassRim.rectTransform);

            // 체력 + 보호막. 보호막은 스타레일처럼 체력 트랙 윗면에 얇은 백색 막으로 겹친다.
            _hpTrack = NewImage(glassBase.transform, "HpTrack", Color.white);
            _hpTrack.sprite = UIShapes.RoundedRect(3, new Color(0.01f, 0.045f, 0.052f, 0.90f));
            _hpTrack.type = Image.Type.Sliced;
            Place(_hpTrack.rectTransform, 6f, HpTop - GlassTop, GlassWidth - 12f, HpHeight);
            _hpFill = NewImage(_hpTrack.transform, "HpFill", UITheme.Hp);
            _shieldFill = NewImage(_hpTrack.transform, "ShieldFill", new Color(0.894f, 0.745f, 0.365f, 0.98f));
            Image sheen = UIBuild.Solid("Sheen", _hpTrack.transform, new Color(1f, 1f, 1f, 0.22f));
            sheen.raycastTarget = false;
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            sheen.rectTransform.anchorMax = Vector2.one;
            sheen.rectTransform.offsetMin = Vector2.zero;
            sheen.rectTransform.offsetMax = Vector2.zero;

            // ── 행동 게이지 ──
            _actionTrack = UIBuild.Solid("ActionTrack", glassBase.transform, new Color(0.01f, 0.04f, 0.05f, 0.52f));
            Place(_actionTrack.rectTransform, 6f, ActionTop - GlassTop, GlassWidth - 12f, ActionHeight);
            _actionFill = NewImage(_actionTrack.transform, "ActionFill", new Color(0.77f, 0.84f, 0.89f, 0.94f));

            // ── 상태 아이콘 ──
            for (int i = 0; i < MaxDots; i++)
            {
                Image back = NewImage(glassBase.transform, $"StatusBack{i}", DotBacking);
                back.sprite = StatusIcons.Frame(null, false);
                Place(back.rectTransform, 7f + i * DotGap, DotTop - GlassTop, DotSize, DotSize);
                back.enabled = false;
                _dotBacks[i] = back;

                Image rim = NewImage(back.transform, $"StatusRim{i}", new Color(0.90f, 0.98f, 1f, 0.42f));
                rim.sprite = StatusIcons.Frame(null, true);
                UIBuild.Stretch(rim.rectTransform, -0.15f, -0.15f);
                rim.enabled = false;
                _dotRims[i] = rim;

                Image dot = NewImage(back.transform, $"Status{i}", new Color(0.97f, 0.985f, 1f, 1f));
                dot.preserveAspect = true;
                float inset = DotSize * (1f - DotIconRatio) * 0.5f;
                UIBuild.Stretch(dot.rectTransform, inset, inset);
                dot.enabled = false;
                _dots[i] = dot;

                Image arrow = NewImage(back.transform, $"StatusArrow{i}", Color.white);
                arrow.preserveAspect = true;
                arrow.rectTransform.anchorMin = new Vector2(1f, 1f);
                arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
                arrow.rectTransform.pivot = new Vector2(1f, 1f);
                arrow.rectTransform.sizeDelta = new Vector2(DotSize * 0.43f, DotSize * 0.43f);
                arrow.rectTransform.anchoredPosition = new Vector2(DotSize * 0.08f, DotSize * 0.08f);
                arrow.enabled = false;
                _dotArrows[i] = arrow;

                Image countBack = NewImage(back.transform, $"StatusCountBack{i}", new Color(0.075f, 0.125f, 0.20f, 0.86f));
                countBack.sprite = UIShapes.Disc(48, Color.white);
                countBack.rectTransform.anchorMin = new Vector2(1f, 0f);
                countBack.rectTransform.anchorMax = new Vector2(1f, 0f);
                countBack.rectTransform.pivot = new Vector2(1f, 0f);
                countBack.rectTransform.sizeDelta = new Vector2(DotCountSize, DotCountSize);
                countBack.rectTransform.anchoredPosition = new Vector2(DotCountSize * 0.24f, -DotCountSize * 0.18f);
                countBack.enabled = false;
                _dotCountBacks[i] = countBack;

                Image count = NewImage(countBack.transform, $"StatusCount{i}", Color.white);
                count.preserveAspect = true;
                UIBuild.Stretch(count.rectTransform, DotCountSize * 0.22f, DotCountSize * 0.14f);
                count.enabled = false;
                _dotCounts[i] = count;
            }
            ApplyGlassHeight(false);
            ApplyHudLod(false, true);

        }

        /// <summary>
        /// 코드로 만든 HUD의 필수 참조가 모두 살아 있는지 확인한다.
        /// <para>
        /// Unity 도메인 리로드는 기존 GameObject와 일부 컴포넌트 참조를 복원하지만, 리로드 사이에
        /// 새로 생긴 배열 칸까지 채워 주지는 않는다. 그래서 <c>_canvas != null</c>만으로 조립 완료를
        /// 판단하면 비어 있는 원소 표식·상태 아이콘 배열을 매 프레임 역참조하게 된다.
        /// </para>
        /// </summary>
        private bool HudIsComplete()
        {
            return _orbCanvas != null && _orbGroup != null && _canvas != null && _group != null &&
                   _bottomHudRoot != null && _actingOutline != null &&
                   _ultRoot != null && _ultRearFill != null && _ultFill != null && _ultFillLight != null &&
                   _ultFillShade != null && _ultRearWave != null && _ultWave != null && _ultOutline != null &&
                   _ultMarkShadow != null && _ultMark != null && _ultGlass != null && _ultReadyGlow != null &&
                   _ultReadyRing != null && _resourceBadge != null && _resourceBadgeDigit != null &&
                   _resourceBadgeText != null && _strongShotText != null && _suryaResourcePlate != null &&
                    _bottomGlass != null && _bottomGlassShadow != null &&
                   _glassTint != null && _hpTrack != null && _hpFill != null && _shieldFill != null &&
                   _actionTrack != null && _actionFill != null &&
                   AllAlive(_dots, MaxDots) && AllAlive(_dotBacks, MaxDots) &&
                   AllAlive(_dotRims, MaxDots) && AllAlive(_dotArrows, MaxDots) &&
                   AllAlive(_dotCountBacks, MaxDots) && AllAlive(_dotCounts, MaxDots) &&
                   AllAlive(_elementBadges, MaxElementBadges);
        }

        private static bool AllAlive(Image[] images, int expectedLength)
        {
            if (images == null || images.Length != expectedLength) return false;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) return false;
            }
            return true;
        }

        private bool EnsureHudReady()
        {
            if (HudIsComplete()) return true;
            if (_body == null) return false;
            RebuildHud();
            return HudIsComplete();
        }

        /// <summary>몸체·원화·모션은 보존하고 불완전한 런타임 HUD 구획만 새로 조립한다.</summary>
        private void RebuildHud()
        {
            if (_body == null) return;

            bool compact = _compactHud;
            float pixelScale = _hudPixelScale;
            if (_canvas != null)
            {
                // Destroy는 프레임 끝에 실행되므로 낡은 캔버스가 한 프레임 겹치지 않게 먼저 숨긴다.
                _canvas.gameObject.SetActive(false);
                Destroy(_canvas.gameObject);
            }
            if (_orbCanvas != null)
            {
                _orbCanvas.gameObject.SetActive(false);
                Destroy(_orbCanvas.gameObject);
            }

            ResetHudReferences();
            EnsureResourceLabels();
            BuildHud();
            _hudPixelScale = pixelScale > 0f ? pixelScale : 1f;
            ApplyHudLod(compact, true);
            if (_cardSize > 0f) LayOut(_cardSize);
            ApplySorting();
            ApplyHudVisibility();
        }

        private void ResetHudReferences()
        {
            _orbCanvas = null;
            _orbGroup = null;
            _canvas = null;
            _group = null;
            _bottomHudRoot = null;
            _actingOutline = null;
            _ultRoot = null;
            _ultRearFill = null;
            _ultFill = null;
            _ultFillLight = null;
            _ultFillShade = null;
            _ultRearWave = null;
            _ultWave = null;
            _ultOutline = null;
            _ultMarkShadow = null;
            _ultMark = null;
            _ultGlass = null;
            _ultReadyGlow = null;
            _ultReadyRing = null;
            _resourceBadge = null;
            _resourceBadgeDigit = null;
            _treasureBeads = new Image[8];
            _bottomGlass = null;
            _bottomGlassShadow = null;
            _glassTint = null;
            _hpTrack = null;
            _hpFill = null;
            _shieldFill = null;
            _actionTrack = null;
            _actionFill = null;
            _glassHeightInitialized = false;
            _dots = new Image[MaxDots];
            _dotBacks = new Image[MaxDots];
            _dotRims = new Image[MaxDots];
            _dotArrows = new Image[MaxDots];
            _dotCountBacks = new Image[MaxDots];
            _dotCounts = new Image[MaxDots];
            _elementBadges = new Image[MaxElementBadges];
            _dotStatuses ??= new List<UnitStatus>();
            _dotStackCounts ??= new List<int>();
        }

        /// <summary>칸 크기가 바뀌었을 때(보스 단독 배치 등) 카드 치수를 다시 잡는다.</summary>
        public void Resize(float cardSize)
        {
            if (_body == null || Mathf.Approximately(_cardSize, cardSize)) return;
            _cardSize = cardSize;
            LayOut(cardSize);
            if (_portrait != null) LayOutPortrait();
        }

        private void LayOut(float s)
        {
            _auraGlow.transform.localPosition = new Vector3(0f, -s * 0.405f, 0f);
            _auraGlow.transform.localScale = new Vector3(s * 1.12f, s * 0.2f, 1f);
            _shadow.transform.localPosition = new Vector3(0f, -s * 0.43f, 0f);

            // 이름과 HP를 같은 첫 행에 놓는다. 이름 폭은 48%를 넘기지 않아 5자리 HP와 충돌하지 않는다.
            LayOutNameLabel(s, _compactHud);
            LayOutHpLabel(s, _compactHud);
            LayOutResourceBadgeText(s);
            ApplyBottomHudScale(s);

            var canvasRect = (RectTransform)_canvas.transform;
            canvasRect.localScale = Vector3.one * (s / CanvasWidth);
            canvasRect.localPosition = new Vector3(0f, s * 0.5f, -0.01f);
            var orbCanvasRect = (RectTransform)_orbCanvas.transform;
            orbCanvasRect.localScale = Vector3.one * (s / CanvasWidth);
            orbCanvasRect.localPosition = new Vector3(0f, s * 0.5f, -0.005f);
        }

        private void LayOutNameLabel(float s, bool compact)
        {
            float hudSize = BottomHudSize(s);
            float y = s * (0.5f - GlassCenterY / CanvasWidth) +
                      hudSize * (GlassCenterY - (GlassTop + 5.5f)) / CanvasWidth;
            float pixelScale = compact ? _hudPixelScale : 1f;
            PlaceLabel(_nameLabel, new Vector2(-hudSize * 0.225f, y),
                new Vector2(hudSize * 0.41f, hudSize * (compact ? 0.074f : 0.068f) * pixelScale),
                hudSize * (compact ? 0.61f : 0.56f) * pixelScale);
            _nameLabel.enableAutoSizing = true;
            _nameLabel.fontSizeMin = hudSize * (compact ? 0.46f : 0.36f) * pixelScale;
            _nameLabel.fontSizeMax = hudSize * (compact ? 0.61f : 0.56f) * pixelScale;
            _nameLabel.outlineWidth = compact ? 0.18f : 0.13f;
            _nameLabel.enabled = true;
        }

        private void LayOutHpLabel(float s, bool compact)
        {
            float hudSize = BottomHudSize(s);
            float y = s * (0.5f - GlassCenterY / CanvasWidth) +
                      hudSize * (GlassCenterY - (GlassTop + 5.5f)) / CanvasWidth;
            float pixelScale = compact ? _hudPixelScale : 1f;
            PlaceLabel(_hpLabel, new Vector2(hudSize * 0.245f, y),
                new Vector2(hudSize * 0.43f, hudSize * (compact ? 0.074f : 0.067f) * pixelScale),
                hudSize * (compact ? 0.60f : 0.52f) * pixelScale);
            _hpLabel.enableAutoSizing = !compact;
            _hpLabel.fontSizeMin = hudSize * (compact ? 0.60f : 0.36f) * pixelScale;
            _hpLabel.fontSizeMax = hudSize * (compact ? 0.60f : 0.52f) * pixelScale;
            _hpLabel.outlineWidth = compact ? 0.17f : 0.12f;
        }

        private static void PlaceLabel(TextMeshPro label, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = (RectTransform)label.transform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localPosition = new Vector3(position.x, position.y, 0f);
            label.fontSize = fontSize;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable]
        public sealed class ResourceHudDiagnostic
        {
            public int unitId, ammo, maximum, strong;
            public bool combatHud, bodyActive, ammoEnabled, strongEnabled, ammoMeshVisible, strongMeshVisible;
            public string unitName, nameText, ammoText, strongText, font, nameFont, strongFont, sorting;
            public int ammoCharacters, strongCharacters, nameCharacters;
            public float ammoFontSize, strongFontSize, ammoScreenHeight, strongScreenHeight;
        }

        /// <summary>게임 자원과 실제 월드 TMP 메시를 함께 기록하는 검수 전용 관측. 상태를 덮어쓰지 않는다.</summary>
        public ResourceHudDiagnostic VerificationResourceHud(bool forceMesh = false)
        {
            if (forceMesh)
            {
                _nameLabel.ForceMeshUpdate();
                _resourceBadgeText.ForceMeshUpdate();
                _strongShotText.ForceMeshUpdate();
            }
            float Height(TextMeshPro text)
            {
                Bounds bounds = text.GetComponent<MeshRenderer>().bounds;
                Camera camera = Camera.main;
                return camera == null ? 0f : Mathf.Abs(camera.WorldToScreenPoint(bounds.max).y - camera.WorldToScreenPoint(bounds.min).y);
            }
            bool Visible(TextMeshPro text) => text.enabled && text.gameObject.activeInHierarchy &&
                text.textInfo.characterCount > 0 && Height(text) >= 5f;
            return new ResourceHudDiagnostic
            {
                unitId = _unit.ID, unitName = _unit.UnitName, nameText = _nameLabel.text, nameFont = _nameLabel.font?.name, nameCharacters = _nameLabel.textInfo.characterCount,
                ammo = _unit.GetCombatResource(Codes.Passive.SuryaBeforeSunset.BulletResourceId),
                maximum = _unit.GetCombatResourceMaximum(Codes.Passive.SuryaBeforeSunset.BulletResourceId),
                strong = _unit.GetCombatResource(Codes.Passive.SuryaBeforeSunset.EmpoweredResourceId),
                combatHud = _combatHud, bodyActive = _body.gameObject.activeInHierarchy,
                ammoEnabled = _resourceBadgeText.enabled, strongEnabled = _strongShotText.enabled,
                ammoMeshVisible = Visible(_resourceBadgeText), strongMeshVisible = Visible(_strongShotText),
                ammoText = _resourceBadgeText.text, strongText = _strongShotText.text,
                ammoCharacters = _resourceBadgeText.textInfo.characterCount, strongCharacters = _strongShotText.textInfo.characterCount,
                ammoFontSize = _resourceBadgeText.fontSize, strongFontSize = _strongShotText.fontSize,
                ammoScreenHeight = Height(_resourceBadgeText), strongScreenHeight = Height(_strongShotText),
                font = _resourceBadgeText.font?.name, strongFont = _strongShotText.font?.name,
                sorting = _resourceBadgeText.GetComponent<MeshRenderer>().sortingLayerName + ":" + _resourceBadgeText.GetComponent<MeshRenderer>().sortingOrder,
            };
        }

        /// <summary>같은 실제 자원의 기존 위치와 수정 위치를 비교하는 검수 전용. 일반 렌더는 수정 위치를 따른다.</summary>
        public void VerificationLegacyResourcePlacement(bool legacy)
        {
            LayOutResourceBadgeText(_cardSize);
            if (!legacy) return;
            float s = _cardSize;
            PlaceLabel(_resourceBadgeText, new Vector2(s * 0.25f, s * 0.439f), new Vector2(s * 0.30f, s * 0.075f), s * 0.43f);
            PlaceLabel(_strongShotText, new Vector2(s * 0.34f, s * 0.35f), new Vector2(s * 0.22f, s * 0.07f), s * 0.20f);
            _suryaResourcePlate.enabled = false;
        }
#endif

        private void LayOutResourceBadgeText(float s)
        {
            if (_resourceBadgeText == null) return;
            if (_unit?.ID == 67)
            {
                // 잔탄과 강탄은 원화 뒤의 오브가 아닌 하단 유리판 바로 위의 한 줄에서 읽는다.
                float hud = BottomHudSize(s);
                float pixel = _compactHud ? _hudPixelScale : 1f;
                float y = s * (0.5f - GlassCenterY / CanvasWidth) +
                    hud * (GlassCenterY - (GlassTop - 6.5f)) / CanvasWidth;
                PlaceLabel(_resourceBadgeText, new Vector2(-hud * 0.17f, y),
                    new Vector2(hud * 0.42f, hud * 0.11f * pixel), hud * 0.68f * pixel);
                if (_strongShotText != null)
                    PlaceLabel(_strongShotText, new Vector2(hud * 0.22f, y),
                        new Vector2(hud * 0.30f, hud * 0.11f * pixel), hud * 0.65f * pixel);
                if (_suryaResourcePlate != null)
                {
                    _suryaResourcePlate.transform.localPosition = new Vector3(0f, y, -0.01f);
                    Vector2 source = _suryaResourcePlate.sprite.bounds.size;
                    _suryaResourcePlate.transform.localScale = new Vector3(hud * 0.84f / source.x, hud * 0.13f * pixel / source.y, 1f);
                }
                return;
            }
            PlaceLabel(_resourceBadgeText, new Vector2(s * 0.25f, s * 0.439f),
                new Vector2(s * 0.30f, s * 0.075f), s * 0.43f);
            if (_strongShotText != null)
                PlaceLabel(_strongShotText, new Vector2(s * 0.34f, s * 0.35f),
                    new Vector2(s * 0.22f, s * 0.07f), s * 0.20f);
        }

        private static float BottomHudSize(float cardSize)
            => Mathf.Min(cardSize, global::Cell.CellSize * 0.98f);

        private void ApplyBottomHudScale(float cardSize)
        {
            if (_bottomHudRoot == null || cardSize <= 0f) return;
            float scale = BottomHudSize(cardSize) / cardSize;
            _bottomHudRoot.localScale = Vector3.one * scale;
            // 축소 중심을 패널 정중앙에 고정해 큰 카드에서도 정보판이 슬롯 가운데에 남는다.
            _bottomHudRoot.anchoredPosition = new Vector2(
                (1f - scale) * CanvasWidth * 0.5f,
                -(1f - scale) * GlassCenterY);
        }

        /// <summary>앞줄이 뒷줄을 가리도록 정렬 순서를 맞춘다. 한 줄에 10칸을 쓴다.</summary>
        public void ApplyDepth(int order, int sortingLayerId)
        {
            _baseOrder = order;
            _layerId = sortingLayerId;
            ApplySorting();
        }

        private void ApplySorting()
        {
            if (_body == null) return;
            int o = _baseOrder + _sortBoost;

            SetOrder(_shadow, o);
            SetOrder(_auraGlow, o);
            _orbCanvas.sortingLayerID = _layerId;
            _orbCanvas.sortingOrder = o + 2;
            SetOrder(_portrait, o + 3);

            // 두 루트 캔버스가 SpriteRenderer와 같은 축에서 비교된다. 오브는 원화 뒤, 정보는 앞이다.
            _canvas.sortingLayerID = _layerId;
            _canvas.sortingOrder = o + 6;
            var nameRenderer = _nameLabel.GetComponent<MeshRenderer>();
            nameRenderer.sortingLayerID = _layerId;
            nameRenderer.sortingOrder = o + 7;
            var hpRenderer = _hpLabel.GetComponent<MeshRenderer>();
            hpRenderer.sortingLayerID = _layerId;
            hpRenderer.sortingOrder = o + 8;
            var resourceRenderer = _resourceBadgeText.GetComponent<MeshRenderer>();
            resourceRenderer.sortingLayerID = _layerId;
            resourceRenderer.sortingOrder = o + 9;
            if (_strongShotText != null) SetOrder(_strongShotText.GetComponent<MeshRenderer>(), o + 10);
            SetOrder(_suryaResourcePlate, o + 8);
        }

        private void SetOrder(Renderer renderer, int order)
        {
            if (renderer == null) return;
            renderer.sortingLayerID = _layerId;
            renderer.sortingOrder = order;
        }

        // ══════════════════════════════════════════════════════
        // 초상화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 칸의 초상화 렌더러를 카드 몸체 아래로 옮겨 받는다. 몸체가 날아가면 원화도 함께 간다.
        /// </summary>
        public void BindPortrait(SpriteRenderer portrait)
        {
            _portrait = portrait;
            if (_portrait == null) return;
            _portrait.transform.SetParent(_body, false);
            _portrait.maskInteraction = SpriteMaskInteraction.None;
            ApplyPortraitBottomFade();
            ApplySorting();
            LayOutPortrait();
        }

        /// <summary>
        /// 원화를 카드 한 변(× <see cref="PortraitFit"/>) 안에 <b>통째로 넣어</b> 가운데 세운다.
        /// 세로뿐 아니라 가로도 제한해야 넓은 그림이 옆 칸을 침범하지 않는다.
        /// </summary>
        public void LayOutPortrait()
        {
            if (_portrait == null) return;
            ApplyPortraitBottomFade();
            Sprite sprite = _portrait.sprite;
            if (sprite == null)
            {
                _portrait.transform.localScale = Vector3.one;
                _portrait.transform.localPosition = Vector3.zero;
                return;
            }

            Vector3 size = sprite.bounds.size;
            Vector3 center = sprite.bounds.center;
            if (PortraitSources.TryGetValue(sprite, out Sprite source) && source != null)
            {
                Rect content = source.textureRect;
                size = new Vector3(content.width / source.pixelsPerUnit,
                    content.height / source.pixelsPerUnit, 0f);
                center = new Vector3(
                    (content.center.x - source.rect.xMin - source.pivot.x) / source.pixelsPerUnit,
                    (content.center.y - source.rect.yMin - source.pivot.y) / source.pixelsPerUnit,
                    0f);
            }
            if (size.x <= 0.0001f || size.y <= 0.0001f) return;
            float fit = _cardSize * PortraitFit;
            float scale = Mathf.Min(fit / size.x, fit / size.y);
            center *= scale;
            _portrait.transform.localScale = new Vector3(scale, scale, 1f);
            _portrait.transform.localPosition = new Vector3(-center.x, -center.y, 0f);
        }

        /// <summary>
        /// 원본 텍스처는 읽기 금지로 임포트되어 있다. GPU 임시 표면을 거쳐 현재 스프라이트 영역만
        /// 한 번 읽고, 흰 매트가 밴 반투명 RGB와 잘린 밑변·모서리를 정리한 사본을 캐시한다.
        /// 이후 배치 이동이나 크기 변경은 같은 사본을 쓴다.
        /// </summary>
        private void ApplyPortraitBottomFade()
        {
            if (_portrait == null || _portrait.sprite == null) return;

            Sprite source = PortraitSources.TryGetValue(_portrait.sprite, out Sprite original)
                ? original
                : _portrait.sprite;
            if (FadedPortraits.TryGetValue(source, out Sprite cached) && cached != null)
            {
                _portrait.sprite = cached;
                return;
            }

            Sprite faded = CreateBottomFadedSprite(source);
            if (faded == null || faded == source) return;
            FadedPortraits[source] = faded;
            PortraitSources[faded] = source;
            _portrait.sprite = faded;
        }

        private static Sprite CreateBottomFadedSprite(Sprite source)
        {
            if (source == null || source.texture == null || source.packed) return source;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
#endif

            Rect spriteRect = source.rect;
            Rect contentRect = source.textureRect;
            int width = Mathf.RoundToInt(spriteRect.width);
            int height = Mathf.RoundToInt(spriteRect.height);
            if (width <= 1 || height <= 1) return source;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source.texture, temporary);
                RenderTexture.active = temporary;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    filterMode = FilterMode.Trilinear,
                    mipMapBias = PortraitMipBias,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"{source.name}_HudFade",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                // textureRect는 Tight 메시의 불투명 경계라 그 자체를 FullRect로 만들면 네 변의 픽셀이
                // Clamp되어 사각선이 된다. 원본 rect의 투명 여백까지 읽고 배치는 contentRect 기준으로 맞춘다.
                copy.ReadPixels(spriteRect, 0, 0, false);

                Color32[] pixels = copy.GetPixels32();
                ClearTransparentCompressionNoise(pixels);
                FindOpaqueBounds(pixels, width, height,
                    out int opaqueMinX, out int opaqueMinY, out int opaqueMaxX, out int opaqueMaxY);
                DetectHardEdgeCuts(pixels, width, height,
                    opaqueMinX, opaqueMinY, opaqueMaxX, opaqueMaxY,
                    out bool fadeLeft, out bool fadeRight, out bool fadeBottom);
                BleedOpaqueColorsIntoFringe(pixels, width, height);
                SoftenPortraitAlphaEdge(pixels, width, height);

                float contentBottom = contentRect.yMin - spriteRect.yMin;
                float clearPixels = contentRect.height * PortraitClearFraction;
                float fadePixels = Mathf.Max(1f, contentRect.height * PortraitFadeFraction - clearPixels);
                float sideFadePixels = Mathf.Max(1f,
                    (opaqueMaxX - opaqueMinX + 1) * PortraitSideFadeFraction);
                float bottomFadePixels = Mathf.Max(1f,
                    (opaqueMaxY - opaqueMinY + 1) * PortraitSideFadeFraction);
                for (int y = 0; y < height; y++)
                {
                    float rawFadeProgress = (y - contentBottom - clearPixels) / fadePixels;
                    float fadeProgress = Mathf.Clamp01(rawFadeProgress);
                    float alpha = Mathf.SmoothStep(0f, 1f, fadeProgress);
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int index = row + x;
                        Color32 pixel = pixels[index];
                        float edgeAlpha = 1f;
                        if (fadeLeft)
                            edgeAlpha = Mathf.Min(edgeAlpha, Mathf.SmoothStep(0f, 1f,
                                (x - opaqueMinX) / sideFadePixels));
                        if (fadeRight)
                            edgeAlpha = Mathf.Min(edgeAlpha, Mathf.SmoothStep(0f, 1f,
                                (opaqueMaxX - x) / sideFadePixels));
                        if (fadeBottom)
                            edgeAlpha = Mathf.Min(edgeAlpha, Mathf.SmoothStep(0f, 1f,
                                (y - opaqueMinY) / bottomFadePixels));

                        pixel.a = (byte)Mathf.RoundToInt(pixel.a * alpha * edgeAlpha);
                        pixels[index] = pixel;
                    }
                }
                copy.SetPixels32(pixels);
                copy.Apply(true, true);

                Vector2 pivot = new(
                    source.pivot.x / Mathf.Max(1f, source.rect.width),
                    source.pivot.y / Mathf.Max(1f, source.rect.height));
                Sprite faded = Sprite.Create(copy, new Rect(0f, 0f, width, height), pivot,
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                faded.name = $"{source.name}_HudFade";
                faded.hideFlags = HideFlags.HideAndDontSave;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                double elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt)
                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Debug.Log($"[UnitCardView] 원화 가장자리 사본 생성: {source.name}, " +
                    $"{elapsedMs:0.00} ms, 좌/우/하단 페이드=" +
                    $"{(fadeLeft ? "적용" : "생략")}/{(fadeRight ? "적용" : "생략")}/" +
                    $"{(fadeBottom ? "적용" : "생략")}");
#endif
                return faded;
            }
            catch (Exception exception)
            {
                if (copy != null) Destroy(copy);
                Debug.LogWarning($"[UnitCardView] 원화 하단 페이드 사본 생성 실패: {source.name} ({exception.Message})");
                return source;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void FindOpaqueBounds(Color32[] pixels, int width, int height,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= PortraitOpaqueThreshold) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX >= minX && maxY >= minY) return;
            minX = minY = 0;
            maxX = Mathf.Max(0, width - 1);
            maxY = Mathf.Max(0, height - 1);
        }

        /// <summary>
        /// 임포트 압축 뒤 투명 면에 생긴 1~16/255 알파는 원화 전체 사각형과 점선을 만든다.
        /// 원본의 실질적인 커버리지가 아닌 값만 먼저 0으로 정리하고 외곽 안티앨리어싱은 아래에서 다시 만든다.
        /// </summary>
        private static void ClearTransparentCompressionNoise(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > PortraitOpaqueThreshold) continue;
                Color32 pixel = pixels[i];
                pixel.a = 0;
                pixels[i] = pixel;
            }
        }

        private static void DetectHardEdgeCuts(Color32[] pixels, int width, int height,
            int minX, int minY, int maxX, int maxY,
            out bool left, out bool right, out bool bottom)
        {
            int contentHeight = maxY - minY + 1;
            int contentWidth = maxX - minX + 1;
            int requiredVerticalRun = Mathf.Max(24,
                Mathf.CeilToInt(contentHeight * PortraitSideRunFraction));
            int requiredHorizontalRun = Mathf.Max(24,
                Mathf.CeilToInt(contentWidth * PortraitSideRunFraction));

            // 하단 모서리에만 한정하면 날개·창·옷자락의 위쪽 절단면은 그대로 남는다.
            // 어느 높이에서든 연속 접촉 길이만 보되, 짧은 발·꼬리는 기존 최소 길이로 보전한다.
            left = LongestVerticalEdgeRun(pixels, width, height, minX, 1) >= requiredVerticalRun;
            right = LongestVerticalEdgeRun(pixels, width, height, maxX, -1) >= requiredVerticalRun;
            bottom = LongestHorizontalEdgeRun(pixels, width, height, minY, 1) >= requiredHorizontalRun;
        }

        private static int LongestVerticalEdgeRun(Color32[] pixels, int width, int height,
            int edgeX, int direction)
        {
            const int edgeBand = 5;
            int longest = 0;
            int current = 0;
            for (int y = 0; y < height; y++)
            {
                bool touches = false;
                for (int offset = 0; offset < edgeBand; offset++)
                {
                    int x = edgeX + offset * direction;
                    if (x < 0 || x >= width) continue;
                    if (pixels[y * width + x].a <= PortraitOpaqueThreshold) continue;
                    touches = true;
                    break;
                }

                current = touches ? current + 1 : 0;
                longest = Mathf.Max(longest, current);
            }

            return longest;
        }

        private static int LongestHorizontalEdgeRun(Color32[] pixels, int width, int height,
            int edgeY, int direction)
        {
            const int edgeBand = 5;
            int longest = 0;
            int current = 0;
            for (int x = 0; x < width; x++)
            {
                bool touches = false;
                for (int offset = 0; offset < edgeBand; offset++)
                {
                    int y = edgeY + offset * direction;
                    if (y < 0 || y >= height) continue;
                    if (pixels[y * width + x].a <= PortraitOpaqueThreshold) continue;
                    touches = true;
                    break;
                }

                current = touches ? current + 1 : 0;
                longest = Mathf.Max(longest, current);
            }

            return longest;
        }

        /// <summary>
        /// 흰 배경에서 뽑힌 원화의 반투명 RGB를 가장 가까운 안쪽 불투명색으로 바꾼다.
        /// 불투명 코어와 알파 커버리지는 그대로 두고 RGB만 안쪽 색으로 바꿔 흰 선을 줄인다.
        /// </summary>
        private static void BleedOpaqueColorsIntoFringe(Color32[] pixels, int width, int height)
        {
            Color32[] source = (Color32[])pixels.Clone();
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    Color32 fringe = source[index];
                    if (fringe.a == 0) continue;

                    if (fringe.a < PortraitBleedThreshold)
                    {
                        int bestDistance = int.MaxValue;
                        int bestAlpha = -1;
                        Color32 best = fringe;
                        int minProbeY = Mathf.Max(0, y - PortraitBleedRadius);
                        int maxProbeY = Mathf.Min(height - 1, y + PortraitBleedRadius);
                        int minProbeX = Mathf.Max(0, x - PortraitBleedRadius);
                        int maxProbeX = Mathf.Min(width - 1, x + PortraitBleedRadius);
                        for (int probeY = minProbeY; probeY <= maxProbeY; probeY++)
                        {
                            int probeRow = probeY * width;
                            for (int probeX = minProbeX; probeX <= maxProbeX; probeX++)
                            {
                                Color32 candidate = source[probeRow + probeX];
                                if (candidate.a < PortraitBleedThreshold) continue;
                                int distance = Mathf.Abs(probeX - x) + Mathf.Abs(probeY - y);
                                if (distance > bestDistance ||
                                    (distance == bestDistance && candidate.a <= bestAlpha)) continue;
                                bestDistance = distance;
                                bestAlpha = candidate.a;
                                best = candidate;
                            }
                        }

                        if (bestAlpha >= 0)
                        {
                            fringe.r = best.r;
                            fringe.g = best.g;
                            fringe.b = best.b;
                        }
                    }
                    pixels[index] = fringe;
                }
            }
        }

        /// <summary>원본 경계를 반 픽셀만 부드럽게 해 밉 단계에서도 얇은 선이 끊기지 않게 한다.</summary>
        private static void SoftenPortraitAlphaEdge(Color32[] pixels, int width, int height)
        {
            const float softness = 0.42f;
            Color32[] source = (Color32[])pixels.Clone();
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    Color32 pixel = source[index];
                    if (pixel.a == 0) continue;

                    byte neighborMin = pixel.a;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int py = y + oy;
                        if (py < 0 || py >= height) continue;
                        int probeRow = py * width;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int px = x + ox;
                            if ((ox == 0 && oy == 0) || px < 0 || px >= width) continue;
                            neighborMin = Math.Min(neighborMin, source[probeRow + px].a);
                        }
                    }

                    if (neighborMin >= pixel.a) continue;
                    Color32 softened = pixels[index];
                    softened.a = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.a, neighborMin, softness));
                    pixels[index] = softened;
                }
            }
        }


        /// <summary>초상화 배율이 바뀌었을 때 — 예전 호출부 호환용.</summary>
        public void RefreshPortraitBase() => LayOutPortrait();

        public Sprite PortraitSprite => _portrait != null ? _portrait.sprite : null;
        internal SpriteRenderer PortraitRenderer => _portrait;
        internal Transform Body => _body;
        internal float CardSize => _cardSize;
        internal int LayerId => _layerId;

        /// <summary>
        /// 사망 직후 칸이 비워져도 원화와 두 HUD 캔버스를 같은 박자로 지울 수 있도록 몸체 전체를 뜬다.
        /// 직전 <see cref="Tick"/>이 체력 0 카드를 낮춘 알파만 되돌리고, 실제 뷰의 생명주기에는 손대지 않는다.
        /// </summary>
        internal GameObject CreateDeathSnapshot()
        {
            if (_body == null || !_body.gameObject.activeInHierarchy) return null;

            GameObject snapshot = Instantiate(_body.gameObject);
            snapshot.name = $"DeathSnapshot_{(_unit != null ? _unit.UnitName : name)}";
            Transform snapshotTransform = snapshot.transform;
            snapshotTransform.position = _body.position;
            snapshotTransform.rotation = _body.rotation;
            snapshotTransform.localScale = _body.lossyScale;

            foreach (CanvasGroup group in snapshot.GetComponentsInChildren<CanvasGroup>(true))
                group.alpha = Mathf.Clamp01(group.alpha / InactiveAlpha);

            foreach (SpriteRenderer renderer in snapshot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!string.Equals(renderer.gameObject.name, "Portrait", StringComparison.Ordinal)) continue;
                Color color = renderer.color;
                color.a = Mathf.Clamp01(color.a / InactiveAlpha);
                renderer.color = color;
            }

            foreach (TMP_Text label in snapshot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label is TextMeshProUGUI) continue;
                Color color = label.color;
                color.a = Mathf.Clamp01(color.a / InactiveAlpha);
                label.color = color;
            }

            return snapshot;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>하네스가 카드 하위 렌더러를 하나씩 격리할 때만 쓰는 개발 전용 스위치.</summary>
        public void SetVerificationHiddenLayers(IEnumerable<string> layerNames)
        {
            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (layerNames != null)
            {
                foreach (string layer in layerNames)
                    if (!string.IsNullOrWhiteSpace(layer)) hidden.Add(layer.Trim());
            }

            bool IsHidden(params string[] aliases)
            {
                foreach (string alias in aliases)
                    if (hidden.Contains(alias)) return true;
                return false;
            }

            ApplyHudVisibility();
            if (_portrait != null)
                _portrait.gameObject.SetActive(!IsHidden("portrait", "portrait_copy", "art", "art_copy"));
            if (_shadow != null) _shadow.enabled = !IsHidden("shadow", "ground_shadow");
            if (_auraGlow != null) _auraGlow.enabled = !IsHidden("aura", "foot_marker");
            if (_orbCanvas != null) _orbCanvas.gameObject.SetActive(!IsHidden("orb"));
            bool panelHidden = IsHidden("panel", "bottom_panel");
            if (_bottomGlass != null) _bottomGlass.SetActive(_combatHud && !panelHidden);
            if (_bottomGlassShadow != null) _bottomGlassShadow.SetActive(_combatHud && !panelHidden);
        }

        /// <summary>임포트 원본과 화면에 그리는 런타임 사본을 PNG·알파 통계로 함께 남긴다.</summary>
        public void DumpVerificationPortraitCopies(string directory, string label)
        {
            if (_portrait?.sprite == null || string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);

            Sprite runtime = _portrait.sprite;
            Sprite source = PortraitSources.TryGetValue(runtime, out Sprite original) && original != null
                ? original
                : runtime;
            string stem = SafeDiagnosticName(label);
            DumpVerificationSprite(source, directory, stem + "_source");
            if (runtime != source)
                DumpVerificationSprite(runtime, directory, stem + "_runtime");

            Camera camera = Camera.main;
            if (camera == null) return;
            Bounds bounds = _portrait.bounds;
            Vector3 min = camera.WorldToScreenPoint(bounds.min);
            Vector3 max = camera.WorldToScreenPoint(bounds.max);
            float screenWidth = Mathf.Abs(max.x - min.x);
            float screenHeight = Mathf.Abs(max.y - min.y);
            var display = new PortraitDisplayReport
            {
                sourceSprite = source.name,
                sourceWidth = Mathf.RoundToInt(source.rect.width),
                sourceHeight = Mathf.RoundToInt(source.rect.height),
                runtimeWidth = runtime.texture.width,
                runtimeHeight = runtime.texture.height,
                runtimeMipCount = runtime.texture.mipmapCount,
                screenWidth = screenWidth,
                screenHeight = screenHeight,
                sourcePixelsPerScreenPixelX = source.rect.width / Mathf.Max(1f, screenWidth),
                sourcePixelsPerScreenPixelY = source.rect.height / Mathf.Max(1f, screenHeight),
                filterMode = runtime.texture.filterMode.ToString(),
                mipMapBias = runtime.texture.mipMapBias,
            };
            File.WriteAllText(Path.Combine(directory, stem + "_display.json"),
                JsonUtility.ToJson(display, true));
        }

        [Serializable]
        private sealed class PortraitAlphaReport
        {
            public string sprite;
            public int width;
            public int height;
            public int alphaMin;
            public int alphaMax;
            public int transparent;
            public int low1To16;
            public int low17To32;
            public int low33To48;
            public int partial;
            public int opaque;
            public int isolatedLowAlpha;
            public int lowMinX = -1;
            public int lowMinY = -1;
            public int lowMaxX = -1;
            public int lowMaxY = -1;
        }

        [Serializable]
        private sealed class PortraitDisplayReport
        {
            public string sourceSprite;
            public int sourceWidth;
            public int sourceHeight;
            public int runtimeWidth;
            public int runtimeHeight;
            public int runtimeMipCount;
            public float screenWidth;
            public float screenHeight;
            public float sourcePixelsPerScreenPixelX;
            public float sourcePixelsPerScreenPixelY;
            public string filterMode;
            public float mipMapBias;
        }

        private static void DumpVerificationSprite(Sprite sprite, string directory, string stem)
        {
            Texture2D texture = ReadSpritePixels(sprite);
            if (texture == null) return;
            try
            {
                File.WriteAllBytes(Path.Combine(directory, stem + ".png"), texture.EncodeToPNG());
                PortraitAlphaReport report = BuildAlphaReport(sprite.name, texture.GetPixels32(),
                    texture.width, texture.height);
                File.WriteAllText(Path.Combine(directory, stem + "_alpha.json"),
                    JsonUtility.ToJson(report, true));
            }
            finally
            {
                Destroy(texture);
            }
        }

        private static Texture2D ReadSpritePixels(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.packed) return null;
            int width = Mathf.RoundToInt(sprite.rect.width);
            int height = Mathf.RoundToInt(sprite.rect.height);
            if (width <= 0 || height <= 0) return null;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            try
            {
                Graphics.Blit(sprite.texture, temporary);
                RenderTexture.active = temporary;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(sprite.rect, 0, 0, false);
                copy.Apply(false, false);
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static PortraitAlphaReport BuildAlphaReport(string spriteName, Color32[] pixels,
            int width, int height)
        {
            var report = new PortraitAlphaReport
            {
                sprite = spriteName,
                width = width,
                height = height,
                alphaMin = 255,
                alphaMax = 0,
            };

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[row + x].a;
                    report.alphaMin = Mathf.Min(report.alphaMin, alpha);
                    report.alphaMax = Mathf.Max(report.alphaMax, alpha);
                    if (alpha == 0) report.transparent++;
                    else if (alpha <= 16) report.low1To16++;
                    else if (alpha <= 32) report.low17To32++;
                    else if (alpha <= PortraitDiagnosticLowAlphaThreshold) report.low33To48++;
                    else if (alpha < 255) report.partial++;
                    else report.opaque++;

                    if (alpha == 0 || alpha > PortraitDiagnosticLowAlphaThreshold) continue;
                    if (report.lowMinX < 0)
                    {
                        report.lowMinX = report.lowMaxX = x;
                        report.lowMinY = report.lowMaxY = y;
                    }
                    else
                    {
                        report.lowMinX = Mathf.Min(report.lowMinX, x);
                        report.lowMinY = Mathf.Min(report.lowMinY, y);
                        report.lowMaxX = Mathf.Max(report.lowMaxX, x);
                        report.lowMaxY = Mathf.Max(report.lowMaxY, y);
                    }

                    bool nearCore = false;
                    for (int py = Mathf.Max(0, y - PortraitDiagnosticCoreRadius);
                         py <= Mathf.Min(height - 1, y + PortraitDiagnosticCoreRadius) && !nearCore; py++)
                    {
                        int probeRow = py * width;
                        for (int px = Mathf.Max(0, x - PortraitDiagnosticCoreRadius);
                             px <= Mathf.Min(width - 1, x + PortraitDiagnosticCoreRadius); px++)
                        {
                            if (pixels[probeRow + px].a <= PortraitDiagnosticLowAlphaThreshold) continue;
                            nearCore = true;
                            break;
                        }
                    }
                    if (!nearCore) report.isolatedLowAlpha++;
                }
            }
            return report;
        }

        private static string SafeDiagnosticName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "portrait";
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }
#endif

        // ══════════════════════════════════════════════════════
        // 연결
        // ══════════════════════════════════════════════════════

        public void Bind(Unit unit, bool combatHud)
        {
            if (!EnsureHudReady()) return;
            ResetMotion();
            _unit = unit;
            _combatHud = combatHud;
            LayOutResourceBadgeText(_cardSize);

            Show(unit != null);
            if (unit == null) return;

            _facing = unit.IsEnemy ? -1 : 1;
            _lastHp = unit.HpCurr;

            _nameLabel.text = unit.UnitName;
            _ringElementName = null;
            RefreshUltimateRingColor();

            ApplyHudVisibility();

            LayOutPortrait();
            Tick();
        }

        private void ApplyHudVisibility()
        {
            if (!HudIsComplete()) return;
            bool visible = _unit != null && _combatHud;

            // 대기석 카드는 전투 정보를 들지 않는다. 원화와 이름만 남는다.
            _ultRoot.gameObject.SetActive(visible);
            _bottomGlass.SetActive(visible);
            _bottomGlassShadow.SetActive(visible);
            _hpLabel.gameObject.SetActive(visible);
            _glassTint.color = new Color(0.149f, 0.22f, 0.29f, 0.72f);
            if (!visible)
            {
                foreach (Image dot in _dots) dot.enabled = false;
                foreach (Image back in _dotBacks) back.enabled = false;
                foreach (Image rim in _dotRims) rim.enabled = false;
                foreach (Image arrow in _dotArrows) arrow.enabled = false;
                foreach (Image countBack in _dotCountBacks) countBack.enabled = false;
                foreach (Image count in _dotCounts) count.enabled = false;
                foreach (Image badge in _elementBadges) badge.enabled = false;
            }
        }

        private void Show(bool visible)
        {
            if (_body == null) return;
            _body.gameObject.SetActive(visible);
            _shadow.enabled = visible;
        }

        // ══════════════════════════════════════════════════════
        // HUD 갱신
        // ══════════════════════════════════════════════════════

        /// <summary>값이 바뀐 HUD를 다시 그린다. 움직임은 여기서 진행하지 않는다(프레임당 여러 번 불릴 수 있다).</summary>
        public void Tick()
        {
            if (!EnsureHudReady()) return;
            if (_unit == null) return;

            // 체력이 줄었으면 맞은 것이다. 출처를 모르는 피해도 여기서 잡힌다.
            if (_lastHp >= 0 && _unit.HpCurr < _lastHp && _flash <= 0f) PlayHitReaction(0.5f, false, false);
            _lastHp = _unit.HpCurr;

            RefreshElementBadges();
            RefreshResourceBadge();
            if (!_combatHud) return;

            // 보호막은 별도 생명처럼 체력 오른쪽을 늘리지 않고, 최대 체력 대비 양을 윗면에 겹쳐 보여 준다.
            float hpRatio = _unit.HpMax > 0 ? Mathf.Clamp01(_unit.HpCurr / (float)_unit.HpMax) : 0f;
            float shieldRatio = _unit.HpMax > 0 ? Mathf.Clamp01(_unit.ShieldCurr / (float)_unit.HpMax) : 0f;
            SetSpan(_hpFill.rectTransform, 0f, hpRatio);
            SetSpan(_shieldFill.rectTransform, 0f, shieldRatio);
            _shieldFill.rectTransform.offsetMin = new Vector2(0f, HpHeight * 0.58f);
            _hpFill.enabled = hpRatio > 0f;
            _shieldFill.enabled = shieldRatio > 0f;
            _hpFill.color = _unit.IsEnemy
                ? new Color(0.878f, 0.353f, 0.408f, 1f)
                : new Color(0.157f, 0.78f, 0.682f, 1f);
            _hpLabel.text = $"{Mathf.Max(0, _unit.HpCurr):N0} / {Mathf.Max(0, _unit.HpMax):N0}";
            bool wideHp = Mathf.Abs(_unit.HpCurr) >= 1000 || Mathf.Abs(_unit.HpMax) >= 1000;
            _hpLabel.transform.localScale = new Vector3(wideHp ? 0.88f : 1f, 1f, 1f);

            RefreshUltimateRingColor();
            float fill = _unit.ManaMax > 0 ? Mathf.Clamp01(_unit.ManaCurr / (float)_unit.ManaMax) : 0f;
            _ultFillAmount = fill;
            _ultRearFill.fillAmount = fill;
            _ultFill.fillAmount = fill;
            _ultFillLight.fillAmount = fill;
            _ultFillShade.fillAmount = fill;
            RefreshUltimateReady(fill >= 0.999f);

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            SetSpan(_actionFill.rectTransform, 0f, scheduler?.ActionProgress(_unit) ?? 0f);

            RefreshStatusDots();
        }

        /// <summary>
        /// 지원 연출이 끝난 순간 실제 상태 아이콘 또는 보호막 게이지를 한 번 튕긴다.
        /// 월드 이펙트만으로 의미를 추측하게 두지 않고, 변화가 생긴 HUD와 시선을 직접 연결한다.
        /// </summary>
        public void PulseSupportHud(bool shield, bool health, Color accent)
        {
            if (!_combatHud) return;
            Tick();

            _supportHudPulseTarget = null;
            _supportHudPulseImage = null;
            if (shield && _shieldFill != null && _shieldFill.enabled)
            {
                _supportHudPulseTarget = _shieldFill.rectTransform;
                _supportHudPulseImage = _shieldFill;
            }
            else if (health && _hpFill != null && _hpFill.enabled)
            {
                _supportHudPulseTarget = _hpFill.rectTransform;
                _supportHudPulseImage = _hpFill;
            }
            else
            {
                for (int i = 0; i < _dotBacks.Length; i++)
                {
                    if (_dotBacks[i] == null || !_dotBacks[i].enabled) continue;
                    _supportHudPulseTarget = _dotBacks[i].rectTransform;
                    _supportHudPulseImage = _dotRims[i];
                    break;
                }
            }

            if (_supportHudPulseTarget == null && _hpTrack != null)
            {
                _supportHudPulseTarget = _hpTrack.rectTransform;
                _supportHudPulseImage = _hpTrack;
            }

            if (_supportHudPulseTarget == null) return;
            _supportHudPulseBaseColor = _supportHudPulseImage != null
                ? _supportHudPulseImage.color
                : Color.white;
            _supportHudPulseAge = 0f;
            _supportHudPulseTarget.localScale = Vector3.one;
            if (_supportHudPulseImage != null)
            {
                accent.a = 1f;
                _supportHudPulseImage.color = Color.Lerp(_supportHudPulseBaseColor, accent, 0.72f);
            }
        }

        private void RefreshElementBadges()
        {
            int i = 0;
            if (_unit.HasAnyCombatElement)
            {
                foreach (BaseEnums.UnitElement element in _unit.AttachedElements)
                {
                    if (i >= MaxElementBadges) break;
                    _elementBadges[i].sprite = UltimateGlyph(null, element);
                    _elementBadges[i].color = new Color(0.95f, 0.94f, 0.91f, 0.88f);
                    _elementBadges[i].enabled = true;
                    i++;
                }
            }
            for (; i < MaxElementBadges; i++) _elementBadges[i].enabled = false;
        }

        /// <summary>
        /// 유닛이 카드 HUD용으로 명시한 고유 자원의 현재 수를 오브 왼쪽 위 배지에 적는다.
        /// 작은 카드에서 일곱 개 마름모는 채움 수를 세기 어려워 한 자리 숫자가 더 빨리 읽힌다.
        /// </summary>
        private void RefreshResourceBadge()
        {
            bool treasure = _combatHud && _unit != null && !_unit.IsEnemy && _unit.ID == 65;
            int treasureCount = treasure ? Mathf.Clamp(_unit.ManaCurr, 0, 8) : 0;
            for (int i = 0; i < _treasureBeads.Length; i++)
            {
                Image bead = _treasureBeads[i];
                if (bead == null) continue;
                bead.enabled = treasure;
                bead.color = i < treasureCount ? new Color(1f, 0.80f, 0.28f, 1f)
                    : new Color(0.24f, 0.32f, 0.40f, 0.94f);
            }
            string resource = BadgeResourceId(_unit);
            int max = treasure ? 8 : _combatHud && resource != null ? _unit.GetCombatResourceMaximum(resource) : 0;
            int current = treasure ? treasureCount : max > 0 ? _unit.GetCombatResource(resource) : 0;
            bool visible = treasure || max > 0 && (_unit.ID is 7 or 67 or 121 || current > 0);
            bool surya = _unit?.ID == 67;
            _resourceBadge.enabled = visible && !surya;
            if (_suryaResourcePlate != null) _suryaResourcePlate.enabled = visible && surya;
            _resourceBadgeDigit.enabled = false;
            _resourceBadgeText.enabled = visible;
            if (_strongShotText != null)
                _strongShotText.enabled = _combatHud && _unit != null && _unit.ID == 67 && _unit.isActive &&
                    _unit.GetCombatResource(Codes.Passive.SuryaBeforeSunset.EmpoweredResourceId) > 0;
            if (!visible) return;

            _resourceBadgeText.text = surya ? $"탄 {current}/{max}" : current.ToString();
            _resourceBadge.rectTransform.sizeDelta = new Vector2(Mathf.Max(9.2f, 5.4f + current.ToString().Length * 3.8f), 7.2f);
            float complete = current >= max ? 1f : 0f;
            _resourceBadge.color = Color.Lerp(
                new Color(0.70f, 0.82f, 0.90f, 0.88f),
                new Color(0.96f, 0.88f, 0.62f, 0.96f), complete);
        }

        /// <summary>
        /// 자원 생성 순서가 바뀌어도 배지가 엉뚱한 값을 고르지 않도록 유닛별 표시 자원을 여기서 명시한다.
        /// 명시하지 않은 유닛은 배지를 숨긴다.
        /// </summary>
        private static string BadgeResourceId(Unit unit)
        {
            if (unit == null || unit.IsEnemy) return null;
            return unit.ID switch
            {
                1 => Codes.Passive.SeiBigBang.ResourceId,
                7 => Codes.Passive.JeanIronSanction.ResourceId,
                67 => Codes.Passive.SuryaBeforeSunset.BulletResourceId,
                121 => Codes.Passive.AnubisSoulHarvest.ResourceId,
                _ => null,
            };
        }

        private void RefreshUltimateRingColor()
        {
            string elementName = _unit != null ? _unit.Element : null;
            if (elementName == _ringElementName) return;
            _ringElementName = elementName;

            BaseEnums.UnitElement element = ElementPalette.Parse(elementName);
            _ringColor = element == BaseEnums.UnitElement.None ? UITheme.Mana : ElementPalette.PastelColorFor(element);
            _ultOutline.color = new Color(0.78f, 0.89f, 0.96f, 0.98f);
            _ultFill.color = _ringColor;
            Color rearLiquid = Color.Lerp(_ringColor, new Color(0.025f, 0.055f, 0.085f, 1f), 0.34f);
            rearLiquid.a = 0.96f;
            _ultRearFill.color = rearLiquid;
            Color liquidLight = Color.Lerp(_ringColor, Color.white, 0.58f);
            liquidLight.a = 0.62f;
            _ultFillLight.color = liquidLight;
            Sprite glyph = UltimateGlyph(_unit, element);
            _ultMark.sprite = glyph;
            _ultMarkShadow.sprite = glyph;
            _ultMarkShadow.color = MarkShadow;
            _ultMark.color = MarkIdle;
        }

        /// <summary>다 찼는지를 맥동으로 알린다. 8배속에서도 깜빡임이 되지 않게 실시간으로 센다.</summary>
        private void RefreshUltimateReady(bool ready)
        {
            _ultReady = ready;
            if (!ready)
            {
                _ultMark.color = MarkIdle;
                _ultMarkShadow.color = MarkShadow;
                // 비어 있어도 짙은 원판이 되지 않는다. 밝은 크롬과 투명 유리 하드웨어는 항상 남긴다.
                _ultOutline.color = new Color(0.78f, 0.89f, 0.96f, 0.98f);
                _ultReadyGlow.color = Color.clear;
                _ultReadyRing.color = Color.clear;
                return;
            }

            float pulse = 0.74f + 0.26f * Mathf.Sin(Time.unscaledTime * 6.6f);
            _ultMark.color = new Color(1f, 1f, 1f, 0.92f + pulse * 0.08f);
            _ultMarkShadow.color = new Color(MarkShadow.r, MarkShadow.g, MarkShadow.b, 0.94f);
            // 준비 상태에서도 하드웨어 외곽은 실버블루로 유지한다. 바깥 원을 금색으로 바꾸면
            // 작은 카드에서 아래쪽 호만 수평 막대처럼 튀므로 금색은 안쪽 준비 링에만 쓴다.
            _ultOutline.color = new Color(0.78f, 0.89f, 0.96f, 0.98f);
            _ultReadyGlow.color = new Color(_ringColor.r, _ringColor.g, _ringColor.b, 0.13f + pulse * 0.12f);
            // 21×21에서는 준비 링의 아래쪽 호만 금색 막대로 남았다. 완충 정보는 내부 글리프와
            // 오브 안에 갇힌 호흡광으로 충분하므로 외곽 장식은 투명하게 유지한다.
            _ultReadyRing.color = Color.clear;
        }

        /// <summary>
        /// 상태 아이콘. <b>같은 그림으로 보이는 것은 한 칸에 모으고 수를 적는다.</b>
        /// 순서는 행동 불가 → 해로움 → 이로움. 칸이 모자라면 마지막 칸에 셰브론을 둔다.
        /// </summary>
        private void RefreshStatusDots()
        {
            _dotStatuses.Clear();
            _dotStackCounts.Clear();

            IReadOnlyList<UnitStatus> statuses = _unit.ActiveStatuses;
            if (statuses != null)
            {
                for (int i = 0; i < statuses.Count; i++)
                {
                    UnitStatus status = statuses[i];
                    if (status == null) continue;
                    int slot = IndexOfLook(status);
                    if (slot >= 0)
                    {
                        _dotStackCounts[slot]++;
                        continue;
                    }
                    _dotStatuses.Add(status);
                    _dotStackCounts.Add(1);
                }
            }

            SortDotsByUrgency();
            ApplyGlassHeight(_dotStatuses.Count > 0);

            int slots = Mathf.Clamp(_visibleDotSlots, 1, MaxDots);
            int iconCapacity = _compactHud ? Mathf.Min(CompactStatusIconCount, slots - 1) : slots;
            bool overflow = _dotStatuses.Count > iconCapacity;
            int iconSlots = overflow && !_compactHud
                ? slots - 1
                : Mathf.Min(_dotStatuses.Count, iconCapacity);
            int overflowSlot = iconSlots;
            for (int i = 0; i < _dots.Length; i++)
            {
                bool isOverflow = overflow && i == overflowSlot;
                bool has = i < iconSlots || isOverflow;
                _dotBacks[i].enabled = has;
                _dotRims[i].enabled = has;
                _dots[i].enabled = has;
                _dotArrows[i].enabled = false;
                _dotCountBacks[i].enabled = has && !isOverflow && _dotStackCounts[i] > 1;
                _dotCounts[i].enabled = has && !isOverflow && _dotStackCounts[i] > 1;
                if (!has) continue;

                _dotBacks[i].color = _compactHud
                    ? new Color(0.075f, 0.125f, 0.20f, 0.22f)
                    : DotBacking;
                _dotRims[i].color = _compactHud
                    ? new Color(0.97f, 0.985f, 1f, 0.62f)
                    : new Color(0.90f, 0.98f, 1f, 0.42f);

                if (isOverflow)
                {
                    _dotBacks[i].sprite = StatusIcons.Frame(null, false);
                    _dotRims[i].sprite = StatusIcons.Frame(null, true);
                    _dots[i].sprite = StatusIcons.Overflow;
                    _dots[i].color = Color.white;
                    _dots[i].rectTransform.localRotation = Quaternion.identity;
                    continue;
                }

                UnitStatus status = _dotStatuses[i];
                StatusArrow arrow = StatusIcons.Resolve(status).Arrow;
                _dotBacks[i].sprite = StatusIcons.Frame(status, false);
                _dotRims[i].sprite = StatusIcons.Frame(status, true);
                _dots[i].sprite = StatusIcons.For(_dotStatuses[i]);
                _dots[i].color = StatusIcons.Tint(status);
                _dots[i].rectTransform.localRotation = Quaternion.identity;
                _dotArrows[i].sprite = StatusIcons.Arrow(arrow);
                _dotArrows[i].enabled = _dotArrows[i].sprite != null;
                if (!_dotCounts[i].enabled) continue;
                _dotCounts[i].sprite = StatusIcons.StackDigit(Mathf.Min(9, _dotStackCounts[i]));
                _dotCounts[i].color = Color.white;
            }
        }

        /// <summary>
        /// 상태가 없을 때 빈 셋째 행을 접는다. 패널 상단과 정보 좌표는 고정해 상태가 생기거나
        /// 사라져도 이름·HP가 흔들리지 않고, 바깥 림과 그림자만 실제 내용 높이를 따른다.
        /// </summary>
        private void ApplyGlassHeight(bool showStatusRow)
        {
            if (_bottomGlass == null || _bottomGlassShadow == null || _actingOutline == null) return;
            if (_glassHeightInitialized && _statusRowVisible == showStatusRow) return;

            _glassHeightInitialized = true;
            _statusRowVisible = showStatusRow;
            float height = showStatusRow ? GlassHeight : GlassEmptyHeight;
            Place(_actingOutline.rectTransform, GlassLeft - 0.65f, GlassTop - 0.65f,
                GlassWidth + 1.3f, height + 1.3f);
            Place((RectTransform)_bottomGlassShadow.transform, GlassLeft, GlassTop + 0.8f,
                GlassWidth, height);
            Place((RectTransform)_bottomGlass.transform, GlassLeft, GlassTop, GlassWidth, height);
        }

        private int IndexOfLook(UnitStatus status)
        {
            (StatusGlyph glyph, StatusArrow arrow) = StatusIcons.Resolve(status);
            StatusFrameKind frame = StatusIcons.FrameKind(status);
            for (int i = 0; i < _dotStatuses.Count; i++)
            {
                UnitStatus other = _dotStatuses[i];
                if (string.Equals(other.Key, status.Key, StringComparison.Ordinal)) return i;
                (StatusGlyph otherGlyph, StatusArrow otherArrow) = StatusIcons.Resolve(other);
                if (otherGlyph == glyph && otherArrow == arrow && StatusIcons.FrameKind(other) == frame) return i;
            }
            return -1;
        }

        private void SortDotsByUrgency()
        {
            for (int i = 1; i < _dotStatuses.Count; i++)
            {
                UnitStatus status = _dotStatuses[i];
                int count = _dotStackCounts[i];
                int rank = Urgency(status);
                int j = i - 1;
                while (j >= 0 && Urgency(_dotStatuses[j]) > rank)
                {
                    _dotStatuses[j + 1] = _dotStatuses[j];
                    _dotStackCounts[j + 1] = _dotStackCounts[j];
                    j--;
                }
                _dotStatuses[j + 1] = status;
                _dotStackCounts[j + 1] = count;
            }
        }

        private static int Urgency(UnitStatus status)
        {
            return StatusIcons.FrameKind(status) switch
            {
                StatusFrameKind.Control => 0,
                StatusFrameKind.Harmful => 1,
                StatusFrameKind.Neutral => 2,
                _ => 3,
            };
        }

        // ══════════════════════════════════════════════════════
        // 반응 (피격 · 발사)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 맞는 순간의 반응. 하얗게 번쩍이며 뒤로 밀리고, <b>부딪힌 공격이면 떤다</b>.
        /// </summary>
        /// <param name="strength">0~1 남짓. 1이 한 방에 가깝다.</param>
        /// <param name="physical">카드가 직접 부딪힌 공격인가.</param>
        public void PlayHitReaction(float strength, bool physical, bool crit)
        {
            float scaled = Mathf.Clamp(strength, 0.2f, 1.4f);
            // 전용 착탄광과 겹쳐도 카드 원화가 사라지지 않도록 최종 면광을 0.25 아래로 제한한다.
            _flash = Mathf.Max(_flash, crit ? 0.67f : 0.62f);
            _punch = Mathf.Max(_punch, Mathf.Min(1f, 0.6f + 0.3f * scaled));
            _recoil = Mathf.Max(_recoil, Mathf.Min(1f, scaled));
            if (physical || crit)
            {
                _shake = 1f;
                _shakeStrength = Mathf.Min(1.3f, scaled * (crit ? 1.3f : 1f));
                _shakeSeed = UnityEngine.Random.value * 10f;
                _hitSquash = Mathf.Max(_hitSquash, Mathf.Min(1.25f, scaled * (physical ? 1.05f : 0.72f)));
            }
            if (_unit != null) _lastHp = _unit.HpCurr;
        }

        /// <summary>
        /// 큰 스킬이 주변 원화에도 같은 색의 짧은 반사광을 남긴다. 흰 부분을 더 밝힐 수는 없고
        /// RGB만 약하게 기울이므로 얼굴과 의상이 과노출되지 않는다.
        /// </summary>
        public void PulseVfxLight(Color color, float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f) return;
            _vfxLightColor = color;
            _vfxLightPeak = Mathf.Clamp(strength, 0f, 0.28f);
            _vfxLightAge = 0f;
            _vfxLightDuration = Mathf.Max(0.08f, duration);
        }

        /// <summary>발사 반동 — 예전 호출부 호환용. 적 쪽으로 짧게 튄다.</summary>
        public void PlayAttackReaction(float strength = 1f) => PlayThrow(new Vector3(_facing, 0f, 0f), strength);

        /// <summary>
        /// 던지는 동작. 카드가 <paramref name="direction"/>으로 짧게 튀었다 돌아온다.
        /// 원거리 공격은 카드가 날아가지 않는다 — 대신 이 반동이 "쐈다"를 말한다.
        /// </summary>
        public void PlayThrow(Vector3 direction, float strength = 1f)
        {
            direction.z = 0f;
            _lungeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector3(_facing, 0f, 0f);
            _lunge = Mathf.Clamp01(strength);
            _punch = Mathf.Max(_punch, 0.5f * Mathf.Clamp01(strength));
        }

        /// <summary>
        /// 발톱 끝처럼 방향이 보이는 VFX가 지나갈 때만 쓰는 짧은 밀림이다.
        /// 전투 위치는 바꾸지 않고 몸체 계층만 이동했다 되돌려 판정과 배치를 건드리지 않는다.
        /// </summary>
        public void PlayVfxKick(Vector3 direction, float strength = 0.55f)
        {
            direction.z = 0f;
            _vfxKickDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            _vfxKick = 1f;
            _vfxKickStrength = Mathf.Max(_vfxKickStrength, Mathf.Clamp01(strength));
            _hitSquash = Mathf.Max(_hitSquash, 0.62f * Mathf.Clamp01(strength));
        }

        /// <summary>
        /// 패시브가 끼어들기 전에 원 공격 카드가 대상 쪽으로 절반만 나갔다 멈추는 프리뷰다.
        /// 실제 공격 모션은 개입 뒤 처음부터 다시 시작하므로 위치·행동치에는 손대지 않는다.
        /// </summary>
        public IEnumerator InterruptionWindup(Vector3 direction)
        {
            direction.z = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector3(_facing, 0f, 0f);
            float tiltSign = direction.x >= 0f ? -1f : 1f;
            Vector3 half = direction * _cardSize * 0.09f;
            IsPerformingMotion = true;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);

            const float enter = 0.12f;
            for (float age = 0f; age < enter; age += Time.deltaTime)
            {
                float t = EaseOutCubic(age / enter);
                _motionOffset = Vector3.Lerp(Vector3.zero, half, t);
                _tilt = Mathf.Lerp(0f, -tiltSign * 3.5f, t);
                _squash = Vector2.Lerp(Vector2.one, new Vector2(1.025f, 0.982f), t);
                yield return null;
            }
            _motionOffset = half;
            yield return new WaitForSeconds(0.10f);

            const float rewind = 0.08f;
            for (float age = 0f; age < rewind; age += Time.deltaTime)
            {
                float t = EaseInOutQuad(age / rewind);
                _motionOffset = Vector3.Lerp(half, Vector3.zero, t);
                _tilt = Mathf.Lerp(-tiltSign * 3.5f, 0f, t);
                _squash = Vector2.Lerp(new Vector2(1.025f, 0.982f), Vector2.one, t);
                yield return null;
            }
            _motionOffset = Vector3.zero;
            _tilt = 0f;
            _squash = Vector2.one;
            SetSortBoost(0);
            IsPerformingMotion = false;
        }

        /// <summary>
        /// 지상형 적의 할퀴기 준비 동작. 대상까지 돌진하지 않고 뒤로 물러났다가 카드 폭의 일부만 전진해
        /// <paramref name="onAdvanced"/>를 부른 뒤, 연출이 그려지는 동안 버티고 제자리로 돌아온다.
        /// </summary>
        public IEnumerator GroundRakeAdvance(Vector3 direction, float holdTime, Action onAdvanced)
        {
            direction.z = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector3(_facing, 0f, 0f);
            float tiltSign = direction.x >= 0f ? -1f : 1f;
            Vector3 windup = -direction * _cardSize * 0.10f;
            Vector3 advanced = direction * _cardSize * 0.18f;

            IsPerformingMotion = true;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);

            const float windupTime = 0.11f;
            for (float t = 0f; t < windupTime; t += Time.deltaTime)
            {
                float k = EaseOutCubic(t / windupTime);
                _motionOffset = Vector3.Lerp(Vector3.zero, windup, k);
                _tilt = tiltSign * 5f * k;
                _squash = Vector2.Lerp(Vector2.one, new Vector2(0.96f, 1.035f), k);
                yield return null;
            }

            const float advanceTime = 0.13f;
            for (float t = 0f; t < advanceTime; t += Time.deltaTime)
            {
                float k = EaseInOutQuad(t / advanceTime);
                _motionOffset = Vector3.Lerp(windup, advanced, k);
                _tilt = Mathf.Lerp(tiltSign * 5f, -tiltSign * 4f, k);
                _squash = Vector2.Lerp(new Vector2(0.96f, 1.035f), new Vector2(1.045f, 0.97f), k);
                yield return null;
            }

            _motionOffset = advanced;
            _tilt = -tiltSign * 4f;
            _squash = new Vector2(1.045f, 0.97f);
            onAdvanced?.Invoke();
            yield return new WaitForSeconds(Mathf.Max(0f, holdTime));

            const float returnTime = 0.18f;
            Vector3 from = _motionOffset;
            float fromTilt = _tilt;
            Vector2 fromSquash = _squash;
            for (float t = 0f; t < returnTime; t += Time.deltaTime)
            {
                float k = EaseInOutQuad(t / returnTime);
                _motionOffset = Vector3.Lerp(from, Vector3.zero, k);
                _tilt = Mathf.Lerp(fromTilt, 0f, k);
                _squash = Vector2.Lerp(fromSquash, Vector2.one, k);
                yield return null;
            }

            _motionOffset = Vector3.zero;
            _tilt = 0f;
            _squash = Vector2.one;
            SetSortBoost(0);
            IsPerformingMotion = false;
        }

        /// <summary>
        /// 시전 동작 — 카드가 잠깐 들렸다 내려앉는다. 궁극기 · 버프처럼 대상에게 날아가지 않는 행동이 쓴다.
        /// </summary>
        public IEnumerator Channel(float riseTime = 0.18f, float holdTime = 0.2f, float settleTime = 0.24f)
        {
            IsPerformingMotion = true;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);
            for (float t = 0f; t < riseTime; t += Time.deltaTime)
            {
                _lift = EaseOutCubic(t / riseTime);
                yield return null;
            }
            _lift = 1f;
            yield return new WaitForSeconds(holdTime);
            for (float t = 0f; t < settleTime; t += Time.deltaTime)
            {
                _lift = 1f - EaseInOutQuad(t / settleTime);
                yield return null;
            }
            _lift = 0f;
            SetSortBoost(0);
            IsPerformingMotion = false;
        }

        // ══════════════════════════════════════════════════════
        // 돌진 (하스스톤 하수인 공격)
        // ══════════════════════════════════════════════════════

        private const float ChargeLiftTime = 0.10f;
        private const float ChargeWindupTime = 0.075f;
        private const float ChargeDashTime = 0.13f;
        private const float ChargeHoldTime = 0.075f;
        private const float ChargeReturnTime = 0.24f;

        /// <summary>
        /// 카드가 들려 대상 카드에게 날아가 부딪히고 돌아온다.
        /// 부딪히는 그 프레임에 <paramref name="onImpact"/>가 불린다 — 피해는 거기서 넣는다.
        ///
        /// 하스스톤의 박자를 따랐다: 들기(짧게) → 살짝 뒤로 → <b>가속하며</b> 돌진 → 쿵 → 느리게 복귀.
        /// 돌진 중에는 다른 카드 위로 올라와야 겹침이 읽힌다.
        /// </summary>
        public IEnumerator Charge(Func<Vector3> targetCenter, Action<Vector3, Vector3> onImpact)
        {
            IsPerformingMotion = true;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);

            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 Direction()
            {
                Vector3 d = targetCenter() - home;
                d.z = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : new Vector3(_facing, 0f, 0f);
            }

            // 1) 들기
            for (float t = 0f; t < ChargeLiftTime; t += Time.deltaTime)
            {
                _lift = EaseOutCubic(t / ChargeLiftTime);
                yield return null;
            }
            _lift = 1f;

            // 2) 살짝 뒤로 빼며 기운다
            Vector3 direction = Direction();
            float tiltSign = direction.x >= 0f ? -1f : 1f;
            Vector3 windup = -direction * _cardSize * 0.13f;
            for (float t = 0f; t < ChargeWindupTime; t += Time.deltaTime)
            {
                float k = EaseOutCubic(t / ChargeWindupTime);
                _motionOffset = windup * k;
                _tilt = tiltSign * 6f * k;
                yield return null;
            }

            // 3) 가속 돌진 — 대상 카드 가장자리에서 멈춘다(겹쳐 들어가면 부딪힌 게 아니라 통과로 보인다).
            Vector3 start = _motionOffset;
            for (float t = 0f; t < ChargeDashTime; t += Time.deltaTime)
            {
                float k = t / ChargeDashTime;
                k = k * k * k * k;
                direction = Direction();
                Vector3 contact = targetCenter() - direction * _cardSize * 0.92f - home;
                contact.z = 0f;
                _motionOffset = Vector3.LerpUnclamped(start, contact, k);
                _tilt = Mathf.Lerp(tiltSign * 6f, -tiltSign * 9f, k);
                _squash = new Vector2(1f + 0.10f * k, 1f - 0.065f * k);
                yield return null;
            }
            direction = Direction();
            Vector3 impactPoint = targetCenter() - direction * _cardSize * 0.46f;
            _motionOffset = impactPoint - home - direction * _cardSize * 0.46f;
            _motionOffset.z = 0f;

            // 4) 쿵 — 부딪히는 프레임에 피해와 충돌 연출이 함께 나간다.
            _squash = new Vector2(0.9f, 1.1f);
            _punch = 1f;
            onImpact?.Invoke(impactPoint, direction);
            Vector3 bounce = _motionOffset - direction * _cardSize * 0.12f;
            for (float t = 0f; t < ChargeHoldTime; t += Time.deltaTime)
            {
                float k = EaseOutCubic(t / ChargeHoldTime);
                _motionOffset = Vector3.Lerp(_motionOffset, bounce, k);
                _squash = Vector2.Lerp(new Vector2(0.9f, 1.1f), Vector2.one, k);
                yield return null;
            }

            // 5) 복귀
            Vector3 from = _motionOffset;
            float fromTilt = _tilt;
            for (float t = 0f; t < ChargeReturnTime; t += Time.deltaTime)
            {
                float k = EaseInOutQuad(t / ChargeReturnTime);
                _motionOffset = Vector3.Lerp(from, Vector3.zero, k);
                _tilt = Mathf.Lerp(fromTilt, 0f, k);
                _lift = 1f - k;
                _squash = Vector2.one;
                yield return null;
            }

            _motionOffset = Vector3.zero;
            _tilt = 0f;
            _lift = 0f;
            _squash = Vector2.one;
            SetSortBoost(0);
            IsPerformingMotion = false;
        }

        /// <summary>
        /// 사바흐가 아래로 녹아 그림자 상태로 대상 앞까지 이동한 뒤 다시 솟는다.
        /// 실제 이동은 카드 루트가 맡아 HUD·원화·정렬이 서로 어긋나지 않게 한다.
        /// </summary>
        public IEnumerator ShadowApproach(Func<Vector3> targetCenter)
        {
            IsPerformingMotion = true;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);
            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;

            const float meltTime = 0.16f;
            for (float age = 0f; age < meltTime; age += Time.deltaTime)
            {
                float t = EaseInOutQuad(age / meltTime);
                _alpha = 1f - t;
                _squash = new Vector2(1f + t * 0.10f, 1f - t * 0.66f);
                _lift = -0.20f * t;
                yield return null;
            }

            Vector3 from = _motionOffset;
            const float travelTime = 0.24f;
            for (float age = 0f; age < travelTime; age += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, age / travelTime);
                Vector3 direction = targetCenter() - home;
                direction.z = 0f;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right * _facing;
                Vector3 destination = targetCenter() - direction * _cardSize * 0.54f - home;
                destination.z = 0f;
                _motionOffset = Vector3.Lerp(from, destination, t);
                _alpha = 0f;
                yield return null;
            }

            const float reformTime = 0.14f;
            for (float age = 0f; age < reformTime; age += Time.deltaTime)
            {
                float t = EaseOutCubic(age / reformTime);
                _alpha = t;
                _squash = new Vector2(1f - t * 0.04f, Mathf.Lerp(0.32f, 1f, t));
                _lift = Mathf.Lerp(-0.14f, 0.18f, t);
                yield return null;
            }
            _alpha = 1f;
            _squash = Vector2.one;
            _lift = 0.18f;
        }

        /// <summary>대상 앞에서 다시 녹아 원래 칸으로 미끄러진 뒤 복원한다.</summary>
        public IEnumerator ShadowReturn()
        {
            const float meltTime = 0.11f;
            for (float age = 0f; age < meltTime; age += Time.deltaTime)
            {
                float t = EaseInOutQuad(age / meltTime);
                _alpha = 1f - t;
                _squash = new Vector2(1f + t * 0.08f, 1f - t * 0.62f);
                _lift = Mathf.Lerp(0.18f, -0.18f, t);
                yield return null;
            }

            Vector3 from = _motionOffset;
            const float travelTime = 0.20f;
            for (float age = 0f; age < travelTime; age += Time.deltaTime)
            {
                _motionOffset = Vector3.Lerp(from, Vector3.zero,
                    Mathf.SmoothStep(0f, 1f, age / travelTime));
                _alpha = 0f;
                yield return null;
            }

            const float reformTime = 0.15f;
            for (float age = 0f; age < reformTime; age += Time.deltaTime)
            {
                float t = EaseOutCubic(age / reformTime);
                _alpha = t;
                _squash = new Vector2(1f - t * 0.03f, Mathf.Lerp(0.34f, 1f, t));
                _lift = Mathf.Lerp(-0.12f, 0f, t);
                yield return null;
            }

            _motionOffset = Vector3.zero;
            _alpha = 1f;
            _tilt = 0f;
            _lift = 0f;
            _squash = Vector2.one;
            SetSortBoost(0);
            IsPerformingMotion = false;
        }

        /// <summary>초월기에서 자리를 옮기지 않고 그림자로만 가라앉아 사라진다.</summary>
        public IEnumerator ShadowVanish()
        {
            IsPerformingMotion = true;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);
            const float duration = 0.20f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = EaseInOutQuad(age / duration);
                _alpha = 1f - t;
                _squash = new Vector2(1f + t * 0.12f, 1f - t * 0.70f);
                _lift = -0.20f * t;
                yield return null;
            }
            _alpha = 0f;
        }

        /// <summary>초월기가 끝난 뒤 원래 칸에서 그림자 위로 다시 조립된다.</summary>
        public IEnumerator ShadowReappear()
        {
            const float duration = 0.22f;
            _motionOffset = Vector3.zero;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = EaseOutCubic(age / duration);
                _alpha = t;
                _squash = new Vector2(1f - t * 0.04f, Mathf.Lerp(0.28f, 1f, t));
                _lift = Mathf.Lerp(-0.16f, 0f, t);
                yield return null;
            }
            _alpha = 1f;
            _lift = 0f;
            _squash = Vector2.one;
            SetSortBoost(0);
            IsPerformingMotion = false;
        }

        // ══════════════════════════════════════════════════════
        // 등장
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 발밑 Hovl 원소 Flash와 엣지 스윕이 열린 뒤, 카드가 가속해 내려앉는다.
        /// 여러 장이 한꺼번에 서면 <paramref name="delay"/>를 조금씩 벌려 차례로 나타낸다.
        /// </summary>
        public void PlayEntry(float delay = 0f)
        {
            _entryDelay = Mathf.Max(0f, delay);
            _entryAge = 0f;
            _landingAge = -1f;
            _entryFxStarted = false;
            _alpha = 0f;
            SetSortBoost(VfxSpace.FlyingCardSortingOrder);
            ApplyTransforms();
        }

        private void AdvanceEntry(float dt)
        {
            if (_entryAge < 0f) return;

            if (_entryDelay > 0f)
            {
                _entryDelay -= dt;
                _alpha = 0f;
                return;
            }

            if (!_entryFxStarted)
            {
                _entryFxStarted = true;
                OnEntryStarted();
            }

            _entryAge += dt;
            // 8배속에서도 인장이 열린 뒤 카드가 내려앉는 핵심 자세를 세 프레임 이상 남긴다.
            float t = Mathf.Clamp01(_entryAge / VfxTiming.AtLeastFrames(EntryDropTime, 3));
            float reveal = EaseOutCubic(Mathf.Clamp01(t / 0.36f));
            _alpha = reveal;
            // 끝으로 갈수록 빨라지는 낙하가 착지 프레임에 무게를 모은다.
            _lift = (1f - t * t) * 0.88f;
            _squash = new Vector2(Mathf.Lerp(0.76f, 1f, reveal), Mathf.Lerp(1.12f, 1f, reveal));
            if (t < 1f) return;

            _entryAge = -1f;
            _landingAge = 0f;
            _lift = 0f;
            _alpha = 1f;
            SetSortBoost(0);
            OnLanded();
        }

        /// <summary>등장 시작 — 공용 모듈이 Hovl 발사광과 단일 엣지 스윕을 그린다.</summary>
        private void OnEntryStarted()
            => EntryFx.Begin(_unit, transform.position, _cardSize, _layerId, _baseOrder);

        /// <summary>복원이 끝나는 순간 — Hovl 착탄광과 바닥 먼지가 착지 프레임에 겹친다.</summary>
        private void OnLanded()
            => EntryFx.Land(_unit, CenterWorld, _cardSize, _layerId, _baseOrder);

        // ══════════════════════════════════════════════════════
        // 매 프레임 움직임
        // ══════════════════════════════════════════════════════

        private void LateUpdate()
        {
            if (_body == null || !_body.gameObject.activeSelf) return;
            float dt = Time.deltaTime;
            AdvanceEntry(dt);
            AdvanceReaction(dt);
            ApplyTransforms();
            ApplyAuras();
            UpdateHudLod();
            AnimateHud();
            AdvanceSupportHudPulse(Time.unscaledDeltaTime);
            if (_resourcePulseAge >= 0f)
            {
                _resourcePulseAge += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_resourcePulseAge / .34f);
                float pulse = 1f + .35f * _resourcePulseStrength * Mathf.Sin(t * Mathf.PI);
                _resourceBadge.rectTransform.localScale = Vector3.one * pulse;
                _resourceBadgeText.transform.localScale = Vector3.one * pulse;
                if (t >= 1f) _resourcePulseAge = -1f;
            }
        }

        private void AdvanceSupportHudPulse(float dt)
        {
            if (_supportHudPulseAge < 0f || _supportHudPulseTarget == null) return;
            _supportHudPulseAge += Mathf.Max(0f, dt);
            float t = Mathf.Clamp01(_supportHudPulseAge / SupportHudPulseDuration);
            // 1.0 → 1.35 → 1.0. 뒤쪽 복귀를 길게 둬 작은 카드에서도 한 번의 팝으로 읽힌다.
            float wave = Mathf.Sin(t * Mathf.PI);
            _supportHudPulseTarget.localScale = Vector3.one * (1f + 0.35f * wave);
            if (_supportHudPulseImage != null)
                _supportHudPulseImage.color = Color.Lerp(Color.white, _supportHudPulseBaseColor, t);
            if (t < 1f) return;
            _supportHudPulseTarget.localScale = Vector3.one;
            if (_supportHudPulseImage != null) _supportHudPulseImage.color = _supportHudPulseBaseColor;
            _supportHudPulseAge = -1f;
            _supportHudPulseTarget = null;
            _supportHudPulseImage = null;
        }

        /// <summary>
        /// 카메라가 여러 카드를 담느라 멀어지면 화면 픽셀 기준으로 한 단계 큰 HUD를 쓴다.
        /// 진입/이탈 문턱을 다르게 둬 카메라 보간 중 레이아웃이 깜빡이지 않게 한다.
        /// </summary>
        private void UpdateHudLod()
        {
            if (!_combatHud || Time.frameCount % 10 != 0) return;
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 half = _body.right * (_cardSize * 0.5f);
            float screenWidth = Vector2.Distance(
                camera.WorldToScreenPoint(CenterWorld - half),
                camera.WorldToScreenPoint(CenterWorld + half));
            bool compact = _compactHud ? screenWidth < 300f : screenWidth < 260f;
            float pixelScale = compact ? Mathf.Clamp(260f / Mathf.Max(1f, screenWidth), 1f, 1.14f) : 1f;
            bool scaleChanged = Mathf.Abs(pixelScale - _hudPixelScale) >= 0.025f;
            _hudPixelScale = pixelScale;
            ApplyHudLod(compact, scaleChanged);
        }

        private void ApplyHudLod(bool compact, bool force = false)
        {
            if (!force && _hudLodInitialized && _compactHud == compact) return;
            _hudLodInitialized = true;
            _compactHud = compact;
            _visibleDotSlots = compact ? CompactStatusIconCount + 1 : MaxDots;

            float size = compact ? 8.6f * _hudPixelScale : DotSize;
            float gap = compact ? size + 1.05f : DotGap;
            float top = compact ? 89.4f : DotTop;
            float countSize = compact ? 5.5f * _hudPixelScale : DotCountSize;
            float iconRatio = compact ? 0.74f : DotIconRatio;
            for (int i = 0; i < MaxDots; i++)
            {
                Place(_dotBacks[i].rectTransform, 6f + i * gap, top - GlassTop, size, size);
                float inset = size * (1f - iconRatio) * 0.5f;
                UIBuild.Stretch(_dots[i].rectTransform, inset, inset);
                _dotArrows[i].rectTransform.sizeDelta = new Vector2(size * 0.43f, size * 0.43f);
                _dotArrows[i].rectTransform.anchoredPosition = new Vector2(size * 0.08f, size * 0.08f);
                _dotCountBacks[i].rectTransform.sizeDelta = new Vector2(countSize, countSize);
                _dotCountBacks[i].rectTransform.anchoredPosition = new Vector2(countSize * 0.24f, -countSize * 0.18f);
            }

            if (_cardSize > 0f)
            {
                LayOutNameLabel(_cardSize, compact);
                LayOutHpLabel(_cardSize, compact);
                LayOutResourceBadgeText(_cardSize);
            }
            if (_unit != null && _combatHud) RefreshStatusDots();
        }

        /// <summary>
        /// 수면·회전 링은 정보값이 아니라 읽기 보조 연출이므로 전투 배속과 무관한 실시간으로 움직인다.
        /// 수면은 원 안에서 1px 남짓만 기울어 작은 카드에서도 액체처럼 보이고 글리프를 방해하지 않는다.
        /// </summary>
        private void AnimateHud()
        {
            if (!_combatHud || _ultRoot == null || !_ultRoot.gameObject.activeInHierarchy) return;

            float time = Time.unscaledTime;
            float innerHeight = RingSize - (RingEdge + 0.5f) * 2f;
            float y = Mathf.Lerp(-0.8f, innerHeight + 0.8f, _ultFillAmount);
            float rearAmount = Mathf.Clamp01(_ultFillAmount + Mathf.Sin(time * 1.85f + 0.8f) * 0.028f);
            _ultRearFill.fillAmount = rearAmount;
            _ultRearFill.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(time * 1.35f) * 0.72f, 0f);
            float rearY = Mathf.Lerp(-0.8f, innerHeight + 0.8f, rearAmount);
            _ultRearWave.enabled = rearAmount > 0.015f && rearAmount < 0.995f;
            _ultRearWave.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(time * 1.35f) * 0.72f,
                rearY + Mathf.Sin(time * 2.1f + 1.3f) * 0.28f);
            _ultRearWave.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                -Mathf.Sin(time * 1.7f + 0.6f) * 2.8f);

            _ultWave.enabled = _ultFillAmount > 0.015f && _ultFillAmount < 0.995f;
            _ultWave.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(time * 2.45f + 0.4f) * 0.46f,
                y + Mathf.Sin(time * 3.4f) * 0.34f);
            _ultWave.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2.25f) * 2.2f);

            if (!_ultReady) return;
            _ultReadyRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 12f);
            float pulse = 1f + 0.025f * (0.5f + 0.5f * Mathf.Sin(time * 6.6f));
            _ultReadyRing.rectTransform.localScale = Vector3.one * pulse;
            _ultReadyGlow.rectTransform.localScale = Vector3.one * (1f + (pulse - 1f) * 1.8f);
        }

        private void AdvanceReaction(float dt)
        {
            if (dt <= 0f) return;

            float airborneTarget = _unit != null && _unit.HasStatusKey(Effects.Negative.ControlStatuses.AirborneKey)
                ? AirborneLiftHeight : 0f;
            _airborneLift = Mathf.MoveTowards(_airborneLift, airborneTarget, dt * 0.8f);

            _flash = Mathf.Max(0f, _flash - dt / FlashTime);
            _punch = Mathf.Max(0f, _punch - dt / PunchTime);
            _recoil = Mathf.Max(0f, _recoil - dt / PunchTime);
            _hitSquash = Mathf.Max(0f, _hitSquash - dt / (PunchTime * 0.8f));
            _shake = Mathf.Max(0f, _shake - dt / ShakeTime);
            _lunge = Mathf.Max(0f, _lunge - dt / LungeTime);
            _vfxKick = Mathf.Max(0f, _vfxKick - dt / VfxKickTime);
            if (_vfxKick <= 0f) _vfxKickStrength = 0f;
            if (_vfxLightDuration > 0f)
            {
                _vfxLightAge += dt;
                if (_vfxLightAge >= _vfxLightDuration)
                {
                    _vfxLightAge = 0f;
                    _vfxLightDuration = 0f;
                    _vfxLightPeak = 0f;
                }
            }

            if (_landingAge >= 0f)
            {
                _landingAge += dt;
                float k = Mathf.Clamp01(_landingAge / LandingTime);
                // 찌그러졌다가 한 번 튕겨 돌아온다.
                float spring = Mathf.Exp(-7f * k) * Mathf.Cos(k * Mathf.PI * 2f);
                _squash = new Vector2(1f + 0.055f * spring, 1f - 0.055f * spring);
                if (k >= 1f)
                {
                    _landingAge = -1f;
                    _squash = Vector2.one;
                }
            }
        }

        private void ApplyTransforms()
        {
            if (_body == null) return;
            float s = _cardSize;

            // 루트 — 돌진 등 월드 이동. 칸의 배율을 되돌려 월드 거리 그대로 움직인다.
            transform.localPosition = transform.parent != null
                ? transform.parent.InverseTransformVector(_motionOffset)
                : _motionOffset;

            // 몸체 — 들림 · 기울기 · 찌그러짐 · 피격 반응
            float lift = Mathf.Max(0f, _lift);
            float scale = (1f + 0.10f * Mathf.Min(1f, lift) + PunchAmount * _punch) * (1f + 0.12f * Mathf.Max(0f, lift - 1f));
            Vector3 offset = new(0f, s * (0.09f * lift + _airborneLift), 0f);
            offset += new Vector3(-_facing * RecoilDistance * s * _recoil, 0f, 0f);
            float lungeShape = Mathf.Sin(Mathf.PI * (1f - _lunge));
            offset += _lungeDirection * LungeDistance * s * lungeShape * (_lunge > 0f ? 1f : 0f);
            float kickShape = Mathf.Sin(Mathf.PI * (1f - _vfxKick));
            offset += _vfxKickDirection * VfxKickDistance * s * kickShape * _vfxKickStrength;
            if (_shake > 0f)
            {
                float elapsed = (1f - _shake) * ShakeTime;
                float amplitude = ShakeDistance * s * _shakeStrength * _shake * _shake;
                offset.x += Mathf.Sin((elapsed + _shakeSeed) * ShakeFrequency * Mathf.PI * 2f) * amplitude;
                offset.y += Mathf.Sin((elapsed + _shakeSeed) * ShakeFrequency * 0.72f * Mathf.PI * 2f + 1.3f) * amplitude * 0.55f;
            }

            _body.localPosition = offset;
            _body.localRotation = Quaternion.Euler(0f, 0f, _tilt);
            float hitWidth = 1f - 0.075f * _hitSquash;
            float hitHeight = 1f + 0.060f * _hitSquash;
            _body.localScale = new Vector3(scale * _squash.x * hitWidth, scale * _squash.y * hitHeight, 1f);

            // 돌진의 기울기는 원화의 물성만 말한다. 정보판까지 돌아가면 이름·HP를 읽기 어려우므로
            // 몸체 회전을 같은 양만큼 상쇄하고, 이동·들림·스케일 반동은 그대로 따라간다.
            Quaternion hudCounterRotation = Quaternion.Euler(0f, 0f, -_tilt);
            if (_orbCanvas != null) _orbCanvas.transform.localRotation = hudCounterRotation;
            if (_canvas != null) _canvas.transform.localRotation = hudCounterRotation;
            if (_nameLabel != null) _nameLabel.transform.localRotation = hudCounterRotation;
            if (_hpLabel != null) _hpLabel.transform.localRotation = hudCounterRotation;

            // 그림자 — 바닥에 남아 높이를 말한다. 들릴수록 넓고 옅어진다.
            float spread = 1f + 0.35f * Mathf.Min(2f, lift);
            _shadow.transform.localScale = new Vector3(s * 0.94f * spread, s * 0.15f * spread, 1f);
            _shadow.color = new Color(GroundShadow.r, GroundShadow.g, GroundShadow.b,
                GroundShadow.a * _alpha / spread);

            // 별도 사각 면을 만들지 않고 원화 자체만 잠깐 따뜻하게 변조한다.
            // 스프라이트 알파를 그대로 쓰므로 어두운 배경에서도 피격 피드백이 실루엣 밖으로 새지 않는다.
            if (_portrait != null)
            {
                float warmth = 0.18f * _flash * _flash;
                Color portraitColor = _portrait.color;
                portraitColor.r = 1f;
                portraitColor.g = 1f - warmth * 0.28f;
                portraitColor.b = 1f - warmth;
                if (_vfxLightDuration > 0f && _vfxLightPeak > 0f)
                {
                    float phase = Mathf.Clamp01(_vfxLightAge / _vfxLightDuration);
                    float light = Mathf.Sin(phase * Mathf.PI) * _vfxLightPeak;
                    Color reflected = new(1f,
                        Mathf.Lerp(0.96f, 1f, _vfxLightColor.g),
                        Mathf.Lerp(0.88f, 1f, _vfxLightColor.b), 1f);
                    portraitColor = Color.Lerp(portraitColor, reflected, light);
                }
                _portrait.color = portraitColor;
            }

            ApplyAlpha();
        }

        private float _appliedAlpha = -1f;

        private void ApplyAlpha()
        {
            float alive = _unit == null || (_unit.isActive && _unit.HpCurr > 0) ? 1f : InactiveAlpha;
            float a = _alpha * alive;
            if (Mathf.Approximately(a, _appliedAlpha)) return;
            _appliedAlpha = a;

            SetAlpha(_portrait, a);
            _orbGroup.alpha = a;
            _group.alpha = a;
            Color nameColor = _nameLabel.color;
            nameColor.a = a;
            _nameLabel.color = nameColor;
            Color hpColor = _hpLabel.color;
            hpColor.a = a;
            _hpLabel.color = hpColor;
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }

        /// <summary>
        /// 행동 중 표시(하단 글라스 바깥 림)와 상태 오라.
        /// </summary>
        private void ApplyAuras()
        {
            if (_unit == null || !_combatHud)
            {
                _actingOutline.enabled = false;
                _auraGlow.color = Color.clear;
                return;
            }

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            bool acting = scheduler != null && ReferenceEquals(scheduler.ActingUnit, _unit);
            _actingOutline.enabled = acting;

            Color glow = Color.clear;
            foreach (UnitStatus status in _unit.ActiveStatuses)
            {
                if (status?.Effects == null) continue;
                foreach (UnitStatus.EffectInstance instance in status.Effects)
                {
                    if (instance?.EffectObject is not ICardAura aura) continue;
                    if (aura.AuraStyle == CardAuraStyle.Glow && glow.a <= 0f) glow = aura.AuraColor;
                }
            }

            float auraPulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3.2f);
            _auraGlow.color = glow.a > 0f ? new Color(glow.r, glow.g, glow.b, glow.a * auraPulse * _alpha) : Color.clear;
        }

        private void SetSortBoost(int boost)
        {
            if (_sortBoost == boost) return;
            _sortBoost = boost;
            ApplySorting();
        }

        private void ResetMotion()
        {
            _flash = _punch = _recoil = _shake = _lunge = 0f;
            _hitSquash = 0f;
            _vfxLightPeak = 0f;
            _vfxLightAge = 0f;
            _vfxLightDuration = 0f;
            _airborneLift = 0f;
            _motionOffset = Vector3.zero;
            _lift = 0f;
            _tilt = 0f;
            _squash = Vector2.one;
            _alpha = 1f;
            _appliedAlpha = -1f;
            _entryAge = -1f;
            _entryDelay = -1f;
            _landingAge = -1f;
            _entryFxStarted = false;
            _lastHp = -1;
            IsPerformingMotion = false;
            SetSortBoost(0);
            ApplyTransforms();
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseInOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        }

        // ── 조립 헬퍼 ────────────────────────────────────────────────

        /// <summary>
        /// 임포트 설정에 기대지 않고 생성 PNG를 런타임 스프라이트로 읽는다.
        /// </summary>
        private static Sprite HudSprite(string name, Sprite fallback)
        {
            ref Sprite cache = ref _orbGlassSprite;
            if (name == "element_geo_glyph") cache = ref _geoGlyphSprite;
            else if (name == "element_void_glyph") cache = ref _voidGlyphSprite;
            else if (name == "element_neutral_glyph") cache = ref _neutralGlyphSprite;

            if (cache != null) return cache;
            Texture2D texture = Resources.Load<Texture2D>("UI/CardHud/" + name);
            if (texture == null) return fallback;

            cache = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            cache.name = "CardHud_" + name;
            cache.hideFlags = HideFlags.HideAndDontSave;
            return cache;
        }

        /// <summary>원소별 오브 글리프. 무원소 공허 적도 옛 셰브론 대신 공허 표식을 쓴다.</summary>
        private static Sprite UltimateGlyph(Unit unit, BaseEnums.UnitElement element)
        {
            if (element == BaseEnums.UnitElement.None && unit?.HasUnitTag("Void") == true)
                return HudSprite("element_void_glyph", StatusIcons.Get(StatusGlyph.Mark));

            return element switch
            {
                BaseEnums.UnitElement.Geo => HudSprite("element_geo_glyph", VfxArt.Diamond),
                BaseEnums.UnitElement.Pyro => StatusIcons.Get(StatusGlyph.Burn),
                BaseEnums.UnitElement.Hydro => StatusIcons.Get(StatusGlyph.Bleed),
                BaseEnums.UnitElement.Dendro => StatusIcons.Get(StatusGlyph.Thorns),
                BaseEnums.UnitElement.Anemo => StatusIcons.Get(StatusGlyph.Speed),
                BaseEnums.UnitElement.Electro => StatusIcons.Get(StatusGlyph.Crit),
                BaseEnums.UnitElement.Cryo => StatusIcons.Get(StatusGlyph.Freeze),
                BaseEnums.UnitElement.Void => HudSprite("element_void_glyph", StatusIcons.Get(StatusGlyph.Mark)),
                _ => HudSprite("element_neutral_glyph", StatusIcons.Get(StatusGlyph.Focus)),
            };
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.Solid(Color.white);
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>트랙 안에서 [from, to] 구간만 차지하게 만든다. 보호막을 체력 오른쪽에서 시작시키려고 앵커를 쓴다.</summary>
        private static void SetSpan(RectTransform rect, float from, float to)
        {
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(Mathf.Max(from, to));
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
