using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using Managers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Effects.Vfx.Presets
{
    /// <summary>여섯 발 곡선 연발의 원화·색·소리만 교체하는 공용 프리셋.</summary>
    public sealed class RadiantVolleyStyle
    {
        public Sprite Bolt;
        public string ImpactResourcePath;
        public string ImpactPeakResourcePath;
        public string ProjectileCue;
        public Color FocusTint;
        public Color CrestBody;
        public Color CrestCore;
        public Color BodyTint = Color.white;
        public Color CoreColor = Color.white;
        public Color PaletteLift = Color.white;
        public Color CardLight = Color.white;
        public Color GroundLight = Color.white;
        public float BodyAlpha = 0.82f;
        public float CoreAlpha = 0.58f;
        public float ImpactScale = 1f;
        public bool SuppressDefaultCrest;
        /// <summary>오른쪽 진행 원화를 적→아군 방향으로 수평 반전한다. 곡선·발사점·착탄 분산도 함께 뒤집힌다.</summary>
        public bool MirrorHorizontal;
    }

    /// <summary>
    /// 카드 앞 문장에서 여섯 발을 0.05초 간격으로 쏘고 서로 다른 실루엣 지점에 꽂는다.
    /// 세 번째 탄의 착탄 정점만 판정 콜백을 호출해 캐릭터별 피해 계약을 한 번으로 보존한다.
    /// </summary>
    public static class RadiantVolleyFx
    {
        private static readonly int BackdropLuminance = Shader.PropertyToID("_BackdropLuminance");
        private static readonly int PaletteLift = Shader.PropertyToID("_PaletteLift");
        private static readonly int LiftStrength = Shader.PropertyToID("_LiftStrength");
        private static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int UvRect = Shader.PropertyToID("_UvRect");
        private static readonly int GlowThreshold = Shader.PropertyToID("_GlowThreshold");
        private static readonly int GlowSoftness = Shader.PropertyToID("_GlowSoftness");
        private static readonly int GlowGain = Shader.PropertyToID("_GlowGain");

        private static float S => VfxSpace.CardSize;
        private static int Order => VfxSpace.OverlaySortingOrder + 20;

        private static readonly Vector2[] ImpactAnchors =
        {
            new(-0.18f, 0.20f),
            new(0.17f, 0.18f),
            new(-0.21f, 0.00f),
            new(0.20f, -0.02f),
            new(-0.13f, -0.21f),
            new(0.13f, -0.22f),
            new(0.00f, 0.00f),
        };

        private sealed class CompositeArt
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
        }

        public static IEnumerator Play(Unit caster, Unit target, Action onHit, RadiantVolleyStyle style,
            float scale = 1f)
        {
            if (caster == null || target == null || style == null)
            {
                onHit?.Invoke();
                yield break;
            }

            const int shotCount = 6;
            const int primaryShot = 2;
            float[] fan = { -0.13f, 0.08f, -0.035f, 0.14f, -0.09f, 0.025f };
            int layer = VfxSpace.SortingLayer(caster);
            Vector3 casterPoint = VfxSpace.WorldPoint(caster);
            Vector3 targetPoint = VfxSpace.WorldPoint(target);
            Vector3 direction = Direction(casterPoint, targetPoint);
            Vector3 normal = new(-direction.y, direction.x, 0f);

            CardMotion.Throw(caster, target, 0.72f);
            BattlefieldBackdrop.PushFocus(0.13f, 0.035f, 0.18f, 0.16f,
                style.FocusTint, casterPoint, targetPoint);
            Vector3 crest = casterPoint + direction * 0.31f * S;
            if (!style.SuppressDefaultCrest)
            {
                SpriteFx.Play(VfxArt.Diamond, crest, style.CrestBody, false,
                    0.08f * S * scale, 0.25f * S * scale, 0.31f, 0f, 80f, 0.025f, 0.24f,
                    SpriteFx.Ease.OutCubic, layer, Order + 4);
                SpriteFx.Play(VfxArt.Glint, crest, style.CrestCore, true,
                    0.05f * S * scale, 0.29f * S * scale, 0.24f, 0f, 65f, 0.02f, 0.18f,
                    SpriteFx.Ease.OutCubic, layer, Order + 6);
            }
            CardMotion.ViewOf(caster)?.PulseVfxLight(style.CardLight, 0.11f, 0.34f);

            int completed = 0;
            bool impactInvoked = false;
            Vector2[] impactOffsets = CreateImpactOffsets(shotCount);
            for (int i = 0; i < shotCount; i++)
            {
                int shotIndex = i;
                float vertical = fan[i] * (style.MirrorHorizontal ? -1f : 1f);
                if (style.MirrorHorizontal) impactOffsets[i].x = -impactOffsets[i].x;
                Vector3 start = crest + normal * vertical * 0.16f * S;
                if (!string.IsNullOrWhiteSpace(style.ProjectileCue))
                    AudioManager.PlayCue(style.ProjectileCue, 0.78f + i * 0.025f);
                VfxRunner.Run(FlyShot(start, target, impactOffsets[i], vertical, layer, scale, style, () =>
                {
                    if (shotIndex != primaryShot || impactInvoked) return;
                    impactInvoked = true;
                    onHit?.Invoke();
                    CameraShake.Shake(0.018f * scale);
                    BattlefieldBackdrop.PulseFlash(style.CoreColor, 0.025f, 0.050f);
                }, () => completed++));

                if (i + 1 < shotCount) yield return new WaitForSeconds(0.05f);
            }

            float guard = 3f;
            while (completed < shotCount && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
            if (!impactInvoked) onHit?.Invoke();
        }

        private static IEnumerator FlyShot(Vector3 start, Unit target, Vector2 impactOffset, float curve,
            int layer, float scale, RadiantVolleyStyle style, Action onPrimaryImpact, Action onComplete)
        {
            CompositeArt bolt = NewCompositeArt("RadiantVolleyBolt", style.Bolt, start, layer, Order + 14,
                0.38f * S * scale, style);
            Vector3 offset = new(impactOffset.x * S, impactOffset.y * S, 0f);
            Vector3 fixedEnd = target != null ? VfxSpace.WorldPoint(target) + offset : start + Vector3.right * S;
            Vector3 initialDirection = Direction(start, fixedEnd);
            Vector3 normal = new(-initialDirection.y, initialDirection.x, 0f);
            float duration = VfxTiming.AtLeastFrames(0.18f + Mathf.Abs(curve) * 0.05f, 4);
            Vector3 previous = start;

            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                Vector3 end = target != null && target.isActive ? VfxSpace.WorldPoint(target) + offset : fixedEnd;
                Vector3 control = Vector3.Lerp(start, end, 0.50f) + normal * curve * 0.82f * S;
                float u = 1f - t;
                Vector3 position = u * u * start + 2f * u * t * control + t * t * end;
                Vector3 tangent = position - previous;
                if (tangent.sqrMagnitude > 0.000001f)
                {
                    float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                    // 적 탄은 180도 회전이 아니라 수평 반전해야 원화의 위아래가 뒤집히지 않는다.
                    bolt.Root.rotation = Quaternion.Euler(0f, 0f,
                        style.MirrorHorizontal ? angle + 180f : angle);
                }
                bolt.Root.position = position;
                float pulse = Mathf.Sin(t * Mathf.PI);
                SetCompositeAlpha(bolt, Mathf.Lerp(style.BodyAlpha * 0.90f, style.BodyAlpha, pulse),
                    Mathf.Lerp(style.CoreAlpha * 0.68f, style.CoreAlpha, pulse), style);
                previous = position;
                yield return null;
            }

            UnityEngine.Object.Destroy(bolt.Root.gameObject);
            CardMotion.ViewOf(target)?.PulseVfxLight(style.CardLight, 0.055f, 0.24f);
            SkillFx.PlayImpact(ImpactCueAt(style, impactOffset, scale), target, layer, onPrimaryImpact, onComplete);
        }

        private static CompositeArt NewCompositeArt(string name, Sprite sprite, Vector3 position,
            int layer, int order, float size, RadiantVolleyStyle style)
        {
            var root = new GameObject(name).transform;
            root.position = position;
            root.localScale = Vector3.one * Mathf.Max(0.01f, size);
            SpriteRenderer body = VfxSpace.NewSprite(root, "Body", sprite, Color.white,
                layer, order, additive: false);
            body.sharedMaterial = VfxSpace.CompositeAlpha;
            SpriteRenderer core = VfxSpace.NewSprite(root, "LuminanceCore", sprite, Color.white,
                layer, order + 1, additive: false);
            core.sharedMaterial = VfxSpace.LuminanceGlow;
            body.flipX = style.MirrorHorizontal;
            core.flipX = style.MirrorHorizontal;
            ApplyCompositeProperties(body, core, style);

            var art = new CompositeArt { Root = root, Body = body, Core = core };
            SetCompositeAlpha(art, style.BodyAlpha, style.CoreAlpha, style);
            return art;
        }

        private static void ApplyCompositeProperties(SpriteRenderer body, SpriteRenderer core,
            RadiantVolleyStyle style)
        {
            if (body == null || body.sprite == null || core == null) return;
            Rect rect = body.sprite.textureRect;
            Texture2D texture = body.sprite.texture;
            Vector4 uv = new(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height);

            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetFloat(BackdropLuminance, BattlefieldBackdrop.CurrentBackgroundLuminance);
            block.SetColor(PaletteLift, style.PaletteLift);
            block.SetFloat(LiftStrength, 0.34f);
            block.SetFloat(EdgeSoftness, 0.032f);
            block.SetVector(UvRect, uv);
            body.SetPropertyBlock(block);
            block.Clear();

            core.GetPropertyBlock(block);
            block.SetFloat(GlowThreshold, 0.71f);
            block.SetFloat(GlowSoftness, 0.15f);
            block.SetFloat(GlowGain, 1.24f);
            block.SetFloat(EdgeSoftness, 0.030f);
            block.SetVector(UvRect, uv);
            core.SetPropertyBlock(block);
        }

        private static void SetCompositeAlpha(CompositeArt art, float bodyAlpha, float coreAlpha,
            RadiantVolleyStyle style)
        {
            if (art == null) return;
            float brightBoard = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.08f, 0.65f, BattlefieldBackdrop.CurrentBackgroundLuminance));
            if (art.Body != null)
            {
                Color body = style.BodyTint;
                body.a = Mathf.Clamp01(bodyAlpha * Mathf.Lerp(0.86f, 1f, brightBoard));
                art.Body.color = body;
            }
            if (art.Core != null)
            {
                Color core = style.CoreColor;
                core.a = Mathf.Clamp01(coreAlpha * Mathf.Lerp(0.72f, 1f, brightBoard));
                art.Core.color = core;
            }
        }

        private static Vector2[] CreateImpactOffsets(int count)
        {
            var anchors = new List<Vector2>(ImpactAnchors);
            for (int i = anchors.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (anchors[i], anchors[swap]) = (anchors[swap], anchors[i]);
            }

            var result = new Vector2[Mathf.Max(0, count)];
            for (int i = 0; i < result.Length; i++)
            {
                Vector2 anchor = i < anchors.Count
                    ? anchors[i]
                    : new Vector2(Mathf.Cos(i * 2.39996f), Mathf.Sin(i * 2.39996f)) * 0.23f;
                result[i] = anchor + new Vector2(Random.Range(-0.012f, 0.012f),
                    Random.Range(-0.012f, 0.012f));
            }
            return result;
        }

        private static SkillFxCue ImpactCueAt(RadiantVolleyStyle style, Vector2 impactOffset, float scale)
        {
            float size = Mathf.Max(0.1f, scale * style.ImpactScale);
            return new SkillFxCue
            {
                ResourcePath = style.ImpactResourcePath,
                FrameDelays = SkillFx.Delays(0.018f, 0.022f, 0.028f, 0.032f),
                ImpactFrame = 4,
                Pivot = new Vector2(0.5f, 0.5f),
                SizeInCards = new Vector2(0.56f, 0.56f) * size,
                OffsetInCards = impactOffset,
                Color = style.BodyTint,
                FlipX = style.MirrorHorizontal,
                OrderOffset = 16,
                Composite = new FlipbookCompositeStyle
                {
                    PeakAlpha = 0.76f,
                    FadeInFraction = 0.08f,
                    FadeOutFraction = 0.34f,
                    GlowColor = style.CoreColor,
                    GlowAlpha = 0.62f,
                    GlowThreshold = 0.72f,
                    GlowSoftness = 0.14f,
                    BrightBackdropLift = style.PaletteLift,
                    BrightBackdropLiftStrength = 0.34f,
                    StartScale = 0.72f,
                    CrestScale = 1.04f,
                    EndScale = 1.12f,
                    GroundLightColor = new Color(style.GroundLight.r, style.GroundLight.g,
                        style.GroundLight.b, 0.20f),
                    GroundLightSizeInCards = new Vector2(0.38f, 0.10f) * size,
                    ContactShadowSizeInCards = new Vector2(0.31f, 0.065f) * size,
                    GroundOffsetInCards = -0.12f,
                    SceneLightColor = style.CardLight,
                    SceneLightStrength = 0.055f,
                    PeakResourcePath = style.ImpactPeakResourcePath,
                    PeakStart = 0.28f,
                    PeakHoldEnd = 0.60f,
                    PeakEnd = 0.80f,
                    PeakLayerAlpha = 0.68f,
                },
            };
        }

        private static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.z = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        }
    }
}
