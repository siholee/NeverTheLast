using Random = Effects.Vfx.PresentationRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Effects.Vfx.Presets;
using Entities;
using Managers;
using UnityEngine;


namespace Effects.Vfx
{
    /// <summary>
    /// 바위(Geo) 원소 연출 — 세이의 코드가 쓴다.
    ///
    /// 세이의 세 핵심 스킬은 <see cref="SkillFx"/> 4×4 플립북을 주인공으로 쓰고,
    /// Hovl은 결정 획득·보호막·집중 해제처럼 작은 단발 보조에만 남긴다.
    /// 돌은 알파(무게 · 먼지), 좁은 금빛 코어만 가산으로 나눠 그린다. 크기는 전부 카드 한 변 대비 비율이다.
    /// </summary>
    public static class GeoFx
    {
        private static float S => VfxSpace.CardSize;
        private static int Order => VfxSpace.OverlaySortingOrder + 20;
        private const int RearOrder = -100;
        private static readonly int BackdropLuminance = Shader.PropertyToID("_BackdropLuminance");
        private static readonly int PaletteLift = Shader.PropertyToID("_PaletteLift");
        private static readonly int LiftStrength = Shader.PropertyToID("_LiftStrength");
        private static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int UvRect = Shader.PropertyToID("_UvRect");
        private static readonly int GlowThreshold = Shader.PropertyToID("_GlowThreshold");
        private static readonly int GlowSoftness = Shader.PropertyToID("_GlowSoftness");
        private static readonly int GlowGain = Shader.PropertyToID("_GlowGain");

        public static Color Gold => ElementPalette.ColorFor(BaseEnums.UnitElement.Geo);
        private static readonly Color GoldBright = new(1f, 0.88f, 0.56f, 1f);
        private static readonly Color HandpaintGoldBody = new(1f, 0.96f, 0.82f, 0.98f);
        private static readonly Color EarthDeep = new(0.439f, 0.275f, 0.180f, 1f);
        private static readonly Color EarthOchre = new(0.608f, 0.376f, 0.220f, 1f);
        private static readonly Color EarthSand = new(0.773f, 0.541f, 0.325f, 1f);
        private static readonly Color DustEdge = new(0.914f, 0.847f, 0.741f, 1f);
        private static readonly Color ContactShadow = new(0.29f, 0.40f, 0.48f, 1f);

        private static FlipbookCompositeStyle VolleyImpactComposite() => new()
        {
            PeakAlpha = 0.76f,
            FadeInFraction = 0.08f,
            FadeOutFraction = 0.34f,
            GlowAlpha = 0.62f,
            GlowThreshold = 0.72f,
            GlowSoftness = 0.14f,
            StartScale = 0.72f,
            CrestScale = 1.04f,
            EndScale = 1.12f,
            GroundLightSizeInCards = new Vector2(0.38f, 0.10f),
            ContactShadowSizeInCards = new Vector2(0.31f, 0.065f),
            GroundOffsetInCards = -0.12f,
            SceneLightStrength = 0.055f,
        };

        private static FlipbookCompositeStyle GenesisComposite(bool large) => new()
        {
            PeakAlpha = large ? 0.66f : 0.70f,
            FadeInFraction = 0.18f,
            FadeOutFraction = 0.30f,
            GlowAlpha = large ? 0.56f : 0.62f,
            GlowThreshold = 0.75f,
            GlowSoftness = 0.14f,
            StartScale = 0.82f,
            CrestScale = 1.015f,
            EndScale = 1.065f,
            RotationDegrees = large ? 3.2f : 1.4f,
            GroundLightSizeInCards = large ? new Vector2(1.52f, 0.30f) : new Vector2(0.76f, 0.16f),
            ContactShadowSizeInCards = large ? new Vector2(1.28f, 0.21f) : new Vector2(0.66f, 0.12f),
            GroundOffsetInCards = -0.34f,
            PeakResourcePath = "VfxTex/Maple/Sei/GenesisCrestPeak_v2",
            PeakStart = 0.28f,
            PeakHoldEnd = 0.67f,
            PeakEnd = 0.84f,
            PeakLayerAlpha = large ? 0.62f : 0.54f,
            PeakLayerScale = large ? 0.94f : 0.88f,
            SceneLightStrength = large ? 0.16f : 0.10f,
        };

        private static readonly SkillFxCue VolleyImpactCue = new()
        {
            ResourcePath = "VfxTex/Maple/Sei/RadiantVolleyImpact_4x4",
            FrameDelays = SkillFx.Delays(0.018f, 0.022f, 0.028f, 0.032f),
            ImpactFrame = 4,
            Pivot = new Vector2(0.5f, 0.5f),
            SizeInCards = new Vector2(0.56f, 0.56f),
            OrderOffset = 16,
            Composite = VolleyImpactComposite(),
        };

        // 몸통·양 어깨·옆구리·다리 쪽을 나눈다. 작은 무작위 흔들림을 더해도 서로 겹치지 않는 간격이다.
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

        private static readonly SkillFxPlan GenesisBlessingPlan = new()
        {
            Hit = new SkillFxCue
            {
                ResourcePath = "VfxTex/Maple/Sei/GenesisCrest_4x4",
                FrameDelays = SkillFx.Delays(0.032f, 0.044f, 0.055f, 0.048f),
                ImpactFrame = 7,
                SizeInCards = new Vector2(1.06f, 1.06f),
                OffsetInCards = new Vector2(0f, -0.02f),
                BehindPortrait = true,
                OrderOffset = 5,
                Composite = GenesisComposite(false),
            },
            HitSfx = "sei.genesis.hit",
        };

        private static readonly SkillFxCue GenesisCastCue = new()
        {
            ResourcePath = "VfxTex/Maple/Sei/GenesisCrest_4x4",
            FrameDelays = SkillFx.Delays(0.040f, 0.050f, 0.072f, 0.060f),
            ImpactFrame = 7,
            SizeInCards = new Vector2(2.72f, 2.72f),
            OffsetInCards = new Vector2(0f, 0.05f),
            OrderOffset = 2,
            Composite = GenesisComposite(true),
        };

        private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        private sealed class CompositeArt
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
        }

        // ══════════════════════════════════════════════════════
        // 전방 연발 — 세이 일반행동
        // ══════════════════════════════════════════════════════

