using System;
using System.Collections;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx.Presets
{
    /// <summary>
    /// 대상 발밑의 균열에서 결정질 바위 창이 솟는 보관 프리셋.
    /// 캐릭터 전용 코드와 분리해 시트·합성·판정 프레임·기존 SFX 박자를 한 호출로 재사용한다.
    /// </summary>
    public static class RisingSpikeFx
    {
        private static FlipbookCompositeStyle Composite() => new()
        {
            PeakAlpha = 0.78f,
            FadeInFraction = 0.14f,
            FadeOutFraction = 0.28f,
            GlowAlpha = 0.68f,
            GlowThreshold = 0.72f,
            GlowSoftness = 0.15f,
            StartScale = 0.80f,
            CrestScale = 1.03f,
            EndScale = 1.08f,
            StartOffsetInCards = new Vector2(0f, -0.035f),
            EndOffsetInCards = new Vector2(0f, 0.018f),
            GroundLightSizeInCards = new Vector2(1.12f, 0.25f),
            ContactShadowSizeInCards = new Vector2(1.02f, 0.17f),
            GroundOffsetInCards = -0.36f,
            PeakResourcePath = "VfxTex/Maple/Sei/GroundLevelingPeak_v2",
            PeakStart = 0.30f,
            PeakHoldEnd = 0.61f,
            PeakEnd = 0.78f,
            PeakLayerAlpha = 0.72f,
            PeakLayerScale = 1.04f,
            SceneLightStrength = 0.13f,
        };

        private static readonly SkillFxPlan Plan = new()
        {
            Hit = new SkillFxCue
            {
                ResourcePath = "VfxTex/Maple/Sei/GroundLeveling_4x4",
                FrameDelays = SkillFx.Delays(0.030f, 0.036f, 0.048f, 0.042f),
                ImpactFrame = 7,
                Pivot = new Vector2(0.5f, 0.44f),
                SizeInCards = new Vector2(1.48f, 1.48f),
                OffsetInCards = new Vector2(0f, -0.04f),
                OrderOffset = 8,
                Composite = Composite(),
            },
            Screen = new SkillFxScreenCue
            {
                FocusStrength = 0.24f,
                FadeIn = 0.04f,
                Hold = 0.13f,
                FadeOut = 0.18f,
                FlashColor = new Color(1f, 0.82f, 0.46f),
                FlashStrength = 0.025f,
                FlashDuration = 0.065f,
                Shake = 0.012f,
            },
            CastLead = 0.025f,
            ScreenSfx = "sei.ground.screen",
        };

        /// <summary>
        /// 기존 땅고르기 연출을 그대로 재생한다. 호출부는 피해 공식과 원소 후처리만 콜백에 둔다.
        /// </summary>
        public static IEnumerator Play(Unit caster, Unit target, Action onHit, float scale = 1f)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            CardMotion.Throw(caster, target, 1f);
            AudioManager.PlayCue("sei.ground.projectile");
            yield return SkillFx.PlayTargeted(caster, target, Plan, () =>
            {
                onHit?.Invoke();
                CameraShake.Shake(0.022f * scale);
                BattlefieldBackdrop.PulseFlash(new Color(1f, 0.84f, 0.50f), 0.035f, 0.055f);
            });
        }
    }
}
