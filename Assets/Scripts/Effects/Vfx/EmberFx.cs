using System;
using System.Collections;
using Effects.Vfx.Presets;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 히미코·피그말리온 공용 불씨 연출. 히미코는 한 번의 부채형 시전 뒤 대상 위 한 번의 화염 피격,
    /// 피그말리온은 짧은 카드 반동 뒤 한 줄 대형 화염 획으로 갈라지고 맹화/집중은 아래 보조 API가 맡는다.
    /// </summary>
    public static class EmberFx
    {
        private static readonly Color Ash = new(0.18f, 0.055f, 0.035f, 0.78f);
        private static readonly Color Crimson = new(0.72f, 0.10f, 0.045f, 0.88f);
        private static readonly Color Gold = new(1f, 0.62f, 0.12f, 0.96f);
        private static readonly Color WhiteHot = new(1f, 0.91f, 0.52f, 0.96f);

        /// <summary>한 번의 부채형 시전 뒤 대상 카드 안 여섯 지점에서 화염 피격 리듬을 연다.</summary>
        public static IEnumerator Volley(Unit caster, Unit target, Action onImpact)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Himiko))
            {
                yield return VfxComposer.PlaySingleTarget("himiko.ember", caster, target,
                    onImpact);
                yield break;
            }

            yield return LegacyVolley(caster, target, onImpact);
        }

        private static IEnumerator HandpaintVolley(Unit caster, Unit target, Action onImpact)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 source = VfxSpace.WorldPoint(caster);
            Vector3 destination = VfxSpace.WorldPoint(target);
            Vector3 direction = destination - source;
            float rotation = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;
            HandpaintFx.PlayAt(source + direction.normalized * VfxSpace.CardSize * 0.22f,
                VfxSpace.SortingLayer(caster), HandpaintFx.Muzzle1Red, new Vector2(0.48f, 0.40f),
                new Color(1f, 0.72f, 0.42f, 0.94f), WhiteHot, rotation, 38, 0.94f, 0.76f);
            CardMotion.ViewOf(caster)?.PulseVfxLight(Gold, 0.10f, 0.34f);

            yield return HandpaintFx.PlayVolley(caster, target, onImpact,
                HandpaintFx.FireAttack1Red, HandpaintFx.Explosion2Red, HandpaintFx.Explosion1Red,
                new Color(1f, 0.76f, 0.52f, 0.94f), WhiteHot,
                new Color(0.31f, 0.055f, 0.018f, 1f), "himiko.ember.projectile", 1.04f, 1.20f);
        }

        private static IEnumerator LegacyVolley(Unit caster, Unit target, Action onImpact)
        {
            var style = new RadiantVolleyStyle
            {
                Bolt = VfxArt.HimikoFlameVolleyBolt,
                ImpactResourcePath = "VfxTex/Maple/Pyro/FlameVolleyImpact_4x4",
                ProjectileCue = "himiko.ember.projectile",
                FocusTint = new Color(0.31f, 0.055f, 0.018f, 1f),
                CrestBody = new Color(0.66f, 0.10f, 0.025f, 0.78f),
                CrestCore = new Color(1f, 0.67f, 0.18f, 0.78f),
                BodyTint = new Color(1f, 0.78f, 0.58f, 1f),
                CoreColor = WhiteHot,
                PaletteLift = new Color(1f, 0.53f, 0.16f, 1f),
                CardLight = new Color(1f, 0.38f, 0.08f, 1f),
                GroundLight = new Color(0.92f, 0.20f, 0.035f, 1f),
                BodyAlpha = 0.84f,
                CoreAlpha = 0.60f,
            };
            yield return RadiantVolleyFx.Play(caster, target, onImpact, style, 1.03f);
        }

        /// <summary>
        /// 피그말리온이 짧게 움찔한 뒤 화염 한 가닥을 크게 그린다. 긴 후반부 때문에 시보다 느리지만
        /// 선두가 중심을 지나는 0.10초 단일 판정은 그대로 둔다.
        /// </summary>
        public static IEnumerator Slash(Unit caster, Unit target, Action onImpact)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Pygmalion))
            {
                caster?.StartCoroutine(CardMotion.Channel(caster, 0.06f, 0.19f, 0.10f));
                yield return VfxComposer.PlaySingleTarget("pygmalion.ember", caster, target,
                    onImpact, 1f, 0.60f);
                yield break;
            }

            yield return LegacySlash(caster, target, onImpact);
        }

        private static IEnumerator HandpaintSlash(Unit caster, Unit target, Action onImpact)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 origin = VfxSpace.WorldPoint(caster);
            Vector3 end = VfxSpace.WorldPoint(target);
            int layer = VfxSpace.SortingLayer(target);
            caster.StartCoroutine(CardMotion.Channel(caster, 0.06f, 0.19f, 0.10f));
            BattlefieldBackdrop.PushFocus(0.38f, 0.02f, 0.13f, 0.18f,
                new Color(0.15f, 0.035f, 0.012f), origin, end);

            HandpaintFx.PlayAt(end + Vector3.up * VfxSpace.CardSize * 0.04f, layer,
                HandpaintFx.Slash1RedSlow, new Vector2(1.92f, 1.34f),
                new Color(1f, 0.48f, 0.20f, 0.96f), WhiteHot,
                38f, 46, 0.96f, 0.80f);
            // 같은 손그림 한 획을 안쪽에 얇게 겹쳐 적갈 몸체 안의 백열 칼심을 밝은 판에서도 보존한다.
            HandpaintFx.PlayAt(end + Vector3.up * VfxSpace.CardSize * 0.04f, layer,
                HandpaintFx.Slash1RedSlow, new Vector2(1.66f, 1.12f),
                new Color(1f, 0.82f, 0.48f, 0.48f), WhiteHot,
                38f, 49, 0.62f, 0.88f);

            // 원화 재생률과 무관하게 실제 판정은 기존 계약인 시전 후 0.10초에 한 번만 발생한다.
            yield return new WaitForSeconds(0.10f);
            Vector3 point = target != null && target.isActive ? VfxSpace.WorldPoint(target) : end;
            // 한 획이 대상을 가르는 판정 정점에만 짧은 백열 심광을 얹어 붉은 호의 중심 통과를 읽힌다.
            SpriteFx.Play(VfxArt.Glint, point + Vector3.up * VfxSpace.CardSize * 0.04f,
                WhiteHot, true, VfxSpace.CardSize * 0.10f, VfxSpace.CardSize * 0.54f,
                0.14f, 38f, 0f, 0f, 0.04f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 52);
            PlayHandpaintAfterglow(point, layer);
            PlayScorch(point, layer);
            PlayHeatHaze(point, layer);
            BattlefieldBackdrop.PulseFlash(new Color(1f, 0.22f, 0.025f), 0.075f, 0.075f);
            CameraShake.ShakeDirectional(end - origin, 0.068f);
            onImpact?.Invoke();

            // 한 획 0.28초와 잔화 감쇠를 합쳐 Legacy와 같은 약 0.60초 행동 길이를 유지한다.
            yield return new WaitForSeconds(0.50f);
        }

        private static IEnumerator LegacySlash(Unit caster, Unit target, Action onImpact)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 origin = VfxSpace.WorldPoint(caster);
            Vector3 end = VfxSpace.WorldPoint(target);
            int layer = VfxSpace.SortingLayer(target);
            caster.StartCoroutine(CardMotion.Channel(caster, 0.06f, 0.19f, 0.10f));
            BattlefieldBackdrop.PushFocus(0.38f, 0.02f, 0.13f, 0.18f,
                new Color(0.15f, 0.035f, 0.012f), origin, end);

            ClawRakeStyle rake = PygmalionRakeStyle();
            // 느린 한 획의 중심 통과 비율을 앞당겨 기존 0.10초 판정 계약과 맞춘다.
            yield return new WaitForSeconds(Mathf.Max(0f, 0.10f - rake.CenterCrossSeconds));
            yield return ClawRakeFx.Play(target, end, layer, rake, () =>
            {
                Vector3 point = target != null && target.isActive ? VfxSpace.WorldPoint(target) : end;
                PlayScorch(point, layer);
                PlayHeatHaze(point, layer);
                BattlefieldBackdrop.PulseFlash(new Color(1f, 0.22f, 0.025f), 0.075f, 0.075f);
                CameraShake.ShakeDirectional(end - origin, 0.068f);
                onImpact?.Invoke();
            });
        }

        private static void PlayHandpaintAfterglow(Vector3 point, int layer)
        {
            HandpaintFx.PlayAt(point + Vector3.down * VfxSpace.CardSize * 0.03f, layer,
                HandpaintFx.Fire1Red, new Vector2(0.50f, 0.62f),
                new Color(1f, 0.64f, 0.34f, 0.78f), WhiteHot,
                0f, 38, 0.82f, 0.66f);
            HandpaintFx.PlayAt(point + Vector3.down * VfxSpace.CardSize * 0.12f, layer,
                HandpaintFx.Smoke1Red, new Vector2(0.74f, 0.54f),
                new Color(0.38f, 0.055f, 0.025f, 0.48f), Gold,
                5f, 35, 0.62f, 0.20f);
        }

        private static ClawRakeStyle PygmalionRakeStyle() => new()
        {
            Sprite = VfxArt.ShiCrimsonClawStroke,
            LineCount = 1,
            LengthInCards = 1.96f,
            ThicknessInCards = 0.16f,
            AngleDegrees = 38f,
            DrawDuration = 0.28f,
            CenterCrossFraction = 0.3571429f,
            LineDelay = 0f,
            LineSpacingInCards = 0f,
            LingerDuration = 0.12f,
            FadeDuration = 0.20f,
            RevealSoftness = 0.050f,
            TipWidth = 0.092f,
            CardKickStrength = 0.66f,
            OffsetInCards = new Vector2(0.045f, 0.08f),
            BodyColor = new Color(0.24f, 0.045f, 0.014f, 0.94f),
            EdgeColor = new Color(1f, 0.27f, 0.025f, 0.94f),
            CoreColor = WhiteHot,
        };

        private static void PlayScorch(Vector3 point, int layer)
        {
            float size = VfxSpace.CardSize;
            SpriteFx.Play(VfxArt.Streak, point + Vector3.up * size * 0.07f,
                new Color(0.20f, 0.045f, 0.018f, 0.20f), false,
                new Vector2(size * 0.66f, size * 0.022f),
                new Vector2(size * 1.12f, size * 0.014f),
                0.42f, 38f, 0f, 0.01f, 0.20f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 27);
        }

        private static void PlayHeatHaze(Vector3 point, int layer)
        {
            float size = VfxSpace.CardSize;
            VfxParticles.Burst(point + Vector3.up * size * 0.06f, new ParticleSpec
            {
                Sprite = VfxArt.Smoke,
                Additive = false,
                StartColor = new Color(1f, 0.63f, 0.30f, 0.075f),
                EndColor = new Color(0.48f, 0.12f, 0.035f, 0f),
                Lifetime = new Vector2(0.20f, 0.34f),
                Speed = new Vector2(size * 0.12f, size * 0.28f),
                Angle = 90f,
                Spread = 34f,
                StartSize = new Vector2(size * 0.055f, size * 0.12f),
                EndSizeScale = 1.5f,
                Gravity = -size * 0.06f,
                Drag = 1.8f,
                Spin = new Vector2(10f, 36f),
                SpawnRadius = size * 0.30f,
                FadeIn = 0.10f,
            }, 5, layer, VfxSpace.OverlaySortingOrder + 36);
        }

        public static void Blaze(Unit target, bool detonate)
        {
            if (target == null) return;
            float size = VfxSpace.CardSize;
            Vector3 point = VfxSpace.WorldPoint(target) + Vector3.up * size * 0.10f;
            int layer = VfxSpace.SortingLayer(target);
            if (detonate)
            {
                if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Himiko))
                    VfxRunner.Run(PlayComposedBlazeBurst(target));
                else
                    PlayLegacyBlazeBurst(target, point, layer);
                return;
            }

            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Himiko))
            {
                VfxComposer.PlayAffectedPart(target, "affected.debuff.blaze");
                return;
            }

            SpriteFx.Play(VfxArt.SharedEmberRise, point, new Color(1f, 0.52f, 0.28f, 0.72f), false,
                new Vector2(size * 0.08f, size * 0.08f), new Vector2(size * 0.26f, size * 0.26f),
                0.28f, 0f, 0f, 0f, 0.34f, SpriteFx.Ease.OutBack,
                layer, VfxSpace.OverlaySortingOrder + 4);
        }

        private static IEnumerator PlayComposedBlazeBurst(Unit target)
        {
            if (target == null) yield break;
            AudioManager.PlayCue("himiko.blaze.burst");
            // 일반 공격 피격과 표식광은 반복하지 않고, 실제 3중첩 폭발 자리의 연기 한 장만 남긴다.
            yield return VfxComposer.PlayHitSlot("himiko.blaze_burst", 0,
                target, target, null);
        }

        private static IEnumerator PlayHandpaintBlazeBurst(Unit target, Vector3 point, int layer)
        {
            float size = VfxSpace.CardSize;
            // 상태 표식이 먼저 안쪽으로 조여진 뒤 손그림 화염이 점화되어 폭발하는 인과를 한 박자로 묶는다.
            SpriteFx.Play(VfxArt.RuneOrnate, point, new Color(0.62f, 0.055f, 0.018f, 0.68f), false,
                size * 0.34f, size * 0.08f, 0.075f, 0f, -26f, 0f, 0.04f,
                SpriteFx.Ease.InCubic, layer, VfxSpace.OverlaySortingOrder + 25);
            HandpaintFx.PlayAt(point, layer, HandpaintFx.Fire1Red, new Vector2(0.26f, 0.34f),
                new Color(1f, 0.68f, 0.36f, 0.88f), WhiteHot,
                0f, 31, 0.90f, 0.72f);
            yield return new WaitForSeconds(0.065f);

            AudioManager.PlayCue("himiko.blaze.burst");
            HandpaintFx.PlayAt(point, layer, HandpaintFx.Explosion2Red, new Vector2(1.58f, 1.48f),
                new Color(1f, 0.62f, 0.36f, 0.94f), WhiteHot,
                0f, 45, 0.96f, 0.82f);
            HandpaintFx.PlayAt(point, layer, HandpaintFx.Explosion1Red, new Vector2(1.02f, 0.96f),
                new Color(1f, 0.80f, 0.48f, 0.86f), WhiteHot,
                -12f, 48, 0.88f, 0.86f);
            SpriteFx.Play(VfxArt.Glint, point, WhiteHot, true,
                size * 0.16f, size * 0.68f, 0.16f, 0f, 0f, 0f, 0.05f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 50);
            CardMotion.ViewOf(target)?.PulseVfxLight(Gold, 0.10f, 0.34f);
            BattlefieldBackdrop.PulseFlash(new Color(1f, 0.24f, 0.025f), 0.072f, 0.070f);
            CameraShake.Shake(0.060f);

            yield return new WaitForSeconds(0.10f);
            HandpaintFx.PlayAt(point + Vector3.down * size * 0.05f, layer,
                HandpaintFx.Fire1Red, new Vector2(0.58f, 0.70f),
                new Color(1f, 0.62f, 0.32f, 0.76f), WhiteHot,
                0f, 36, 0.80f, 0.62f);
            HandpaintFx.PlayAt(point + Vector3.down * size * 0.14f, layer,
                HandpaintFx.Smoke1Red, new Vector2(0.96f, 0.68f),
                new Color(0.34f, 0.045f, 0.020f, 0.52f), Gold,
                0f, 33, 0.60f, 0.18f);
        }

        private static void PlayLegacyBlazeBurst(Unit target, Vector3 point, int layer)
        {
            AudioManager.PlayCue("himiko.blaze.burst");
            var cue = new SkillFxCue
            {
                ResourcePath = "VfxTex/Gen/Enemy2/HimikoBlazeBurst_4x4",
                FrameDelays = SkillFx.Delays(0.014f, 0.020f, 0.028f, 0.034f),
                ImpactFrame = 0,
                Pivot = new Vector2(0.5f, 0.5f),
                SizeInCards = new Vector2(0.92f, 0.92f),
                Color = Color.white,
                OrderOffset = 28,
                Composite = new FlipbookCompositeStyle
                {
                    PeakAlpha = 0.76f,
                    FadeInFraction = 0f,
                    FadeOutFraction = 0.40f,
                    GlowColor = WhiteHot,
                    GlowAlpha = 0.62f,
                    GlowThreshold = 0.71f,
                    GlowSoftness = 0.14f,
                    GlowGain = 1.22f,
                    BrightBackdropLift = new Color(1f, 0.48f, 0.12f, 1f),
                    BrightBackdropLiftStrength = 0.30f,
                    StartScale = 0.88f,
                    CrestScale = 1.05f,
                    EndScale = 1.10f,
                    GroundLightColor = new Color(1f, 0.28f, 0.04f, 0.18f),
                    GroundLightSizeInCards = new Vector2(0.60f, 0.13f),
                    ContactShadowColor = new Color(0.24f, 0.055f, 0.018f, 0.20f),
                    ContactShadowSizeInCards = new Vector2(0.52f, 0.10f),
                    GroundOffsetInCards = -0.34f,
                    PeakResourcePath = "VfxTex/Gen/Enemy2/HimikoBlazeBurstPeak",
                    PeakStart = 0.05f,
                    PeakHoldEnd = 0.48f,
                    PeakEnd = 0.72f,
                    PeakLayerAlpha = 0.72f,
                    SceneLightColor = Gold,
                    SceneLightStrength = 0.070f,
                },
            };
            SkillFx.PlayCue(cue, point, layer, VfxSpace.OverlaySortingOrder + 20, target);
            CardMotion.ViewOf(target)?.PulseVfxLight(Gold, 0.10f, 0.34f);
            BattlefieldBackdrop.PulseFlash(new Color(1f, 0.24f, 0.025f), 0.072f, 0.070f);
            CameraShake.Shake(0.060f);
        }

        public static void Focus(Unit unit, Color tint)
        {
            if (unit == null) return;
            float size = VfxSpace.CardSize;
            Vector3 point = VfxSpace.WorldPoint(unit);
            int layer = VfxSpace.SortingLayer(unit);
            SpriteFx.Play(VfxArt.RuneOrnate, point, new Color(0.18f, 0.04f, 0.035f, 0.62f), false,
                new Vector2(size * 0.18f, size * 0.18f), new Vector2(size * 0.92f, size * 0.92f),
                0.46f, 0f, 48f, 0.01f, 0.50f, SpriteFx.Ease.OutBack,
                layer, VfxSpace.OverlaySortingOrder + 1);
            SpriteFx.Play(VfxArt.Crescent, point, tint, true,
                size * 0.12f, size * 0.54f, 0.38f, -30f, 62f, 0f, 0.42f,
                SpriteFx.Ease.OutBack, layer, VfxSpace.OverlaySortingOrder + 3);
        }
    }
}