        /// <summary>한 번의 결정 시전 뒤 대상 카드 안 여섯 지점에서 파쇄 리듬을 연다.</summary>
        public static IEnumerator RadiantVolley(Unit caster, Unit target, Action onHit, float scale = 1f)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sei))
            {
                yield return VfxComposer.PlaySingleTarget("sei.ground", caster, target, onHit, scale);
                yield break;
            }

            var style = new RadiantVolleyStyle
            {
                Bolt = VfxArt.SeiRadiantVolleyBolt,
                ImpactResourcePath = "VfxTex/Maple/Sei/RadiantVolleyImpact_4x4",
                ProjectileCue = "sei.ground.projectile",
                FocusTint = new Color(0.20f, 0.32f, 0.44f, 1f),
                CrestBody = new Color(0.82f, 0.55f, 0.22f, 0.74f),
                CrestCore = new Color(1f, 0.88f, 0.54f, 0.70f),
                CoreColor = GoldBright,
                PaletteLift = new Color(1f, 0.91f, 0.72f, 1f),
                CardLight = GoldBright,
                GroundLight = GoldBright,
            };
            yield return RadiantVolleyFx.Play(caster, target, onHit, style, scale);
        }

        /// <summary>
        /// 발사된 암편을 대상에게 곧게 날린다. 짧은 주황빛 꼬리 · 흙먼지 띠를 달고 부스러기를 흘린다.
        /// 착탄점은 비행 내내 대상 카드를 따라간다.
        /// </summary>
        private static IEnumerator FlyRock(Transform root, SpriteRenderer rock, Unit target, float scale, int layer,
            float duration, float spin, Action<Vector3> arrived, bool directedShard = false, bool compactTrail = false)
        {
            // 판석은 몸체가 주인공이다. 연속 광실 대신 짧은 흙먼지 자국만 세 번 남겨
            // 빠른 비행 중에도 에너지 빔으로 바뀌지 않게 한다.
            TrailRenderer wake = compactTrail ? null
                : VfxArt.NewTrail(root, WithAlpha(EarthOchre, 0.58f), WithAlpha(EarthOchre, 0f),
                    0.07f * S * scale, 0.12f, layer, Order - 1, additive: false, endWidth: 0.12f);

            SpriteRenderer shell = root.Find("AirShell")?.GetComponent<SpriteRenderer>();
            SpriteRenderer shellEdge = root.Find("AirShellEdge")?.GetComponent<SpriteRenderer>();
            if (shell != null)
                shell.color = compactTrail ? new Color(1f, 0.82f, 0.46f, 0.26f) : new Color(1f, 0.88f, 0.58f, 0.34f);
            if (shellEdge != null) shellEdge.color = WithAlpha(EarthDeep, compactTrail ? 0.52f : 0.42f);

            Vector3 start = root.position;
            Vector3 end = target != null ? VfxSpace.WorldPoint(target) : start;
            float spinSpeed = (Random.value < 0.5f ? -1f : 1f) * Random.Range(900f, 1300f);
            Vector3 flightScale = rock.transform.localScale;
            int dustIndex = 0;
            float[] dustMarks = { 0.16f, 0.42f, 0.70f };
            duration = VfxTiming.AtLeastFrames(duration, 3);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                if (target != null && target.isActive) end = VfxSpace.WorldPoint(target);
                Vector3 direction = Direction(start, end);
                Vector3 up = new(-direction.y, direction.x, 0f);
                if (up.y < 0f) up = -up;
                // 초반의 짧은 정지감을 지나 끝에서 급격히 빨라진다.
                float progress = Mathf.Pow(k, 1.58f);
                Vector3 position = Vector3.Lerp(start, end, progress) + up * Mathf.Sin(k * Mathf.PI) * 0.018f * S;
                root.position = position;
                if (directedShard)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    float bank = Mathf.Sin(k * Mathf.PI * 2f) * 1.5f * (1f - k);
                    rock.transform.localRotation = Quaternion.Euler(0f, 0f, angle + bank);
                    float stretch = 1.05f + Mathf.Sin(k * Mathf.PI) * 0.12f;
                    rock.transform.localScale = new Vector3(flightScale.x * stretch, flightScale.y / stretch, flightScale.z);
                    if (shell != null)
                    {
                        float reach = Mathf.Abs(flightScale.x) * 0.54f;
                        shell.transform.localPosition = direction * reach;
                        shell.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                        shell.color = new Color(1f, 0.84f, 0.52f, Mathf.Lerp(0.14f, 0.28f, progress));
                        if (shellEdge != null)
                        {
                            shellEdge.transform.localPosition = direction * reach;
                            shellEdge.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                            shellEdge.color = WithAlpha(EarthDeep, Mathf.Lerp(0.34f, 0.56f, progress));
                        }
                    }
                }
                else
                {
                    spin += spinSpeed * Time.deltaTime;
                    rock.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
                }

                // 세 장이 서로 이어져 한 덩어리의 짧은 흙먼지 꼬리로 읽힌다.
                while (compactTrail && dustIndex < dustMarks.Length && k >= dustMarks[dustIndex])
                {
                    float depth = dustIndex / (float)(dustMarks.Length - 1);
                    float dustProgress = Mathf.Pow(dustMarks[dustIndex], 1.58f);
                    Vector3 markPosition = Vector3.Lerp(start, end, dustProgress)
                                           + up * Mathf.Sin(dustMarks[dustIndex] * Mathf.PI) * 0.018f * S;
                    Vector3 dustPoint = markPosition - direction * Mathf.Lerp(0.11f, 0.20f, depth) * S;
                    Vector3 normalOffset = up * (dustIndex % 2 == 0 ? -1f : 1f) * (0.018f + depth * 0.012f) * S;
                    SpriteFx.Play(VfxArt.DustPuff(dustIndex % 4), dustPoint + normalOffset,
                        new Color(0.66f, 0.43f, 0.25f, Mathf.Lerp(0.52f, 0.30f, depth)), false,
                        new Vector2(Mathf.Lerp(0.08f, 0.11f, depth), Mathf.Lerp(0.04f, 0.06f, depth)) * S * scale,
                        new Vector2(Mathf.Lerp(0.24f, 0.31f, depth), Mathf.Lerp(0.12f, 0.17f, depth)) * S * scale,
                        Mathf.Lerp(0.26f, 0.34f, depth), Random.Range(-6f, 6f), 0f, 0.01f, 0.46f,
                        SpriteFx.Ease.OutCubic, layer, Order - 3 - (dustIndex % 2));
                    dustIndex++;
                }
                yield return null;
            }

            root.position = end;
            VfxArt.ReleaseTrail(wake);
            arrived?.Invoke(end);
        }

        /// <summary>발사 프레임의 압축광. 암편 뒤 근거리에서만 갈라져 레이저가 아니라 투척 속도를 만든다.</summary>
        private static void LaunchStreaks(Vector3 origin, Vector3 direction, float scale, int layer, bool compactWake = false)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 normal = new(-direction.y, direction.x, 0f);
            // 연속 발사는 먼지 없이 짧은 두 선만, 땅고르기는 먼지·작은 파편을 한 묶음으로 쓴다.
            if (!compactWake)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    float offset = i * 0.045f * S * scale;
                    SpriteFx.Play(VfxArt.Streak, origin + normal * offset - direction * 0.10f * S,
                        new Color(1f, 0.78f, 0.40f, 0.42f), true,
                        new Vector2(0.07f, 0.012f) * S * scale,
                        new Vector2(0.42f, 0.008f) * S * scale,
                        0.10f, angle + 180f, 0f, 0.004f, 0.18f,
                        SpriteFx.Ease.OutCubic, layer, Order + i);
                }
                return;
            }

            for (int i = -1; i <= 1; i++)
            {
                float offset = i * 0.055f * S * scale;
                float width = i == 0 ? 0.018f : 0.011f;
                float length = i == 0 ? 0.34f : 0.25f;
                Color color = i == 0
                    ? new Color(0.50f, 0.29f, 0.15f, 0.62f)
                    : new Color(0.66f, 0.39f, 0.19f, 0.48f);
                Vector3 start = origin + normal * offset - direction * (0.12f + (i + 1) * 0.03f) * S;
                SpriteFx.Play(VfxArt.Streak, start, color, false,
                    new Vector2(0.07f, width) * S * scale, new Vector2(length, width * 0.48f) * S * scale,
                    0.10f, angle + 180f, 0f, 0.003f, 0.16f, SpriteFx.Ease.OutCubic, layer, Order + i);
            }

            VfxParticles.Burst(origin - direction * 0.12f * S, new ParticleSpec
            {
                Sprite = VfxArt.DustPuff(Random.Range(0, 4)),
                StartColor = WithAlpha(EarthOchre, 0.62f),
                EndColor = WithAlpha(EarthDeep, 0f),
                Lifetime = new Vector2(0.22f, 0.36f),
                Speed = new Vector2(0.18f * S, 0.58f * S) * scale,
                Angle = angle + 180f,
                Spread = 42f,
                StartSize = new Vector2(0.08f * S, 0.13f * S) * scale,
                EndSizeScale = 1.7f,
                Drag = 3.4f,
                Spin = new Vector2(8f, 45f),
                SpawnRadius = 0.06f * S,
                FadeIn = 0.08f,
            }, 3, layer, Order - 3);
            VfxParticles.Burst(origin - direction * 0.08f * S, new ParticleSpec
            {
                Sprite = VfxArt.SandShard(Random.Range(0, 6)),
                StartColor = WithAlpha(EarthSand, 0.98f),
                EndColor = WithAlpha(EarthDeep, 0f),
                Lifetime = new Vector2(0.18f, 0.3f),
                Speed = new Vector2(0.55f * S, 1.25f * S) * scale,
                Angle = angle + 180f,
                Spread = 48f,
                StartSize = new Vector2(0.024f * S, 0.05f * S) * scale,
                EndSizeScale = 0.48f,
                Gravity = 2.5f * S,
                Drag = 2.2f,
                Spin = new Vector2(110f, 340f),
            }, 2, layer, Order - 1);
        }

        /// <summary>
        /// 판 위를 달리는 바위가 남기는 짧은 지면 박자. 평평한 충격파, 흙먼지, 자갈만 써서
        /// 장식 마법진보다 물성과 진행 방향이 먼저 보이게 한다.
        /// </summary>
        private static void GroundBeat(Vector3 point, Vector3 direction, float scale, int layer)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            SpriteFx.Play(VfxArt.Shockwave, point, new Color(0.72f, 0.58f, 0.38f, 0.72f), false,
                new Vector2(0.08f, 0.025f) * S * scale, new Vector2(0.55f, 0.13f) * S * scale,
                0.22f, angle, 0f, 0.01f, 0.28f, SpriteFx.Ease.OutCubic, layer, Order - 3);
            VfxParticles.Burst(point, new ParticleSpec
            {
                Sprite = VfxSpace.Pebble,
                StartColor = new Color(0.76f, 0.64f, 0.46f, 0.85f),
                EndColor = new Color(0.76f, 0.64f, 0.46f, 0f),
                Lifetime = new Vector2(0.18f, 0.32f),
                Speed = new Vector2(0.18f * S, 0.52f * S) * scale,
                Angle = 90f,
                Spread = 70f,
                StartSize = new Vector2(0.012f * S, 0.028f * S) * scale,
                Gravity = 4f * S,
                Spin = new Vector2(160f, 520f),
            }, 2, layer, Order - 1);
        }

        /// <summary>넓은 주 판석 또는 빅뱅용 작은 지층 조각을 만든다.</summary>
        private static Transform NewTectonicShard(int layer, float size, out SpriteRenderer rock,
            out SpriteRenderer halo, bool broadPlate = false, bool decoratePlate = true)
        {
            var root = new GameObject("TectonicShard").transform;
            UnityEngine.Object.Destroy(root.gameObject, 6f);

            halo = VfxSpace.NewSprite(root, "ShardGlow", VfxArt.SoftGlow, Color.clear, layer, Order, additive: true);
            halo.transform.localScale = new Vector3(size * 1.15f, size * 0.46f, 1f);

            // 하늘색 유리 바닥에서는 바닥 쪽 냉회색 그림자가 있어야 판석이 흰 면에 뜨지 않는다.
            SpriteRenderer shadow = VfxSpace.NewSprite(root, "ShardContactShadow", VfxArt.SoftGlow,
                WithAlpha(ContactShadow, 0.52f), layer, Order - 2, additive: false);
            shadow.transform.localPosition = new Vector3(-size * 0.03f, -size * 0.10f, 0f);
            shadow.transform.localScale = new Vector3(size * 1.16f, size * 0.34f, 1f);

            Sprite shardSprite = broadPlate ? VfxArt.Streak : VfxArt.SandShard(Random.Range(0, 6));
            rock = VfxSpace.NewSprite(root, "ShardCore", shardSprite, Color.white, layer, Order + 3);
            rock.transform.localScale = Vector3.one * size;
            SpriteRenderer rim = VfxSpace.NewSprite(rock.transform, "ShardRim", shardSprite,
                WithAlpha(EarthDeep, 0.62f), layer, Order + 1, additive: false);
            rim.transform.localScale = Vector3.one * 1.045f;

            // 주 판석에만 두 개의 평행 조각을 붙인다. 빅뱅 일곱 발에는 이 장식을 반복하지 않는다.
            for (int i = 0; broadPlate && decoratePlate && i < 2; i++)
            {
                SpriteRenderer splinter = VfxSpace.NewSprite(rock.transform, "TrailingSplinter" + i,
                    VfxArt.SandShard((i + 2) % 6),
                    i == 0 ? new Color(0.72f, 0.59f, 0.40f, 0.95f) : new Color(0.45f, 0.35f, 0.25f, 0.9f),
                    layer, Order + 2 - i);
                splinter.transform.localPosition = new Vector3(-0.26f, i == 0 ? 0.26f : -0.24f, 0f);
                splinter.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -5f : 7f);
                splinter.transform.localScale = Vector3.one * (i == 0 ? 0.30f : 0.22f);
            }

            // 공기막은 암편 앞만 감싸는 짧은 초승달이다. 암편 길이의 약 1.2배를 넘기지 않아
            // 발사점까지 이어지는 렌즈형 에너지 띠가 되지 않는다.
            SpriteRenderer shellEdge = VfxSpace.NewSprite(root, "AirShellEdge", VfxArt.Crescent, Color.clear,
                layer, Order + 4, additive: false);
            shellEdge.transform.localPosition = new Vector3(size * 0.52f, 0f, 0f);
            shellEdge.transform.localScale = new Vector3(size * 0.38f, size * 0.58f, 1f);
            SpriteRenderer shell = VfxSpace.NewSprite(root, "AirShell", VfxArt.Crescent, Color.clear,
                layer, Order + 5, additive: true);
            shell.transform.localPosition = new Vector3(size * 0.52f, 0f, 0f);
            shell.transform.localScale = new Vector3(size * 0.29f, size * 0.46f, 1f);

            return root;
        }

        /// <summary>땅이 갈라지며 바위가 뽑혀 나오는 자리 — 바닥 충격파 · 흙먼지 · 튀는 자갈.</summary>
        private static void Erupt(Vector3 ground, Vector3 front, float scale, int layer)
        {
            SpriteFx.Play(VfxArt.SoftGlow, ground + Vector3.down * 0.015f * S, WithAlpha(ContactShadow, 0.32f), false,
                new Vector2(0.24f, 0.04f) * S * scale, new Vector2(1.02f, 0.16f) * S * scale, 0.52f,
                0f, 0f, 0.02f, 0.34f, SpriteFx.Ease.OutCubic, layer, Order - 4);
            SpriteFx.Play(VfxArt.DustCone, ground + Vector3.down * 0.025f * S,
                WithAlpha(ContactShadow, 0.38f), false,
                new Vector2(0.18f, 0.05f) * S * scale, new Vector2(0.88f, 0.16f) * S * scale, 0.48f,
                front.x < 0f ? 180f : 0f, 0f, 0.01f, 0.36f, SpriteFx.Ease.OutCubic, layer, Order - 5);
            SpriteFx.Play(VfxArt.Shockwave, ground, new Color(0.62f, 0.50f, 0.34f, 0.8f), false,
                new Vector2(0.12f, 0.04f) * S * scale, new Vector2(0.72f, 0.2f) * S * scale, 0.36f,
                0f, 0f, 0.02f, 0.3f, SpriteFx.Ease.OutCubic, layer, Order - 2);
            VfxParticles.Burst(ground, new ParticleSpec
            {
                Sprite = VfxArt.DustPuff(Random.Range(0, 4)),
                StartColor = WithAlpha(EarthOchre, 0.66f),
                EndColor = WithAlpha(DustEdge, 0f),
                Lifetime = new Vector2(0.45f, 0.7f),
                Speed = new Vector2(0.15f * S, 0.45f * S) * scale,
                Angle = 90f,
                Spread = 55f,
                StartSize = new Vector2(0.18f * S, 0.28f * S) * scale,
                EndSizeScale = 1.8f,
                Drag = 2.5f,
                Spin = new Vector2(10f, 60f),
                FadeIn = 0.1f,
            }, 3, layer, Order - 3);
            VfxParticles.Burst(ground, new ParticleSpec
            {
                Sprite = VfxArt.SandShard(Random.Range(0, 6)),
                StartColor = Color.white,
                EndColor = new Color(1f, 1f, 1f, 0f),
                Lifetime = new Vector2(0.35f, 0.55f),
                Speed = new Vector2(1.0f * S, 2.0f * S) * scale,
                Angle = Mathf.Atan2(0.7f, front.x) * Mathf.Rad2Deg,
                Spread = 28f,
                StartSize = new Vector2(0.03f * S, 0.06f * S) * scale,
                EndSizeScale = 0.8f,
                Gravity = 7f * S,
                Spin = new Vector2(300f, 800f),
                SpawnRadius = 0.06f * S,
            }, 3, layer, Order + 1);
        }

        private static void Erupt(Vector3 ground, float scale, int layer)
            => Erupt(ground, Vector3.right, scale, layer);

        // ══════════════════════════════════════════════════════
        // 창세의 결정
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 결정 하나를 얻었다 — 맞은 아군의 카드에서 호박빛 결정이 튀어나와 금빛 실을 끌며 세이에게 날아든다.
        /// 세이 자신이 맞았으면 그 자리에서 빛나기만 한다.
        /// </summary>
        public static void CrystalGained(MonoBehaviour runner, Unit from, Unit to)
        {
            if (to == null) return;
            if (from == null || from == to)
            {
                CrystalAbsorbed(VfxSpace.WorldPoint(to) + Vector3.down * 0.36f * S,
                    VfxSpace.SortingLayer(to), behindPortrait: true);
                return;
            }
            VfxRunner.Run(FlyCrystal(VfxSpace.WorldPoint(from), to));
        }

        private static IEnumerator FlyCrystal(Vector3 origin, Unit to)
        {
            int layer = VfxSpace.SortingLayer(to);
            ProjectileStyle style = ProjectileFx.ElementStyle(BaseEnums.UnitElement.Cryo, 0.46f);
            style.Hovl.Tint = new Color(1f, 0.70f, 0.22f);
            style.Hovl.Alpha = 0.82f;
            style.BodyUnderlayColor = WithAlpha(ContactShadow, 0.13f);
            style.Duration = 0.46f;
            style.ArcHeight = 0.12f;
            style.Acceleration = 1.85f;
            style.TargetOffset = Vector3.down * 0.36f;
            style.RibbonWidth = 0.018f;
            style.OnImpact = (point, direction) => CrystalAbsorbed(point, layer, behindPortrait: true);
            yield return ProjectileFx.FlyFrom(origin, to, style, null, layer);
        }

        private static void CrystalAbsorbed(Vector3 point, int layer, bool behindPortrait = false)
        {
            HovlProjectileSet crystal = HovlFx.ForElement(BaseEnums.UnitElement.Cryo, 0.48f);
            crystal.Tint = new Color(1f, 0.68f, 0.18f);
            crystal.Alpha = 0.86f;
            crystal.UnderlayColor = WithAlpha(ContactShadow, 0.12f);
            int order = behindPortrait ? RearOrder + 4 : Order + 4;
            CommonHandpaintFx.Hit(crystal, point, Vector3.up, S, layer, order);

            // Hovl 결정 폭발이 주인공이고, 두 가닥 반짝임만 흡수 방향을 보조한다.
            SpriteFx.Play(VfxArt.Glint, point, WithAlpha(EarthDeep, 0.66f), false,
                0.10f * S, 0.38f * S, 0.21f, 0f, 65f, 0.02f, 0.30f,
                SpriteFx.Ease.OutCubic, layer, order);
            SpriteFx.Play(VfxArt.Glint, point, WithAlpha(GoldBright, 0.62f), true,
                0.06f * S, 0.27f * S, 0.15f, 0f, 70f, 0.02f, 0.18f,
                SpriteFx.Ease.OutCubic, layer, order + 1);
        }

        // ══════════════════════════════════════════════════════
        // 빅뱅 — 한 번의 대형 시전 뒤 대상마다 열리는 결정 파쇄
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 빅뱅. 기본 화풍은 0.18초 금빛 결정 예고와 한 번의 대형 시전 뒤
        /// <paramref name="pickTarget"/>으로 고른 적 위에서 일곱 파쇄를 연다.
        /// 피해 일곱 번과 0.075초 선택 간격, 선택 후 0.242초의 판정은 유지한다.
        /// </summary>
        public static IEnumerator BigBang(Unit caster, int shards, Func<Unit> pickTarget, Action<Unit> onHit)
        {
            bool handpaint = VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sei);
            if (handpaint)
            {
                yield return BigBangComposed(caster, shards, pickTarget, onHit);
                yield break;
            }
            int layer = VfxSpace.SortingLayer(caster);
            Vector3 center = VfxSpace.WorldPoint(caster);
            Effects.BattlefieldBackdrop.PushFocus(0.80f, 0.10f, 0.92f, 0.34f,
                new Color(0.13f, 0.25f, 0.42f, 1f), center);
            Effects.BattlefieldBackdrop.PulseFlash(new Color(1f, 0.86f, 0.52f), 0.085f, 0.13f);
            SkillFx.PlayCue(new SkillFxCue
            {
                ResourcePath = GenesisCastCue.ResourcePath,
                FrameDelays = GenesisCastCue.FrameDelays,
                SizeInCards = new Vector2(1.62f, 1.62f),
                OffsetInCards = new Vector2(0f, 0.04f),
                OrderOffset = GenesisCastCue.OrderOffset,
                Composite = GenesisComposite(false),
            }, center, layer, RearOrder, caster);
            if (handpaint)
                HandpaintFx.PlayAt(center, layer, HandpaintFx.Muzzle1Yellow,
                    new Vector2(1.08f, 1.08f), HandpaintGoldBody, GoldBright, 0f, 12, 0.84f, 0.62f);
            MajorRise(center, layer, 0.82f);
            CameraShake.Shake(0.045f);

            // 발사대 없이도 기존 예고 박자는 보존한다. 이 동안 문장과 상승광이 먼저 장면을 연다.
            yield return new WaitForSeconds(0.18f);

            int landed = 0;
            Vector2[] impactOffsets = CreateImpactOffsets(shards);
            for (int i = 0; i < shards; i++)
            {
                Unit target = pickTarget?.Invoke();
                if (target == null)
                {
                    landed++;
                    continue;
                }

                Unit captured = target;
                Vector2 impactOffset = impactOffsets[i];
                float scatterAngle = i * (360f / Mathf.Max(1, shards)) + Random.Range(-14f, 14f);
                float scatterRadius = Random.Range(0.48f, 0.68f);
                float scatterFraction = Random.Range(0.27f, 0.40f);
                Vector3 muzzle = center + DirectionFromAngle(scatterAngle) * 0.10f * S;
                SpriteFx.Play(VfxArt.Glint, muzzle, new Color(1f, 0.89f, 0.58f, 0.68f), true,
                    0.04f * S, 0.22f * S, 0.13f, 0f, 72f, 0.015f, 0.12f,
                    SpriteFx.Ease.OutCubic, layer, RearOrder + 10);
                if (handpaint)
                    HandpaintFx.PlayAt(muzzle, layer, HandpaintFx.Muzzle1Yellow,
                        new Vector2(0.46f, 0.46f), HandpaintGoldBody, GoldBright,
                        scatterAngle, 24, 0.78f, 0.58f);
                AudioManager.PlayCue("sei.bigbang.projectile", 0.88f + i * 0.015f);
                VfxRunner.Run(FlyBigBangProjectile(muzzle, captured, impactOffset, i, layer,
                    scatterAngle, scatterRadius, scatterFraction, handpaint));
                VfxRunner.Run(PlayBigBangImpact(captured, impactOffset, layer, handpaint, () =>
                {
                    onHit?.Invoke(captured);
                    CameraShake.Shake(0.018f);
                    Effects.BattlefieldBackdrop.PulseFlash(GoldBright, 0.025f, 0.050f);
                }, () => landed++));
                yield return new WaitForSeconds(0.075f);
            }

            // 마지막 착탄 콜백까지 기다려 추가행동의 전투 계약을 유지한다.
            float guard = 3f;
            while (landed < shards && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }

        }

        private static IEnumerator BigBangComposed(Unit caster, int shards, Func<Unit> pickTarget,
            Action<Unit> onHit)
        {
            if (caster == null) yield break;
            int layer = VfxSpace.SortingLayer(caster);
            Vector3 center = VfxSpace.WorldPoint(caster);
            Unit aimTarget = FindAimTarget(caster);
            VfxComposer.PlayScreen("sei.bigbang", caster, aimTarget);
            VfxComposer.PlaySlot("sei.bigbang", "cast", 0, caster, aimTarget, center, layer);
            // 승인된 검사 리듬만큼 시전광을 읽힌 뒤, 각 결정은 선택 후 0.242초 판정을 그대로 지킨다.
            yield return new WaitForSeconds(0.04f);

            int landed = 0;
            for (int i = 0; i < shards; i++)
            {
                Unit target = pickTarget?.Invoke();
                if (target == null)
                {
                    landed++;
                    continue;
                }

                Unit captured = target;
                VfxRunner.Run(Chain(PlayComposedBigBangImpact(caster, captured, () =>
                {
                    onHit?.Invoke(captured);
                    CameraShake.Shake(0.018f);
                    Effects.BattlefieldBackdrop.PulseFlash(GoldBright, 0.025f, 0.050f);
                }), () => landed++));
                if (i + 1 < shards) yield return new WaitForSeconds(0.075f);
            }

            float guard = 3f;
            while (landed < shards && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        private static IEnumerator PlayComposedBigBangImpact(Unit caster, Unit target, Action onImpact)
        {
            // 30fps 충격 시트의 3번 정점(0.10초)을 더해 기존 0.242초 판정을 맞춘다.
            yield return new WaitForSeconds(VfxComposer.Offset("sei.bigbang", "hit", 0, 0.142f));
            yield return VfxComposer.PlayHitSlot("sei.bigbang", 0, caster, target, () =>
            {
                AudioManager.PlayCue("sei.bigbang.hit");
                onImpact?.Invoke();
            });
        }

        private static IEnumerator FlyBigBangProjectile(Vector3 start, Unit target, Vector2 impactOffset,
            int index, int layer, float scatterAngle, float scatterRadius, float scatterFraction,
            bool handpaint)
        {
            CompositeArt bolt = null;
            FlipbookFx paintedBolt = null;
            TrailRenderer trail = null;
            if (handpaint)
            {
                paintedBolt = HandpaintFx.PlayAt(start, layer, HandpaintFx.Ball1Yellow,
                    new Vector2(0.42f, 0.42f), HandpaintGoldBody, GoldBright,
                    scatterAngle, 38, 0.82f, 0.58f, true);
            }
            else
            {
                bolt = NewCompositeArt("BigBangHomingShard_" + index,
                    VfxArt.SeiBigBangShard, start, layer, Order + 18, 0.32f * S, 0.86f, 0.62f);
                trail = VfxArt.NewTrail(bolt.Root, WithAlpha(GoldBright, 0.48f),
                    WithAlpha(GoldBright, 0f), 0.026f * S, 0.13f, layer, Order + 14, additive: true,
                    endWidth: 0.05f);
            }
            Vector3 offset = new(impactOffset.x * S, impactOffset.y * S, 0f);
            Vector3 fixedEnd = target != null ? VfxSpace.WorldPoint(target) + offset : start + Vector3.right * S;
            Vector3 outward = DirectionFromAngle(scatterAngle);
            Vector3 outerPoint = start + outward * scatterRadius * S;
            float arcSign = index % 2 == 0 ? 1f : -1f;
            Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * arcSign;
            // 기존 운석 시트가 7번 프레임에 도달하는 0.242초와 비행 도착을 맞춘다.
            const float duration = 0.242f;
            float scatterDuration = duration * scatterFraction;
            Vector3 previous = start;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                Vector3 end = target != null && target.isActive ? VfxSpace.WorldPoint(target) + offset : fixedEnd;
                Vector3 position;
                if (age < scatterDuration)
                {
                    float outwardT = EaseOutCubic(Mathf.Clamp01(age / scatterDuration));
                    position = Vector3.Lerp(start, outerPoint, outwardT);
                }
                else
                {
                    float returnT = Mathf.Clamp01((age - scatterDuration) / (duration - scatterDuration));
                    float eased = EaseInOutSine(returnT);
                    Vector3 controlA = outerPoint + tangent * (0.48f + 0.05f * (index % 3)) * S;
                    Vector3 incoming = Direction(outerPoint, end);
                    Vector3 controlB = end - incoming * (0.34f + 0.04f * (index % 2)) * S;
                    position = CubicBezier(outerPoint, controlA, controlB, end, eased);
                }

                Vector3 flightTangent = position - previous;
                if (flightTangent.sqrMagnitude > 0.000001f)
                {
                    Transform moving = paintedBolt != null ? paintedBolt.FxTransform : bolt.Root;
                    moving.rotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(flightTangent.y, flightTangent.x) * Mathf.Rad2Deg);
                }
                Transform root = paintedBolt != null ? paintedBolt.FxTransform : bolt.Root;
                root.position = position;
                float pulse = Mathf.Sin(t * Mathf.PI);
                if (bolt != null)
                    SetCompositeAlpha(bolt, Mathf.Lerp(0.72f, 0.90f, pulse),
                        Mathf.Lerp(0.42f, 0.66f, pulse));
                previous = position;
                yield return null;
            }

            if (trail != null) VfxArt.ReleaseTrail(trail);
            if (paintedBolt != null) UnityEngine.Object.Destroy(paintedBolt.gameObject);
            if (bolt?.Root != null) UnityEngine.Object.Destroy(bolt.Root.gameObject);
            CardMotion.ViewOf(target)?.PulseVfxLight(GoldBright, 0.055f, 0.24f);
        }

        private static IEnumerator PlayBigBangImpact(Unit target, Vector2 impactOffset, int layer,
            bool handpaint, Action onImpact, Action onComplete)
        {
            if (handpaint)
            {
                // 45fps 시트의 4번 칸이 0.089초 뒤 열리므로 총 0.242초 판정을 맞춘다.
                yield return new WaitForSeconds(0.153f);
                yield return HandpaintFx.PlayHit(target, HandpaintFx.Explosion3Yellow,
                    new Vector2(0.84f, 0.84f), HandpaintGoldBody, GoldBright, () =>
                    {
                        AudioManager.PlayCue("sei.bigbang.hit");
                        onImpact?.Invoke();
                        Vector3 point = VfxSpace.WorldPoint(target) +
                                        new Vector3(impactOffset.x, impactOffset.y) * S;
                        HandpaintFx.PlayAt(point, layer, HandpaintFx.Smoke6Yellow,
                            new Vector2(0.66f, 0.66f), HandpaintGoldBody, GoldBright,
                            0f, 31, 0.70f, 0.42f);
                    }, 0f, false, 4, HandpaintFx.Impact1Yellow);
                onComplete?.Invoke();
                yield break;
            }

            // 일반행동 시트의 4번 프레임이 0.076초 뒤이므로 0.166초에 열어 기존 0.242초 판정을 지킨다.
            yield return new WaitForSeconds(0.166f);
            bool completed = false;
            SkillFx.PlayImpact(ImpactCueAt(impactOffset), target, layer, onImpact,
                () => completed = true, "sei.bigbang.hit");
            float guard = 2f;
            while (!completed && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
            onComplete?.Invoke();
        }

        private static IEnumerator Chain(IEnumerator routine, Action done)
        {
            yield return routine;
            done?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 창세의 노래
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 창세의 노래 — 시전자 뒤의 거대한 결정 문장이 화면을 열고, 아군마다 작은 문장이 강림한다.
        /// 원화 얼굴을 비우기 위해 주 문장은 뒤에만 두며, <paramref name="onReach"/>는 각 축복 시트의
        /// 정점 프레임에서 불린다. 아군에게 날아가는 구체는 사용하지 않는다.
        /// </summary>
        public static IEnumerator SongOfGenesis(Unit caster, IReadOnlyList<Unit> allies, Action<Unit> onReach)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sei))
            {
                yield return SongOfGenesisComposed(caster, allies, onReach);
                yield break;
            }

            int layer = VfxSpace.SortingLayer(caster);
            Vector3 center = VfxSpace.WorldPoint(caster);
            Managers.AudioManager.PlayCue("sei.genesis.screen");
            Effects.BattlefieldBackdrop.PushFocus(0.84f, 0.10f, 0.86f, 0.38f,
                new Color(0.14f, 0.28f, 0.46f, 1f), center);
            Effects.BattlefieldBackdrop.PulseFlash(new Color(1f, 0.92f, 0.70f), 0.11f, 0.12f);
            SkillFx.PlayCue(GenesisCastCue, center, layer, RearOrder, caster);
            MajorRise(center, layer, 1.0f);
            CameraShake.Shake(0.040f);

            yield return new WaitForSeconds(0.22f);

            int pending = 0;
            foreach (Unit ally in allies)
            {
                if (ally == null) continue;
                if (ally == caster)
                {
                    GenesisCasterArrival(VfxSpace.WorldPoint(ally), layer);
                    Managers.AudioManager.PlayCue("sei.genesis.hit");
                    onReach?.Invoke(ally);
                    continue;
                }
                pending++;
                Unit captured = ally;
                VfxRunner.Run(Chain(Blessing(caster, captured, onReach), () => pending--));
            }

            float guard = 2f;
            while (pending > 0 && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 기본 화풍은 공격처럼 터지는 문장·상승광을 쓰지 않는다. 시전자 앞의 방향성 시전광 한 번 뒤
        /// 자기 자신은 표시 없이 적용하고, 다른 아군에게만 실제 버프와 보호막을 한 affected로 묶어 보여 준다.
        /// </summary>
        private static IEnumerator SongOfGenesisComposed(Unit caster, IReadOnlyList<Unit> allies,
            Action<Unit> onReach)
        {
            if (caster == null) yield break;
            Unit aimTarget = FindAimTarget(caster);
            VfxComposer.PlayScreen("sei.genesis", caster, caster);
            VfxComposer.PlaySlot("sei.genesis", "cast", 0, caster, aimTarget,
                VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));

            // 모든 대상의 실제 효과와 받는 연출을 같은 프레임에 열어 버프/보호막이 한 묶음으로 읽힌다.
            yield return new WaitForSeconds(0.22f);
            if (allies != null)
            {
                VfxComposer.PlayScreen("sei.genesis", caster, allies);
                foreach (Unit ally in allies)
                {
                    if (ally == null || !ally.isActive) continue;
                    onReach?.Invoke(ally);
                    VfxComposer.PlayAffectedSlot("sei.genesis", 0, caster, ally);
                    VfxComposer.PlayAffectedSlot("sei.genesis", 1, caster, ally);
                }
                Managers.AudioManager.PlayCue("sei.genesis.hit");
            }
            yield return new WaitForSeconds(0.52f);
        }

        /// <summary>아군 위치에 금빛 결정 문장이 바로 강림한다. 이동 구체 없이 정점 프레임에 축복을 적용한다.</summary>
        private static IEnumerator Blessing(Unit caster, Unit ally, Action<Unit> onReach)
        {
            int layer = VfxSpace.SortingLayer(ally);
            yield return SkillFx.PlayTargeted(caster, ally, GenesisBlessingPlan, () =>
            {
                GenesisArrival(VfxSpace.WorldPoint(ally), layer, 0.72f);
                onReach?.Invoke(ally);
            });
        }

        private static void GenesisCasterArrival(Vector3 point, int layer)
        {
            Vector3 feet = point + Vector3.down * 0.36f * S;
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sei))
            {
                HandpaintFx.PlayAt(feet, layer, HandpaintFx.Explosion3Yellow,
                    new Vector2(0.88f, 0.88f), HandpaintGoldBody, GoldBright,
                    0f, -92, 0.76f, 0.50f);
                HandpaintFx.PlayAt(feet, layer, HandpaintFx.Smoke2Yellow,
                    new Vector2(0.76f, 0.76f), HandpaintGoldBody, GoldBright,
                    0f, -94, 0.62f, 0.34f);
                Effects.BattlefieldBackdrop.PulseFlash(new Color(1f, 0.90f, 0.62f), 0.06f, 0.09f);
                return;
            }
            HovlProjectileSet impact = HovlFx.ForElement(BaseEnums.UnitElement.Geo, 1.16f);
            impact.UnderlayColor = WithAlpha(ContactShadow, 0.04f);

            // 자기 축복은 공격 착탄이 아니다. 원화보다 확실히 낮은 순서에서 발밑과 외곽만 밝혀
            // 흰 의상 위에 주황 면광이 생기지 않게 하고, 우상단 오브도 침범하지 않는다.
            CommonHandpaintFx.Hit(impact, feet, Vector3.up, S,
                layer, RearOrder + 4, 1.06f, 0.92f);
            SpriteFx.Play(VfxSpace.Ring, feet + Vector3.down * 0.08f * S,
                new Color(1f, 0.76f, 0.28f, 0.66f), true,
                0.12f * S, 0.86f * S, 0.26f, 0f, 0f, 0.004f, 0.20f,
                SpriteFx.Ease.OutCubic, layer, RearOrder + 5);
            Effects.BattlefieldBackdrop.PulseFlash(new Color(1f, 0.90f, 0.62f), 0.06f, 0.09f);
        }

        private static void GenesisArrival(Vector3 point, int layer, float scale)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sei))
            {
                HandpaintFx.PlayAt(point + Vector3.up * 0.08f * S, layer,
                    HandpaintFx.Impact1Yellow, new Vector2(0.82f, 0.82f) * scale,
                    HandpaintGoldBody, GoldBright, 0f, 30, 0.80f, 0.54f);
                HandpaintFx.PlayAt(point + Vector3.down * 0.18f * S, layer,
                    HandpaintFx.Smoke6Yellow, new Vector2(0.78f, 0.78f) * scale,
                    HandpaintGoldBody, GoldBright, 0f, 26, 0.66f, 0.36f);
                Effects.BattlefieldBackdrop.PulseFlash(new Color(1f, 0.90f, 0.62f), 0.06f, 0.09f);
                return;
            }
            HovlProjectileSet impact = HovlFx.ForElement(BaseEnums.UnitElement.Geo, 1.48f * scale);
            impact.UnderlayColor = WithAlpha(ContactShadow, 0.04f);
            CommonHandpaintFx.Hit(impact, point + Vector3.up * 0.08f * S, Vector3.down, S,
                layer, Order + 5, 1.18f, 1f);
            SpriteFx.Play(VfxSpace.Ring, point + Vector3.down * 0.32f * S,
                new Color(1f, 0.76f, 0.28f, 0.72f), true,
                0.14f * S * scale, 0.92f * S * scale, 0.26f, 0f, 0f, 0.004f, 0.20f,
                SpriteFx.Ease.OutCubic, layer, Order + 6);
            Effects.BattlefieldBackdrop.PulseFlash(new Color(1f, 0.90f, 0.62f), 0.06f, 0.09f);
        }

        private static void MajorRise(Vector3 center, int layer, float scale)
        {
            VfxParticles.Burst(center + Vector3.down * 0.35f * S, new ParticleSpec
            {
                Sprite = VfxArt.Glint,
                Additive = true,
                StartColor = new Color(1f, 0.82f, 0.34f, 0.92f),
                EndColor = new Color(0.48f, 0.76f, 1f, 0f),
                Lifetime = new Vector2(0.44f, 0.78f),
                Speed = new Vector2(0.90f * S, 1.75f * S) * scale,
                Angle = 90f,
                Spread = 34f,
                StartSize = new Vector2(0.035f * S, 0.10f * S) * scale,
                EndSizeScale = 0.18f,
                Drag = 1.6f,
                Spin = new Vector2(-80f, 80f),
                SpawnRadius = 0.46f * S * scale,
            }, Mathf.Max(10, Mathf.RoundToInt(14 * scale)), layer, RearOrder + 7);
        }

        // ══════════════════════════════════════════════════════
        // 결정화 보호막
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 대지의 방패 — 호박빛 결정 네 조각이 원화의 얼굴을 피해 카드 하단 반원으로 모여든다.
        /// </summary>
        /// <param name="strength">1 = 특성의 작은 방패. 궁극기의 두꺼운 방패는 1.5쯤.</param>
        public static void StoneShield(Unit ally, float strength = 1f, string composedSkillKey = "sei.genesis")
        {
            if (ally == null) return;
            VfxRunner.Run(ShieldRoutine(ally, strength, composedSkillKey));
        }

        private static IEnumerator ShieldRoutine(Unit ally, float strength, string composedSkillKey)
        {
            if (VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sei))
            {
                if (!string.IsNullOrWhiteSpace(composedSkillKey))
                    VfxComposer.PlayAffectedSlot(composedSkillKey, 0, null, ally,
                        Mathf.Lerp(1f, 1.15f, Mathf.Clamp01(strength - 1f)));
                yield return new WaitForSeconds(0.12f);
                yield break;
            }
            int layer = VfxSpace.SortingLayer(ally);
            float sizeScale = Mathf.Lerp(1f, 1.15f, Mathf.Clamp01(strength - 1f));
            Vector3 center = VfxSpace.WorldPoint(ally);
            Vector3 feet = center + Vector3.down * 0.42f * S;
            HovlProjectileSet crystal = HovlFx.ForElement(BaseEnums.UnitElement.Cryo, 1.18f * sizeScale);
            crystal.Tint = new Color(1f, 0.70f, 0.20f);
            crystal.Alpha = 0.88f;
            crystal.UnderlayColor = new Color(0.13f, 0.31f, 0.44f, 0.10f);

            // 중심 crystal Hit이 방패가 닫히는 순간을 만들고, 좌우 Flash가 낮은 반원만 암시한다.
            CommonHandpaintFx.Hit(crystal, feet, Vector3.up, S, layer, RearOrder + 3, 1.18f, 1f);
            yield return new WaitForSeconds(0.045f);
            CommonHandpaintFx.Flash(crystal, feet + Vector3.left * 0.34f * S,
                new Vector3(0.24f, 1f, 0f), S, layer, RearOrder + 2, 0.54f, 0.72f);
            CommonHandpaintFx.Flash(crystal, feet + Vector3.right * 0.34f * S,
                new Vector3(-0.24f, 1f, 0f), S, layer, RearOrder + 2, 0.54f, 0.72f);
            yield return new WaitForSeconds(0.075f);
            CommonHandpaintFx.Hit(crystal, feet, Vector3.up, S, layer, RearOrder + 2, 0.82f, 0.78f);
        }

        private static Unit FindAimTarget(Unit caster)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || caster == null) return caster;
            List<Unit> pool = caster.IsEnemy ? grid.heroList : grid.enemyList;
            foreach (Unit unit in pool)
            {
                if (unit != null && unit.isActive && unit.IsOnField && !unit.IsUntargetable) return unit;
            }
            return caster;
        }

        /// <summary>집중이 끊겼다 — 발밑 광맥이 꺼지고 결정 조각이 부서져 흩어진다.</summary>
        public static void ConcentrationBroken(Unit caster)
        {
            if (caster == null) return;
            int layer = VfxSpace.SortingLayer(caster);
            Vector3 center = VfxSpace.WorldPoint(caster);
            Vector3 feet = center + Vector3.down * 0.42f * S;
            HovlProjectileSet broken = HovlFx.ForElement(BaseEnums.UnitElement.Cryo, 0.74f);
            broken.Tint = new Color(0.72f, 0.61f, 0.43f);
            broken.Alpha = 0.78f;
            broken.UnderlayColor = new Color(0.25f, 0.31f, 0.34f, 0.66f);
            CommonHandpaintFx.Hit(broken, feet, Vector3.down, S, layer, Order + 1, 0.92f, 0.9f);

            HovlProjectileSet fracture = HovlFx.ForElement(BaseEnums.UnitElement.None, 0.40f);
            fracture.Tint = new Color(0.48f, 0.57f, 0.62f);
            fracture.Alpha = 0.62f;
            fracture.UnderlayColor = new Color(0.18f, 0.24f, 0.28f, 0.58f);
            CommonHandpaintFx.Flash(fracture, feet + Vector3.left * 0.24f * S,
                new Vector3(-0.7f, -0.35f, 0f), S, layer, Order, 0.72f, 0.82f);
            CommonHandpaintFx.Flash(fracture, feet + Vector3.right * 0.24f * S,
                new Vector3(0.7f, -0.35f, 0f), S, layer, Order, 0.72f, 0.82f);
        }

        private static CompositeArt NewCompositeArt(string name, Sprite sprite, Vector3 position,
            int layer, int order, float size, float bodyAlpha, float coreAlpha)
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
            ApplyCompositeProperties(body, core);

            var art = new CompositeArt { Root = root, Body = body, Core = core };
            SetCompositeAlpha(art, bodyAlpha, coreAlpha);
            return art;
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
            block.SetFloat(BackdropLuminance, Effects.BattlefieldBackdrop.CurrentBackgroundLuminance);
            block.SetColor(PaletteLift, new Color(1f, 0.91f, 0.72f, 1f));
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

        private static void SetCompositeAlpha(CompositeArt art, float bodyAlpha, float coreAlpha)
        {
            if (art == null) return;
            float brightBoard = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.08f, 0.65f, Effects.BattlefieldBackdrop.CurrentBackgroundLuminance));
            if (art.Body != null)
                art.Body.color = new Color(1f, 1f, 1f,
                    Mathf.Clamp01(bodyAlpha * Mathf.Lerp(0.86f, 1f, brightBoard)));
            if (art.Core != null)
                art.Core.color = new Color(GoldBright.r, GoldBright.g, GoldBright.b,
                    Mathf.Clamp01(coreAlpha * Mathf.Lerp(0.72f, 1f, brightBoard)));
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

        private static SkillFxCue ImpactCueAt(Vector2 impactOffset)
        {
            return new SkillFxCue
            {
                ResourcePath = VolleyImpactCue.ResourcePath,
                Columns = VolleyImpactCue.Columns,
                Rows = VolleyImpactCue.Rows,
                FrameDelays = VolleyImpactCue.FrameDelays,
                ImpactFrame = VolleyImpactCue.ImpactFrame,
                Pivot = VolleyImpactCue.Pivot,
                SizeInCards = VolleyImpactCue.SizeInCards,
                OffsetInCards = impactOffset,
                Color = VolleyImpactCue.Color,
                Additive = VolleyImpactCue.Additive,
                Loop = VolleyImpactCue.Loop,
                FlipX = VolleyImpactCue.FlipX,
                BehindPortrait = VolleyImpactCue.BehindPortrait,
                OrderOffset = VolleyImpactCue.OrderOffset,
                Composite = VolleyImpactCue.Composite,
            };
        }

        // ── 곡선 ─────────────────────────────────────────────────────

        private static Vector3 DirectionFromAngle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        private static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            float clamped = 1f - u;
            return u * u * u * a + 3f * u * u * clamped * b +
                   3f * u * clamped * clamped * c + clamped * clamped * clamped * d;
        }

        private static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.z = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
