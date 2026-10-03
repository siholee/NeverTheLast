using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>동일 스킬을 합성 단계별로 비교하는 하네스 선택값. 제품 기본은 최종 하이브리드다.</summary>
    public enum SkillFxPlaybackMode
    {
        Final,
        Legacy16,
        Blended16,
        Hybrid32,
    }

    /// <summary>메이플식 독립 노드의 위치와 정렬을 기술하는 플립북 한 겹.</summary>
    public sealed class SkillFxCue
    {
        public string ResourcePath;
        public int Columns = 4;
        public int Rows = 4;
        public float[] FrameDelays;
        public int ImpactFrame = 7;
        public Vector2 Pivot = new(0.5f, 0.5f);
        public Vector2 SizeInCards = Vector2.one;
        public Vector2 OffsetInCards;
        public Color Color = Color.white;
        public bool Additive;
        public bool Loop;
        public bool FlipX;
        public bool ReverseFrames;
        public bool BehindPortrait;
        public int OrderOffset;
        public FlipbookCompositeStyle Composite;
    }

    /// <summary>화면 톤·섬광·셰이크는 시트와 독립된 Screen 노드다.</summary>
    public sealed class SkillFxScreenCue
    {
        public float FocusStrength;
        public float FadeIn = 0.08f;
        public float Hold = 0.24f;
        public float FadeOut = 0.24f;
        public Color Tint = new(0.14f, 0.27f, 0.43f, 1f);
        public Color FlashColor = Color.white;
        public float FlashStrength;
        public float FlashDuration = 0.08f;
        public float Shake;
    }

    /// <summary>
    /// 한 스킬의 시각 노드를 전투 판정과 분리해 묶는다.
    /// Cast는 시전자 뒤/앞에 동시에 겹칠 수 있고 Projectile은 선택, Hit은 대상마다 재생한다.
    /// Ground와 Screen 역시 독립이므로 투사체가 없는 광역기도 같은 계약을 쓴다.
    /// </summary>
    public sealed class SkillFxPlan
    {
        public SkillFxCue CastBehind;
        public SkillFxCue CastFront;
        public SkillFxCue Projectile;
        public SkillFxCue Hit;
        public SkillFxCue Ground;
        public SkillFxScreenCue Screen;
        public float CastLead;
        public float ProjectileDuration = 0.2f;
        public float ProjectileArc;
        public int VisualHitCount = 1;
        public float VisualHitInterval = 0.07f;
        public string CastSfx;
        public string ProjectileSfx;
        public string HitSfx;
        public string ScreenSfx;
    }

    /// <summary>
    /// 독립 Cast/Projectile/Hit/Screen/Ground 노드를 실행한다.
    /// 피해 콜백은 Projectile이 없으면 Hit의 ImpactFrame, 있으면 마지막 Hit의 ImpactFrame에 한 번만 호출한다.
    /// VisualHitCount가 2 이상이어도 호출부가 피해를 나누지 않는 한 나머지는 연출 전용이다.
    /// </summary>
    public static class SkillFx
    {
        private const int RearOrder = -100;
        private static int FrontOrder => VfxSpace.OverlaySortingOrder + 40;

        /// <summary>
        /// 최종 빌드는 고해상도 정점 하이브리드를 쓴다. 하네스만 이 값을 바꿔 같은 판정 타이밍의 A/B/C를 찍는다.
        /// </summary>
        public static SkillFxPlaybackMode PlaybackMode { get; set; } = SkillFxPlaybackMode.Final;

        public static IEnumerator PlayTargeted(Unit caster, Unit target, SkillFxPlan plan, Action onImpact)
        {
            bool invoked = false;
            void ImpactOnce()
            {
                if (invoked) return;
                invoked = true;
                onImpact?.Invoke();
            }

            if (caster == null || target == null || plan == null)
            {
                ImpactOnce();
                yield break;
            }

            int layer = VfxSpace.SortingLayer(caster);
            Vector3 casterPoint = VfxSpace.WorldPoint(caster);
            Vector3 targetPoint = VfxSpace.WorldPoint(target);
            if (!string.IsNullOrWhiteSpace(plan.CastSfx)) AudioManager.PlayCue(plan.CastSfx);
            if (plan.Screen != null && !string.IsNullOrWhiteSpace(plan.ScreenSfx))
                AudioManager.PlayCue(plan.ScreenSfx);
            PlayScreen(plan.Screen, casterPoint, targetPoint);
            PlayCue(plan.CastBehind, casterPoint, layer, RearOrder, caster);
            PlayCue(plan.CastFront, casterPoint, layer, FrontOrder, caster);
            PlayCue(plan.Ground, GroundPoint(target), layer, RearOrder + 10);

            if (plan.CastLead > 0f) yield return new WaitForSeconds(plan.CastLead);

            if (plan.Projectile != null)
            {
                if (!string.IsNullOrWhiteSpace(plan.ProjectileSfx)) AudioManager.PlayCue(plan.ProjectileSfx);
                FlipbookFx projectile = PlayCue(plan.Projectile, casterPoint, layer, FrontOrder, null);
                float duration = VfxTiming.AtLeastFrames(plan.ProjectileDuration, 3);
                Vector3 start = casterPoint;
                for (float age = 0f; age < duration; age += Time.deltaTime)
                {
                    float t = Mathf.Clamp01(age / duration);
                    Vector3 end = target != null && target.isActive ? VfxSpace.WorldPoint(target) : targetPoint;
                    Vector3 position = Vector3.Lerp(start, end, t);
                    position.y += Mathf.Sin(t * Mathf.PI) * plan.ProjectileArc * VfxSpace.CardSize;
                    if (projectile != null) projectile.FxTransform.position = position;
                    yield return null;
                }
            }

            int hitCount = Mathf.Max(1, plan.VisualHitCount);
            int completed = 0;
            for (int i = 0; i < hitCount; i++)
            {
                bool callbackHit = i == 0;
                Unit captured = target;
                PlayImpact(plan.Hit, captured, layer, callbackHit ? ImpactOnce : null, () => completed++,
                    plan.HitSfx);
                if (i + 1 < hitCount) yield return new WaitForSeconds(plan.VisualHitInterval);
            }

            float guard = 4f;
            while (completed < hitCount && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
            if (!invoked) ImpactOnce();
        }

        public static FlipbookFx PlayImpact(SkillFxCue cue, Unit target, int layer, Action onImpact,
            Action onComplete = null, string sfxCue = null)
        {
            Vector3 point = target != null ? VfxSpace.WorldPoint(target) : Vector3.zero;
            int impactFrame = cue != null ? Mathf.Max(0, cue.ImpactFrame) : 0;
            bool invoked = false;
            void Frame(int frame)
            {
                if (!invoked && frame >= impactFrame)
                {
                    invoked = true;
                    if (!string.IsNullOrWhiteSpace(sfxCue)) AudioManager.PlayCue(sfxCue);
                    onImpact?.Invoke();
                }
            }
            void Complete()
            {
                if (!invoked)
                {
                    invoked = true;
                    if (!string.IsNullOrWhiteSpace(sfxCue)) AudioManager.PlayCue(sfxCue);
                    onImpact?.Invoke();
                }
                onComplete?.Invoke();
            }

            int baseOrder = cue != null && cue.BehindPortrait ? RearOrder : FrontOrder;
            FlipbookFx fx = PlayCue(cue, point, layer, baseOrder, target, Frame, Complete);
            if (fx == null) Complete();
            return fx;
        }

        public static FlipbookFx PlayCue(SkillFxCue cue, Vector3 anchor, int layer, int baseOrder,
            Unit follow = null, Action<int> onFrame = null, Action onComplete = null)
        {
            if (cue == null || string.IsNullOrWhiteSpace(cue.ResourcePath)) return null;
            float s = VfxSpace.CardSize;
            Vector3 offset = new(cue.OffsetInCards.x * s, cue.OffsetInCards.y * s, 0f);
            FlipbookFx fx = FlipbookFx.Play(cue.ResourcePath, cue.Columns, cue.Rows, cue.Pivot,
                anchor + offset, cue.SizeInCards * s, cue.Color, cue.Additive, cue.Loop, cue.FlipX,
                cue.FrameDelays, layer, baseOrder + cue.OrderOffset, onFrame, onComplete,
                cue.Composite, PlaybackMode, cue.ReverseFrames);
            if (fx != null && follow != null)
                fx.Follow(() => VfxSpace.WorldPoint(follow), offset);
            if (fx != null && cue.Composite != null && cue.Composite.SceneLightStrength > 0f &&
                PlaybackMode != SkillFxPlaybackMode.Legacy16)
            {
                PulseSceneLight(anchor + offset, cue.Composite.SceneLightColor,
                    cue.Composite.SceneLightStrength, Duration(cue.FrameDelays));
            }
            return fx;
        }

        public static void PlayScreen(SkillFxScreenCue cue, Vector3 primary, Vector3 secondary)
        {
            if (cue == null) return;
            float speed = Mathf.Max(1f, Managers.UI.HUD.BattleHud.CurrentSpeedMultiplier);
            float speedStrength = speed >= 4f ? Mathf.Lerp(0.72f, 0.56f, Mathf.InverseLerp(4f, 8f, speed)) : 1f;
            float speedDuration = speed >= 4f ? Mathf.Lerp(0.68f, 0.48f, Mathf.InverseLerp(4f, 8f, speed)) : 1f;
            if (cue.FocusStrength > 0f)
                BattlefieldBackdrop.PushFocus(cue.FocusStrength * speedStrength,
                    cue.FadeIn * speedDuration, cue.Hold * speedDuration, cue.FadeOut * speedDuration,
                    cue.Tint, primary, secondary);
            if (cue.FlashStrength > 0f)
                BattlefieldBackdrop.PulseFlash(cue.FlashColor, cue.FlashStrength, cue.FlashDuration);
            if (cue.Shake > 0f) CameraShake.Shake(cue.Shake);
        }

        public static Vector3 GroundPoint(Unit unit)
            => VfxSpace.WorldPoint(unit) + Vector3.down * (0.42f * VfxSpace.CardSize);

        public static float[] Delays(float anticipation, float action, float peak, float fade)
        {
            var delays = new float[16];
            for (int i = 0; i < delays.Length; i++)
            {
                delays[i] = i < 3 ? anticipation : i < 8 ? action : i < 11 ? peak : fade;
            }
            return delays;
        }

        private static float Duration(float[] delays)
        {
            float total = 0f;
            if (delays != null)
                foreach (float delay in delays) total += Mathf.Max(0f, delay);
            return Mathf.Max(0.22f, total);
        }

        private static void PulseSceneLight(Vector3 origin, Color color, float strength, float duration)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;
            PulseList(grid.heroList, origin, color, strength, duration);
            PulseList(grid.enemyList, origin, color, strength, duration);
        }

        private static void PulseList(IReadOnlyList<Unit> units, Vector3 origin, Color color,
            float strength, float duration)
        {
            if (units == null) return;
            float radius = Mathf.Max(0.01f, VfxSpace.CardSize * 2.4f);
            foreach (Unit unit in units)
            {
                if (unit == null || !unit.isActive || !unit.IsOnField) continue;
                float distance = Vector2.Distance(origin, VfxSpace.WorldPoint(unit));
                float falloff = Mathf.Lerp(0.34f, 1f, 1f - Mathf.Clamp01(distance / radius));
                CardMotion.ViewOf(unit)?.PulseVfxLight(color, strength * falloff, duration);
            }
        }
    }
}
