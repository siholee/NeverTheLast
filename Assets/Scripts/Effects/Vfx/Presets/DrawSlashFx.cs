using Entities;
using UnityEngine;

namespace Effects.Vfx.Presets
{
    /// <summary>카드 위에서 좌하단→우상단으로 검을 뽑는 한 줄 직선 발도 프리셋의 색·형태다.</summary>
    public sealed class DrawSlashStyle
    {
        public Color BodyColor = new(0.006f, 0.006f, 0.009f, 0.94f);
        public Color EdgeColor = new(0.34f, 0.008f, 0.015f, 0.76f);
        public Color CoreColor = new(1f, 0.055f, 0.035f, 0.88f);
        public float LengthInCards = 1.30f;
        public float ThicknessInCards = 0.075f;
        public float AngleDegrees = 34f;
        public float Duration = 0.18f;
        public Vector2 OffsetInCards = new(0f, -0.16f);
    }

    /// <summary>
    /// 시전자 카드 위에 한 줄 직선 발도를 그린다. 원소별 팔레트와 길이·굵기·각도를 주입할 수 있어
    /// 다른 캐릭터도 <c>DrawSlashFx.Play(caster, style)</c> 한 줄로 재사용한다.
    /// </summary>
    public static class DrawSlashFx
    {
        /// <param name="mirror">적 카드처럼 반대 진영에서 쓸 때 우하단→좌상단으로 좌우 반전한다.</param>
        public static void Play(Unit caster, DrawSlashStyle style = null, bool mirror = false)
        {
            if (caster == null) return;
            style ??= new DrawSlashStyle();
            style.BodyColor = BattleVfxStyle.Body(style.BodyColor,style.EdgeColor);
            style.CoreColor = BattleVfxStyle.Light(style.CoreColor);

            float size = CardMotion.ViewOf(caster)?.CardSize ?? VfxSpace.CardSize;
            Vector3 point = VfxSpace.WorldPoint(caster) + (Vector3)(style.OffsetInCards * size);
            int layer = VfxSpace.SortingLayer(caster);
            float life = VfxTiming.AtLeastFrames(style.Duration, 5);
            float length = size * Mathf.Max(0.2f, style.LengthInCards);
            float thickness = size * Mathf.Max(0.012f, style.ThicknessInCards);
            float angle = mirror ? 180f - style.AngleDegrees : style.AngleDegrees;
            Vector3 direction = Direction(angle);

            // 중심 확대는 시작점이 양쪽으로 갈라져 발도 방향을 읽을 수 없었다.
            // 루트를 낮은 쪽 끝에 두고 몸체·코어·잔상·선두광을 모두 같은 진행률로 이동시킨다.
            var root = new GameObject("DrawSlashFx");
            root.transform.position = point - direction * length * 0.5f;
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            root.AddComponent<DrawSlashInstance>().Initialize(style, length, thickness, life, layer);
        }

        private static Vector3 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        /// <summary>한쪽 끝에서만 길어지는 와이프와 이동 선두광을 묶어 공용 발도의 방향을 보존한다.</summary>
        private sealed class DrawSlashInstance : MonoBehaviour
        {
            private SpriteRenderer _body;
            private SpriteRenderer _edge;
            private SpriteRenderer _core;
            private SpriteRenderer _trail;
            private SpriteRenderer _afterimage;
            private SpriteRenderer _tip;
            private DrawSlashStyle _style;
            private Vector2 _streakBounds;
            private Vector2 _glintBounds;
            private float _length;
            private float _thickness;
            private float _life;
            private float _age;

