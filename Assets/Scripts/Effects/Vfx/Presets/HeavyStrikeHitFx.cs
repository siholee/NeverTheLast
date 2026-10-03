using System;
using System.Collections;
using Entities;
using UnityEngine;

namespace Effects.Vfx.Presets
{
    /// <summary>공허 괴수의 제자리 중타격에 주입할 색·크기·흔들림 묶음.</summary>
    public sealed class HeavyStrikeHitStyle
    {
        public Color BodyColor = new(0.42f, 0.12f, 0.58f, 1f);
        public Color CoreColor = new(1f, 0.86f, 0.68f, 1f);
        public Color PaletteLift = new(0.82f, 0.50f, 0.94f, 1f);
        public float Scale = 1f;
        public float Shake = 0.065f;
    }

    /// <summary>
    /// 시전자는 멀리 돌진하지 않고 짧게 들썩인다. 대상에서는 좁은 접촉점이 부채꼴 몸체와
    /// 휘도 코어로 벌어지며, 지정 프레임에서 판정 콜백을 정확히 한 번 부른다.
    /// </summary>
    public static class HeavyStrikeHitFx
    {
        public static IEnumerator Play(Unit caster, Unit target, Action onHit, HeavyStrikeHitStyle style)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            style ??= new HeavyStrikeHitStyle();
            Vector3 origin = VfxSpace.WorldPoint(caster);
            Vector3 impact = VfxSpace.WorldPoint(target);
            Vector3 direction = impact - origin;
            int layer = VfxSpace.SortingLayer(target);
            float scale = Mathf.Max(0.1f, style.Scale);

            // 피그말리온과 같은 제자리 반동만 쓰되, 옛 접촉 돌진의 충돌 박자(약 0.30초)는 보존한다.
            caster.StartCoroutine(CardMotion.Channel(caster, 0.07f, 0.10f, 0.14f));
            BattlefieldBackdrop.PushFocus(0.26f, 0.04f, 0.16f, 0.18f,
                Color.Lerp(style.BodyColor, new Color(0.08f, 0.045f, 0.10f, 1f), 0.58f), origin, impact);
            CardMotion.ViewOf(caster)?.PulseVfxLight(style.CoreColor, 0.045f, 0.28f);
            yield return new WaitForSeconds(0.17f);

            bool completed = false;
            var cue = new SkillFxCue
            {
                ResourcePath = "VfxTex/Gen/Enemy2/HeavyStrikeHit_4x4",
                FrameDelays = SkillFx.Delays(0.014f, 0.018f, 0.026f, 0.032f),
                ImpactFrame = 6,
                Pivot = new Vector2(0.5f, 0.5f),
                SizeInCards = new Vector2(1.26f, 0.72f) * scale,
                Color = style.BodyColor,
                FlipX = direction.x > 0f,
                OrderOffset = 24,
                Composite = new FlipbookCompositeStyle
                {
                    PeakAlpha = 0.78f,
                    FadeInFraction = 0.04f,
                    FadeOutFraction = 0.38f,
                    GlowColor = style.CoreColor,
                    GlowAlpha = 0.68f,
                    GlowThreshold = 0.69f,
                    GlowSoftness = 0.15f,
                    GlowGain = 1.26f,
                    BrightBackdropLift = style.PaletteLift,
                    BrightBackdropLiftStrength = 0.36f,
                    StartScale = 0.86f,
                    CrestScale = 1.04f,
                    EndScale = 1.10f,
                    GroundLightColor = new Color(style.CoreColor.r, style.CoreColor.g, style.CoreColor.b, 0.18f),
                    GroundLightSizeInCards = new Vector2(0.62f, 0.13f) * scale,
                    ContactShadowColor = new Color(0.20f, 0.14f, 0.17f, 0.22f),
                    ContactShadowSizeInCards = new Vector2(0.58f, 0.11f) * scale,
                    GroundOffsetInCards = -0.31f,
                    PeakResourcePath = "VfxTex/Gen/Enemy2/HeavyStrikeHitPeak",
                    PeakStart = 0.28f,
                    PeakHoldEnd = 0.62f,
                    PeakEnd = 0.80f,
                    PeakLayerAlpha = 0.70f,
                    SceneLightColor = style.CoreColor,
                    SceneLightStrength = 0.075f,
                },
            };

            SkillFx.PlayImpact(cue, target, layer, () =>
            {
                CardMotion.ViewOf(target)?.PulseVfxLight(style.CoreColor, 0.085f, 0.24f);
                BattlefieldBackdrop.PulseFlash(style.CoreColor, 0.060f, 0.065f);
                CameraShake.ShakeDirectional(direction, style.Shake * scale);
                onHit?.Invoke();
            }, () => completed = true);

            float guard = 2f;
            while (!completed && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }
    }
}
