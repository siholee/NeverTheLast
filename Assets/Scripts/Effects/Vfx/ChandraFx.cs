using System;
using System.Collections;
using System.Collections.Generic;
using Effects.Vfx.Presets;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 찬드라 전용 월광 연출. 은백·연청 몸체와 좁은 백색 코어를 분리하고,
    /// 축복 원화는 얼굴 중앙을 비운 채 원화 뒤·둘레에서만 열린다.
    /// </summary>
    public static class ChandraFx
    {
        private static readonly Color MoonInk = new(0.16f, 0.22f, 0.42f, 0.78f);
        private static readonly Color MoonBlue = new(0.56f, 0.83f, 1f, 0.88f);
        private static readonly Color MoonSilver = new(0.88f, 0.96f, 1f, 0.96f);
        private static readonly Color MoonCore = new(1f, 1f, 1f, 1f);
        private static readonly Color MoonGold = new(1f, 0.87f, 0.56f, 0.80f);

        private const float SomaGatherDuration = 1.12f;
        private const float SomaSealDuration = 0.56f;
        private const float SomaSettleDuration = 0.52f;
        private const float ChangyaGatherDuration = 1.20f;
        private const float ChangyaSealDuration = 0.58f;
        private const float ChangyaSettleDuration = 0.56f;
        private const float ChangyaImpactDuration = 0.54f;

        private static readonly int BackdropLuminance = Shader.PropertyToID("_BackdropLuminance");
        private static readonly int PaletteLift = Shader.PropertyToID("_PaletteLift");
        private static readonly int LiftStrength = Shader.PropertyToID("_LiftStrength");
        private static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int UvRect = Shader.PropertyToID("_UvRect");
        private static readonly int GlowThreshold = Shader.PropertyToID("_GlowThreshold");
        private static readonly int GlowSoftness = Shader.PropertyToID("_GlowSoftness");
        private static readonly int GlowGain = Shader.PropertyToID("_GlowGain");

        private sealed class CompositeArt
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
            public float BaseScale;
            public Vector3 BasePosition;
        }

        private sealed class ConvergingShard
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
            public Vector3 Start;
            public float Curl;
            public float Rotation;
            public float BaseScale;
            public bool Handpaint;
        }

        /// <summary>한 번의 월광 시전 뒤 대상 카드 안 여섯 지점에서 은백 피격 리듬을 연다.</summary>
        public static IEnumerator Sachi(Unit caster, Unit target, Action onImpact, float scale = 1f)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                yield return VfxComposer.PlaySingleTarget("chandra.sachi", caster, target,
                    onImpact, scale);
                yield break;
            }

            var style = new RadiantVolleyStyle
            {
                Bolt = VfxArt.ChandraMoonlightVolleyBolt,
                ImpactResourcePath = "VfxTex/Maple/Sei/RadiantVolleyImpact_4x4",
                ProjectileCue = "chandra.sachi.projectile",
                FocusTint = new Color(0.20f, 0.29f, 0.48f, 1f),
                CrestBody = new Color(0.46f, 0.66f, 0.92f, 0.72f),
                CrestCore = new Color(0.92f, 0.98f, 1f, 0.76f),
                BodyTint = Color.white,
                CoreColor = MoonCore,
                PaletteLift = new Color(0.78f, 0.91f, 1f, 1f),
                CardLight = MoonBlue,
                GroundLight = MoonBlue,
                BodyAlpha = 0.80f,
                CoreAlpha = 0.56f,
            };
            yield return RadiantVolleyFx.Play(caster, target, onImpact, style, scale);
        }

        /// <summary>흩어진 월광이 찬드라에게 역류한 뒤 전 아군을 하나의 성역으로 감싼다.</summary>
        public static IEnumerator Soma(Unit caster, Unit buffTarget, IReadOnlyList<Unit> allies, Action onSeal)
        {
            if (caster == null) yield break;
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                yield return SomaComposed(caster, buffTarget, allies, onSeal);
                yield break;
            }

            float total = SomaGatherDuration + SomaSealDuration + SomaSettleDuration;
            Vector3 source = VfxSpace.WorldPoint(caster);
            BattlefieldBackdrop.PushFocus(0.70f, 0.06f, total - 0.38f, 0.38f,
                new Color(0.12f, 0.18f, 0.34f), source);
            caster.StartCoroutine(CardMotion.Channel(caster, 0.16f, total, 0.26f));
            yield return ReverseSanctuary(caster, buffTarget, allies, false,
                SomaGatherDuration, SomaSealDuration, SomaSettleDuration, onSeal);
        }

        /// <summary>
        /// 소마와 같은 역수렴 문법을 더 많은 층과 잔광으로 반복한다.
        /// 적 피해는 중심 섬광 정점에서 대상마다 기존 콜백 한 번만 실행한다.
        /// </summary>
        public static IEnumerator Changya(Unit caster, IReadOnlyList<Unit> allies, IReadOnlyList<Unit> targets,
            Action onSeal, Action<Unit> onHit)
        {
            if (caster == null || targets == null || targets.Count == 0) yield break;
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                yield return ChangyaComposed(caster, allies, targets, onSeal, onHit);
                yield break;
            }

            float sanctuaryLife = ChangyaGatherDuration + ChangyaSealDuration + ChangyaSettleDuration;
            Vector3 source = VfxSpace.WorldPoint(caster);
            BattlefieldBackdrop.PushFocus(0.76f, 0.05f,
                sanctuaryLife + ChangyaImpactDuration - 0.40f, 0.40f,
                new Color(0.11f, 0.17f, 0.33f), source);
            caster.StartCoroutine(CardMotion.Channel(caster, 0.14f,
                sanctuaryLife + ChangyaImpactDuration, 0.24f));

            yield return ReverseSanctuary(caster, caster, allies, true,
                ChangyaGatherDuration, ChangyaSealDuration, ChangyaSettleDuration, onSeal);

            AudioManager.PlayCue("chandra.changya.hit");
            BattlefieldBackdrop.PulseFlash(MoonSilver, 0.075f, 0.10f);
            CameraShake.Shake(0.050f);
            foreach (Unit target in targets)
            {
                if (target == null || !target.isActive) continue;
                PlayEnemyImpact(target);
                // 첫 십자 섬광이 나타나는 프레임이 판정 정점이다.
                onHit?.Invoke(target);
            }
            yield return new WaitForSeconds(ChangyaImpactDuration);
        }

        /// <summary>
        /// 소마 기본 화풍. 역수렴·돔은 버리고 방향성 시전 한 번과 실제 버프 대상의 받는 이펙트만 쓴다.
        /// 구현 효과에 보호막이 없으므로 보호막 부품도 재생하지 않는다.
        /// </summary>
        private static IEnumerator SomaComposed(Unit caster, Unit buffTarget, IReadOnlyList<Unit> allies,
            Action onSeal)
        {
            Unit aimTarget = buffTarget != caster ? buffTarget : FirstOtherAlly(allies, caster);
            aimTarget ??= buffTarget ?? caster;
            VfxComposer.PlayScreen("chandra.soma", caster, aimTarget);
            VfxComposer.PlaySlot("chandra.soma", "cast", 0, caster, aimTarget,
                VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            caster.StartCoroutine(CardMotion.Channel(caster, 0.10f, 0.52f, 0.16f));

            yield return new WaitForSeconds(0.20f);
            onSeal?.Invoke();
            if (buffTarget != null && buffTarget.isActive)
                VfxComposer.PlaySlot("chandra.soma", "affected", 0, caster, buffTarget,
                    VfxSpace.WorldPoint(buffTarget), VfxSpace.SortingLayer(buffTarget));
            yield return new WaitForSeconds(0.32f);
        }

        /// <summary>
        /// 창야 기본 화풍. 시전 한 번 뒤 대상별 월광 피격을 열고 실제 회복 뒤 지원 피격을 받는다.
        /// 시전에서 첫 피격 정점까지 0.32 게임초로 맞춰 승인된 1배속 리듬을 유지한다.
        /// </summary>
        private static IEnumerator ChangyaComposed(Unit caster, IReadOnlyList<Unit> allies,
            IReadOnlyList<Unit> targets, Action onSeal, Action<Unit> onHit)
        {
            Unit aimTarget = targets[0];
            VfxComposer.PlayScreen("chandra.changya", caster, caster);
            VfxComposer.PlaySlot("chandra.changya", "cast", 0, caster, aimTarget,
                VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            caster.StartCoroutine(CardMotion.Channel(caster, 0.10f, 0.62f, 0.16f));

            yield return new WaitForSeconds(0.16f);
            onSeal?.Invoke();
            // 월광 획득·피해 준비 정점과 대상별 피해 간격은 유지한다. 받는 연출은 코드의 실제 회복 뒤에만 열린다.
            AudioManager.PlayCue("chandra.changya.hit");
            yield return VfxComposer.PlayMultiTargetHits("chandra.changya", caster,
                targets, onHit, 0.05f, 1f, playCasterSlots: false);
            yield return new WaitForSeconds(0.12f);
        }

        private static Unit FirstOtherAlly(IReadOnlyList<Unit> allies, Unit caster)
        {
            if (allies == null) return null;
            foreach (Unit ally in allies)
                if (ally != null && ally != caster && ally.isActive) return ally;
            return null;
        }

        private static IEnumerator ReverseSanctuary(Unit caster, Unit buffTarget,
            IReadOnlyList<Unit> allies, bool ornate, float gatherDuration, float sealDuration,
            float settleDuration, Action onSeal)
        {
            Vector3 casterPoint = VfxSpace.WorldPoint(caster);
            ComputeSanctuaryEnvelope(caster, allies, out Vector3 center, out float domeScale);
            int layer = VfxSpace.SortingLayer(caster);
            int order = VfxSpace.OverlaySortingOrder - 72;

            CompositeArt outerRing = NewComposite("ChandraReverseOuterRing", VfxArt.ChandraMoonWaveRing,
                center, layer, order, domeScale * 1.08f, ornate ? 0.74f : 0.66f, ornate ? 0.52f : 0.44f);
            CompositeArt innerRing = NewComposite("ChandraReverseInnerRing",
                ornate ? VfxArt.RuneOrnate : VfxArt.RuneRadiant,
                center, layer, order + 3, domeScale * (ornate ? 0.88f : 0.76f),
                ornate ? 0.58f : 0.46f, ornate ? 0.44f : 0.32f);
            List<ConvergingShard> shards = CreateConvergingShards(caster, center,
                domeScale, ornate ? 18 : 12, layer, order + 6);

            gatherDuration = VfxTiming.AtLeastFrames(gatherDuration, 8);
            for (float age = 0f; age < gatherDuration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / gatherDuration);
                float eased = t * t * (3f - 2f * t);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));
                Vector3 ringPoint = Vector3.Lerp(center, casterPoint, eased);
                float collapse = Mathf.Lerp(1f, ornate ? 0.075f : 0.09f, eased);
                SetCompositeTransform(outerRing, ringPoint, domeScale * 1.08f * collapse, 110f * eased);
                SetCompositeAlpha(outerRing, (ornate ? 0.74f : 0.66f) * fade,
                    (ornate ? 0.52f : 0.44f) * fade);
                SetCompositeTransform(innerRing, ringPoint,
                    domeScale * (ornate ? 0.88f : 0.76f) * collapse, -145f * eased);
                SetCompositeAlpha(innerRing, (ornate ? 0.58f : 0.46f) * fade,
                    (ornate ? 0.44f : 0.32f) * fade);
                AnimateConvergingShards(shards, casterPoint, t, ornate);
                yield return null;
            }
            DestroyComposite(outerRing);
            DestroyComposite(innerRing);
            DestroyShards(shards);

            AudioManager.PlayCue(ornate ? "chandra.changya.screen" : "chandra.soma.screen");
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                if (!ornate)
                    VfxComposer.PlaySlot("chandra.soma", "cast", 0, caster, buffTarget,
                        center, layer);
                HandpaintFx.PlayAt(casterPoint, layer, HandpaintFx.Muzzle1Blue,
                    new Vector2(1.12f, 1.12f), MoonBlue, MoonCore,
                    0f, 28, 0.82f, 0.62f);
                HandpaintFx.PlayAt(center, layer, HandpaintFx.Smoke1Blue,
                    new Vector2(1.46f, 1.12f), MoonInk, MoonCore,
                    0f, order + 10 - VfxSpace.OverlaySortingOrder, 0.60f, 0.38f);
            }
            CompositeArt dome = NewComposite("ChandraMoonSanctuary", VfxArt.ChandraMoonSanctuaryDome,
                casterPoint, layer, order + 1, domeScale * 0.12f, 0f, 0f);
            CompositeArt sealRing = NewComposite("ChandraSanctuarySeal", VfxArt.ChandraMoonWaveRing,
                casterPoint, layer, order + 4, domeScale * 0.10f, 0f, 0f);
            bool sealedOnce = false;
            sealDuration = VfxTiming.AtLeastFrames(sealDuration, 6);
            for (float age = 0f; age < sealDuration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / sealDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float closure = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.72f, t));
                dome.Root.position = Vector3.Lerp(casterPoint, center, eased);
                dome.Root.localScale = Vector3.one * (VfxSpace.CardSize * domeScale *
                                                       Mathf.Lerp(0.12f, 1f, eased));
                SetCompositeAlpha(dome, (ornate ? 0.80f : 0.72f) * closure,
                    (ornate ? 0.57f : 0.48f) * closure);
                sealRing.Root.position = Vector3.Lerp(casterPoint, center, eased);
                sealRing.Root.localScale = Vector3.one * (VfxSpace.CardSize * domeScale *
                                                           Mathf.Lerp(0.10f, 0.86f, eased));
                sealRing.Root.localRotation = Quaternion.Euler(0f, 0f, -40f * eased);
                SetCompositeAlpha(sealRing, (ornate ? 0.56f : 0.44f) * closure,
                    (ornate ? 0.42f : 0.30f) * closure);

                if (!sealedOnce && t >= 0.70f)
                {
                    sealedOnce = true;
                    if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
                        HandpaintFx.PlayAt(center, layer, HandpaintFx.Impact1White,
                            new Vector2(1.92f, 1.42f), MoonSilver, MoonCore,
                            ornate ? 12f : 0f, 24, 0.72f, 0.62f);
                    onSeal?.Invoke();
                    if (!ornate) ReactSanctuaryAllies(allies, buffTarget, false);
                    BattlefieldBackdrop.PulseFlash(MoonSilver, ornate ? 0.070f : 0.052f, 0.09f);
                    CameraShake.Shake(ornate ? 0.038f : 0.022f);
                }
                yield return null;
            }
            if (!sealedOnce)
            {
                onSeal?.Invoke();
                if (!ornate) ReactSanctuaryAllies(allies, buffTarget, false);
            }

            settleDuration = VfxTiming.AtLeastFrames(settleDuration, 5);
            for (float age = 0f; age < settleDuration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / settleDuration);
                float breathe = 1f + Mathf.Sin(t * Mathf.PI) * (ornate ? 0.035f : 0.020f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, t));
                dome.Root.position = center;
                dome.Root.localScale = Vector3.one * (VfxSpace.CardSize * domeScale * breathe);
                SetCompositeAlpha(dome, (ornate ? 0.80f : 0.72f) * fade,
                    (ornate ? 0.57f : 0.48f) * fade);
                sealRing.Root.position = center;
                sealRing.Root.localScale = Vector3.one * (VfxSpace.CardSize * domeScale * 0.86f * breathe);
                sealRing.Root.Rotate(0f, 0f, -12f * Time.deltaTime);
                SetCompositeAlpha(sealRing, (ornate ? 0.56f : 0.44f) * fade,
                    (ornate ? 0.42f : 0.30f) * fade);
                yield return null;
            }
            DestroyComposite(dome);
            DestroyComposite(sealRing);
        }

        private static void ComputeSanctuaryEnvelope(Unit caster, IReadOnlyList<Unit> allies,
            out Vector3 center, out float scale)
        {
            Vector3 fallback = VfxSpace.WorldPoint(caster);
            Vector3 min = fallback;
            Vector3 max = fallback;
            bool found = false;
            if (allies != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    Unit ally = allies[i];
                    if (ally == null || !ally.isActive || !ally.IsOnField) continue;
                    Vector3 point = VfxSpace.WorldPoint(ally);
                    if (!found)
                    {
                        min = max = point;
                        found = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, point);
                        max = Vector3.Max(max, point);
                    }
                }
            }
            center = found ? (min + max) * 0.5f : fallback;
            center += Vector3.up * VfxSpace.CardSize * 0.03f;
            float spanX = Mathf.Abs(max.x - min.x) + VfxSpace.CardSize * 1.40f;
            float spanY = Mathf.Abs(max.y - min.y) + VfxSpace.CardSize * 1.25f;
            scale = Mathf.Clamp(Mathf.Max(spanX, spanY * 1.50f) / VfxSpace.CardSize, 4.8f, 7.2f);
        }

        private static List<ConvergingShard> CreateConvergingShards(Unit caster, Vector3 center,
            float domeScale, int count, int layer, int order)
        {
            var shards = new List<ConvergingShard>(count);
            float radius = VfxSpace.CardSize * domeScale * 0.50f;
            Vector3 casterPoint = VfxSpace.WorldPoint(caster);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + (i % 2) * 0.11f;
                Vector3 start = center + new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.60f, 0f);
                Transform root = new GameObject("ChandraConvergingLight").transform;
                root.position = start;
                bool handpaint = VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra);
                SpriteRenderer body = null;
                SpriteRenderer core = null;
                if (handpaint)
                {
                    FlipbookFx painted = HandpaintFx.PlayAt(start, layer, HandpaintFx.Ball1Blue,
                        new Vector2(0.30f, 0.30f), MoonSilver, MoonCore,
                        angle * Mathf.Rad2Deg, order + i % 3 - VfxSpace.OverlaySortingOrder,
                        0.76f, 0.54f, true);
                    if (painted != null)
                    {
                        UnityEngine.Object.Destroy(root.gameObject);
                        root = painted.FxTransform;
                    }
                }
                else
                {
                    Sprite sprite = i % 3 == 0 ? VfxArt.Crescent : i % 3 == 1 ? VfxArt.Glint : VfxArt.Diamond;
                    body = VfxSpace.NewSprite(root, "AlphaBody", sprite, MoonBlue,
                        layer, order + i % 3, false);
                    body.sharedMaterial = VfxSpace.CompositeAlpha;
                    core = VfxSpace.NewSprite(root, "LuminanceCore", sprite, MoonCore,
                        layer, order + 1 + i % 3, false);
                    core.sharedMaterial = VfxSpace.LuminanceGlow;
                    ApplyCompositeProperties(body, core);
                }
                float size = VfxSpace.CardSize * (0.10f + (i % 4) * 0.018f);
                if (!handpaint) root.localScale = Vector3.one * size;
                shards.Add(new ConvergingShard
                {
                    Root = root,
                    Body = body,
                    Core = core,
                    Start = start,
                    Curl = (i % 2 == 0 ? 1f : -1f) * VfxSpace.CardSize * (0.28f + 0.03f * (i % 5)),
                    Rotation = angle * Mathf.Rad2Deg,
                    BaseScale = size,
                    Handpaint = handpaint,
                });
            }
            return shards;
        }

        private static void AnimateConvergingShards(List<ConvergingShard> shards, Vector3 target,
            float t, bool ornate)
        {
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < shards.Count; i++)
            {
                ConvergingShard shard = shards[i];
                if (shard?.Root == null) continue;
                Vector3 chord = target - shard.Start;
                Vector3 normal = new Vector3(-chord.y, chord.x, 0f).normalized;
                float arc = Mathf.Sin(eased * Mathf.PI) * shard.Curl;
                shard.Root.position = Vector3.Lerp(shard.Start, target, eased) + normal * arc;
                shard.Root.localRotation = Quaternion.Euler(0f, 0f,
                    shard.Rotation + (ornate ? 220f : 150f) * eased);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.16f, t)) *
                              (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, t)));
                float squeeze = Mathf.Lerp(1f, 0.18f, eased);
                shard.Root.localScale = shard.Handpaint
                    ? Vector3.one * Mathf.Max(0.04f, squeeze)
                    : Vector3.one * (shard.BaseScale * squeeze);
                if (!shard.Handpaint)
                {
                    shard.Body.color = new Color(MoonBlue.r, MoonBlue.g, MoonBlue.b,
                        (ornate ? 0.74f : 0.62f) * alpha);
                    shard.Core.color = new Color(1f, 1f, 1f, (ornate ? 0.62f : 0.50f) * alpha);
                }
            }
        }

        private static void ReactSanctuaryAllies(IReadOnlyList<Unit> allies, Unit buffTarget, bool ornate)
        {
            if (allies == null) return;
            for (int i = 0; i < allies.Count; i++)
            {
                Unit ally = allies[i];
                if (ally == null || !ally.isActive) continue;
                ReactAlly(ally, buffTarget, ornate);
            }
        }

        private static void ReactAlly(Unit ally, Unit buffTarget, bool ornate)
        {
            if (ally == null || !ally.isActive) return;
            Vector3 point = VfxSpace.WorldPoint(ally) + Vector3.up * VfxSpace.CardSize * 0.04f;
            int layer = VfxSpace.SortingLayer(ally);
            bool emphasized = ally == buffTarget;
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                HandpaintFx.PlayAt(point, layer, HandpaintFx.Impact1White,
                    new Vector2(emphasized ? 0.62f : 0.50f, emphasized ? 0.62f : 0.50f),
                    MoonSilver, MoonCore, ornate ? 12f : 0f, 26, 0.76f, 0.60f);
                HandpaintFx.PlayAt(point + Vector3.down * VfxSpace.CardSize * 0.10f, layer,
                    HandpaintFx.Smoke5Blue, new Vector2(0.54f, 0.42f),
                    MoonBlue, MoonCore, 0f, 22, 0.56f, 0.34f);
                CardMotion.ViewOf(ally)?.PulseVfxLight(MoonBlue,
                    (emphasized ? 0.08f : 0.055f) + (ornate ? 0.018f : 0f), ornate ? 0.38f : 0.30f);
                return;
            }
            SpriteFx.Play(VfxArt.Crescent, point, MoonInk, false,
                0.10f * VfxSpace.CardSize, (emphasized ? 0.48f : 0.38f) * VfxSpace.CardSize,
                ornate ? 0.38f : 0.30f, -22f, ornate ? 82f : 55f, 0.02f, 0.52f, SpriteFx.Ease.OutBack,
                layer, VfxSpace.OverlaySortingOrder + 1);
            SpriteFx.Play(VfxArt.Glint, point, emphasized ? MoonGold : MoonCore, true,
                0.035f * VfxSpace.CardSize, (emphasized ? 0.24f : 0.18f) * VfxSpace.CardSize,
                ornate ? 0.30f : 0.22f, 0f, ornate ? 70f : 45f, 0f, 0.46f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 3);
            CardMotion.ViewOf(ally)?.PulseVfxLight(MoonBlue,
                (emphasized ? 0.08f : 0.055f) + (ornate ? 0.018f : 0f), ornate ? 0.38f : 0.30f);
        }

        private static void PlayEnemyImpact(Unit target)
        {
            Vector3 point = VfxSpace.WorldPoint(target);
            int layer = VfxSpace.SortingLayer(target);
            float size = VfxSpace.CardSize;
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                VfxRunner.Run(PlayHandpaintEnemyImpact(point, layer));
                VfxRunner.Run(AnimateImpactFragments(point, size * 0.85f, layer));
                CardMotion.ViewOf(target)?.PulseVfxLight(MoonBlue, 0.070f, 0.30f);
                return;
            }
            SpriteFx.Play(VfxArt.Shockwave, point, MoonInk, false,
                new Vector2(size * 0.16f, size * 0.12f), new Vector2(size * 1.26f, size * 0.92f),
                0.42f, -8f, 90f, 0.01f, 0.52f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 2);
            SpriteFx.Play(VfxArt.Shockwave, point, MoonBlue, true,
                new Vector2(size * 0.12f, size * 0.09f), new Vector2(size * 1.02f, size * 0.72f),
                0.34f, 14f, -120f, 0f, 0.46f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 3);
            // 비숍 타격의 톱니 고리·십자 리듬은 월광 눈금 결계로 번역한다.
            SpriteFx.Play(VfxArt.RuneRadiant, point, MoonInk, false,
                size * 0.12f, size * 1.12f, 0.38f, 0f, 145f, 0.01f, 0.54f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 4);
            SpriteFx.Play(VfxArt.RuneRadiant, point, MoonBlue, true,
                size * 0.10f, size * 0.92f, 0.31f, 30f, -175f, 0f, 0.48f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 5);
            SpriteFx.Play(VfxArt.ImpactStar, point, MoonCore, true,
                new Vector2(size * 0.05f, size * 0.10f), new Vector2(size * 1.02f, size * 0.62f),
                0.20f, 0f, 0f, 0f, 0.40f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 6);
            SpriteFx.Play(VfxArt.Glint, point, MoonGold, true,
                new Vector2(size * 0.04f, size * 0.08f), new Vector2(size * 0.86f, size * 0.76f),
                0.24f, 45f, 0f, 0f, 0.44f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 7);
            VfxRunner.Run(AnimateImpactFragments(point, size, layer));
            CardMotion.ViewOf(target)?.PulseVfxLight(MoonBlue, 0.070f, 0.30f);
        }

        private static IEnumerator PlayHandpaintEnemyImpact(Vector3 point, int layer)
        {
            // Muzzle 시트의 긴 좌우 화살촉은 네 대상에 동시에 놓일 때 사각 띠처럼 읽힌다.
            // 손그림 착탄을 주역으로 두고, 창야의 기존 십자·월륜 문법만 작은 실루엣으로 보존한다.
            HandpaintFx.PlayAt(point, layer, HandpaintFx.Explosion2Blue,
                new Vector2(1.18f, 0.98f), MoonBlue, MoonCore,
                0f, 38, 0.72f, 0.56f);
            float size = VfxSpace.CardSize;
            SpriteFx.Play(VfxArt.ImpactStar, point, MoonCore, true,
                new Vector2(size * 0.04f, size * 0.08f), new Vector2(size * 0.74f, size * 0.46f),
                0.18f, 0f, 0f, 0f, 0.38f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 44);
            SpriteFx.Play(VfxArt.RuneRadiant, point, MoonBlue, false,
                size * 0.10f, size * 0.82f, 0.32f, 8f, 132f, 0.01f, 0.48f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 39);
            yield return new WaitForSeconds(0.090f);
        }

        private static IEnumerator AnimateImpactFragments(Vector3 point, float size, int layer)
        {
            const int count = 12;
            var roots = new Transform[count];
            var renderers = new SpriteRenderer[count];
            var directions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) + (i % 2) * 9f;
                Transform root = new GameObject("ChandraImpactFragment").transform;
                root.position = point;
                SpriteRenderer renderer = VfxSpace.NewSprite(root, "Fragment",
                    i % 3 == 0 ? VfxArt.Glint : VfxArt.Diamond,
                    i % 4 == 0 ? MoonGold : MoonSilver, layer,
                    VfxSpace.OverlaySortingOrder + 7 + i % 2, true);
                float fragmentSize = size * (0.045f + 0.010f * (i % 3));
                root.localScale = Vector3.one * fragmentSize;
                root.localRotation = Quaternion.Euler(0f, 0f, angle);
                roots[i] = root;
                renderers[i] = renderer;
                directions[i] = Quaternion.Euler(0f, 0f, angle) * Vector3.right *
                                size * (0.52f + 0.08f * (i % 4));
            }

            float duration = VfxTiming.AtLeastFrames(0.40f, 5);
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 1f, t));
                for (int i = 0; i < count; i++)
                {
                    if (roots[i] == null) continue;
                    roots[i].position = point + directions[i] * eased;
                    roots[i].Rotate(0f, 0f, (i % 2 == 0 ? 180f : -160f) * Time.deltaTime);
                    Color baseColor = i % 4 == 0 ? MoonGold : MoonSilver;
                    renderers[i].color = new Color(baseColor.r, baseColor.g, baseColor.b,
                        baseColor.a * alpha);
                }
                yield return null;
            }
            for (int i = 0; i < count; i++)
                if (roots[i] != null) UnityEngine.Object.Destroy(roots[i].gameObject);
        }

        public static void Moonlight(Unit unit, bool gained)
        {
            if (unit == null) return;
            if (!gained)
            {
                // 실제 ModifyHp→Heal이 이미 공통 지원 피격을 한 번 연다. 월광 소비에서 같은 부품을 중복하지 않는다.
                return;
            }
            if (gained && VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Chandra))
            {
                VfxComposer.PlayAffectedPart(unit, "affected.buff.moon");
                return;
            }
            Vector3 point = VfxSpace.WorldPoint(unit) + Vector3.up * VfxSpace.CardSize * 0.12f;
            float size = VfxSpace.CardSize;
            int layer = VfxSpace.SortingLayer(unit);
            Color color = gained ? MoonBlue : MoonInk;
            SpriteFx.Play(VfxArt.Crescent, point, MoonInk, false,
                size * 0.14f, size * (gained ? 0.52f : 0.72f), 0.28f,
                gained ? -24f : 20f, gained ? 50f : -70f, 0.01f, 0.46f,
                SpriteFx.Ease.OutBack, layer, VfxSpace.OverlaySortingOrder + 2);
            SpriteFx.Play(VfxArt.Crescent, point, color, true,
                size * 0.10f, size * (gained ? 0.40f : 0.60f), 0.23f,
                gained ? -24f : 20f, gained ? 50f : -70f, 0.01f, 0.42f,
                SpriteFx.Ease.OutBack, layer, VfxSpace.OverlaySortingOrder + 3);
        }

        private static void SetCompositeTransform(CompositeArt art, Vector3 point, float scale, float rotation)
        {
            if (art?.Root == null) return;
            art.Root.position = point;
            art.Root.localScale = Vector3.one * (VfxSpace.CardSize * scale);
            art.Root.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static void DestroyComposite(CompositeArt art)
        {
            if (art?.Root != null) UnityEngine.Object.Destroy(art.Root.gameObject);
        }

        private static void DestroyShards(List<ConvergingShard> shards)
        {
            if (shards == null) return;
            foreach (ConvergingShard shard in shards)
                if (shard?.Root != null) UnityEngine.Object.Destroy(shard.Root.gameObject);
        }

        private static CompositeArt NewComposite(string name, Sprite sprite, Vector3 point,
            int layer, int order, float scale, float bodyAlpha, float coreAlpha)
        {
            var root = new GameObject(name).transform;
            root.position = point;
            root.localScale = Vector3.one * (VfxSpace.CardSize * scale);
            SpriteRenderer body = VfxSpace.NewSprite(root, "AlphaBody", sprite, Color.white,
                layer, order, false);
            body.sharedMaterial = VfxSpace.CompositeAlpha;
            SpriteRenderer core = VfxSpace.NewSprite(root, "LuminanceCore", sprite, Color.white,
                layer, order + 1, false);
            core.sharedMaterial = VfxSpace.LuminanceGlow;
            ApplyCompositeProperties(body, core);
            var art = new CompositeArt
            {
                Root = root,
                Body = body,
                Core = core,
                BaseScale = scale,
                BasePosition = point,
            };
            SetCompositeAlpha(art, bodyAlpha, coreAlpha);
            return art;
        }

        private static IEnumerator AnimateComposite(CompositeArt art, float duration, float startScale,
            float endScale, float bodyAlpha, float coreAlpha, float rotation, float downwardTravel)
        {
            if (art?.Root == null) yield break;
            duration = VfxTiming.AtLeastFrames(duration, 5);
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.20f, t));
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 1f, t));
                float alpha = fadeIn * fadeOut;
                art.Root.localScale = Vector3.one * (VfxSpace.CardSize * art.BaseScale *
                                                     Mathf.Lerp(startScale, endScale, eased));
                art.Root.localRotation = Quaternion.Euler(0f, 0f, rotation * eased);
                float descent = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.90f, t));
                art.Root.position = art.BasePosition + Vector3.up *
                    (VfxSpace.CardSize * downwardTravel * (1f - descent));
                SetCompositeAlpha(art, bodyAlpha * alpha, coreAlpha * alpha);
                yield return null;
            }
            UnityEngine.Object.Destroy(art.Root.gameObject);
        }

        private static void ApplyCompositeProperties(SpriteRenderer body, SpriteRenderer core)
        {
            if (body == null || body.sprite == null || core == null) return;
            Rect rect = body.sprite.textureRect;
            Texture2D texture = body.sprite.texture;
            Vector4 uv = new(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height);
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetFloat(BackdropLuminance, BattlefieldBackdrop.CurrentBackgroundLuminance);
            block.SetColor(PaletteLift, new Color(0.80f, 0.92f, 1f, 1f));
            block.SetFloat(LiftStrength, 0.30f);
            block.SetFloat(EdgeSoftness, 0.038f);
            block.SetVector(UvRect, uv);
            body.SetPropertyBlock(block);
            block.Clear();
            core.GetPropertyBlock(block);
            block.SetFloat(GlowThreshold, 0.76f);
            block.SetFloat(GlowSoftness, 0.13f);
            block.SetFloat(GlowGain, 1.12f);
            block.SetFloat(EdgeSoftness, 0.032f);
            block.SetVector(UvRect, uv);
            core.SetPropertyBlock(block);
        }

        private static void SetCompositeAlpha(CompositeArt art, float bodyAlpha, float coreAlpha)
        {
            if (art == null) return;
            float bright = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.08f, 0.65f, BattlefieldBackdrop.CurrentBackgroundLuminance));
            if (art.Body != null)
            {
                // 어두운 판에서 생성 원화의 보조 금색이 회갈색으로 눌리지 않게 월광 청색을 더 섞는다.
                Color bodyColor = Color.Lerp(new Color(0.70f, 0.88f, 1f), MoonSilver, bright);
                art.Body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b,
                    Mathf.Clamp01(bodyAlpha * Mathf.Lerp(0.94f, 1f, bright)));
            }
            if (art.Core != null)
                art.Core.color = new Color(MoonCore.r, MoonCore.g, MoonCore.b,
                    Mathf.Clamp01(coreAlpha * Mathf.Lerp(0.82f, 1f, bright)));
        }
    }
}
