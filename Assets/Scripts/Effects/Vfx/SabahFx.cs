using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Effects.Vfx.Presets;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 사바흐 전용 그림자 이동과 아즈라엘 영혼베기.
    /// 생성 원화는 불투명 몸체와 밝은 칼심을 한 장에 담고, 런타임에서 알파 몸체·휘도 코어로 분리 합성한다.
    /// </summary>
    public static class SabahFx
    {
        private static readonly Color ShadowBody = new(0.025f, 0.008f, 0.045f, 0.94f);
        private static readonly Color VioletEdge = new(0.60f, 0.09f, 0.92f, 0.94f);
        private static readonly Color VioletCore = new(0.90f, 0.68f, 1f, 0.96f);
        private static readonly int BackdropLuminance = Shader.PropertyToID("_BackdropLuminance");
        private static readonly int PaletteLift = Shader.PropertyToID("_PaletteLift");
        private static readonly int LiftStrength = Shader.PropertyToID("_LiftStrength");
        private static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int GlowThreshold = Shader.PropertyToID("_GlowThreshold");
        private static readonly int GlowSoftness = Shader.PropertyToID("_GlowSoftness");
        private static readonly int GlowGain = Shader.PropertyToID("_GlowGain");
        private static readonly int UvRect = Shader.PropertyToID("_UvRect");

        private sealed class Composite
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
            public Vector3 BaseScale;
        }

        private sealed class CardSilhouette
        {
            public SpriteRenderer Source;
            public SpriteRenderer Renderer;
        }

        /// <summary>화풍 설정에 따라 제자리 손그림 검격 또는 보관된 그림자 강습을 재생한다.</summary>
        public static IEnumerator ShadowAmbush(Unit caster, Unit target, Action onImpact)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sabah))
            {
                yield return HandpaintLightningSlash(caster, target, onImpact);
                yield break;
            }
            yield return Presets.ShadowAmbushFx.Play(caster, target, onImpact);
        }

        /// <summary>되돌릴 수 있게 프리셋으로 보관한 기존 그림자 이동·할퀴기 구현.</summary>
        public static IEnumerator LegacyShadowAmbush(Unit caster, Unit target, Action onImpact)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 origin = VfxSpace.WorldPoint(caster);
            Vector3 destination = VfxSpace.WorldPoint(target);
            int layer = VfxSpace.SortingLayer(target);
            BattlefieldBackdrop.PushFocus(0.54f, 0.05f, 0.48f, 0.22f,
                new Color(0.055f, 0.012f, 0.10f), origin, destination);
            PlayPortraitErosion(caster, 0.25f);
            PlayMelt(origin, VfxSpace.SortingLayer(caster), 0.42f);
            caster.StartCoroutine(TravelPool(origin, destination, layer, 0.56f));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("sabah_normal_shadow_depart");
#endif
            yield return CardMotion.ShadowApproach(caster, target);
            PlayMelt(destination, layer, 0.34f);

            var style = new ClawRakeStyle
            {
                Sprite = VfxArt.ShiCrimsonClawStroke,
                LineCount = 3,
                LengthInCards = 1.72f,
                ThicknessInCards = 0.155f,
                AngleDegrees = 31f,
                DrawDuration = 0.20f,
                CenterCrossFraction = 0.50f,
                LineDelay = 0.020f,
                LineSpacingInCards = 0.18f,
                LingerDuration = 0.18f,
                FadeDuration = 0.22f,
                RevealSoftness = 0.040f,
                TipWidth = 0.072f,
                CardKickStrength = 0.56f,
                OffsetInCards = new Vector2(0f, 0.08f),
                BodyColor = ShadowBody,
                EdgeColor = VioletEdge,
                CoreColor = VioletCore,
            };

            yield return ClawRakeFx.Play(target, destination, layer, style, () =>
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Core.VerifyEvents.Raise("sabah_normal_hit");
#endif
                BattlefieldBackdrop.PulseFlash(new Color(0.52f, 0.12f, 0.86f), 0.10f, 0.08f);
                CameraShake.ShakeDirectional(destination - origin, 0.075f);
                PlayShadowCrack(destination, layer, 31f);
                onImpact?.Invoke();
            });

            caster.StartCoroutine(TravelPool(destination, origin, VfxSpace.SortingLayer(caster), 0.43f));
            yield return CardMotion.ShadowReturn(caster);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("sabah_normal_reform");
