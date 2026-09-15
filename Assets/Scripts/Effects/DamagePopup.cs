using System.Collections.Generic;
using Effects.Projectiles;
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
    }

    /// <summary>
    /// 맞은 자리에서 튀어 오르는 피해 숫자.
    ///
    /// 전투가 자동으로 굴러가므로 <b>무엇이 얼마나 아팠는지</b>를 체력 바의 변화만으로
    /// 읽게 두면 놓친다. 숫자는 튀어 올랐다 떨어지며 사라져, 큰 값일수록 크게 보인다.
    ///
    /// 월드 공간 <see cref="TextMeshPro"/>를 쓴다. 카드 이름표와 같은 이유다 —
    /// TextMeshProUGUI는 이 프로젝트의 월드 캔버스에서 그려지지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        /// <summary>동시에 떠 있을 수 있는 숫자. 넘으면 가장 오래된 것부터 걷는다.</summary>
        private const int MaxConcurrent = 18;

        private const float Lifetime = 0.78f;

        /// <summary>튀어 오르는 데 걸리는 시간. 이 뒤로는 천천히 떠오르기만 한다.</summary>
        private const float PopTime = 0.14f;

        private static readonly List<DamagePopup> Live = new();
        private static TMP_FontAsset _font;
        private static bool _fontSearched;

        private TextMeshPro _label;
        private MeshRenderer _renderer;

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
        private float _peakScale;
        private float _age;

        /// <summary>카드 이름표와 같은 한글 폰트. 없으면 TMP 기본값으로 떨어진다.</summary>
        private static TMP_FontAsset Font
        {
            get
            {
                if (_fontSearched) return _font;
                _fontSearched = true;
                _font = Resources.Load<TMP_FontAsset>("Font/NotoSansKR-VariableFont_wght SDF")
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
            if (kind != CombatNumberKind.Miss && amount <= 0) return;

            Trim();

            var go = new GameObject("DamagePopup");
            var popup = go.AddComponent<DamagePopup>();
            popup.Build(target, amount, isCrit, accent, kind, Mathf.Clamp01(weight));
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
            CombatNumberKind kind, float weight)
        {
            float cardSize = Cell.CardSize;
            _target = target;

            // 같은 대상을 연달아 때리면 숫자가 포개진다. 좌우로 흩어 읽히게 한다.
            float side = target.IsEnemy ? 1f : -1f;
            _offset = new Vector3(
                side * cardSize * Random.Range(0.02f, 0.16f), cardSize * 0.18f, -0.5f);
            _drift = new Vector3(side * cardSize * 0.10f, cardSize * 0.42f, 0f);
            _anchor = CombatVfxAssets.WorldPoint(target) + _offset;

            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            _label = go.GetComponent<TextMeshPro>();
            _renderer = go.GetComponent<MeshRenderer>();

            // 글자 크기는 이 상자가 정한다. 가로를 넉넉히 잡아 자릿수가 늘어도
            // 세로가 먼저 걸리게 두면 숫자 높이가 항상 같다.
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(cardSize * 1.20f, cardSize * 0.26f);

            _label.font = Font;
            _label.text = kind == CombatNumberKind.Miss ? "회피" : amount.ToString();
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
            _label.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;

            // 큰 피해일수록 크게 뜬다. 치명타는 그 위에 한 번 더 얹는다.
            _baseScale = Mathf.Lerp(0.72f, 1.25f, weight) * (isCrit ? 1.35f : 1f);
            if (kind == CombatNumberKind.Shielded || kind == CombatNumberKind.Miss)
            {
                _baseScale *= 0.8f;
            }
            _peakScale = _baseScale * 1.30f;

            int layer = CombatVfxAssets.SortingLayer(target);
            _renderer.sortingLayerID = layer;
            // 투사체와 근접 연출보다도 위. 숫자가 이펙트에 묻히면 읽을 수 없다.
            _renderer.sortingOrder = CardProjectile.OverlaySortingOrder + 30;

            ApplyFrame(0f);
        }

        private static Color ColorFor(CombatNumberKind kind, bool isCrit, Color accent)
        {
            switch (kind)
            {
                case CombatNumberKind.Shielded:
                    return UITheme.Shield;
                case CombatNumberKind.Heal:
                    return UITheme.Positive;
                case CombatNumberKind.Miss:
                    return UITheme.TextSecondary;
                default:
                    // 치명타는 원소색을 버리고 금색으로 간다 — 색이 곧 '터졌다'는 신호다.
                    if (isCrit) return UITheme.Accent;
                    return Color.Lerp(accent, Color.white, 0.45f);
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
                _anchor = CombatVfxAssets.WorldPoint(_target) + _offset;
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
               (_target.currentCell != null || _target.SummonView != null);

        private void ApplyFrame(float t)
        {
            RefreshAnchor();

            // 처음엔 튀어 오르고(과하게 커졌다 제자리), 뒤로는 천천히 떠오르며 사라진다.
            float pop = Mathf.Clamp01(_age / PopTime);
            float scale = pop < 1f
                ? Mathf.Lerp(_baseScale * 0.35f, _peakScale, pop)
                : Mathf.Lerp(_peakScale, _baseScale, Mathf.Clamp01((_age - PopTime) / 0.10f));

            float rise = Mathf.SmoothStep(0f, 1f, t);
            transform.position = _anchor + _drift * rise;
            transform.localScale = Vector3.one * scale;

            Color color = _label.color;
            color.a = 1f - Mathf.SmoothStep(0.55f, 1f, t);
            _label.color = color;
        }

        private void OnDestroy() => Live.Remove(this);
    }
}
