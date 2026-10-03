using System;
using System.Collections;
using Effects.Vfx.Presets;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>시 전용 멸참 연출. 시전자 한 번 휘두름 뒤 대상 초상화의 세 갈래 할퀴기만 연다.</summary>
    public static class ShiFx
    {
        private static readonly Color DeepBlack = new(0.004f, 0.004f, 0.006f, 0.96f);
        private static readonly Color Crimson = new(0.68f, 0.012f, 0.018f, 0.86f);
        private static readonly Color HotRed = new(1f, 0.055f, 0.035f, 0.92f);

        public static IEnumerator DestructionSlash(Unit caster, Unit target, Action onImpact,
            float scale = 1f)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Shi))
            {
                Vector3 origin = VfxSpace.WorldPoint(caster);
                Vector3 end = VfxSpace.WorldPoint(target);
                VfxComposer.PlayScreen("shi.destruction_slash", caster, target);
                VfxComposer.PlaySlot("shi.destruction_slash", "cast", 0, caster, target,
                    origin, VfxSpace.SortingLayer(caster), scale);
                yield return new WaitForSeconds(VfxComposer.Offset(
                    "shi.destruction_slash", "hit", 0, 0.128f));
                yield return VfxComposer.PlayHitSlot("shi.destruction_slash", 0, caster, target, () =>
                {
                    BattlefieldBackdrop.PulseFlash(new Color(0.30f, 0.006f, 0.014f), 0.085f, 0.075f);
                    CameraShake.ShakeDirectional(end - origin, 0.09f * scale);
                    onImpact?.Invoke();
                }, scale);
                yield break;
            }

            // Legacy 비교는 승인 전 검붉은 절차형 붓획을 보존한다.
            Vector3 legacyOrigin = VfxSpace.WorldPoint(caster);
            Vector3 legacyEnd = VfxSpace.WorldPoint(target);
            VfxAim aim = VfxSpace.AimFrom(caster, target);
            int layer = VfxSpace.SortingLayer(target);
            DrawSlashFx.Play(caster, new DrawSlashStyle
            {
                BodyColor = DeepBlack,
                EdgeColor = Crimson,
                CoreColor = HotRed,
                LengthInCards = 1.30f,
                ThicknessInCards = 0.075f,
                AngleDegrees = aim.StrokeRotation(34f),
                Duration = 0.18f,
                OffsetInCards = new Vector2(0f, -0.16f),
            });
            var style = new ClawRakeStyle
            {
                Sprite = VfxArt.ShiCrimsonClawStroke,
                LineCount = 3,
                LengthInCards = 1.68f * scale,
                ThicknessInCards = 0.10f * scale,
                AngleDegrees = aim.StrokeRotation(29f),
                DrawDuration = 0.18f,
                CenterCrossFraction = 0.50f,
                LineDelay = 0.022f,
                LineSpacingInCards = 0.16f,
                LingerDuration = 0.10f,
                FadeDuration = 0.17f,
                RevealSoftness = 0.038f,
                TipWidth = 0.058f,
                CardKickStrength = 0.54f,
                BodyColor = DeepBlack,
                EdgeColor = Crimson,
                CoreColor = HotRed,
            };
            yield return new WaitForSeconds(Mathf.Max(0f, 0.24f - style.CenterCrossSeconds));
            yield return ClawRakeFx.Play(target, legacyEnd, layer, style, () =>
            {
                CameraShake.ShakeDirectional(legacyEnd - legacyOrigin, 0.09f * scale);
                onImpact?.Invoke();
            });
        }
    }
}
