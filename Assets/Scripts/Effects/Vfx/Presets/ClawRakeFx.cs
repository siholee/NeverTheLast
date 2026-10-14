using System;
using System.Collections;
using Entities;
using UnityEngine;

namespace Effects.Vfx.Presets
{
    /// <summary>한 가닥 원화를 실제 발톱 수만큼 벌리고, 선두부터 방향성 있게 드러내는 색·동작 규격이다.</summary>
    public sealed class ClawRakeStyle
    {
        public Sprite Sprite;
        public int LineCount = 3;
        public float LengthInCards = 1.70f;
        public float ThicknessInCards = 0.13f;
        public float AngleDegrees = 29f;
        public float DrawDuration = 0.20f;
        public float CenterCrossFraction = 0.50f;
        public float LineDelay = 0.020f;
        public float LineSpacingInCards = 0.16f;
        public float LingerDuration = 0.10f;
        public float FadeDuration = 0.17f;
        public float RevealSoftness = 0.040f;
        public float TipWidth = 0.070f;
        public float CardKickStrength = 0.55f;
        public float SourceAngleDegrees = 28f;
        // 특정 피격 문양만 원화 띠에 맞출 수 있게 한다. 기존 부품은 같은 상한을 사용한다.
        public float PortraitWidthFraction = 1.10f;
        public float PortraitHeightFraction = .42f;
        public Vector2 OffsetInCards = new(0f, 0.08f);
        public Color BodyColor = new(0.006f, 0.003f, 0.005f, 0.96f);
        public Color EdgeColor = new(0.70f, 0.008f, 0.016f, 0.90f);
        public Color CoreColor = new(1f, 0.045f, 0.025f, 0.94f);

        /// <summary>가운데 발톱의 선두가 대상 중심을 지나는 시각이다.</summary>
        public float CenterCrossSeconds
        {
            get
            {
                int count = Mathf.Clamp(LineCount, 1, 3);
                float cross = Mathf.Clamp(CenterCrossFraction, 0.20f, 0.80f);
                return Mathf.Max(0.08f, DrawDuration) * cross + (count - 1) * 0.5f * Mathf.Max(0f, LineDelay);
            }
        }
    }

    /// <summary>
    /// 완성된 피격 마크를 한 번에 띄우지 않고, 발톱 끝이 좌하단에서 우상단으로 움직이며 자국을 남긴다.
    /// 줄 수·굵기·길이·각도·팔레트를 주입할 수 있어 흑적 귀참과 한 줄 화염 조각칼이 같은 문법을 쓴다.
    /// </summary>
    public static class ClawRakeFx
    {
        private static readonly int BodyColor = Shader.PropertyToID("_BodyColor");
        private static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
        private static readonly int CoreColor = Shader.PropertyToID("_CoreColor");
        private static readonly int Reveal = Shader.PropertyToID("_Reveal");
        private static readonly int RevealSoftness = Shader.PropertyToID("_RevealSoftness");
        private static readonly int TipWidth = Shader.PropertyToID("_TipWidth");
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private static Material _material;