#endif
        }

        private static IEnumerator HandpaintLightningSlash(Unit caster, Unit target, Action onImpact)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 origin = VfxSpace.WorldPoint(caster);
            Vector3 destination = VfxSpace.WorldPoint(target);
            VfxComposer.PlayScreen("sabah.lightning_slash", caster, target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("sabah_normal_cast_slash");
#endif
            VfxComposer.PlaySlot("sabah.lightning_slash", "cast", 0, caster, target,
                origin, VfxSpace.SortingLayer(caster));

            // 1× 하네스의 실제 시간으로 시·사바흐 기준 박자에 맞춘다.
            yield return new WaitForSeconds(VfxComposer.Offset("sabah.lightning_slash", "hit", 0, 0.224f));
            yield return VfxComposer.PlayHitSlot("sabah.lightning_slash", 0, caster, target, () =>
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Core.VerifyEvents.Raise("sabah_normal_hit");
#endif
                    BattlefieldBackdrop.PulseFlash(new Color(0.55f, 0.18f, 0.88f), 0.10f, 0.08f);
                    CameraShake.ShakeDirectional(destination - origin, 0.075f);
                    onImpact?.Invoke();
                });
        }

        /// <summary>
        /// 번개 베기용 한 획. 시전자 카드에서 한 번만 발도하고 최대 세 대상 위에 피격만 짧게 이어 연다.
        /// 대상 사이를 잇는 칼날·관통선은 만들지 않는다.
        /// </summary>
        public static IEnumerator LightningSweep(Unit caster, IReadOnlyList<Unit> targets,
            Action<Unit> onImpact)
        {
            if (caster == null || targets == null || targets.Count == 0) yield break;
            List<Unit> alive = targets.Where(target => target != null && target.isActive).ToList();
            if (alive.Count == 0) yield break;

            int layer = VfxSpace.SortingLayer(caster);
            VfxComposer.PlayScreen("sabah.lightning_sweep", caster, alive[0]);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("sabah_normal_cast_slash");
#endif
            VfxComposer.PlaySlot("sabah.lightning_sweep", "cast", 0, caster, alive[0],
                VfxSpace.WorldPoint(caster), layer);

            // 첫 대상의 피격 정점은 승인된 박자를 유지하고, 세 대상은 따-라-락으로 읽히게 벌린다.
            yield return new WaitForSeconds(VfxComposer.Offset("sabah.lightning_sweep", "hit", 0, 0.186f));
            IReadOnlyList<VfxTargetHit> hits = VfxSpace.BuildTargetHitSequence(alive, 0.09f);
            int pending = 0;
            yield return VfxSpace.ResolveTargetHits(hits, hit =>
            {
                pending++;
                VfxRunner.Run(LightningSweepHit(caster, hit.Target, hit.Index, () =>
                {
                    onImpact?.Invoke(hit.Target);
                    pending--;
                }));
            });
            float guard = 1.3f;
            while (pending > 0 && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        private static IEnumerator LightningSweepHit(Unit caster, Unit target, int index, Action onPeak)
        {
            if (target == null || !target.isActive)
            {
                onPeak?.Invoke();
                yield break;
            }
            VfxAim aim = VfxSpace.AimFrom(caster, target);
            yield return VfxComposer.PlayHitSlot("sabah.lightning_sweep", 0, caster, target, () =>
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Core.VerifyEvents.Raise("sabah_normal_hit");
#endif
                    CardMotion.RakeReaction(target, aim.Direction, 0.50f);
                    CameraShake.ShakeDirectional(aim.Direction, 0.065f);
                    onPeak?.Invoke();
                }, 1f + index * 0.02f);
        }

        /// <summary>화려한 발도 한 번 뒤 진영 중앙 벼락과 좌우로 퍼지는 연기로 적 전체를 한 번씩 판정한다.</summary>
        public static IEnumerator Azrael(Unit caster, IReadOnlyList<Unit> targets, Action<Unit> onHit)
        {
            if (caster == null || targets == null || targets.Count == 0) yield break;

            List<Unit> alive = targets.Where(target => target != null && target.isActive).ToList();
            if (alive.Count == 0) yield break;
            Vector3 casterPoint = VfxSpace.WorldPoint(caster);
            int layer = VfxSpace.SortingLayer(caster);
            VfxComposer.PlayScreen("sabah.azrael", caster, alive[0]);
            caster.StartCoroutine(CardMotion.Channel(caster, 0.12f, 0.46f, 0.20f));

            // prepare와 수동 잔상을 겹치던 경로를 없애 실제 휘두름은 이 한 장만 재생한다.
            VfxComposer.PlaySlot("sabah.azrael", "cast", 0, caster, alive[0],
                casterPoint, layer);

            AudioManager.PlayAction(caster, SfxActionSlot.Ultimate, SfxCuePhase.Screen, "아즈라엘");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("sabah_ult_blackout");
#endif
            BattlefieldBackdrop.PulseFlash(new Color(0.66f, 0.24f, 0.92f), 0.20f, 0.065f);
            // 단일 발도 뒤 첫 피격 정점을 승인된 실제 시간 구간에 둔다.
            yield return new WaitForSeconds(VfxComposer.Offset("sabah.azrael", "hit", 0, 0.206f));
            Vector3 center = Vector3.zero;
            foreach (Unit target in alive) center += VfxSpace.PortraitPoint(target);
            center /= alive.Count;

            // 벼락은 연기가 열릴 자리를 한 번 찍을 뿐이며 피해는 다음 수평 연기가 닿을 때만 발생한다.
            Unit lightningTarget=alive.OrderBy(unit=>(VfxSpace.PortraitPoint(unit)-center).sqrMagnitude).First();
            // 주 착점은 한 카드, 작은 잔전류는 나머지 수신자에도 남겨 광역 의미를 잇는다.
            foreach(Unit receiver in alive.Where(u=>u!=lightningTarget))
                VfxRunner.Run(VfxComposer.PlayPointHitSlot("sabah.azrael",0,caster,receiver,
                    VfxSpace.PortraitPoint(receiver),null,.68f));
            yield return VfxComposer.PlayPointHitSlot("sabah.azrael", 0, caster, lightningTarget,
                VfxSpace.PortraitPoint(lightningTarget),()=>
                {
                    foreach(Unit receiver in alive)
                        SpriteFx.Play(VfxArt.Shockwave,VfxSpace.PortraitPoint(receiver),new Color(.62f,.43f,.79f,.62f),false,
                            .20f*VfxSpace.CardSize,.40f*VfxSpace.CardSize,.68f,fadeOutFrom:.85f,
                            sortingLayer:VfxSpace.SortingLayer(receiver));
                });
            yield return VfxComposer.PlayHorizontalSweepHit("sabah.azrael", 1, caster, alive[0],
                center, alive, target =>
            {
                if (target == null || !target.isActive) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Core.VerifyEvents.Raise("sabah_ult_hit");
#endif
                VfxAim aim = VfxSpace.AimFrom(caster, target);
                CardMotion.RakeReaction(target, aim.Direction, 0.52f);
                CameraShake.ShakeDirectional(aim.Direction, 0.07f);
                onHit?.Invoke(target);
            });
            AudioManager.PlayAction(caster, SfxActionSlot.Ultimate, SfxCuePhase.Resolve, "아즈라엘");
        }

        private static IEnumerator TravelPool(Vector3 from, Vector3 to, int layer, float duration)
        {
            Composite pool = NewComposite("SabahShadowPool", VfxArt.SabahShadowPuddle, from, layer,
                VfxSpace.OverlaySortingOrder + 18,
                new Vector3(VfxSpace.CardSize * 1.18f, VfxSpace.CardSize * 0.40f, 1f));
            if (pool == null) yield break;
            duration = VfxTiming.AtLeastFrames(duration, 6);
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float move = Mathf.SmoothStep(0f, 1f, t);
                pool.Root.position = Vector3.Lerp(from, to, move) + Vector3.down * VfxSpace.CardSize * 0.37f;
                float pulse = Mathf.Sin(t * Mathf.PI);
                pool.Root.localScale = Vector3.Scale(pool.BaseScale,
                    new Vector3(Mathf.Lerp(0.72f, 1.08f, pulse), Mathf.Lerp(0.66f, 1f, pulse), 1f));
                SetAlpha(pool, 0.82f * pulse, 0.28f * pulse);
                yield return null;
            }
            UnityEngine.Object.Destroy(pool.Root.gameObject);
        }

        /// <summary>
        /// 원형 스포트 없이 전장을 균일하게 누르고, 사바흐의 그림자 집행자와 대검을 전장 뒤에 세운다.
        /// 절단 전에 형태가 충분히 군림해야 다음 백색 전환이 별개의 박자로 읽힌다.
        /// </summary>
        private static IEnumerator DominionPrelude(Vector3 casterPoint, Vector3 enemyCenter, int layer)
        {
            ScreenGeometry(out Vector3 screenCenter, out float width, out float height);
            SpriteRenderer veil = VfxSpace.NewSprite(null, "AzraelPreludeVeil", VfxSpace.RoundedRect,
                Color.clear, layer, -220);
            veil.sharedMaterial = VfxSpace.Alpha;
            veil.transform.position = screenCenter;
            veil.transform.localScale = new Vector3(width * 1.32f, height * 1.32f, 1f);

            Vector3 dominionPoint = Vector3.Lerp(casterPoint, enemyCenter, 0.42f) + Vector3.up * height * 0.08f;
            Composite dominion = NewComposite("AzraelShadowDominion", VfxArt.SabahAzraelShadowDominion,
                dominionPoint, layer, -110, new Vector3(width * 0.98f, height * 1.58f, 1f));

            const int tendrilCount = 8;
            var tendrils = new SpriteRenderer[tendrilCount];
            var starts = new Vector3[tendrilCount];
            var ends = new Vector3[tendrilCount];
            for (int i = 0; i < tendrilCount; i++)
            {
                bool fromLeft = i % 2 == 0;
                float row = (i / 2 - 1.5f) * height * 0.16f;
                starts[i] = screenCenter + new Vector3((fromLeft ? -0.62f : 0.62f) * width, row, 0f);
                ends[i] = dominionPoint + new Vector3((fromLeft ? -0.10f : 0.10f) * width,
                    row * 0.18f, 0f);
                Vector3 direction = ends[i] - starts[i];
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                tendrils[i] = VfxSpace.NewSprite(null, $"AzraelTendril_{i}", VfxArt.Streak,
                    Color.clear, layer, -105 + i);
                tendrils[i].sharedMaterial = VfxSpace.Alpha;
                tendrils[i].transform.position = starts[i];
                tendrils[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                tendrils[i].transform.localScale = new Vector3(width * (0.22f + i * 0.008f),
                    height * 0.014f, 1f);
            }

            const float duration = 0.82f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.24f, t));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 1f, t));
                float alpha = appear * fade;
                veil.color = new Color(0.020f, 0.004f, 0.038f, 0.76f * appear);
                if (dominion != null)
                {
                    dominion.Root.localScale = Vector3.Scale(dominion.BaseScale,
                        new Vector3(Mathf.Lerp(0.90f, 1.03f, Mathf.SmoothStep(0f, 1f, t)),
                            Mathf.Lerp(0.88f, 1.02f, t), 1f));
                    SetAlpha(dominion, 0.80f * alpha, 0.34f * alpha);
                }

                float gather = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.92f, t));
                for (int i = 0; i < tendrils.Length; i++)
                {
                    SpriteRenderer tendril = tendrils[i];
                    if (tendril == null) continue;
                    tendril.transform.position = Vector3.Lerp(starts[i], ends[i], gather);
                    float pulse = Mathf.Sin(Mathf.Clamp01((t - i * 0.025f) / 0.76f) * Mathf.PI);
                    tendril.color = new Color(0.28f, 0.018f, 0.43f, 0.56f * pulse * fade);
                }
                yield return null;
            }

            UnityEngine.Object.Destroy(veil.gameObject);
            if (dominion != null) UnityEngine.Object.Destroy(dominion.Root.gameObject);
            foreach (SpriteRenderer tendril in tendrils)
                if (tendril != null) UnityEngine.Object.Destroy(tendril.gameObject);
        }

        /// <summary>집속과 절단 장면 사이를 분리하는 1~2프레임 라일락 백색 섬광.</summary>
        private static IEnumerator TransitionFlash(int layer)
        {
            ScreenGeometry(out Vector3 center, out float width, out float height);
            SpriteRenderer flash = VfxSpace.NewSprite(null, "AzraelTransitionFlash", VfxSpace.RoundedRect,
                Color.clear, layer, VfxSpace.OverlaySortingOrder + 220);
            flash.sharedMaterial = VfxSpace.Alpha;
            flash.transform.position = center;
            flash.transform.localScale = new Vector3(width * 1.30f, height * 1.30f, 1f);
            float duration = VfxTiming.AtLeastFrames(0.045f, 2);
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                flash.color = new Color(0.97f, 0.90f, 1f, 1f - Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            UnityEngine.Object.Destroy(flash.gameObject);
        }

        /// <summary>
        /// 전장을 짙은 보라 한 색으로 바꾸고 실제 카드 원화 위에 검은 실루엣 사본을 얹는다.
        /// 굵은 화면 절단 띠는 몸체 알파와 좁은 휘도 코어를 분리해 밝은 판에서도 폭이 먼저 읽힌다.
        /// </summary>
        private static IEnumerator ScreenCleave(Vector3 center, int layer, Action onComplete)
        {
            ScreenGeometry(out Vector3 screenCenter, out float width, out float height);
            SpriteRenderer veil = VfxSpace.NewSprite(null, "AzraelCleaveField", VfxSpace.RoundedRect,
                Color.clear, layer, VfxSpace.OverlaySortingOrder + 50);
            veil.sharedMaterial = VfxSpace.Alpha;
            veil.transform.position = screenCenter;
            veil.transform.localScale = new Vector3(width * 1.32f, height * 1.32f, 1f);

            List<CardSilhouette> silhouettes = CreateCardSilhouettes(layer);
            PlayScreenScans(screenCenter, layer, width, height);
            Composite band = NewComposite("AzraelSoulCleaveBand", VfxArt.SabahAzraelCleaveBand,
                screenCenter + Vector3.down * height * 0.035f, layer,
                VfxSpace.OverlaySortingOrder + 86,
                new Vector3(width * 1.16f, height * 0.66f, 1f));

            const float duration = 1.02f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.10f, t));
                float pink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.50f, 0.82f, t));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));
                Color field = Color.Lerp(new Color(0.15f, 0.008f, 0.27f),
                    new Color(0.44f, 0.075f, 0.34f), pink);
                veil.color = new Color(field.r, field.g, field.b, 0.97f * enter * fade);

                foreach (CardSilhouette silhouette in silhouettes)
                {
                    if (silhouette?.Renderer == null) continue;
                    if (silhouette.Source != null)
                    {
                        silhouette.Renderer.transform.position = silhouette.Source.transform.position;
                        silhouette.Renderer.transform.rotation = silhouette.Source.transform.rotation;
                        silhouette.Renderer.transform.localScale = silhouette.Source.transform.lossyScale;
                    }
                    silhouette.Renderer.color = new Color(0.002f, 0.001f, 0.004f, 0.98f * enter * fade);
                }

                if (band != null)
                {
                    float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.18f, t));
                    float bandFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, t));
                    band.Root.position = screenCenter + new Vector3(Mathf.Lerp(-0.08f, 0.02f, open) * width,
                        -height * 0.035f, 0f);
                    band.Root.localScale = Vector3.Scale(band.BaseScale,
                        new Vector3(Mathf.Lerp(0.16f, 1.03f, open), Mathf.Lerp(0.72f, 1.04f, open), 1f));
                    SetAlpha(band, 0.92f * enter * bandFade, 0.68f * enter * bandFade);
                }
                yield return null;
            }

            UnityEngine.Object.Destroy(veil.gameObject);
            foreach (CardSilhouette silhouette in silhouettes)
                if (silhouette?.Renderer != null) UnityEngine.Object.Destroy(silhouette.Renderer.gameObject);
            if (band != null) UnityEngine.Object.Destroy(band.Root.gameObject);
            onComplete?.Invoke();
        }

        private static IEnumerator TargetCleave(Unit target, int index, float delay, Action onResolved)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (target == null || !target.isActive)
            {
                onResolved?.Invoke();
                yield break;
            }

            Vector3 point = VfxSpace.WorldPoint(target) + Vector3.up * VfxSpace.CardSize * 0.06f;
            int layer = VfxSpace.SortingLayer(target);
            float rotation = index % 2 == 0 ? 1.5f : -1.5f;
            Composite arc = NewComposite("SabahSoulCleaveHit", VfxArt.SabahAzraelCleaveBand, point, layer,
                VfxSpace.OverlaySortingOrder + 90 + index,
                new Vector3(VfxSpace.CardSize * 2.45f, VfxSpace.CardSize * 2.05f, 1f));
            if (arc == null)
            {
                onResolved?.Invoke();
                yield break;
            }
            arc.Root.rotation = Quaternion.Euler(0f, 0f, rotation);

            bool resolved = false;
            const float duration = 0.38f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.28f, t));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t));
                float alpha = appear * fade;
                arc.Root.localScale = Vector3.Scale(arc.BaseScale,
                    new Vector3(Mathf.Lerp(0.74f, 1.04f, Mathf.SmoothStep(0f, 1f, t)),
                        Mathf.Lerp(0.70f, 1f, t), 1f));
                SetAlpha(arc, 0.78f * alpha, 0.58f * alpha);

                // 첫 십자 섬광이 아니라 이 원화의 칼심이 카드 중심을 지나는 단일 정점이다.
                if (!resolved && t >= 0.36f)
                {
                    resolved = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Core.VerifyEvents.Raise("sabah_ult_hit");
#endif
                    CardMotion.RakeReaction(target, Quaternion.Euler(0f, 0f, rotation) * Vector3.right, 0.72f);
                    PlaySoulFracture(point, layer, rotation);
                    BattlefieldBackdrop.PulseFlash(new Color(0.54f, 0.10f, 0.90f), 0.11f, 0.075f);
                    CameraShake.ShakeDirectional(Vector3.right, 0.09f);
                    onResolved?.Invoke();
                }
                yield return null;
            }
            if (!resolved) onResolved?.Invoke();
            UnityEngine.Object.Destroy(arc.Root.gameObject);
        }

        /// <summary>굵은 절단 몸체 주위로만 수평 주사선이 끊겨 지나가게 해 화면 축을 고정한다.</summary>
        private static void PlayScreenScans(Vector3 center, int layer, float width, float height)
        {
            for (int i = 0; i < 7; i++)
            {
                float y = (i - 3) * height * 0.032f;
                bool bright = i == 2 || i == 4;
                SpriteFx.Play(VfxArt.Streak, center + Vector3.up * y,
                    bright ? new Color(0.94f, 0.76f, 1f, 0.72f) : new Color(0.12f, 0.005f, 0.19f, 0.82f),
                    bright,
                    new Vector2(width * 0.10f, height * 0.006f),
                    new Vector2(width * (1.00f + i * 0.025f), height * (bright ? 0.010f : 0.016f)),
                    0.34f + i * 0.022f, 0f, 0f, 0.01f, 0.56f,
                    SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 74 + i);
            }
            SpriteFx.Play(VfxArt.Glint, center,
                new Color(0.94f, 0.72f, 1f, 0.82f), true,
                height * 0.02f, width * 0.18f, 0.22f, 0f, 0f, 0f, 0.28f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 78);
        }

        /// <summary>대상별 칼심 정점에만 생기는 비가산 영혼 조각과 좁은 가산 심광.</summary>
        private static void PlaySoulFracture(Vector3 point, int layer, float rotation)
        {
            float size = VfxSpace.CardSize;
            SpriteFx.Play(VfxArt.Glint, point,
                new Color(0.96f, 0.78f, 1f, 0.92f), true,
                new Vector2(size * 0.08f, size * 0.06f),
                new Vector2(size * 0.88f, size * 0.28f),
                0.24f, rotation, 0f, 0f, 0.30f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 106);
            VfxParticles.Burst(point, new ParticleSpec
            {
                Sprite = VfxArt.Crescent,
                Additive = false,
                StartColor = new Color(0.27f, 0.018f, 0.43f, 0.82f),
                EndColor = new Color(0.035f, 0.004f, 0.065f, 0f),
                Lifetime = new Vector2(0.28f, 0.48f),
                Speed = new Vector2(size * 0.42f, size * 1.05f),
                Angle = rotation,
                Spread = 132f,
                StartSize = new Vector2(size * 0.024f, size * 0.070f),
                EndSizeScale = 0.28f,
                Gravity = size * 0.12f,
                Drag = 1.1f,
                Spin = new Vector2(130f, 360f),
                SpawnRadius = size * 0.14f,
                FadeIn = 0.06f,
            }, 9, layer, VfxSpace.OverlaySortingOrder + 102);
        }

        /// <summary>일반행동 정점 뒤에도 검은 균열과 보라 파편이 남아 밝은 5인 전장에서 타격 위치를 붙든다.</summary>
        private static void PlayShadowCrack(Vector3 point, int layer, float rotation)
        {
            float size = VfxSpace.CardSize;
            SpriteFx.Play(VfxArt.Streak, point,
                new Color(0.30f, 0.018f, 0.46f, 0.82f), false,
                new Vector2(size * 0.22f, size * 0.030f),
                new Vector2(size * 1.08f, size * 0.075f),
                0.24f, rotation, 0f, 0f, 0.34f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 44);
            VfxParticles.Burst(point, new ParticleSpec
            {
                Sprite = VfxArt.Crescent,
                Additive = false,
                StartColor = new Color(0.42f, 0.045f, 0.68f, 0.82f),
                EndColor = new Color(0.045f, 0.004f, 0.072f, 0f),
                Lifetime = new Vector2(0.18f, 0.32f),
                Speed = new Vector2(size * 0.22f, size * 0.64f),
                Angle = rotation,
                Spread = 104f,
                StartSize = new Vector2(size * 0.016f, size * 0.050f),
                EndSizeScale = 0.24f,
                Drag = 1.2f,
                Spin = new Vector2(90f, 260f),
                SpawnRadius = size * 0.10f,
                FadeIn = 0.04f,
            }, 11, layer, VfxSpace.OverlaySortingOrder + 45);
        }

        private static List<CardSilhouette> CreateCardSilhouettes(int fallbackLayer)
        {
            var result = new List<CardSilhouette>();
            GridManager grid = GridManager.Instance;
            if (grid == null) return result;

            IEnumerable<Unit> heroes = grid.heroList ?? Enumerable.Empty<Unit>();
            IEnumerable<Unit> enemies = grid.enemyList ?? Enumerable.Empty<Unit>();
            foreach (Unit unit in heroes.Concat(enemies))
            {
                if (unit == null || !unit.isActive || !unit.IsOnField) continue;
                Entities.View.UnitCardView view = CardMotion.ViewOf(unit);
                SpriteRenderer source = view?.PortraitRenderer;
                if (source == null || source.sprite == null) continue;

                SpriteRenderer silhouette = VfxSpace.NewSprite(null, $"AzraelCardSilhouette_{result.Count}",
                    source.sprite, Color.clear,
                    source.sortingLayerID != 0 ? source.sortingLayerID : fallbackLayer,
                    VfxSpace.OverlaySortingOrder + 62 + result.Count);
                silhouette.sharedMaterial = VfxSpace.Alpha;
                silhouette.flipX = source.flipX;
                silhouette.flipY = source.flipY;
                silhouette.transform.position = source.transform.position;
                silhouette.transform.rotation = source.transform.rotation;
                silhouette.transform.localScale = source.transform.lossyScale;
                result.Add(new CardSilhouette { Source = source, Renderer = silhouette });
            }
            return result;
        }

        private static void ScreenGeometry(out Vector3 center, out float width, out float height)
        {
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                height = camera.orthographicSize * 2f;
                width = height * camera.aspect;
                center = camera.transform.position;
                center.z = 0f;
                return;
            }

            width = VfxSpace.CardSize * 7.2f;
            height = width * 0.5625f;
            center = Vector3.zero;
        }

        private static void PlayMelt(Vector3 point, int layer, float scale)
        {
            SpriteFx.Play(VfxArt.SabahShadowPuddle,
                point + Vector3.down * VfxSpace.CardSize * 0.36f,
                new Color(0.30f, 0.035f, 0.52f, 0.68f), false,
                new Vector2(VfxSpace.CardSize * 0.26f, VfxSpace.CardSize * 0.08f),
                new Vector2(VfxSpace.CardSize * 1.12f * scale, VfxSpace.CardSize * 0.36f * scale),
                0.30f, 0f, 0f, 0f, 0.34f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 14);
        }

        /// <summary>
        /// 실제 카드가 아래로 찌그러지며 사라지는 동안 같은 원화를 한 장 복제해 아래 방향 침식 경계를 보인다.
        /// 알파 페이드만 쓰면 밝은 판에서 카드가 순간이동한 것처럼 보여, 침식은 오직 소멸의 물성을 설명한다.
        /// </summary>
        private static void PlayPortraitErosion(Unit caster, float dissolve)
        {
            Entities.View.UnitCardView view = CardMotion.ViewOf(caster);
            Sprite sprite = view != null ? view.PortraitSprite : null;
            if (sprite == null) return;

            Vector3 point = VfxSpace.WorldPoint(caster);
            int layer = VfxSpace.SortingLayer(caster);
            var root = new GameObject("SabahPortraitErosion").transform;
            root.position = point;
            float spriteHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
            root.localScale = Vector3.one * (VfxSpace.CardSize * 1.12f / spriteHeight);
            SpriteRenderer ghost = VfxSpace.NewSprite(root, "Portrait", sprite,
                new Color(0.38f, 0.14f, 0.48f, 0.74f), layer, VfxSpace.FlyingCardSortingOrder + 2);
            ErosionFx.Attach(ghost, 0f, 0f, 0.015f, dissolve,
                new Color(0.98f, 0.80f, 1f), VioletCore, ShadowBody,
                0.18f, new Vector2(0f, -1.75f), true);
        }

        private static Composite NewComposite(string name, Sprite sprite, Vector3 point, int layer,
            int order, Vector3 scale)
        {
            if (sprite == null) return null;
            var root = new GameObject(name).transform;
            root.position = point;
            root.localScale = scale;
            SpriteRenderer body = VfxSpace.NewSprite(root, "AlphaBody", sprite, Color.clear, layer, order);
            body.sharedMaterial = VfxSpace.CompositeAlpha;
            SpriteRenderer core = VfxSpace.NewSprite(root, "LuminanceCore", sprite, Color.clear, layer, order + 1);
            core.sharedMaterial = VfxSpace.LuminanceGlow;
            ApplyCompositeProperties(body, core);
            return new Composite { Root = root, Body = body, Core = core, BaseScale = scale };
        }

        private static void ApplyCompositeProperties(SpriteRenderer body, SpriteRenderer core)
        {
            Rect rect = body.sprite.textureRect;
            Texture2D texture = body.sprite.texture;
            Vector4 uv = new(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height);
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetFloat(BackdropLuminance, BattlefieldBackdrop.CurrentBackgroundLuminance);
            block.SetColor(PaletteLift, new Color(0.60f, 0.22f, 0.88f, 1f));
            block.SetFloat(LiftStrength, 0.28f);
            block.SetFloat(EdgeSoftness, 0.032f);
            block.SetVector(UvRect, uv);
            body.SetPropertyBlock(block);
            block.Clear();
            core.GetPropertyBlock(block);
            block.SetFloat(GlowThreshold, 0.72f);
            block.SetFloat(GlowSoftness, 0.12f);
            block.SetFloat(GlowGain, 1.16f);
            block.SetFloat(EdgeSoftness, 0.028f);
            block.SetVector(UvRect, uv);
            core.SetPropertyBlock(block);
        }

        private static void SetAlpha(Composite art, float bodyAlpha, float coreAlpha)
        {
            float bright = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.08f, 0.65f, BattlefieldBackdrop.CurrentBackgroundLuminance));
            art.Body.color = new Color(0.68f, 0.22f, 0.92f,
                Mathf.Clamp01(bodyAlpha * Mathf.Lerp(0.90f, 1f, bright)));
            art.Core.color = new Color(VioletCore.r, VioletCore.g, VioletCore.b,
                Mathf.Clamp01(coreAlpha * Mathf.Lerp(0.76f, 1f, bright)));
        }
    }
}
