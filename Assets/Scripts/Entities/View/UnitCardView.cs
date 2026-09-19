using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Negative;
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
    /// 우하단 파티 카드를 없애면서 그쪽이 들고 있던 정보가 전부 이리로 왔다.
    /// 한 장에 담기는 것:
    ///   · 컷코너 카드 프레임 — 초상화가 그 위에 선다(초상화는 여전히 SpriteRenderer다)
    ///   · 이름 띠
    ///   · 궁극기 게이지 — 우상단에 걸치는 원형. 테두리는 고정이고 안쪽이 아래에서
    ///     위로 차오른다(스타레일식). 충전 중·완료·예약이 모두 같은 색이다.
    ///   · 체력 바 — 그 <b>오른쪽에 이어 붙는</b> 방어막(하늘색)
    ///   · 행동 게이지 — 체력 바 절반 높이
    ///   · 상태 아이콘 — 이로운 것 초록, 해로운 것 적색(<see cref="StatusIcons"/>)
    ///
    /// 프레임은 SpriteRenderer, 나머지는 월드 스페이스 캔버스다.
    /// 초상화가 SpriteRenderer라 프레임은 그보다 뒤에(정렬 순서가 낮게) 있어야 하므로
    /// 전부를 캔버스 하나로 뭉뚱그릴 수 없다.
    ///
    /// 전투 연출은 <b>카드 자체가 한다</b>. 유닛 스프라이트가 칼을 휘두르는 대신,
    /// 쏠 때 카드가 앞으로 찍고 맞을 때 번쩍이며 뒤로 밀린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitCardView : MonoBehaviour
    {
        /// <summary>칸 한 변 대비 카드 한 변의 비율.</summary>
        public const float CardRatio = 0.96f;

        /// <summary>캔버스 안의 작업 단위. 카드 한 변이 100이 되도록 잡는다.</summary>
        private const float CanvasWidth = 100f;

        /// <summary>카드(100) + 그 아래 바 영역까지의 높이.</summary>
        private const float CanvasHeight = 130f;

        /// <summary>
        /// 상태 아이콘 칸 수. 마지막 칸은 넘친 수(+N)를 적는 자리로도 쓴다.
        /// 다섯 칸에서 조용히 잘리면 여섯 번째 디버프가 있는지조차 알 수 없었다.
        /// </summary>
        private const int MaxDots = 6;

        // 캔버스 좌표(좌상단 기준). 디자인 캔버스의 퍼센트를 그대로 옮겼다.
        // 이름 받침은 한 줄이다. 체력 수치는 바가 대신 말하므로 적지 않는다.
        private const float NameBandTop = 78f;
        private const float NameBandHeight = 20f;
        private const float RingSize = 23f;
        private const float RingOverhang = 2f;

        /// <summary>테두리 안쪽으로 물이 들어갈 여백(캔버스 단위).</summary>
        private const float RingEdge = 1.6f;

        /// <summary>
        /// 아직 차지 않은 부분. 스타레일처럼 <b>흐린 흰색</b>이다.
        /// 예전에는 여기가 카드보다 더 어두워서, 어두운 그릇에 어두운 물이 담긴 꼴이라
        /// 수위가 카드에서 떨어져 보이지 않았다.
        /// </summary>
        private static readonly Color RingWell = new(0.635f, 0.655f, 0.686f, 1f);

        /// <summary>스택 칸을 가르는 선. 물이 찼든 안 찼든 같은 색으로 보이게 어둡다.</summary>
        private static readonly Color RingTick = new(0.067f, 0.071f, 0.082f, 1f);

        /// <summary>상태 아이콘 받침. 초상화 위에서도 그림이 뜨도록 거의 검다.</summary>
        private static readonly Color DotBacking = new(0.047f, 0.051f, 0.059f, 0.84f);

        /// <summary>충전 중인 궁극기 표식. 물 위에 흐리게 깔린다.</summary>
        private static readonly Color MarkIdle = new(1f, 1f, 1f, 0.26f);

        /// <summary>
        /// 칸 나누기를 그려 줄 스택 수의 상한. 이보다 많으면 칸이 선 두께만큼도 안 남아
        /// 줄무늬로만 보이므로 그냥 연속 게이지로 둔다.
        /// </summary>
        private const int MaxRingTicks = 8;

        /// <summary>칸 나누기 선의 두께(캔버스 단위).</summary>
        private const float RingTickThickness = 0.7f;

        /// <summary>
        /// 체력 바를 끊어 그릴 칸 수의 상한. 가로 바는 링보다 길어 더 잘게 나눠도 읽힌다
        /// (오시리스의 14칸이 이 안에 들어온다). 넘어가면 그냥 연속 게이지로 둔다.
        /// </summary>
        private const int MaxHpTicks = 16;

        /// <summary>체력 바 칸 나누기 선의 두께(캔버스 단위).</summary>
        private const float HpTickThickness = 0.9f;
        private const float HpTop = 104.7f;
        private const float HpHeight = 5.7f;
        private const float ActionTop = 112.2f;
        private const float ActionHeight = 2.9f;
        private const float DotTop = 117.0f;

        /// <summary>
        /// 상태 아이콘 한 변. 8배속에서 읽으려면 12로는 모자랐다.
        /// 받침까지 합쳐 카드 한 변의 15%를 준다.
        /// </summary>
        private const float DotSize = 15f;
        private const float DotGap = 16.8f;

        /// <summary>아이콘 받침 안에서 그림이 차지하는 비율. 나머지는 여백이다.</summary>
        private const float DotIconRatio = 0.74f;

        /// <summary>중첩 수 글자의 한 변(캔버스 단위).</summary>
        private const float DotCountSize = 8.5f;

        /// <summary>카드 위로 걸치는 HUD(궁극기 게이지)의 높이. 카메라가 이만큼 위를 비운다.</summary>
        public static float HudAbove(float cardSize) => cardSize * RingOverhang / CanvasWidth;

        /// <summary>카드 아래로 붙는 HUD(체력 · 행동 · 상태 아이콘)의 높이. 카메라가 이만큼 아래를 비운다.</summary>
        public static float HudBelow(float cardSize) => cardSize * (DotTop + DotSize - CanvasWidth) / CanvasWidth;

        private Unit _unit;
        private bool _combatHud;
        private float _cardSize;

        private SpriteRenderer _frame;
        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _actingOutline;
        private TextMeshPro _nameLabel;
        private MeshRenderer _nameRenderer;
        private readonly Image[] _reagentBars = new Image[3];
        private readonly GameObject[] _reagentTracks = new GameObject[3];
        private RectTransform _ultRoot;
        private Image _ultFill;
        private Image _ultOutline;
        private RectTransform _ultTicks;

        /// <summary>칸 나누기를 다시 그릴지 판단하는 값. 스택형이 아니면 0이다.</summary>
        private int _ringTickCount = -1;

        /// <summary>링 색을 다시 계산할지 판단할 때 쓰는 직전 원소 이름.</summary>
        private string _ringElementName;

        /// <summary>충전 중일 때의 링 색. 다 차면 금색으로 바뀌므로 되돌릴 값을 들고 있는다.</summary>
        private Color _ringColor = UITheme.Mana;
        private Image _hpFill;
        private Image _shieldFill;
        private RectTransform _hpTicks;

        /// <summary>체력 바 칸 나누기를 다시 그릴지 판단하는 값. 나누지 않으면 0이다.</summary>
        private int _hpTickCount = -1;
        private Image _actionFill;
        private readonly Image[] _dots = new Image[MaxDots];
        private readonly Image[] _dotBacks = new Image[MaxDots];
        private readonly Image[] _dotCounts = new Image[MaxDots];

        /// <summary>한 칸에 모은 상태와 그 중첩 수. 매 프레임 다시 채우므로 새로 만들지 않는다.</summary>
        private readonly List<UnitStatus> _dotStatuses = new();
        private readonly List<int> _dotStackCounts = new();

        /// <summary>궁극기 게이지 안의 표식. 이 자리가 충전량임을 알린다.</summary>
        private Image _ultMark;
        private Image _hitFlash;

        // ── 타격 · 시전 반응 ─────────────────────────────────────────
        private const float FlashTime = 0.13f;   // 맞았을 때 하얗게 번쩍이는 시간
        private const float PunchTime = 0.18f;   // 카드가 커졌다 돌아오는 시간
        private const float LungeTime = 0.22f;   // 쏠 때 앞으로 찍는 시간
        private const float PunchAmount = 0.07f; // 커지는 비율
        private const float LungeDistance = 0.9f;
        private const float RecoilDistance = 0.55f;

        /// <summary>물리 타격에 카드가 떠는 시간. 길면 '맞았다'가 아니라 '고장났다'로 보인다.</summary>
        private const float ShakeTime = 0.20f;

        /// <summary>떨림의 최대 진폭(월드 단위). 찍기(0.9)보다 작아야 반응이 겹쳐도 읽힌다.</summary>
        private const float ShakeDistance = 0.62f;

        /// <summary>가로 떨림의 주파수(Hz). 세로는 이보다 느려 한 방향으로만 떨지 않는다.</summary>
        private const float ShakeFrequency = 32f;
        private const float AirborneRiseTime = 0.16f;
        private const float AirborneFallTime = 0.22f;

        private Transform _portrait;
        private Vector3 _portraitBaseScale = Vector3.one;
        private float _flash;
        private float _punch;
        private float _lunge;
        private float _recoil;

        /// <summary>물리 타격의 떨림. 1에서 0으로 줄며, 남은 값이 곧 진폭이다.</summary>
        private float _shake;
        private float _shakeStrength;
        private float _shakeSeed;
        private float _airborneLift;
        private int _facing = 1;
        private int _lastHp = -1;

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

        /// <summary>
        /// 카드가 함께 흔들 초상화 렌더러를 알려 준다.
        /// 초상화는 칸(Cell)의 자식이라 카드 루트를 흔들어도 따라오지 않는다.
        /// </summary>
        public void BindPortrait(SpriteRenderer portrait)
        {
            _portrait = portrait != null ? portrait.transform : null;
            RefreshPortraitBase();
        }

        /// <summary>초상화 배율이 다시 잡혔을 때 기준값을 갱신한다.</summary>
        public void RefreshPortraitBase()
        {
            if (_portrait != null) _portraitBaseScale = _portrait.localScale;
        }

        private void Build(float cardSize)
        {
            RecoverReagentReferences();
            if (_frame != null && _canvas != null && _hpFill != null && _actionFill != null && ReagentsBuilt())
            {
                Resize(cardSize);
                return;
            }

            _cardSize = cardSize;

            // ── 카드 프레임 ──
            // 컷코너를 카드 한 변의 9%로 잡는다. 스프라이트는 100 px/unit이므로
            // 잘라낼 크기를 픽셀로 환산해 만든 뒤 9-슬라이스로 늘린다.
            var frameObject = new GameObject("Frame", typeof(SpriteRenderer));
            frameObject.transform.SetParent(transform, false);
            _frame = frameObject.GetComponent<SpriteRenderer>();
            _frame.sprite = UIShapes.CutCornerOutline(
                Mathf.RoundToInt(cardSize * 0.09f * 100f),
                new Color(0.20f, 0.43f, 0.46f, 0.18f), 1);
            _frame.drawMode = SpriteDrawMode.Sliced;
            _frame.size = new Vector2(cardSize, cardSize);

            // ── HUD 캔버스 ──
            var canvasObject = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            // TextMeshPro의 SDF 셰이더는 글자 크기 비율을 UV2(TexCoord1)로 받는다.
            // 코드로 붙인 Canvas는 이 채널을 버리기 때문에 글자가 통째로 투명해진다
            // (Image는 멀쩡히 나오는데 글자만 안 보이면 십중팔구 이것이다).
            _canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;

            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.pivot = new Vector2(0.5f, 1f);
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            canvasRect.localScale = Vector3.one * (cardSize / CanvasWidth);
            canvasRect.localPosition = new Vector3(0f, cardSize * 0.5f, -0.01f);

            _group = UIBuild.Group(canvasObject);
            _group.blocksRaycasts = false;

            Transform root = canvasObject.transform;

            // 초상화가 카드 면을 거의 덮으므로 테두리는 초상화 '위'에 그린다.
            // 흰 배경 초상화에서도 카드 형태(컷코너)가 보이게 하기 위한 것이다.
            // 원화 뒤를 막던 불투명 정사각 카드 면은 두지 않는다. 캐릭터는 무대 위에 직접 서고,
            // 정보는 아래의 작은 아이보리 받침 하나에만 모인다.
            Image baseShadow = NewImage(root, "BaseShadow", new Color(0.05f, 0.16f, 0.17f, 0.14f));
            baseShadow.sprite = UIShapes.Disc(96, Color.white);
            Place(baseShadow.rectTransform, 10f, 91f, 80f, 11f);

            // 지금 행동 중인 유닛을 알리는 앰버 테두리.
            _actingOutline = NewImage(root, "ActingOutline", Color.white);
            _actingOutline.sprite = UIShapes.Disc(96, UITheme.Accent, 0.78f);
            _actingOutline.type = Image.Type.Simple;
            Place(_actingOutline.rectTransform, 8f, 88f, 84f, 15f);
            _actingOutline.enabled = false;

            // 글라스 받침. 초상화 위에 얹히므로 HUD 글라스보다 조금 더 불투명하게 잡아 글자가 선다.
            Image nameBand = UIBuild.Glass("NameBand", root, 7, 0.74f);
            nameBand.raycastTarget = false;
            Place(nameBand.rectTransform, 0f, NameBandTop, CanvasWidth, NameBandHeight);

            Image nameAccent = UIBuild.Solid("NameAccent", root, UITheme.Accent);
            Place(nameAccent.rectTransform, 7f, NameBandTop + 4f, 2f, 12f);

            // 이름표만은 캔버스 밖의 월드 TextMeshPro다.
            // TextMeshProUGUI는 이 월드 스페이스 캔버스에서 아예 그려지지 않았다
            // (같은 캔버스의 Image는 멀쩡하고, 라틴 문자·스케일 10에서도 안 나왔다).
            // 원인을 더 파는 대신, 월드 공간용으로 만들어진 쪽을 쓴다 —
            // MeshRenderer로 그려지므로 정렬 순서도 카드 렌더러와 같은 축에서 다룰 수 있다.
            var nameObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshPro));
            nameObject.transform.SetParent(transform, false);
            _nameLabel = nameObject.GetComponent<TextMeshPro>();
            _nameRenderer = nameObject.GetComponent<MeshRenderer>();
            UIBuild.ApplyFont(_nameLabel);
            _nameLabel.color = new Color(0.075f, 0.12f, 0.13f, 1f);
            _nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _nameLabel.fontStyle = FontStyles.Bold;
            _nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _nameLabel.raycastTarget = false;
            LayOutName(cardSize);


            Color[] reagentColors = { UITheme.Danger, UITheme.Mana, UITheme.Accent };
            for (int i = 0; i < 3; i++)
            {
                var track = UIBuild.Solid("Reagent" + i, root, UITheme.SurfaceSunken);
                Place(track.rectTransform, 8f + i * 29f, 73f, 25f, 3f);
                var fill = UIBuild.Solid("Fill", track.transform, reagentColors[i]);
                UIBuild.Stretch(fill.rectTransform);
                _reagentBars[i] = fill;
                _reagentTracks[i] = track.gameObject;
                track.gameObject.SetActive(false);
            }

            // ── 궁극기 충전 ──
            // 붕괴: 스타레일 방식이다. 테두리는 늘 또렷하게 그려 두고,
            // 안쪽이 아래에서 위로 차오른다.
            //
            // 시계 방향으로 도는 링을 쓰지 않는 이유는 각도로만 읽히기 때문이다.
            // 절반쯤 찼는지 3분의 2쯤 찼는지 알려면 호의 끝을 눈으로 따라가야 한다.
            // 수위는 높이 하나로 읽히고, 여러 카드를 훑을 때 특히 차이가 크다.
            _ultRoot = UIBuild.Container("UltGauge", root);
            _ultRoot.anchorMin = new Vector2(1f, 1f);
            _ultRoot.anchorMax = new Vector2(1f, 1f);
            _ultRoot.pivot = new Vector2(1f, 1f);
            _ultRoot.sizeDelta = new Vector2(RingSize, RingSize);
            _ultRoot.anchoredPosition = new Vector2(RingOverhang, RingOverhang);

            // 빈 그릇. 흐린 흰색이라 차오르기 전에도 자리가 또렷하다.
            Image ultWell = NewImage(_ultRoot, "Well", RingWell);
            ultWell.sprite = UIShapes.Disc(96, Color.white);
            UIBuild.Stretch(ultWell.rectTransform, RingEdge, RingEdge);

            // 차오르는 물. 테두리와 같은 진한 원소 색이라 흰 바탕 위에서 수위가 바로 읽힌다.
            _ultFill = NewImage(_ultRoot, "Fill", UITheme.Mana);
            _ultFill.sprite = UIShapes.Disc(96, Color.white);
            _ultFill.type = Image.Type.Filled;
            _ultFill.fillMethod = Image.FillMethod.Vertical;
            _ultFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _ultFill.fillAmount = 0f;
            UIBuild.Stretch(_ultFill.rectTransform, RingEdge, RingEdge);

            // 스택형 궁극기의 칸 나누기. 물 위·테두리 아래에 놓여야 물을 잘라 보인다.
            _ultTicks = UIBuild.Container("Ticks", _ultRoot);
            UIBuild.Stretch(_ultTicks, RingEdge, RingEdge);

            // 게이지 안의 표식(⌃⌃). 원소색 원반 하나만 있으면 "원소 표시"인지 "충전량"인지
            // 색만으로는 갈리지 않는다. 방향을 가진 표식이 들어가야 차오르는 자리로 읽힌다.
            // 충전 중에는 흐리게 깔려 있다가 다 차면 또렷해진다.
            _ultMark = NewImage(_ultRoot, "Mark", Color.white);
            _ultMark.sprite = UIShapes.Chevron(96, Color.white);
            _ultMark.preserveAspect = true;
            UIBuild.Stretch(_ultMark.rectTransform, RingEdge * 3.2f, RingEdge * 3.2f);

            // 테두리는 가장 위에. 수위와 무관하게 늘 같은 굵기다.
            // 색은 유닛의 원소를 따라가므로 Bind에서 다시 칠한다.
            _ultOutline = NewImage(_ultRoot, "Outline", UITheme.Mana);
            _ultOutline.sprite = UIShapes.Disc(96, Color.white, 0.80f);
            UIBuild.Stretch(_ultOutline.rectTransform);

            // ── 체력 + 방어막(같은 트랙, 방어막이 오른쪽에 이어 붙는다) ──
            Image hpTrack = UIBuild.Solid("HpTrack", root, new Color(0.08f, 0.15f, 0.16f, 0.82f));
            Place(hpTrack.rectTransform, 0f, HpTop, CanvasWidth, HpHeight);
            _hpFill = NewImage(hpTrack.transform, "HpFill", UITheme.Hp);
            _shieldFill = NewImage(hpTrack.transform, "ShieldFill", UITheme.Shield);

            // 체력이 단계로 끊기는 유닛(오시리스의 부위 파괴)만 칸 나누기를 얻는다.
            // 채움 뒤에 만들어야 물 위에 선이 놓인다.
            _hpTicks = UIBuild.Stretch(UIBuild.Container("HpTicks", hpTrack.transform));

            // 글라스 반사광. 체력 · 방어막 위 윗부분에 옅은 흰 띠를 얹어 바가 유리관처럼 읽힌다.
            Image hpSheen = UIBuild.Solid("HpSheen", hpTrack.transform, new Color(1f, 1f, 1f, 0.26f));
            hpSheen.raycastTarget = false;
            hpSheen.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            hpSheen.rectTransform.anchorMax = new Vector2(1f, 1f);
            hpSheen.rectTransform.offsetMin = Vector2.zero;
            hpSheen.rectTransform.offsetMax = Vector2.zero;

            // ── 행동 게이지 ──
            Image actionTrack = UIBuild.Solid("ActionTrack", root, new Color(0.08f, 0.15f, 0.16f, 0.34f));
            Place(actionTrack.rectTransform, 0f, ActionTop, CanvasWidth, ActionHeight);
            _actionFill = NewImage(actionTrack.transform, "ActionFill", UITheme.ActionYellow);

            // ── 상태 아이콘 ──
            // 예전에는 초록·적색 점이라 "뭔가 걸려 있다"까지만 읽혔다. 지금은 그림이 붙고,
            // 어두운 받침 위에 얹혀 초상화 위에서도 형태가 뜬다.
            // 색이 분류를 말한다 — 이로움 초록 · 해로움 적색 · 행동 불가 보라.
            for (int i = 0; i < MaxDots; i++)
            {
                Image back = NewImage(root, $"StatusBack{i}", DotBacking);
                back.sprite = UIShapes.Disc(64, Color.white);
                back.type = Image.Type.Simple;
                Place(back.rectTransform, i * DotGap, DotTop, DotSize, DotSize);
                back.enabled = false;
                _dotBacks[i] = back;

                Image dot = NewImage(back.transform, $"Status{i}", UITheme.TextMuted);
                dot.type = Image.Type.Simple;
                dot.preserveAspect = true;
                UIBuild.Stretch(dot.rectTransform,
                    DotSize * (1f - DotIconRatio) * 0.5f, DotSize * (1f - DotIconRatio) * 0.5f);
                dot.enabled = false;
                _dots[i] = dot;

                // 중첩 수는 오른쪽 아래 구석에 붙는다. 1이면 그리지 않는다 —
                // 모든 아이콘에 1이 붙으면 숫자가 배경 무늬가 된다.
                Image count = NewImage(back.transform, $"StatusCount{i}", Color.white);
                count.preserveAspect = true;
                count.type = Image.Type.Simple;
                count.rectTransform.anchorMin = new Vector2(1f, 0f);
                count.rectTransform.anchorMax = new Vector2(1f, 0f);
                count.rectTransform.pivot = new Vector2(1f, 0f);
                count.rectTransform.sizeDelta = new Vector2(DotCountSize, DotCountSize);
                count.rectTransform.anchoredPosition = new Vector2(DotCountSize * 0.30f, -DotCountSize * 0.22f);
                count.enabled = false;
                _dotCounts[i] = count;
            }

            // 맞았을 때 카드 전체가 하얗게 번쩍인다. 카드 위 무엇보다 나중에 만들어 맨 앞에 둔다.
            _hitFlash = NewImage(root, "HitFlash", new Color(1f, 1f, 1f, 0f));
            _hitFlash.sprite = UIShapes.CutCorner(9, Color.white, UIShapes.Corner.Diagonal);
            _hitFlash.type = Image.Type.Sliced;
            Place(_hitFlash.rectTransform, 0f, 0f, CanvasWidth, CanvasWidth);
            _hitFlash.raycastTarget = false;

            Show(false);
        }

        /// <summary>
        /// 플레이 모드 스크립트 재로드 뒤에는 런타임으로 만든 자식은 남아 있어도 배열 원소가
        /// 비어 있을 수 있다. 시약 트랙을 부모 역참조로 추측하지 않고 이름으로 한 번 복구한다.
        /// </summary>
        private void RecoverReagentReferences()
        {
            if (_canvas == null) return;

            for (int i = 0; i < _reagentBars.Length; i++)
            {
                if (_reagentBars[i] != null && _reagentTracks[i] != null) continue;

                Transform track = _canvas.transform.Find("Reagent" + i);
                if (track == null) continue;
                _reagentTracks[i] = track.gameObject;
                Transform fill = track.Find("Fill");
                _reagentBars[i] = fill != null ? fill.GetComponent<Image>() : null;
            }
        }

        private bool ReagentsBuilt()
        {
            for (int i = 0; i < _reagentBars.Length; i++)
                if (_reagentBars[i] == null || _reagentTracks[i] == null) return false;
            return true;
        }

        /// <summary>칸 크기가 바뀌었을 때(카메라 프레이밍 변경 등) 카드 치수를 다시 잡는다.</summary>
        public void Resize(float cardSize)
        {
            if (_frame == null || Mathf.Approximately(_cardSize, cardSize)) return;

            _cardSize = cardSize;
            _frame.size = new Vector2(cardSize, cardSize);

            var canvasRect = (RectTransform)_canvas.transform;
            canvasRect.localScale = Vector3.one * (cardSize / CanvasWidth);
            canvasRect.localPosition = new Vector3(0f, cardSize * 0.5f, -0.01f);

            LayOutName(cardSize);
        }

        /// <summary>이름표를 카드 하단 이름 띠 위에 얹는다. 캔버스와 같은 비율을 쓴다.</summary>
        private void LayOutName(float cardSize)
        {
            if (_nameLabel == null) return;

            var rect = (RectTransform)_nameLabel.transform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            // 왼쪽 민트 액센트(7~9%)와 첫 글자가 겹치지 않도록 그 오른쪽부터 시작한다.
            rect.sizeDelta = new Vector2(cardSize * 0.79f, cardSize * 0.14f);
            // 한 줄짜리 이름 받침의 가운데.
            rect.localPosition = new Vector3(cardSize * 0.055f,
                cardSize * (0.5f - (NameBandTop + NameBandHeight * 0.5f) / CanvasWidth), -0.02f);

            // 월드 TMP 자동 맞춤에 행보다 큰 최소값을 주면 Truncate/Ellipsis가 첫 글자부터
            // 모두 버려 mesh characterCount가 0이 된다. 48px 받침의 윗행에 안전하게 들어오는
            // 고정값을 쓰고, 긴 이름만 말줄임한다.
            _nameLabel.enableAutoSizing = false;
            _nameLabel.fontSize = cardSize * 0.82f;
            _nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>앞줄이 뒷줄을 가리도록 정렬 순서를 맞춘다. 초상화는 <c>order + 1</c>이다.</summary>
        public void ApplyDepth(int order, int sortingLayerId)
        {
            if (_frame != null)
            {
                _frame.sortingLayerID = sortingLayerId;
                _frame.sortingOrder = order;
            }

            if (_canvas != null)
            {
                // 루트 캔버스라 sortingOrder가 SpriteRenderer와 같은 축에서 비교된다.
                _canvas.sortingLayerID = sortingLayerId;
                _canvas.sortingOrder = order + 5;
            }

            if (_nameRenderer != null)
            {
                // 이름 띠(캔버스, order + 5) 바로 위에 올린다.
                _nameRenderer.sortingLayerID = sortingLayerId;
                _nameRenderer.sortingOrder = order + 6;
            }
        }

        public void Bind(Unit unit, bool combatHud)
        {
            DetachEvents();
            ResetReaction();

            _unit = unit;
            _combatHud = combatHud;

            Show(unit != null);
            if (unit == null) return;

            // 아군은 오른쪽(적 방향)으로, 적은 왼쪽으로 찍는다.
            _facing = unit.IsEnemy ? -1 : 1;
            _lastHp = unit.HpCurr;
            RefreshPortraitBase();
            AttachEvents();

            _nameLabel.text = unit.UnitName;

            _ringElementName = null;   // 다른 유닛이 들어왔으니 링 색을 다시 잡는다
            _ringTickCount = -1;
            RefreshUltimateRingColor();
            RefreshUltimateTicks();

            // 대기석 카드는 전투 정보를 들지 않는다. 이름표만 남는다.
            _ultRoot.gameObject.SetActive(combatHud);
            _hpFill.transform.parent.gameObject.SetActive(combatHud);
            _actionFill.transform.parent.gameObject.SetActive(combatHud);
            for (int i = 0; i < _reagentTracks.Length; i++)
                if (_reagentTracks[i] != null) _reagentTracks[i].SetActive(false);
            if (!combatHud)
            {
                foreach (Image dot in _dots) dot.enabled = false;
                foreach (Image back in _dotBacks) back.enabled = false;
                foreach (Image count in _dotCounts) count.enabled = false;
                _actingOutline.enabled = false;
            }

            Tick();
        }

        public void Tick()
        {
            if (_unit == null) return;

            if (!ReagentsBuilt()) RecoverReagentReferences();

            AdvanceReaction(Time.deltaTime);
            if (!_combatHud) return;

            // 체력이 줄었으면 맞은 것이다. 이벤트를 따로 걸지 않고 값의 변화로 잡는다 —
            // 방어막이 대신 깎이든 지속 피해든 한 곳에서 잡히기 때문이다.
            if (_lastHp >= 0 && _unit.HpCurr < _lastHp) PlayHit();
            _lastHp = _unit.HpCurr;

            // ── 체력 · 방어막 ──
            // 트랙 전체가 재는 양은 "최대 체력"이 아니라 "지금 남은 체력 + 방어막"의 상한이다.
            // 예전에는 최대 체력으로만 재고 남은 자리(1 - 체력비율)까지만 방어막을 그렸다.
            // 그래서 체력이 가득 찬 유닛(찬드라 같은 방어형)은 방어막을 받아도 그릴 자리가
            // 0이라 아예 보이지 않았다. 이제 방어막만큼 트랙을 늘려 항상 오른쪽에 이어 붙는다.
            float hpRatio = _unit.HpMax > 0 ? Mathf.Clamp01(_unit.HpCurr / (float)_unit.HpMax) : 0f;
            float track = Mathf.Max(_unit.HpMax, _unit.HpCurr + _unit.ShieldCurr);
            float hpSpan = track > 0f ? Mathf.Clamp01(_unit.HpCurr / track) : 0f;
            float shieldSpan = track > 0f ? Mathf.Clamp01(_unit.ShieldCurr / track) : 0f;

            SetSpan(_hpFill.rectTransform, 0f, hpSpan);
            SetSpan(_shieldFill.rectTransform, hpSpan, hpSpan + shieldSpan);
            _hpFill.enabled = hpSpan > 0f;
            _shieldFill.enabled = shieldSpan > 0f;
            // 적은 비율과 무관하게 적색, 아군은 30% 이하부터 붉어진다.
            _hpFill.color = _unit.IsEnemy ? UITheme.Enemy : UITheme.HpColor(hpRatio);
            RefreshHpSegments();

            // ── 궁극기 링 ──
            // 원소가 런 중에 바뀌는 유닛이 있어 매 프레임 값을 확인한다.
            // 문자열이 그대로면 아무 일도 하지 않으므로 비용은 비교 한 번이다.
            RefreshUltimateRingColor();
            RefreshUltimateTicks();

            // ManaMax는 AttributesUpdate가 GetUltimateResourceMax()로 채워 두는 값이라
            // 마나형이면 파생 마나, 스택형이면 최대 스택이 그대로 들어 있다.
            // 예전에는 yaml 원본값(UltimateResourceMax)을 먼저 봐서, 파생 마나가 100이
            // 아닌 유닛의 수위가 실제와 어긋났다.
            int resourceMax = _unit.ManaMax > 0 ? _unit.ManaMax : _unit.GetUltimateResourceMax();
            _ultFill.fillAmount = resourceMax > 0 ? Mathf.Clamp01(_unit.ManaCurr / (float)resourceMax) : 0f;
            RefreshUltimateReady(_unit.Chemistry != null ? _unit.Chemistry.CanReact : _ultFill.fillAmount >= 0.999f);
            for (int i = 0; i < _reagentBars.Length; i++)
            {
                bool visible = _combatHud && _unit.Chemistry != null;
                GameObject trackObject = _reagentTracks[i];
                Image fill = _reagentBars[i];
                if (trackObject != null) trackObject.SetActive(visible);
                if (visible && fill != null)
                    SetSpan(fill.rectTransform, 0f,
                        _unit.Chemistry.Reagents[(ReagentKind)i] / _unit.Chemistry.Reagents.Capacity);
            }

            // ── 행동 게이지 ──
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            SetSpan(_actionFill.rectTransform, 0f, scheduler?.ActionProgress(_unit) ?? 0f);
            _actingOutline.enabled = scheduler != null && ReferenceEquals(scheduler.ActingUnit, _unit);

            RefreshStatusDots();

            bool alive = _unit.isActive && _unit.HpCurr > 0;
            _group.alpha = alive ? 1f : 0.38f;
            if (_nameLabel != null)
            {
                // 이름표는 이름 한 줄이다. 체력 수치는 바가 대신 말하므로 적지 않고,
                // 고유 패시브 스택 같은 전투 자원이 있을 때만 이름 뒤에 작게 붙인다.
                // (라부아지에의 시약은 카드 위 세 막대가 따로 보여 준다.)
                string resources = _combatHud ? Combat.CombatResourceLabels.Compose(_unit) : "";
                if (resources.StartsWith(_unit.UnitName, System.StringComparison.Ordinal))
                    resources = resources.Substring(_unit.UnitName.Length).Trim();
                _nameLabel.text = string.IsNullOrWhiteSpace(resources)
                    ? _unit.UnitName
                    : $"{_unit.UnitName}  <size=78%><color=#1B5E5A>{resources}</color></size>";

                // 이름표는 캔버스 밖이라 CanvasGroup이 닿지 않는다. 알파를 직접 맞춘다.
                Color nameColor = new Color(0.075f, 0.12f, 0.13f, 1f);
                nameColor.a = alive ? 1f : 0.38f;
                _nameLabel.color = nameColor;
            }
            if (_frame != null)
            {
                Color tint = _frame.color;
                tint.a = alive ? 1f : 0.38f;
                _frame.color = tint;
            }
        }

        /// <summary>
        /// 상태 아이콘을 다시 그린다.
        ///
        /// <b>같은 그림으로 보이는 것은 한 칸에 모으고 수를 적는다.</b> 중첩은 같은 상태를 여러 개
        /// 들고 있는 것으로 구현돼 있어(<c>StatusStackPolicy.Stack</c>), 예전처럼 목록을
        /// 그대로 늘어놓으면 화상 5중첩 하나가 다섯 칸을 전부 먹었다. Key로만 모으면
        /// 출처가 다른 같은 공격력 증가 둘이 똑같은 아이콘 두 칸으로 나란히 서서, 같은 모양이
        /// 겹쳐 보인다는 QA가 있었다. 이제 그림 · 방향 · 색이 같으면 한 칸이다.
        /// 무엇이 모였는지는 카드에 마우스를 올리면 툴팁으로 하나씩 보인다(<see cref="Cell"/>).
        ///
        /// 순서는 행동 불가 → 해로움 → 이로움. 칸이 모자라면 마지막 칸이 남은 수를 적는다.
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

            bool overflow = _dotStatuses.Count > MaxDots;
            int iconSlots = overflow ? MaxDots - 1 : _dotStatuses.Count;

            for (int i = 0; i < _dots.Length; i++)
            {
                bool isOverflow = overflow && i == MaxDots - 1;
                bool has = i < iconSlots || isOverflow;
                _dotBacks[i].enabled = has;
                _dots[i].enabled = has;
                _dotCounts[i].enabled = has && !isOverflow && _dotStackCounts[i] > 1;
                if (!has) continue;

                if (isOverflow)
                {
                    // 넘친 수. 그림 대신 숫자만 흰색으로 — 아이콘이 아니라는 것이 한눈에 갈린다.
                    _dots[i].sprite = UIShapes.Digit(Mathf.Min(9, _dotStatuses.Count - iconSlots), Color.white);
                    _dots[i].color = UITheme.TextPrimary;
                    continue;
                }

                Color tint = StatusIcons.Tint(_dotStatuses[i]);
                _dots[i].sprite = StatusIcons.For(_dotStatuses[i]);
                _dots[i].color = tint;

                if (!_dotCounts[i].enabled) continue;
                _dotCounts[i].sprite = UIShapes.Digit(Mathf.Min(9, _dotStackCounts[i]), Color.white);
                // 숫자는 아이콘보다 밝게 둔다. 같은 색이면 그림에 섞여 읽히지 않는다.
                _dotCounts[i].color = Color.Lerp(tint, Color.white, 0.55f);
            }
        }

        /// <summary>그림 · 방향 · 색이 같은 칸을 찾는다. 셋이 같으면 눈으로는 같은 상태다.</summary>
        private int IndexOfLook(UnitStatus status)
        {
            (StatusGlyph glyph, StatusArrow arrow) = StatusIcons.Resolve(status);
            Color tint = StatusIcons.Tint(status);

            for (int i = 0; i < _dotStatuses.Count; i++)
            {
                UnitStatus other = _dotStatuses[i];
                if (string.Equals(other.Key, status.Key, System.StringComparison.Ordinal)) return i;

                (StatusGlyph otherGlyph, StatusArrow otherArrow) = StatusIcons.Resolve(other);
                if (otherGlyph == glyph && otherArrow == arrow && StatusIcons.Tint(other) == tint) return i;
            }

            return -1;
        }

        /// <summary>행동 불가 → 해로움 → 기타 → 이로움. 같은 급이면 원래 순서를 지킨다(삽입 정렬).</summary>
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
            Color tint = StatusIcons.Tint(status);
            if (tint == UITheme.Control) return 0;
            if (tint == UITheme.Danger) return 1;
            if (tint == UITheme.Positive) return 3;
            return 2;
        }

        private void Show(bool visible)
        {
            // 프레임 렌더러는 조립/리사이즈 기준으로만 남긴다. 화면에는 정사각 카드 면을 그리지 않는다.
            if (_frame != null) _frame.enabled = false;
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
            if (_nameLabel != null) _nameLabel.gameObject.SetActive(visible);
        }

        // ── 타격 · 시전 반응 ─────────────────────────────────────────

        private void AttachEvents()
        {
            if (_unit == null) return;

            // Unit.InitializeUnit()이 이벤트 딕셔너리를 새로 만들기 때문에
            // 셀을 다시 쓸 때마다 연결도 새로 해 줘야 한다.
            _unit.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActivates, OnCast);
            _unit.AddListener<EventContext>(BaseEnums.UnitEventType.OnUltimateActivates, OnUltimateCast);
        }

        private void DetachEvents()
        {
            if (_unit == null) return;

            _unit.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActivates, OnCast);
            _unit.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnUltimateActivates, OnUltimateCast);
        }

        private void OnCast(EventContext _) => PlayCast(1f);

        private void OnUltimateCast(EventContext _) => PlayCast(1.6f);

        /// <summary>쏘는 순간 카드가 적 쪽으로 찍고 살짝 커진다.</summary>
        private void PlayCast(float strength)
        {
            _lunge = Mathf.Min(1f, strength);
            _punch = Mathf.Max(_punch, 0.6f * Mathf.Min(1f, strength));
        }

        /// <summary>
        /// 실제 발사/베기 시점에 쓰는 공개 진입점. 시전 시작 이벤트의 기본 찍기와 별개로
        /// 타격 순간에도 공격자가 조금 전진해 근접 공격의 거리감을 살린다.
        /// </summary>
        public void PlayAttackReaction(float strength = 1f) => PlayCast(strength);

        /// <summary>
        /// 맞는 순간의 반응. 하얗게 번쩍이며 뒤로 밀리고, <b>물리 타격이면 떤다</b>.
        ///
        /// 세기는 <see cref="Effects.CombatFeedback"/>가 '최대 체력의 몇 할을 잃었는가'로 잰다.
        /// 잔매와 한 방이 같은 크기로 흔들리면 무엇이 아팠는지 알 수 없다.
        /// </summary>
        /// <param name="strength">0~1 남짓의 세기. 1이 한 방에 가깝다.</param>
        /// <param name="physical">베기·찌르기·타격 등 물리인가. 마법은 떨지 않고 번쩍이기만 한다.</param>
        /// <param name="crit">치명타면 떨림을 한 번 더 키운다.</param>
        public void PlayHitReaction(float strength, bool physical, bool crit)
        {
            float scaled = Mathf.Clamp(strength, 0.2f, 1.4f);

            _flash = 1f;
            _punch = Mathf.Max(_punch, Mathf.Min(1f, 0.7f + 0.3f * scaled));
            _recoil = Mathf.Max(_recoil, Mathf.Min(1f, scaled));

            if (physical)
            {
                _shake = 1f;
                _shakeStrength = Mathf.Min(1.3f, scaled * (crit ? 1.3f : 1f));
                _shakeSeed = Random.value * 10f;
            }

            // 체력 변화로 잡는 자동 감지와 겹치지 않게 기준값을 맞춰 둔다.
            if (_unit != null) _lastHp = _unit.HpCurr;
        }

        /// <summary>체력이 줄어든 것만 보고 잡아내는 기본 반응. 출처를 모르는 피해가 여기로 온다.</summary>
        private void PlayHit() => PlayHitReaction(0.6f, physical: false, crit: false);

        private void ResetReaction()
        {
            _flash = _punch = _lunge = _recoil = _shake = 0f;
            _airborneLift = 0f;
            _lastHp = -1;
            ApplyReaction();
        }

        private void AdvanceReaction(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            float airborneTarget = _unit != null && _unit.HasStatusKey(ControlStatuses.AirborneKey)
                ? Cell.CardSize * 0.13f
                : 0f;
            float airborneTime = airborneTarget > _airborneLift ? AirborneRiseTime : AirborneFallTime;
            float airborneStep = Cell.CardSize * 0.13f * deltaTime / airborneTime;
            float nextAirborne = Mathf.MoveTowards(_airborneLift, airborneTarget, airborneStep);

            bool moving = _flash > 0f || _punch > 0f || _lunge > 0f || _recoil > 0f || _shake > 0f
                || !Mathf.Approximately(nextAirborne, _airborneLift);
            if (!moving) return;

            _airborneLift = nextAirborne;
            _flash = Mathf.Max(0f, _flash - deltaTime / FlashTime);
            _punch = Mathf.Max(0f, _punch - deltaTime / PunchTime);
            _lunge = Mathf.Max(0f, _lunge - deltaTime / LungeTime);
            _recoil = Mathf.Max(0f, _recoil - deltaTime / PunchTime);
            _shake = Mathf.Max(0f, _shake - deltaTime / ShakeTime);
            ApplyReaction();
        }

        /// <summary>지금의 반응 상태를 카드와 초상화에 함께 입힌다.</summary>
        private void ApplyReaction()
        {
            // 번쩍임은 빠르게 사그라들어야 잔상이 남지 않는다.
            if (_hitFlash != null)
            {
                Color flashColor = _hitFlash.color;
                flashColor.a = 0.55f * _flash * _flash;
                _hitFlash.color = flashColor;
                _hitFlash.enabled = flashColor.a > 0.001f;
            }

            float scale = 1f + PunchAmount * _punch;

            // 찍기는 나갔다 돌아오는 모양(0 → 1 → 0), 밀림은 곧바로 사그라든다.
            float lungeShape = Mathf.Sin(Mathf.PI * (1f - _lunge));
            float offsetX = _facing * (LungeDistance * lungeShape - RecoilDistance * _recoil);
            float offsetY = _airborneLift;

            // 떨림은 남은 시간을 위상으로 삼아 흔든다. 진폭은 제곱으로 줄어 끝이 깔끔하다.
            if (_shake > 0f)
            {
                float elapsed = (1f - _shake) * ShakeTime;
                float amplitude = ShakeDistance * _shakeStrength * _shake * _shake;
                offsetX += Mathf.Sin((elapsed + _shakeSeed) * ShakeFrequency * Mathf.PI * 2f) * amplitude;
                offsetY += Mathf.Sin((elapsed + _shakeSeed) * ShakeFrequency * 0.72f * Mathf.PI * 2f + 1.3f)
                           * amplitude * 0.55f;
            }

            var offset = new Vector3(offsetX, offsetY, 0f);

            transform.localPosition = offset;
            transform.localScale = Vector3.one * scale;

            if (_portrait == null) return;

            _portrait.localPosition = offset;
            _portrait.localScale = _portraitBaseScale * scale;
        }

        // ── 조립 헬퍼 ────────────────────────────────────────────────

        /// <summary>
        /// 궁극기 링을 유닛의 <b>원소 색</b>으로 칠한다.
        ///
        /// 투사체와 <see cref="Effects.Projectiles.ElementalProjectiles"/>의 같은 표를 쓴다.
        /// 그래서 카드에 달린 링 색과 그 유닛이 쏘는 투사체 색이 항상 일치하고,
        /// 편성만 훑어도 어떤 속성이 몇 명인지가 읽힌다.
        /// 원소가 없는 유닛(None)은 예전처럼 자원 파랑을 쓴다.
        ///
        /// 원소 이름이 그대로면 아무 일도 하지 않는다. 매 프레임 불러도 안전하다.
        /// </summary>
        private void RefreshUltimateRingColor()
        {
            string elementName = _unit != null ? _unit.Element : null;
            if (elementName == _ringElementName) return;
            _ringElementName = elementName;

            BaseEnums.UnitElement element = Effects.Projectiles.ElementalProjectiles.Parse(elementName);
            Color ring = element == BaseEnums.UnitElement.None
                ? UITheme.Mana
                : Effects.Projectiles.ElementalProjectiles.PastelColorFor(element);

            _ringColor = ring;
            _ultOutline.color = ring;
            _ultFill.color = ring;
        }

        /// <summary>
        /// 다 찼는지를 색과 맥동으로 알린다.
        ///
        /// 궁극기는 자원이 차는 즉시 예약되므로 '준비됨'은 길어야 한 박자다.
        /// 그래도 표시가 필요한 것은, 그 한 박자가 <b>지금 큰 것이 나온다</b>는 유일한 예고이기 때문이다.
        /// 맥동은 <c>unscaledTime</c>으로 센다 — 8배속에서 전투 시간으로 세면 깜빡임이 된다.
        /// </summary>
        private void RefreshUltimateReady(bool ready)
        {
            if (_ultMark == null) return;

            if (!ready)
            {
                // 충전 중에는 표식이 물 아래 흐리게 깔려 있다. 자리가 무엇인지만 알리면 된다.
                _ultMark.color = MarkIdle;
                _ultOutline.color = _ringColor;
                return;
            }

            float pulse = 0.74f + 0.26f * Mathf.Sin(Time.unscaledTime * 6.6f);
            _ultMark.color = new Color(1f, 1f, 1f, pulse);
            _ultOutline.color = Color.Lerp(_ringColor, UITheme.Accent, 0.75f);
        }

        /// <summary>
        /// 스택형 궁극기(호루스의 우제트처럼 <c>ultimateResourceType: Stack</c>)는
        /// 연속 게이지가 아니라 <b>칸이 하나씩 차는</b> 자원이다.
        /// 마나와 같은 물결로 그리면 "지금 몇 스택인지"를 눈금 없이 재야 한다.
        ///
        /// 그래서 원 안쪽을 스택 수만큼 가로줄로 나눠 둔다. 물이 차 있든 없든 줄은
        /// 늘 보이므로 최대 스택도 함께 읽힌다. 줄 길이는 그 높이에서의 현(chord)에
        /// 맞춰 잘라 원 밖으로 삐져나가지 않게 한다.
        /// </summary>
        private void RefreshUltimateTicks()
        {
            int ticks = 0;
            if (_unit != null && _unit.UltimateResourceType == BaseEnums.UltimateResourceType.Stack)
            {
                int max = _unit.GetUltimateResourceMax();
                if (max >= 2 && max <= MaxRingTicks) ticks = max;
            }

            if (ticks == _ringTickCount) return;
            _ringTickCount = ticks;

            UIBuild.Clear(_ultTicks);
            if (ticks == 0) return;

            float diameter = RingSize - RingEdge * 2f;
            float radius = diameter * 0.5f;

            for (int i = 1; i < ticks; i++)
            {
                float y = diameter * i / ticks;             // 원 아래에서 잰 높이
                float offset = y - radius;                  // 중심에서의 거리
                float half = Mathf.Sqrt(Mathf.Max(0f, radius * radius - offset * offset));

                Image line = UIBuild.Solid($"Tick{i}", _ultTicks, RingTick);
                line.raycastTarget = false;
                UIBuild.Pin(line.rectTransform, new Vector2(0.5f, 0f),
                    new Vector2(half * 2f, RingTickThickness),
                    new Vector2(0f, y - RingTickThickness * 0.5f));
            }
        }

        /// <summary>
        /// 체력 바를 칸으로 끊어 그린다. 궁극기 링의 스택 칸(<see cref="RefreshUltimateTicks"/>)과 같은 방식이며,
        /// 이쪽은 세로선을 가로로 늘어놓는다. 칸 수가 그대로면 아무것도 다시 만들지 않는다.
        /// </summary>
        private void RefreshHpSegments()
        {
            int segments = _unit != null ? _unit.HpSegmentCount : 0;
            if (segments < 2 || segments > MaxHpTicks) segments = 0;

            if (segments == _hpTickCount) return;
            _hpTickCount = segments;

            UIBuild.Clear(_hpTicks);
            if (segments == 0) return;

            for (int i = 1; i < segments; i++)
            {
                Image line = UIBuild.Solid($"HpTick{i}", _hpTicks, RingTick);
                line.raycastTarget = false;
                UIBuild.Pin(line.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(HpTickThickness, HpHeight),
                    new Vector2(CanvasWidth * i / segments - HpTickThickness * 0.5f, 0f));
            }
        }

        /// <summary>캔버스 좌상단 기준으로 자리를 잡는다.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>부모를 꽉 채우는 채움 이미지. 가로 구간은 <see cref="SetSpan"/>으로 잡는다.</summary>
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

        /// <summary>
        /// 트랙 안에서 [from, to] 구간만 차지하게 만든다.
        /// fillAmount 대신 앵커를 쓰는 이유는 <b>방어막을 체력 오른쪽에서 시작</b>시켜야 하기 때문이다.
        /// </summary>
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
