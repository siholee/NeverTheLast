using System;
using System.Collections;
using Entities;
using UnityEngine;

namespace Effects.Vfx.Presets
{
    /// <summary>활 발동 문양·화살·문장형 착탄을 한 팔레트로 묶는 공용 스타일.</summary>
    public sealed class BowShotStyle
    {
        public Sprite Bolt;
        public string ProjectileCue;
        public Color BodyColor = new(0.42f, 0.18f, 0.62f, 1f);
        public Color CoreColor = new(0.94f, 0.88f, 1f, 1f);
        public Color PaletteLift = new(0.86f, 0.66f, 0.96f, 1f);
        public Color FocusTint = new(0.12f, 0.07f, 0.18f, 1f);
        public bool MirrorHorizontal;
    }

    /// <summary>
    /// 활이 강조되는 유닛의 발동 문양과 정밀 착탄을 공용화한다. 탄도와 세 번째 탄 단일 판정은
    /// <see cref="RadiantVolleyFx"/>에 그대로 맡겨 기존 여섯 발 계약을 바꾸지 않는다.
    /// </summary>
    public static class BowShotFx
    {
        public static IEnumerator Play(Unit caster, Unit target, Action onHit, BowShotStyle style,
            float scale = 1f)
        {
            if (caster == null || target == null || style == null)
            {
                onHit?.Invoke();
                yield break;
            }

            int layer = VfxSpace.SortingLayer(caster);
            Vector3 start = VfxSpace.WorldPoint(caster);
            Vector3 end = VfxSpace.WorldPoint(target);
            Vector3 direction = end - start;
            direction.z = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.left;
            Vector3 crest = start + direction * VfxSpace.CardSize * 0.27f;

            var castCue = new SkillFxCue
            {
                ResourcePath = "VfxTex/Gen/Enemy2/BowCast_4x4",
                FrameDelays = SkillFx.Delays(0.016f, 0.020f, 0.024f, 0.030f),
                Pivot = new Vector2(0.5f, 0.5f),
                SizeInCards = new Vector2(1.02f, 0.78f) * scale,
                Color = style.BodyColor,
                FlipX = style.MirrorHorizontal,
                OrderOffset = 10,
                Composite = new FlipbookCompositeStyle
                {
                    PeakAlpha = 0.78f,
                    FadeInFraction = 0.06f,
                    FadeOutFraction = 0.34f,
                    GlowColor = style.CoreColor,
                    GlowAlpha = 0.60f,
                    GlowThreshold = 0.72f,
                    GlowSoftness = 0.14f,
                    GlowGain = 1.20f,
                    BrightBackdropLift = style.PaletteLift,
                    BrightBackdropLiftStrength = 0.34f,
                    StartScale = 0.82f,
                    CrestScale = 1.02f,
                    EndScale = 1.06f,
                    GroundLightSizeInCards = Vector2.zero,
                    ContactShadowSizeInCards = Vector2.zero,
                    PeakResourcePath = "VfxTex/Gen/Enemy2/BowCastPeak",
                    PeakStart = 0.28f,
                    PeakHoldEnd = 0.58f,
                    PeakEnd = 0.78f,
                    PeakLayerAlpha = 0.66f,
                    SceneLightColor = style.CoreColor,
                    SceneLightStrength = 0.050f,
                },
            };
            SkillFx.PlayCue(castCue, crest, layer, VfxSpace.OverlaySortingOrder + 20);

            var volley = new RadiantVolleyStyle
            {
                Bolt = style.Bolt,
                ImpactResourcePath = "VfxTex/Gen/Enemy2/BowImpact_4x4",
                ImpactPeakResourcePath = "VfxTex/Gen/Enemy2/BowImpactPeak",
                ProjectileCue = style.ProjectileCue,
                FocusTint = style.FocusTint,
                CrestBody = new Color(style.BodyColor.r, style.BodyColor.g, style.BodyColor.b, 0f),
                CrestCore = new Color(style.CoreColor.r, style.CoreColor.g, style.CoreColor.b, 0f),
                BodyTint = style.BodyColor,
                CoreColor = style.CoreColor,
                PaletteLift = style.PaletteLift,
                CardLight = style.CoreColor,
                GroundLight = style.CoreColor,
                BodyAlpha = 0.82f,
                CoreAlpha = 0.60f,
                ImpactScale = 1.24f,
                MirrorHorizontal = style.MirrorHorizontal,
                SuppressDefaultCrest = true,
            };
            yield return RadiantVolleyFx.Play(caster, target, onHit, volley, scale);
        }
    }
}
