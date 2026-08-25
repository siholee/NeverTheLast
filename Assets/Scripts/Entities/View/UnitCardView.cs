using System.Collections.Generic;
using BaseClasses;
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
    /// 전장(과 대기석)의 유닛 한 명을 <b>정사각 카드 + 전투 HUD</b>로 그린다.
    ///
    /// 우하단 파티 카드를 없애면서 그쪽이 들고 있던 정보가 전부 이리로 왔다.
    /// 한 장에 담기는 것:
    ///   · 컷코너 카드 프레임 — 초상화가 그 위에 선다(초상화는 여전히 SpriteRenderer다)
    ///   · 진영 띠 — 아군 무채색 / 적 적색. <b>파랑을 쓰지 않는다.</b>
    ///   · 이름 띠
    ///   · 궁극기 링 — 우상단에 걸치는 원형 게이지. 충전 중·완료·예약이 모두 같은 색이다.
    ///   · 체력 바 — 그 <b>오른쪽에 이어 붙는</b> 방어막(회백)
    ///   · 행동 게이지 — 체력 바 절반 높이
    ///   · 상태 점 — 이로운 것 초록, 해로운 것 적색
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
        public const float CardRatio = 0.88f;

        /// <summary>캔버스 안의 작업 단위. 카드 한 변이 100이 되도록 잡는다.</summary>
        private const float CanvasWidth = 100f;

        /// <summary>카드(100) + 그 아래 바 영역까지의 높이.</summary>
        private const float CanvasHeight = 130f;

        private const int MaxDots = 5;

        // 캔버스 좌표(좌상단 기준). 디자인 캔버스의 퍼센트를 그대로 옮겼다.
        private const float BandHeight = 2.6f;
        private const float NameBandTop = 72f;
        private const float NameBandHeight = 28f;
        private const float RingSize = 27f;
        private const float RingOverhang = 5f;
        private const float HpTop = 104.7f;
        private const float HpHeight = 5.7f;
        private const float ActionTop = 112.2f;
        private const float ActionHeight = 2.9f;
        private const float DotTop = 117.7f;
        private const float DotSize = 10f;
        private const float DotGap = 13f;

        private static TMP_FontAsset _font;
        private static bool _fontSearched;

        private Unit _unit;
        private bool _combatHud;
        private float _cardSize;

        private SpriteRenderer _frame;
        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _factionBand;
        private Image _actingOutline;
        private TextMeshPro _nameLabel;
        private MeshRenderer _nameRenderer;
        private Image _ultRing;
        private Image _hpFill;
        private Image _shieldFill;
        private Image _actionFill;
        private readonly Image[] _dots = new Image[MaxDots];
        private Image _hitFlash;

        // ── 타격 · 시전 반응 ─────────────────────────────────────────
        private const float FlashTime = 0.13f;   // 맞았을 때 하얗게 번쩍이는 시간
        private const float PunchTime = 0.18f;   // 카드가 커졌다 돌아오는 시간
        private const float LungeTime = 0.22f;   // 쏠 때 앞으로 찍는 시간
        private const float PunchAmount = 0.07f; // 커지는 비율
        private const float LungeDistance = 0.9f;
        private const float RecoilDistance = 0.55f;

        private Transform _portrait;
        private Vector3 _portraitBaseScale = Vector3.one;
        private float _flash;
        private float _punch;
        private float _lunge;
        private float _recoil;
        private int _facing = 1;
        private int _lastHp = -1;

        /// <summary>한글이 나오는 폰트를 찾는다. Resources에 없으면 TMP 기본값으로 떨어진다.</summary>
        private static TMP_FontAsset Font
        {
            get
            {
                if (_fontSearched) return _font;
                _fontSearched = true;

                _font = Resources.Load<TMP_FontAsset>("Font/NotoSansKR-VariableFont_wght SDF");
                if (_font == null)
                {
                    _font = TMP_Settings.defaultFontAsset;
                    Debug.LogWarning(
                        "[UnitCardView] Resources/Font 아래에서 한글 폰트를 찾지 못했습니다. " +
                        "카드 이름이 네모로 나오면 NotoSansKR SDF를 그 경로에 두세요.");
                }

                return _font;
            }
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
            if (_frame != null)
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
            _frame.sprite = UIShapes.CutCorner(
                Mathf.RoundToInt(cardSize * 0.09f * 100f),
                UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal,
                UITheme.Outline,
                3);
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

            _factionBand = UIBuild.Solid("FactionBand", root, UITheme.TextSecondary);
            Place(_factionBand.rectTransform, 0f, 0f, CanvasWidth, BandHeight);

            // 초상화가 카드 면을 거의 덮으므로 테두리는 초상화 '위'에 그린다.
            // 흰 배경 초상화에서도 카드 형태(컷코너)가 보이게 하기 위한 것이다.
            Image cardOutline = NewImage(root, "CardOutline", Color.white);
            cardOutline.sprite = UIShapes.CutCornerOutline(9, UITheme.Outline, 1);
            cardOutline.type = Image.Type.Sliced;
            Place(cardOutline.rectTransform, 0f, 0f, CanvasWidth, CanvasWidth);

            // 지금 행동 중인 유닛을 알리는 앰버 테두리.
            _actingOutline = NewImage(root, "ActingOutline", Color.white);
            _actingOutline.sprite = UIShapes.CutCornerOutline(9, UITheme.Accent, 2);
            _actingOutline.type = Image.Type.Sliced;
            Place(_actingOutline.rectTransform, 0f, 0f, CanvasWidth, CanvasWidth);
            _actingOutline.enabled = false;

            Image nameBand = UIBuild.Solid("NameBand", root, new Color(0.035f, 0.039f, 0.043f, 0.86f));
            Place(nameBand.rectTransform, 0f, NameBandTop, CanvasWidth, NameBandHeight);

            Image nameAccent = UIBuild.Solid("NameAccent", root, new Color(1f, 1f, 1f, 0.30f));
            Place(nameAccent.rectTransform, 7f, NameBandTop + 14f, 2f, 11f);

            // 이름표만은 캔버스 밖의 월드 TextMeshPro다.
            // TextMeshProUGUI는 이 월드 스페이스 캔버스에서 아예 그려지지 않았다
            // (같은 캔버스의 Image는 멀쩡하고, 라틴 문자·스케일 10에서도 안 나왔다).
            // 원인을 더 파는 대신, 월드 공간용으로 만들어진 쪽을 쓴다 —
            // MeshRenderer로 그려지므로 정렬 순서도 카드 렌더러와 같은 축에서 다룰 수 있다.
            var nameObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshPro));
            nameObject.transform.SetParent(transform, false);
            _nameLabel = nameObject.GetComponent<TextMeshPro>();
            _nameRenderer = nameObject.GetComponent<MeshRenderer>();
            _nameLabel.font = Font;
            _nameLabel.color = UITheme.TextPrimary;
            _nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _nameLabel.raycastTarget = false;
            LayOutName(cardSize);

            // ── 궁극기 링 ──
            _ultRing = UIBuild.RadialBar("UltRing", root, UITheme.Mana,
                new Color(1f, 1f, 1f, 0.14f), 96, 0.68f);
            var ringTrack = (RectTransform)_ultRing.rectTransform.parent;
            ringTrack.anchorMin = new Vector2(1f, 1f);
            ringTrack.anchorMax = new Vector2(1f, 1f);
            ringTrack.pivot = new Vector2(1f, 1f);
            ringTrack.sizeDelta = new Vector2(RingSize, RingSize);
            ringTrack.anchoredPosition = new Vector2(RingOverhang, RingOverhang);

            // ── 체력 + 방어막(같은 트랙, 방어막이 오른쪽에 이어 붙는다) ──
            Image hpTrack = UIBuild.Solid("HpTrack", root, new Color(1f, 1f, 1f, 0.07f));
            Place(hpTrack.rectTransform, 0f, HpTop, CanvasWidth, HpHeight);
            _hpFill = NewImage(hpTrack.transform, "HpFill", UITheme.Hp);
            _shieldFill = NewImage(hpTrack.transform, "ShieldFill", UITheme.Shield);

            // ── 행동 게이지 ──
            Image actionTrack = UIBuild.Solid("ActionTrack", root, new Color(1f, 1f, 1f, 0.07f));
            Place(actionTrack.rectTransform, 0f, ActionTop, CanvasWidth, ActionHeight);
            _actionFill = NewImage(actionTrack.transform, "ActionFill", UITheme.ActionYellow);

            // ── 상태 점 ──
            for (int i = 0; i < MaxDots; i++)
            {
                Image dot = UIBuild.Solid($"Status{i}", root, UITheme.TextMuted);
                Place(dot.rectTransform, i * DotGap, DotTop, DotSize, DotSize);
                dot.enabled = false;
                _dots[i] = dot;
            }

            // 맞았을 때 카드 전체가 하얗게 번쩍인다. 카드 위 무엇보다 나중에 만들어 맨 앞에 둔다.
            _hitFlash = NewImage(root, "HitFlash", new Color(1f, 1f, 1f, 0f));
            _hitFlash.sprite = UIShapes.CutCorner(9, Color.white, UIShapes.Corner.Diagonal);
            _hitFlash.type = Image.Type.Sliced;
            Place(_hitFlash.rectTransform, 0f, 0f, CanvasWidth, CanvasWidth);
            _hitFlash.raycastTarget = false;

            Show(false);
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
            rect.sizeDelta = new Vector2(cardSize * 0.81f, cardSize * 0.16f);
            // 이름 띠(카드 위에서 72~100%)의 한가운데.
            rect.localPosition = new Vector3(cardSize * 0.025f, cardSize * (0.5f - 0.86f), -0.02f);

            // 글자 크기를 직접 주지 않고 상자에 맞춰 키우게 한다.
            // 이 프로젝트의 NotoSansKR 가변폰트 SDF는 월드 공간에서 메트릭이 기대보다
            // 훨씬 작게 잡혀(같은 글자의 preferredWidth가 1/10 수준) 직접 준 크기로는
            // 몇 픽셀짜리로 그려져 보이지 않았다. 자동 맞춤이면 그 차이를 알아서 흡수한다.
            _nameLabel.enableAutoSizing = true;
            _nameLabel.fontSizeMin = 0.02f;
            _nameLabel.fontSizeMax = cardSize * 0.6f;
            _nameLabel.overflowMode = TextOverflowModes.Truncate;
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
            _factionBand.color = unit.IsEnemy ? UITheme.Enemy : UITheme.TextSecondary;

            // 대기석 카드는 전투 정보를 들지 않는다. 이름표만 남는다.
            _ultRing.transform.parent.gameObject.SetActive(combatHud);
            _hpFill.transform.parent.gameObject.SetActive(combatHud);
            _actionFill.transform.parent.gameObject.SetActive(combatHud);
            if (!combatHud)
            {
                foreach (Image dot in _dots) dot.enabled = false;
                _actingOutline.enabled = false;
            }

            Tick();
        }

        public void Tick()
        {
            if (_unit == null) return;

            AdvanceReaction(Time.deltaTime);
            if (!_combatHud) return;

            // 체력이 줄었으면 맞은 것이다. 이벤트를 따로 걸지 않고 값의 변화로 잡는다 —
            // 방어막이 대신 깎이든 지속 피해든 한 곳에서 잡히기 때문이다.
            if (_lastHp >= 0 && _unit.HpCurr < _lastHp) PlayHit();
            _lastHp = _unit.HpCurr;

            // ── 체력 · 방어막 ──
            float hpRatio = _unit.HpMax > 0 ? Mathf.Clamp01(_unit.HpCurr / (float)_unit.HpMax) : 0f;
            // 방어막도 최대 체력을 기준으로 잰다. 그래야 같은 바에 이어 붙일 수 있다.
            float shieldRatio = _unit.HpMax > 0 ? Mathf.Clamp01(_unit.ShieldCurr / (float)_unit.HpMax) : 0f;
            shieldRatio = Mathf.Min(shieldRatio, 1f - hpRatio); // 바를 넘치면 잘라 보여준다

            SetSpan(_hpFill.rectTransform, 0f, hpRatio);
            SetSpan(_shieldFill.rectTransform, hpRatio, hpRatio + shieldRatio);
            _hpFill.enabled = hpRatio > 0f;
            _shieldFill.enabled = shieldRatio > 0f;
            // 적은 비율과 무관하게 적색, 아군은 30% 이하부터 붉어진다.
            _hpFill.color = _unit.IsEnemy ? UITheme.Enemy : UITheme.HpColor(hpRatio);

            // ── 궁극기 링 ──
            int resourceMax = _unit.UltimateResourceMax > 0 ? _unit.UltimateResourceMax : _unit.ManaMax;
            _ultRing.fillAmount = resourceMax > 0 ? Mathf.Clamp01(_unit.ManaCurr / (float)resourceMax) : 0f;

            // ── 행동 게이지 ──
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            SetSpan(_actionFill.rectTransform, 0f, scheduler?.ActionProgress(_unit) ?? 0f);
            _actingOutline.enabled = scheduler != null && ReferenceEquals(scheduler.ActingUnit, _unit);

            RefreshStatusDots();

            bool alive = _unit.isActive && _unit.HpCurr > 0;
            _group.alpha = alive ? 1f : 0.38f;
            if (_nameLabel != null)
            {
                // 이름표는 캔버스 밖이라 CanvasGroup이 닿지 않는다. 알파를 직접 맞춘다.
                Color nameColor = UITheme.TextPrimary;
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

        private void RefreshStatusDots()
        {
            IReadOnlyList<UnitStatus> statuses = _unit.ActiveStatuses;
            for (int i = 0; i < _dots.Length; i++)
            {
                bool has = statuses != null && i < statuses.Count && statuses[i] != null;
                _dots[i].enabled = has;
                if (has) _dots[i].color = statuses[i].IsBeneficial ? UITheme.Positive : UITheme.Danger;
            }
        }

        private void Show(bool visible)
        {
            if (_frame != null) _frame.enabled = visible;
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

        /// <summary>맞는 순간 하얗게 번쩍이며 뒤로 밀린다.</summary>
        private void PlayHit()
        {
            _flash = 1f;
            _punch = 1f;
            _recoil = 1f;
        }

        private void ResetReaction()
        {
            _flash = _punch = _lunge = _recoil = 0f;
            _lastHp = -1;
            ApplyReaction();
        }

        private void AdvanceReaction(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            bool moving = _flash > 0f || _punch > 0f || _lunge > 0f || _recoil > 0f;
            if (!moving) return;

            _flash = Mathf.Max(0f, _flash - deltaTime / FlashTime);
            _punch = Mathf.Max(0f, _punch - deltaTime / PunchTime);
            _lunge = Mathf.Max(0f, _lunge - deltaTime / LungeTime);
            _recoil = Mathf.Max(0f, _recoil - deltaTime / PunchTime);
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
            var offset = new Vector3(offsetX, 0f, 0f);

            transform.localPosition = offset;
            transform.localScale = Vector3.one * scale;

            if (_portrait == null) return;

            _portrait.localPosition = offset;
            _portrait.localScale = _portraitBaseScale * scale;
        }

        // ── 조립 헬퍼 ────────────────────────────────────────────────

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
