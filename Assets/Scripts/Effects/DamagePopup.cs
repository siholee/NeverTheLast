using System.Collections.Generic;
using Effects.Vfx;
using Entities;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Effects
{
    /// <summary>떠오르는 전투 숫자의 종류. 색과 크기를 가른다.</summary>
    public enum CombatNumberKind
    {
        /// <summary>체력에 들어간 피해.</summary>
        Damage,

        /// <summary>방어막이 대신 받은 피해.</summary>
        Shielded,

        /// <summary>회복량.</summary>
        Heal,

        /// <summary>빗나감 등 숫자가 없는 알림.</summary>
        Miss,

        /// <summary>횟수제 무적이 막은 타격. 숫자가 없다.</summary>
        Nullified,
        /// <summary>피해와 별개로 원소·상태 부여에 저항.</summary>
        EffectResisted,
    }

    /// <summary>
    /// 맞은 자리에 찍히는 피해 숫자. 붕괴: 스타레일의 전투 숫자를 본뜬다.
    ///
    /// 전투가 자동으로 굴러가므로 <b>무엇이 얼마나 아팠는지</b>를 체력 바의 변화만으로
    /// 읽게 두면 놓친다. 숫자는 처음에 크게 찍혀 제 크기로 내려앉고(슬램인), 잠깐 머문 뒤
    /// 위로 떠오르며 사라진다. 곧은 굵은 숫자에 흑연 외곽선을 둘러 어떤 배경에서도 읽히고,
    /// 원소색은 얇은 안쪽 림에만 남기며 치명타는 더 크고 금색 느낌표를 붙인다. 한 카드를 연타하면 숫자가
    /// 겹치지 않도록 위·양옆으로 엇갈려 쌓인다.
    ///
    /// 월드 공간 <see cref="TextMeshPro"/>를 쓴다. 카드 이름표와 같은 이유다 —
    /// TextMeshProUGUI는 이 프로젝트의 월드 캔버스에서 그려지지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        /// <summary>동시에 떠 있을 수 있는 숫자. 넘으면 가장 오래된 것부터 걷는다.</summary>
        private const int MaxConcurrent = 18;

        private const float Lifetime = 0.82f;

        /// <summary>크게 찍혀 제 크기로 내려앉는 데 걸리는 시간.</summary>
        private const float PopTime = 0.10f;

        /// <summary>찍히는 순간의 배율(제 크기 대비).</summary>
        private const float SlamScale = 1.18f;

        /// <summary>이 시간 안에 같은 카드에 또 뜨는 숫자는 앞선 숫자와 겹치지 않게 쌓는다.</summary>
        private const float StackWindow = 0.55f;

        /// <summary>쌓임으로 밀어 올리는 단의 상한. 넘으면 같은 자리로 돌아온다.</summary>
        private const int MaxStack = 4;

        /// <summary>치명타 색. 원소색을 버리고 금색으로 간다 — 색이 곧 '터졌다'는 신호다.</summary>
        private static readonly Color CritColor = new(1f, 0.839f, 0.369f, 1f);

        private static readonly Color DamageColor = new(1f, 0.957f, 0.894f, 1f);

        /// <summary>외곽선. 밝은 배경에서도 숫자가 뜨도록 거의 검은 남색으로 둘러싼다.</summary>
        private static readonly Color OutlineColor = new(0.075f, 0.125f, 0.20f, 1f);

        private static readonly Color OuterShadowColor = new(0.018f, 0.039f, 0.065f, 0.88f);

        private static readonly Color CritOutlineColor = new(0.357f, 0.169f, 0.063f, 1f);

        private static readonly List<DamagePopup> Live = new();
        private static TMP_FontAsset _font;
        private static bool _fontSearched;

        private TextMeshPro _label;
        private TextMeshPro _shadowLabel;
        private readonly System.Collections.Generic.List<TextMeshPro> _edges = new();
        private readonly System.Collections.Generic.List<TextMeshPro> _accentEdges = new();
        private MeshRenderer _renderer;
        private SpriteRenderer _critSlash;

        /// <summary>숫자를 띄운 대상. 살아 있는 동안은 이 카드를 따라간다.</summary>
        private Unit _target;

        /// <summary>대상 카드 기준 오프셋. 연타로 겹치지 않게 흩어 둔 자리다.</summary>
        private Vector3 _offset;

        /// <summary>대상을 잃었다. 이때부터는 화면 좌표에 못 박는다.</summary>
        private bool _detached;

        /// <summary>대상을 잃은 순간의 화면 좌표(z는 카메라로부터의 거리).</summary>
        private Vector3 _frozenViewport;

        private Vector3 _anchor;
        private Vector3 _drift;
        private float _baseScale;
        private float _age;

        /// <summary>뜬 시각. 같은 카드에 연달아 뜨는 숫자를 세는 데 쓴다.</summary>
        private float _bornAt;

        /// <summary>카드 이름표와 같은 한글 폰트. 없으면 TMP 기본값으로 떨어진다.</summary>
        private static TMP_FontAsset Font
        {
            get
            {
                if (_fontSearched) return _font;
                _fontSearched = true;
                _font = Resources.Load<TMP_FontAsset>("Font/NotoSansKR-VariableFont_wght SDF")
                        ?? Resources.Load<TMP_FontAsset>("Font/NotoSansKR-Regular SDF")
                        ?? TMP_Settings.defaultFontAsset;
                return _font;
            }
        }

        /// <summary>
        /// 대상 위에 숫자를 띄운다.
        /// </summary>
        /// <param name="amount">표시할 값. <see cref="CombatNumberKind.Miss"/>면 무시한다.</param>
        /// <param name="isCrit">치명타는 더 크고 금색이다.</param>
        /// <param name="accent">원소 색. 마법 피해에만 쓰고 물리는 흰색으로 둔다.</param>
        /// <param name="weight">0~1. 대상 최대 체력 대비 크기로, 글자 크기를 키운다.</param>
        public static void Show(Unit target, int amount, bool isCrit, Color accent,
            CombatNumberKind kind = CombatNumberKind.Damage, float weight = 0f)
        {
            if (target == null) return;
            if (kind != CombatNumberKind.Miss && kind != CombatNumberKind.Nullified && kind != CombatNumberKind.EffectResisted && amount <= 0) return;
            if (isCrit && kind == CombatNumberKind.Damage) Managers.AudioManager.PlayCue("common.crit");

            Trim();

            // 같은 카드에 방금 뜬 숫자가 몇 개인가. 그만큼 위로 밀고 좌우로 번갈아 벌린다.
            int stack = 0;
            foreach (DamagePopup other in Live)
            {
                if (other != null && other._target == target && Time.time - other._bornAt < StackWindow) stack++;
            }

            var go = new GameObject("DamagePopup");
            var popup = go.AddComponent<DamagePopup>();
            popup.Build(target, amount, isCrit, accent, kind, Mathf.Clamp01(weight), stack % MaxStack);
            Live.Add(popup);
        }

        private static void Trim()
        {
            Live.RemoveAll(item => item == null);
            while (Live.Count >= MaxConcurrent)
            {
                DamagePopup oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        private void Build(Unit target, int amount, bool isCrit, Color accent,
            CombatNumberKind kind, float weight, int stack)
        {
            float cardSize = Cell.CardSize;
            _target = target;
            _bornAt = Time.time;

            // 같은 대상을 연달아 때리면 숫자가 포개진다. 먼저 뜬 숫자 위로 한 단씩 올리고
            // 좌우로 번갈아 벌려 전부 읽히게 한다. 얼굴·오브·하단 패널을 피하려 첫 숫자부터
            // 실루엣 바깥쪽 어깨 아래 안전 슬롯에 둔다. 흰 원화가 커져도 얼굴과 눈을 가르지 않도록
            // 첫 숫자부터 카드 중심에서 40% 이상 옆으로 밀고, 연타는 바깥쪽과 위쪽으로 더 벌린다.
            float side = target.IsEnemy ? -1f : 1f;
            float alternate = stack % 2 == 0 ? 1f : -1f;
            float spread = stack == 0 ? 0f : alternate * cardSize * (0.045f + 0.022f * stack);
            _offset = new Vector3(
                side * cardSize * Random.Range(0.40f, 0.47f) + spread,
                cardSize * (0.11f + 0.13f * stack),
                -0.5f);
            _drift = new Vector3(side * cardSize * 0.02f, cardSize * 0.08f, 0f);
            _anchor = VfxSpace.WorldPoint(target) + _offset;

            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            _label = go.GetComponent<TextMeshPro>();
            _renderer = go.GetComponent<MeshRenderer>();

            // 글자 크기는 이 상자가 정한다. 가로를 넉넉히 잡아 자릿수가 늘어도
            // 세로가 먼저 걸리게 두면 숫자 높이가 항상 같다.
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(cardSize * 1.20f, cardSize * 0.26f);

            _label.font = Font;
            // 카드 숫자와 같은 월드 글꼴 재질(굵기 보정 포함)을 쓴다. 폰트 기본 재질로는 가는 글자가 더 가늘었다.
            Managers.UI.Core.UIBuild.ApplyFont(_label);
            _label.text = kind switch
            {
                CombatNumberKind.Miss => "회피",
                CombatNumberKind.EffectResisted => "저항",
                CombatNumberKind.Nullified => "무효",
                CombatNumberKind.Heal => "+" + amount,
                _ => amount + (isCrit ? "!" : ""),
            };
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            _label.color = ColorFor(kind, isCrit, accent);

            // 카드 이름표와 같은 이유로 자동 맞춤을 쓴다. 월드 공간에서 이 폰트의
            // 메트릭이 기대보다 훨씬 작게 잡혀, 직접 준 크기로는 몇 픽셀로 그려진다.
            // 상한은 걸리지 않을 만큼 크게 둔다 — 크기를 정하는 것은 상자다.
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 0.02f;
            _label.fontSizeMax = cardSize * 3f;
            _label.overflowMode = TextOverflowModes.Overflow;

            _label.fontStyle = FontStyles.Bold;
            // SDF 외곽선은 끈다 — 가는 글꼴에서는 외곽선이 글자 안쪽까지 먹어 들어가 숫자 속이 비어 보였다
            // (Play 모드 확인). 테두리는 아래의 검은 사본 여덟 장이 맡는다.
            _label.outlineWidth = 0f;
            _label.outlineColor = isCrit ? CritOutlineColor : OutlineColor;

            // 크기 차이는 절제한다 — 큰 값이 화면을 덮으면 애니메이션이 가려진다.
            // 치명타는 그 위에 한 번 더 크게 얹는다.
            _baseScale = Mathf.Lerp(0.82f, 1.12f, weight) * (isCrit ? 1.32f : 1f);
            if (kind == CombatNumberKind.Shielded || kind == CombatNumberKind.Miss || kind == CombatNumberKind.EffectResisted ||
                kind == CombatNumberKind.Nullified)
            {
                _baseScale *= 0.8f;
            }

            int layer = VfxSpace.SortingLayer(target);
            _renderer.sortingLayerID = layer;
            // 투사체와 근접 연출보다도 위. 숫자가 이펙트에 묻히면 읽을 수 없다.
            _renderer.sortingOrder = VfxSpace.OverlaySortingOrder + 30;

            // 한 장의 아래·오른쪽 그림자가 밝은 하늘과 흰 원화 양쪽에서 숫자 덩어리를 먼저 잡는다.
            // 본문 SDF 외곽은 안쪽을 먹으므로 본문이 아닌 어두운 사본에만 넓은 외곽을 준다.
            GameObject shadow = Instantiate(go, transform);
            shadow.name = "OuterShadow";
            shadow.transform.localPosition = go.transform.localPosition +
                                             new Vector3(cardSize * 0.009f, -cardSize * 0.012f, 0f);
            _shadowLabel = shadow.GetComponent<TextMeshPro>();
            _shadowLabel.color = OuterShadowColor;
            _shadowLabel.outlineColor = OuterShadowColor;
            _shadowLabel.outlineWidth = 0.22f;
            var shadowRenderer = shadow.GetComponent<MeshRenderer>();
            shadowRenderer.sortingLayerID = layer;
            shadowRenderer.sortingOrder = VfxSpace.OverlaySortingOrder + 27;

            // 굵은 테두리 — 같은 글자를 검게 여덟 방향으로 밀어 뒤에 깐다. SDF 외곽선만으로는
            // 가는 UI 폰트의 여백 한계 때문에 밝은 전장 위에서 숫자가 흐려졌다.
            // 밝은 하늘/백색 바닥에서는 8방향 사본이 실제 캡처 기준 1.5~2px은 남아야 한다.
            float edge = cardSize * 0.0105f;
            Vector2[] offsets =
            {
                new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
                new(0.72f, 0.72f), new(-0.72f, 0.72f), new(0.72f, -0.72f), new(-0.72f, -0.72f),
            };
            Color ink = isCrit ? CritOutlineColor : OutlineColor;
            foreach (Vector2 offset in offsets)
            {
                GameObject copy = Instantiate(go, transform);
                copy.name = "Edge";
                copy.transform.localPosition = go.transform.localPosition + (Vector3)(offset * edge);
                var edgeLabel = copy.GetComponent<TextMeshPro>();
                edgeLabel.outlineWidth = 0f;
                edgeLabel.color = ink;
                var edgeRenderer = copy.GetComponent<MeshRenderer>();
                edgeRenderer.sortingLayerID = layer;
                edgeRenderer.sortingOrder = VfxSpace.OverlaySortingOrder + 28;
                _edges.Add(edgeLabel);
            }

            // 채움 바로 아래의 1px 안쪽 림만 원소색을 쓴다. 숫자 전체를 원소색으로 칠하면
            // 밝은 이펙트 위에서 명도 계층이 무너진다.
            float inner = cardSize * 0.0032f;
            Color innerColor = isCrit ? new Color(0.92f, 0.62f, 0.18f, 1f) : Color.Lerp(accent, Color.white, 0.12f);
            Vector2[] innerOffsets = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
            foreach (Vector2 offset in innerOffsets)
            {
                GameObject copy = Instantiate(go, transform);
                copy.name = "InnerRim";
                copy.transform.localPosition = go.transform.localPosition + (Vector3)(offset * inner);
                var rimLabel = copy.GetComponent<TextMeshPro>();
                rimLabel.outlineWidth = 0f;
                rimLabel.color = innerColor;
                var rimRenderer = copy.GetComponent<MeshRenderer>();
                rimRenderer.sortingLayerID = layer;
                rimRenderer.sortingOrder = VfxSpace.OverlaySortingOrder + 29;
                _accentEdges.Add(rimLabel);
            }

            if (isCrit)
            {
                var slashObject = new GameObject("CritSlash", typeof(SpriteRenderer));
                slashObject.transform.SetParent(transform, false);
                _critSlash = slashObject.GetComponent<SpriteRenderer>();
                _critSlash.sprite = UIShapes.RoundedRect(2, Color.white);
                _critSlash.color = new Color(0.96f, 0.66f, 0.20f, 0.72f);
                Vector2 slashBounds = _critSlash.sprite.bounds.size;
                slashObject.transform.localScale = new Vector3(cardSize * 0.34f / slashBounds.x,
                    cardSize * 0.012f / slashBounds.y, 1f);
                slashObject.transform.localPosition = new Vector3(0f, -cardSize * 0.075f, 0.03f);
                slashObject.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
                _critSlash.sortingLayerID = layer;
                _critSlash.sortingOrder = VfxSpace.OverlaySortingOrder + 27;
            }

            ApplyFrame(0f);
        }

        private static Color ColorFor(CombatNumberKind kind, bool isCrit, Color accent)
        {
            switch (kind)
            {
                case CombatNumberKind.Shielded:
                case CombatNumberKind.Nullified:
                    return UITheme.Shield;
                case CombatNumberKind.Heal:
                    return Color.Lerp(UITheme.Positive, Color.white, 0.25f);
                case CombatNumberKind.Miss:
                case CombatNumberKind.EffectResisted:
                    // 밝은 테마의 글자색은 거의 검정이라 어두운 외곽선 위에서 사라진다.
                    return new Color(0.86f, 0.89f, 0.93f, 1f);
                default:
                    return isCrit ? CritColor : DamageColor;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);
            ApplyFrame(t);

            if (t < 1f) return;
            Live.Remove(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// 숫자가 설 자리를 매 프레임 다시 잡는다.
        ///
        /// 생성 시점의 월드 좌표를 그대로 붙들고 있으면 <b>허공에 숫자가 남는다</b>.
        /// 유닛이 쓰러지면 칸이 비고, 빈 칸이 지워지면서 남은 카드가 가운데로 다시 모이며
        /// 카메라까지 다시 잡히기 때문이다. 실제로 전장 빈 자리에 433이 떠 있었다.
        ///
        /// 그래서 살아 있는 동안은 대상 카드를 따라가고, 대상을 잃는 순간
        /// <b>그때의 화면 좌표에 못 박는다</b>. 마지막 일격의 숫자는 카드가 사라져도
        /// 보이던 자리에 그대로 남았다가 사라진다.
        /// </summary>
        private void RefreshAnchor()
        {
            if (!_detached && TargetVisible())
            {
                _anchor = VfxSpace.WorldPoint(_target) + _offset;
                return;
            }

            Camera camera = Camera.main;
            if (camera == null) return;

            if (!_detached)
            {
                _detached = true;
                _frozenViewport = camera.WorldToViewportPoint(_anchor);
            }

            _anchor = camera.ViewportToWorldPoint(_frozenViewport);
        }

        /// <summary>대상이 아직 화면에 카드를 갖고 있는가. 쓰러지면 칸을 반납하므로 여기서 갈린다.</summary>
        private bool TargetVisible()
            => _target != null && _target.isActive &&
               _target.currentCell != null;

        private void ApplyFrame(float t)
        {
            RefreshAnchor();

            // 슬램인: 크게 찍혀 빠르게 제 크기로 내려앉는다(처음에 작았다 커지는 것이 아니다).
            // 그 뒤로는 머물다가 위로 떠오르며 마지막 3할에서 사라진다.
            float pop = Mathf.Clamp01(_age / PopTime);
            float settle = 1f - (1f - pop) * (1f - pop) * (1f - pop);
            float scale = Mathf.Lerp(_baseScale * SlamScale, _baseScale, settle);

            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 1f, t));
            transform.position = _anchor + _drift * rise;
            transform.localScale = Vector3.one * scale;

            Color color = _label.color;
            float appear = Mathf.Clamp01(_age / 0.03f);
            color.a = appear * (1f - Mathf.SmoothStep(0.68f, 1f, t));
            _label.color = color;

            // 외곽선도 함께 걷는다. 글자만 사라지고 검은 테두리가 남으면 유령처럼 보인다.
            Color outline = _label.outlineColor;
            outline.a = color.a;
            _label.outlineColor = outline;
            foreach (TextMeshPro edge in _edges)
            {
                Color ink = edge.color;
                ink.a = color.a * 0.95f;
                edge.color = ink;
            }
            if (_shadowLabel != null)
            {
                Color shadow = _shadowLabel.color;
                shadow.a = color.a * 0.88f;
                _shadowLabel.color = shadow;
                Color shadowOutline = _shadowLabel.outlineColor;
                shadowOutline.a = color.a * 0.88f;
                _shadowLabel.outlineColor = shadowOutline;
            }
            foreach (TextMeshPro edge in _accentEdges)
            {
                Color rim = edge.color;
                rim.a = color.a;
                edge.color = rim;
            }
            if (_critSlash != null)
            {
                Color slash = _critSlash.color;
                slash.a = color.a * 0.72f;
                _critSlash.color = slash;
            }
        }

        private void OnDestroy() => Live.Remove(this);
    }
}