        public static IEnumerator Play(Unit target, Vector3 fallbackPoint, int sortingLayer,
            ClawRakeStyle style, Action onCenterPassed = null)
        {
            style ??= new ClawRakeStyle();
            style.BodyColor = BattleVfxStyle.Body(style.BodyColor,style.EdgeColor);
            style.CoreColor = BattleVfxStyle.Light(style.CoreColor);
            Sprite sprite = style.Sprite != null ? style.Sprite : VfxArt.ShiCrimsonClawStroke;
            if (sprite == null)
            {
                onCenterPassed?.Invoke();
                yield break;
            }

            int count = Mathf.Clamp(style.LineCount, 1, 3);
            float draw = VfxTiming.AtLeastFrames(Mathf.Max(0.08f, style.DrawDuration), 5);
            float delay = Mathf.Max(0f, style.LineDelay);
            float linger = Mathf.Max(0f, style.LingerDuration);
            float fade = VfxTiming.AtLeastFrames(Mathf.Max(0.06f, style.FadeDuration), 3);
            float total = draw + delay * (count - 1) + linger + fade;
            float crossFraction = Mathf.Clamp(style.CenterCrossFraction, 0.20f, 0.80f);
            float centerCross = draw * crossFraction + delay * (count - 1) * 0.5f;
            float size = target != null ? CardMotion.ViewOf(target)?.CardSize ?? VfxSpace.CardSize : VfxSpace.CardSize;
            Vector3 offset = (Vector3)(style.OffsetInCards * size);
            Vector3 direction = Direction(style.AngleDegrees);
            Vector2 bounds = sprite.bounds.size;
            float width = size * Mathf.Max(0.40f, style.LengthInCards);
            float height = size * Mathf.Max(style.ThicknessInCards * 5.0f, style.LengthInCards * 0.24f);
            var targetView=CardMotion.ViewOf(target);
            if(targetView!=null&&targetView.PortraitRenderer!=null)
            {
                Bounds portrait=targetView.PortraitRenderer.bounds;
                width=Mathf.Min(width,portrait.size.x*style.PortraitWidthFraction);
                height=Mathf.Min(height,portrait.size.y*style.PortraitHeightFraction);
            }
            float sourceAngle = style.SourceAngleDegrees;
            // 원화는 비균일하게 눌러 쓰므로 픽셀상의 각도와 화면상의 각도가 다르다.
            // 압축 뒤 실효 각도를 보정해야 AimFrom이 정한 시전자→대상 축과 선두 이동이 일치한다.
            float sourceRadians = sourceAngle * Mathf.Deg2Rad;
            float effectiveSourceAngle = Mathf.Atan2(Mathf.Sin(sourceRadians) * height,
                Mathf.Cos(sourceRadians) * width) * Mathf.Rad2Deg;
            Vector3 sourceDirection = Direction(effectiveSourceAngle);
            Vector3 sourceNormal = new(-sourceDirection.y, sourceDirection.x, 0f);

            var root = new GameObject("ClawRakeFx");
            root.transform.position = CurrentPoint(target, fallbackPoint) + offset;
            root.transform.rotation = Quaternion.Euler(0f, 0f, style.AngleDegrees - effectiveSourceAngle);

            var renderers = new SpriteRenderer[count];
            var tips = new SpriteRenderer[count];
            var blocks = new MaterialPropertyBlock[count];
            float center = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var line = new GameObject($"ClawLine_{i + 1}");
                line.transform.SetParent(root.transform, false);
                float across = (center - i) * style.LineSpacingInCards * size;
                float along = (i - center) * size * 0.018f;
                line.transform.localPosition = sourceNormal * across + sourceDirection * along;
                renderers[i] = VfxSpace.NewSprite(line.transform, "BodyAndCore", sprite, Color.white,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 30 + i);
                renderers[i].sharedMaterial = Material;
                renderers[i].transform.localScale = new Vector3(
                    width / Mathf.Max(0.0001f, bounds.x),
                    height / Mathf.Max(0.0001f, bounds.y), 1f);
                blocks[i] = new MaterialPropertyBlock();

                tips[i] = VfxSpace.NewSprite(line.transform, "MovingClawTip", VfxArt.Glint, Color.clear,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 34 + i, true);
                float tipSize = size * Mathf.Max(0.045f, style.ThicknessInCards * 0.72f);
                tips[i].transform.localScale = new Vector3(tipSize, tipSize * 0.58f, 1f);
            }

            bool crossed = false;
            float elapsed = 0f;
            while (elapsed < total)
            {
                if (target != null && target.isActive)
                    root.transform.position = CurrentPoint(target, fallbackPoint) + offset;

                for (int i = 0; i < count; i++)
                {
                    float age = elapsed - i * delay;
                    float progress = Mathf.Clamp01(age / draw);
                    // 5차 곡선은 중앙에서 너무 빨라 0.03초 캡처에서 완성 마크처럼 보였다.
                    // 3차 가감속으로 선두가 이동하는 중간 구간을 더 오래 남긴다.
                    // 피그말리온처럼 긴 한 획은 중심 통과 뒤의 잔여 그리기를 늘리되,
                    // 기본 0.5는 시의 기존 가감속 곡선을 그대로 보존한다.
                    float eased = Mathf.Approximately(crossFraction, 0.5f)
                        ? SmoothStep(progress)
                        : CenteredSmoothStep(progress, crossFraction);
                    float alpha = age < 0f ? 0f : 1f;
                    float fadeStarts = draw + linger;
                    if (age > fadeStarts)
                        alpha *= 1f - Mathf.Clamp01((age - fadeStarts) / fade);

                    Apply(renderers[i], blocks[i], style, eased, alpha);
                    UpdateTip(tips[i], sprite, width, height, eased,
                        age >= 0f && age <= draw ? alpha : 0f, style.CoreColor);
                }

                if (!crossed && elapsed >= centerCross)
                {
                    crossed = true;
                    Vector3 point = CurrentPoint(target, fallbackPoint) + offset;
                    CardMotion.RakeReaction(target, direction, style.CardKickStrength);
                    BurstAtTip(point, direction, size, style, sortingLayer);
                    onCenterPassed?.Invoke();
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!crossed) onCenterPassed?.Invoke();
            UnityEngine.Object.Destroy(root);
        }