            public void Initialize(DrawSlashStyle style, float length, float thickness, float life, int layer)
            {
                _style = style;
                _length = length;
                _thickness = thickness;
                _life = life;
                _streakBounds = VfxArt.Streak != null ? VfxArt.Streak.bounds.size : Vector2.one;
                _glintBounds = VfxArt.Glint != null ? VfxArt.Glint.bounds.size : Vector2.one;

                // 밝은 판의 실루엣은 비가산 몸체가, 어두운 판의 날은 좁은 가산 코어가 맡는다.
                _afterimage = VfxSpace.NewSprite(transform, "Afterimage", VfxArt.Streak, Color.clear,
                    layer, VfxSpace.OverlaySortingOrder + 19);
                _body = VfxSpace.NewSprite(transform, "AlphaBody", VfxArt.Streak, Color.clear,
                    layer, VfxSpace.OverlaySortingOrder + 20);
                _edge = VfxSpace.NewSprite(transform, "AlphaEdge", VfxArt.Streak, Color.clear,
                    layer, VfxSpace.OverlaySortingOrder + 21);
                _core = VfxSpace.NewSprite(transform, "AdditiveCore", VfxArt.Streak, Color.clear,
                    layer, VfxSpace.OverlaySortingOrder + 22, true);
                _trail = VfxSpace.NewSprite(transform, "MovingTrail", VfxArt.Streak, Color.clear,
                    layer, VfxSpace.OverlaySortingOrder + 23, true);
                _tip = VfxSpace.NewSprite(transform, "LeadingGlint", VfxArt.Glint, Color.clear,
                    layer, VfxSpace.OverlaySortingOrder + 24, true);
                Apply(0f);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                if (_age >= _life)
                {
                    Destroy(gameObject);
                    return;
                }

                Apply(_age / Mathf.Max(0.001f, _life));
            }

            private void Apply(float normalizedAge)
            {
                float drawFraction = 0.68f;
                float linear = Mathf.Clamp01(normalizedAge / drawFraction);
                float reveal = SmoothStep(linear);
                float visibleLength = Mathf.Max(_length * 0.015f, _length * reveal);
                float alpha = Visibility(normalizedAge, 0.62f);

                SetSegment(_body, 0f, visibleLength, _thickness * 1.72f,
                    WithAlpha(_style.BodyColor, alpha));
                SetSegment(_edge, 0f, visibleLength * 0.985f, _thickness * 1.02f,
                    WithAlpha(_style.EdgeColor, alpha * 0.94f));
                SetSegment(_core, 0f, visibleLength * 0.92f, _thickness * 0.34f,
                    WithAlpha(_style.CoreColor, alpha * 0.92f));

                // 선두 뒤의 짧은 가산 꼬리와 완성선의 낮은 잔상이 같은 방향으로 쌓인다.
                float trailLength = Mathf.Min(visibleLength, _length * 0.28f);
                float trailAlpha = alpha * Mathf.Sin(linear * Mathf.PI) * 0.58f;
                SetSegment(_trail, Mathf.Max(0f, visibleLength - trailLength), visibleLength,
                    _thickness * 0.46f, WithAlpha(_style.CoreColor, trailAlpha));
                SetSegment(_afterimage, 0f, visibleLength, _thickness * 0.42f,
                    WithAlpha(_style.EdgeColor, alpha * Mathf.Lerp(0.10f, 0.36f, reveal)));

                float tipAlpha = linear < 1f ? Mathf.Sin(linear * Mathf.PI) * alpha : 0f;
                float tipSize = _thickness * Mathf.Lerp(1.65f, 0.92f, linear);
                _tip.transform.localPosition = new Vector3(visibleLength, 0f, 0f);
                _tip.transform.localScale = new Vector3(
                    tipSize / Mathf.Max(0.0001f, _glintBounds.x),
                    tipSize * 0.58f / Mathf.Max(0.0001f, _glintBounds.y), 1f);
                _tip.color = WithAlpha(_style.CoreColor, tipAlpha);
            }

            private void SetSegment(SpriteRenderer renderer, float from, float to, float height, Color color)
            {
                if (renderer == null) return;
                float width = Mathf.Max(_length * 0.012f, to - from);
                renderer.transform.localPosition = new Vector3((from + to) * 0.5f, 0f, 0f);
                renderer.transform.localScale = new Vector3(
                    width / Mathf.Max(0.0001f, _streakBounds.x),
                    height / Mathf.Max(0.0001f, _streakBounds.y), 1f);
                renderer.color = color;
            }

            private static Color WithAlpha(Color color, float multiplier)
                => new(color.r, color.g, color.b, color.a * Mathf.Clamp01(multiplier));

            private static float Visibility(float age, float fadeOutFrom)
            {
                float fadeIn = SmoothStep(Mathf.Clamp01(age / 0.08f));
                if (age <= fadeOutFrom) return fadeIn;
                float fadeOut = 1f - SmoothStep((age - fadeOutFrom) / Mathf.Max(0.001f, 1f - fadeOutFrom));
                return fadeIn * fadeOut;
            }

            private static float SmoothStep(float value)
            {
                float t = Mathf.Clamp01(value);
                return t * t * (3f - 2f * t);
            }
        }
    }
}
