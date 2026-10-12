using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using Managers;
using UnityEngine;
using Random = Effects.Vfx.PresentationRandom;

namespace Effects.Vfx.Presets
{
    /// <summary>구매 손그림 시트의 그리드·재생률을 한곳에서 보존한다.</summary>
    public readonly struct HandpaintSheet
    {
        public readonly string Name;
        public readonly int Columns;
        public readonly int Rows;
        public readonly float Fps;
        public readonly Vector2 Pivot;

        public HandpaintSheet(string name, int columns, int rows, float fps, Vector2 pivot)
        {
            Name = name;
            Columns = columns;
            Rows = rows;
            Fps = fps;
            Pivot = pivot;
        }

        public string ResourcePath => $"VfxTex/Handpaint/{Name}";
    }

    /// <summary>
    /// Effect Collection - Handpaint Spritesheet의 실제 사용분을 FlipbookFx로 재생한다.
    /// 원본 색은 런타임 틴트하고, 알파 몸체와 휘도 코어를 분리해 밝은 전장에서도 형태를 남긴다.
    /// </summary>
    public static class HandpaintFx
    {
        public static readonly HandpaintSheet Slash1Blue = new("Slash1_Blue", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Slash4Blue = new("Slash4_Blue", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Slash5Blue = new("Slash5_Blue", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Explosion2Blue = new("Explosion2_Blue", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Impact1Blue = new("Impact1_Blue", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Explosion2Green = new("Explosion2_Green", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Impact1Green = new("Impact1_Green", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Slash1Purple = new("Slash1_Purple", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Lightning2Purple = new("Lightning2_Purple", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Impact1Purple = new("Impact1_Purple", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Ball1Yellow = new("Ball1_Sheet_Yellow", 3, 3, 18f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Muzzle1Yellow = new("Muzzle1_Yellow", 2, 2, 40f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Explosion3Yellow = new("Explosion3_Yellow", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Impact1Yellow = new("Impact1_Yellow", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Smoke2Yellow = new("Smoke2_Yellow", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Smoke6Yellow = new("Smoke6_Yellow", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Ball1Blue = new("Ball1_Sheet_Blue", 3, 3, 18f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Muzzle1Blue = new("Muzzle1_Blue", 2, 2, 40f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Impact1White = new("Impact1_White", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Smoke1Blue = new("Smoke1_Blue", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Smoke5Blue = new("Smoke5_Blue", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        // 카탈로그의 표기는 행×열이다. 런타임 생성자는 열, 행 순서이므로 실제 시트 배열로 뒤집어 넣는다.
        public static readonly HandpaintSheet FireAttack1Red = new("FireAttack1_Red", 4, 3, 60f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Muzzle1Red = new("Muzzle1_Red", 2, 2, 40f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Explosion1Red = new("Explosion1_Red", 3, 2, 30f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Explosion2Red = new("Explosion2_Red", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Fire1Red = new("Fire1_Red", 4, 4, 25.6f, new Vector2(0.5f, 0.5f));
        public static readonly HandpaintSheet Smoke1Red = new("Smoke1_Red", 3, 3, 45f, new Vector2(0.5f, 0.5f));
        // 피그말리온은 같은 원화를 0.28초 한 획으로 늘려 기존 조각가의 무거운 붓질 리듬을 보존한다.
        public static readonly HandpaintSheet Slash1RedSlow = new("Slash1_Red", 3, 3, 32.15f, new Vector2(0.5f, 0.5f));

        private static readonly Vector2[] VolleyAnchors =
        {
            new(-0.18f, 0.20f), new(0.17f, 0.18f), new(-0.21f, 0.00f),
            new(0.20f, -0.02f), new(-0.13f, -0.21f), new(0.13f, -0.22f),
        };

        public static SkillFxCue Cue(HandpaintSheet sheet, Vector2 size, Color body, Color core,
            float rotation = 0f, bool flipX = false, int impactFrame = 4, Vector2 offset = default,
            float peakAlpha = 0.86f, float glowAlpha = 0.60f)
        {
            return new SkillFxCue
            {
                ResourcePath = sheet.ResourcePath,
                Columns = sheet.Columns,
                Rows = sheet.Rows,
                FrameDelays = Delays(sheet),
                ImpactFrame = impactFrame,
                Pivot = sheet.Pivot,
                SizeInCards = size,
                OffsetInCards = offset,
                Color = body,
                FlipX = flipX,
                OrderOffset = 22,
                Composite = new FlipbookCompositeStyle
                {
                    PeakAlpha = peakAlpha,
                    AlphaCutoff = 0.035f,
                    AlphaSoftness = 0.055f,
                    FadeInFraction = 0.07f,
                    FadeOutFraction = 0.27f,
                    BrightBackdropLift = new Color(core.r, core.g, core.b, 1f),
                    BrightBackdropLiftStrength = 0.34f,
                    GlowColor = core,
                    GlowAlpha = glowAlpha,
                    GlowThreshold = 0.66f,
                    GlowSoftness = 0.17f,
                    GlowGain = 1.26f,
                    StartScale = 0.88f,
                    CrestScale = 1.04f,
                    EndScale = 1.09f,
                    RotationDegrees = rotation,
                    ContactShadowColor = new Color(0.18f, 0.24f, 0.31f, 0.20f),
                    ContactShadowSizeInCards = new Vector2(size.x * 0.64f, size.y * 0.11f),
                    GroundLightColor = new Color(core.r, core.g, core.b, 0.16f),
                    GroundLightSizeInCards = new Vector2(size.x * 0.58f, size.y * 0.10f),
                    GroundOffsetInCards = -0.30f,
                    SceneLightColor = core,
                    SceneLightStrength = 0.08f,
                },
            };
        }

        // 크기를 키우지 않고 정점의 몸체를 유지한다. 긴 교차합성으로 알파가 반씩 나뉘면
        // 실제 1배속에서 지원·회오리의 의미가 사라지므로 끝부분에서만 다음 셀로 넘긴다.
        private static void KeepReadableBody(SkillFxCue cue)
        {
            cue.Composite.StartScale = 1f;
            cue.Composite.CrestScale = 1f;
            cue.Composite.EndScale = 1f;
            cue.Composite.BlendFraction = 0.20f;
        }

        // 야마·바루나 선택 옵션이다. 주변 카드 조명과 어두운 접지면을 줄이고 국소 몸체를 보존한다.
        private static void CalmLocalLight(SkillFxCue cue)
        {
            cue.Composite.SoftPulse = true;
            cue.Composite.SceneLightStrength = 0f;
            cue.Composite.ContactShadowColor = Color.clear;
            cue.Composite.GroundLightColor = Color.clear;
            cue.Composite.GlowAlpha = Mathf.Min(cue.Composite.GlowAlpha, 0.18f);
            cue.Composite.GlowGain = 1f;
        }

        public static FlipbookFx PlayCast(Unit caster, HandpaintSheet sheet, Vector2 size,
            Color body, Color core, float rotation, bool flipX = false)
        {
            if (caster == null) return null;
            SkillFxCue cue = Cue(sheet, size, body, core, rotation, flipX, sheet.Columns * sheet.Rows - 1,
                new Vector2(0f, -0.04f), 0.90f, 0.58f);
            cue.OrderOffset = 34;
            return SkillFx.PlayCue(cue, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster),
                VfxSpace.OverlaySortingOrder + 20, caster);
        }

        /// <summary>판정과 무관한 발사광·잔광을 같은 합성 규격으로 즉시 겹친다.</summary>
        public static FlipbookFx PlayAt(Vector3 point, int layer, HandpaintSheet sheet, Vector2 size,
            Color body, Color core, float rotation = 0f, int orderOffset = 30,
            float peakAlpha = 0.82f, float glowAlpha = 0.58f, bool loop = false,
            bool flipX = false, int heldFrame = -1, float peakHoldSeconds = 0f,
            bool reverseFrames = false, float desaturate = 0f, bool steadyBody = false, int startFrame = 0, int tailHoldFrame = -1, float tailHoldSeconds = 0f, bool calmLight = false, float paintContrast = 0f)
        {
            SkillFxCue cue = Cue(sheet, size, body, core, rotation, flipX,
                sheet.Columns * sheet.Rows - 1, Vector2.zero, peakAlpha, glowAlpha);
            HoldFrame(cue, heldFrame, peakHoldSeconds);
            HoldFrame(cue, tailHoldFrame, tailHoldSeconds);
            cue.StartFrame = startFrame;
            cue.OrderOffset = orderOffset;
            cue.Loop = loop;
            cue.ReverseFrames = reverseFrames;
            cue.Composite.Desaturate = desaturate;
            cue.Composite.PaintContrast = Mathf.Clamp01(paintContrast);
            if (calmLight) CalmLocalLight(cue);
            if (steadyBody)
            {
                KeepReadableBody(cue);
                // 낙뢰 판정 뒤 가장 넓은 5번 셀만 유지해 피해 정점 앞의 시간은 바꾸지 않는다.
                HoldFrame(cue, tailHoldFrame, tailHoldSeconds);
            }
            return SkillFx.PlayCue(cue, point, layer, VfxSpace.OverlaySortingOrder);
        }

        public static IEnumerator PlayHit(Unit target, HandpaintSheet sheet, Vector2 size,
            Color body, Color core, Action onPeak, float rotation = 0f, bool flipX = false,
            int impactFrame = 4, HandpaintSheet? accent = null, Vector2 offsetInCards = default,
            float peakHoldSeconds = 0f, float peakAlpha = 0.86f, float glowAlpha = 0.60f, bool portraitAnchor = false, bool steadyBody = false, float desaturate = 0f, int tailHoldFrame = -1, float tailHoldSeconds = 0f, bool calmLight = false, float paintContrast = 0f)
        {
            if (target == null)
            {
                onPeak?.Invoke();
                yield break;
            }

            SkillFxCue cue = Cue(sheet, size, body, core, rotation, flipX, impactFrame,
                offsetInCards, peakAlpha, glowAlpha);
            cue.PortraitAnchor = portraitAnchor;
            cue.Composite.Desaturate = desaturate;
            cue.Composite.PaintContrast = Mathf.Clamp01(paintContrast);
            if (calmLight) CalmLocalLight(cue);
            if (steadyBody)
            {
                KeepReadableBody(cue);
                // 낙뢰 판정 뒤 가장 넓은 5번 셀만 유지해 피해 정점 앞의 시간은 바꾸지 않는다.
                HoldFrame(cue, tailHoldFrame, tailHoldSeconds);
            }
            HoldFrame(cue, impactFrame, peakHoldSeconds);
            int layer = VfxSpace.SortingLayer(target);
            bool done = false;
            SkillFx.PlayImpact(cue, target, layer, () =>
            {
                if (accent.HasValue)
                {
                    SkillFxCue accentCue = Cue(accent.Value, size * 0.72f, body, core,
                        rotation - 8f, flipX, accent.Value.Columns * accent.Value.Rows - 1,
                        offsetInCards, 0.76f, calmLight ? Mathf.Min(.16f,glowAlpha) : .68f);
                    accentCue.Composite.Desaturate = desaturate;
                    accentCue.Composite.PaintContrast = Mathf.Clamp01(paintContrast);
                    if(calmLight) CalmLocalLight(accentCue);
                    SkillFx.PlayCue(accentCue, VfxSpace.WorldPoint(target), layer,
                        VfxSpace.OverlaySortingOrder + 34, target);
                }
                onPeak?.Invoke();
            }, () => done = true);

            float guard = 1.2f;
            // 연출 옵션이 프레임률/일시정지에 따라 판정 완료를 앞당기지 않게 게임 시간을 따른다.
            while (!done && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>유닛을 따라가지 않는 전장 좌표에 판정 정점을 가진 손그림 피격을 재생한다.</summary>
        public static IEnumerator PlayPoint(Vector3 point, int layer, HandpaintSheet sheet, Vector2 size,
            Color body, Color core, Action onPeak, float rotation = 0f, bool flipX = false,
            int impactFrame = 4, float peakAlpha = 0.86f, float glowAlpha = 0.60f,
            bool floating = false, float peakHoldSeconds = 0f, bool calmLight = false, float desaturate = 0f)
        {
            SkillFxCue cue = Cue(sheet, size, body, core, rotation, flipX, impactFrame,
                Vector2.zero, peakAlpha, glowAlpha);
            cue.Composite.Desaturate = Mathf.Clamp01(desaturate);
            HoldFrame(cue, impactFrame, peakHoldSeconds);
            if (floating)
            {
                // 수평 연기는 대상 상체를 가로지르는 부품이다. 기본 접지 그림자를 남기면
                // 밝은 전장에서 바닥 얼룩처럼 읽히므로 제거하고, 원본 휘도 코어를 더 살린다.
                cue.Composite.ContactShadowColor = Color.clear;
                cue.Composite.GroundLightColor = Color.clear;
                cue.Composite.BrightBackdropLiftStrength = 0.52f;
                cue.Composite.GlowAlpha = Mathf.Max(glowAlpha, 0.70f);
                cue.Composite.GlowThreshold = 0.50f;
                cue.Composite.GlowSoftness = 0.22f;
                cue.Composite.GlowGain = 1.58f;
                cue.Composite.SceneLightStrength = 0.11f;
            }
            if(calmLight)CalmLocalLight(cue);
            bool invoked = false;
            bool done = false;
            SkillFx.PlayCue(cue, point, layer, VfxSpace.OverlaySortingOrder + 34, null, frame =>
            {
                if (invoked || frame < impactFrame) return;
                invoked = true;
                onPeak?.Invoke();
            }, () =>
            {
                if (!invoked)
                {
                    invoked = true;
                    onPeak?.Invoke();
                }
                done = true;
            });

            float guard = 1.2f;
            while (!done && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
            if (!invoked) onPeak?.Invoke();
        }

        public static IEnumerator PlayVolley(Unit caster, Unit target, Action onHit,
            HandpaintSheet projectile, HandpaintSheet impact, HandpaintSheet? accent,
            Color body, Color core, Color focus, string projectileCue, float scale = 1f,
            float impactScale = 1f)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            const int count = 6;
            const int primary = 2;
            int completed = 0;
            bool invoked = false;
            Vector3 source = VfxSpace.WorldPoint(caster);
            Vector3 destination = VfxSpace.WorldPoint(target);
            BattlefieldBackdrop.PushFocus(0.13f, 0.035f, 0.18f, 0.16f, focus, source, destination);
            CardMotion.Throw(caster, target, 0.72f);

            var anchors = new List<Vector2>(VolleyAnchors);
            for (int i = anchors.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (anchors[i], anchors[swap]) = (anchors[swap], anchors[i]);
            }

            for (int i = 0; i < count; i++)
            {
                int shot = i;
                if (!string.IsNullOrWhiteSpace(projectileCue))
                    AudioManager.PlayCue(projectileCue, 0.78f + i * 0.025f);
                VfxRunner.Run(FlyVolleyShot(caster, target, anchors[i], shot, primary,
                    projectile, impact, accent, body, core, scale, impactScale, () =>
                    {
                        if (invoked) return;
                        invoked = true;
                        onHit?.Invoke();
                    }, () => completed++));
                if (i + 1 < count) yield return new WaitForSeconds(0.05f);
            }

            float guard = 3f;
            while (completed < count && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
            if (!invoked) onHit?.Invoke();
        }

        private static IEnumerator FlyVolleyShot(Unit caster, Unit target, Vector2 impactOffset,
            int shot, int primary, HandpaintSheet projectile, HandpaintSheet impact,
            HandpaintSheet? accent, Color body, Color core, float scale, float impactScale,
            Action onPrimary, Action complete)
        {
            Vector3 start = VfxSpace.WorldPoint(caster);
            Vector3 fixedEnd = VfxSpace.WorldPoint(target) + new Vector3(impactOffset.x, impactOffset.y) * VfxSpace.CardSize;
            SkillFxCue projectileCue = Cue(projectile, new Vector2(0.68f, 0.68f) * scale,
                body, core, 0f, false, 8, Vector2.zero, 0.80f, 0.52f);
            projectileCue.Loop = true;
            FlipbookFx fx = SkillFx.PlayCue(projectileCue, start, VfxSpace.SortingLayer(caster),
                VfxSpace.OverlaySortingOrder + 22);
            float duration = VfxTiming.AtLeastFrames(0.18f, 4);
            Vector3 previous = start;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                Vector3 end = target != null && target.isActive
                    ? VfxSpace.WorldPoint(target) + new Vector3(impactOffset.x, impactOffset.y) * VfxSpace.CardSize
                    : fixedEnd;
                Vector3 direction = (end - start).normalized;
                Vector3 normal = new(-direction.y, direction.x, 0f);
                Vector3 position = Vector3.Lerp(start, end, t) + normal * Mathf.Sin(t * Mathf.PI) *
                    ((shot % 2 == 0 ? -1f : 1f) * 0.07f * VfxSpace.CardSize);
                if (fx != null)
                {
                    fx.FxTransform.position = position;
                    Vector3 tangent = position - previous;
                    if (tangent.sqrMagnitude > 0.000001f)
                        fx.FxTransform.rotation = Quaternion.Euler(0f, 0f,
                            Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                }
                previous = position;
                yield return null;
            }
            if (fx != null) UnityEngine.Object.Destroy(fx.gameObject);
            yield return PlayHit(target, impact, new Vector2(1.06f, 1.06f) * scale * impactScale,
                body, core, shot == primary ? onPrimary : null, 0f, false, 4, accent);
            complete?.Invoke();
        }

        private static float[] Delays(HandpaintSheet sheet)
        {
            int count = Mathf.Max(1, sheet.Columns * sheet.Rows);
            float delay = 1f / Mathf.Max(1f, sheet.Fps);
            var result = new float[count];
            for (int i = 0; i < count; i++) result[i] = delay;
            return result;
        }

        private static void HoldFrame(SkillFxCue cue, int frame, float seconds)
        {
            if (cue?.FrameDelays == null || seconds <= 0f || frame < 0 || frame >= cue.FrameDelays.Length) return;
            cue.FrameDelays[frame] += seconds;
        }
    }
}