        private static Material Material
        {
            get
            {
                if (_material != null) return _material;
                Shader shader = Resources.Load<Shader>("Shaders/VfxClawRake")
                                ?? Shader.Find("NeverTheLast/VfxClawRake")
                                ?? Shader.Find("Sprites/Default");
                _material = new Material(shader) { name = "Vfx_ClawRake", hideFlags = HideFlags.HideAndDontSave };
                return _material;
            }
        }

        private static void Apply(SpriteRenderer renderer, MaterialPropertyBlock block,
            ClawRakeStyle style, float reveal, float alpha)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BodyColor, style.BodyColor);
            block.SetColor(EdgeColor, style.EdgeColor);
            block.SetColor(CoreColor, style.CoreColor);
            block.SetFloat(Reveal, reveal);
            block.SetFloat(RevealSoftness, Mathf.Clamp(style.RevealSoftness, 0.005f, 0.20f));
            block.SetFloat(TipWidth, Mathf.Clamp(style.TipWidth, 0.01f, 0.24f));
            block.SetFloat(Alpha, Mathf.Clamp01(alpha));
            renderer.SetPropertyBlock(block);
            block.Clear();
        }

        private static void UpdateTip(SpriteRenderer renderer, Sprite sprite, float width, float height,
            float reveal, float alpha, Color color)
        {
            if (renderer == null || sprite == null) return;
            float x = Mathf.Lerp(-width * 0.50f, width * 0.50f, reveal);
            float y = Mathf.Lerp(-height * 0.44f, height * 0.44f, reveal);
            renderer.transform.localPosition = new Vector3(x, y, 0f);
            renderer.color = new Color(color.r, color.g, color.b,
                color.a * alpha * Mathf.Sin(Mathf.Clamp01(reveal) * Mathf.PI));
        }

        private static void BurstAtTip(Vector3 point, Vector3 direction, float size,
            ClawRakeStyle style, int layer)
        {
            point += direction * size * style.LengthInCards * 0.05f;
            VfxParticles.Burst(point, new ParticleSpec
            {
                Sprite = VfxArt.Crescent,
                Additive = true,
                StartColor = style.CoreColor,
                EndColor = new Color(style.EdgeColor.r, style.EdgeColor.g, style.EdgeColor.b, 0f),
                Lifetime = new Vector2(0.13f, 0.25f),
                Speed = new Vector2(size * 0.24f, size * 0.62f),
                Angle = style.AngleDegrees,
                Spread = 46f,
                StartSize = new Vector2(size * 0.018f, size * 0.050f),
                EndSizeScale = 0.18f,
                Drag = 1.15f,
                Spin = new Vector2(70f, 180f),
                SpawnRadius = size * 0.055f,
            }, 7, layer, VfxSpace.OverlaySortingOrder + 42);
        }

        private static Vector3 CurrentPoint(Unit target, Vector3 fallback)
            => target != null && target.isActive ? VfxSpace.PortraitPoint(target)+Vector3.up*(VfxSpace.CardSize*.10f) : fallback;

        private static Vector3 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        private static float SmoothStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static float CenteredSmoothStep(float value, float center)
        {
            float t = Mathf.Clamp01(value);
            if (t <= center)
                return 0.5f * SmoothStep(t / Mathf.Max(0.0001f, center));
            return 0.5f + 0.5f * SmoothStep((t - center) / Mathf.Max(0.0001f, 1f - center));
        }
    }
}
