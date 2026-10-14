using Random = Effects.Vfx.PresentationRandom;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 착탄 · 충돌 연출 모음.
    ///
    /// 타격은 세 겹이다 — <b>섬광</b>(한 프레임에 '닿았다'), <b>충격파</b>(힘이 퍼진다),
    /// <b>파편</b>(무엇이 부딪혔는지). 한 겹만 있으면 약하고, 셋이 같은 프레임에 나와야 무게가 실린다.
    /// 섬광과 충격파는 <see cref="VfxArt"/>의 손그림 텍스처로, 파편은 <see cref="VfxParticles"/>로 그린다.
    /// 크기는 전부 카드 한 변 대비 비율이다.
    /// </summary>
    public static class ImpactFx
    {
        private static int Layer => 0;
        private static int Order => VfxSpace.OverlaySortingOrder + 10;
        private static float S => VfxSpace.CardSize;

        /// <summary>한 점에서 커지며 사라지는 스프라이트 하나(속도 0 파티클).</summary>
        public static void Pulse(Vector3 position, Sprite sprite, Color color, float fromSize, float toSize,
            float duration, bool additive = true, int orderOffset = 0)
        {
            Color end = color;
            end.a = 0f;
            VfxParticles.Burst(position, new ParticleSpec
            {
                Sprite = sprite,
                Additive = additive,
                StartColor = color,
                EndColor = end,
                Lifetime = new Vector2(duration, duration),
                Speed = Vector2.zero,
                StartSize = new Vector2(fromSize * S, fromSize * S),
                EndSizeScale = fromSize > 0f ? toSize / fromSize : 1f,
            }, 1, Layer, Order + orderOffset);
        }

        /// <summary>곡선 · 회전이 필요한 한 장. 크기는 카드 대비 비율이다.</summary>
        private static void Flash(Sprite sprite, Vector3 position, Color color, float from, float to, float life,
            float rotation = 0f, float spin = 0f, int orderOffset = 0, bool additive = true,
            SpriteFx.Ease ease = SpriteFx.Ease.OutCubic, float fadeOutFrom = 0.3f)
            => SpriteFx.Play(sprite, position, color, additive, from * S, to * S, life, rotation, spin,
                0.02f, fadeOutFrom, ease, Layer, Order + orderOffset);

        private static float AngleOf(Vector3 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        /// <summary>
        /// 지층 쐐기가 부서지며 박히는 타격. 세이의 공격이 쓴다.
        /// 진행 방향으로 찢긴 백색 심광 → 금빛 부채꼴 → 날카로운 암편 · 자갈 · 흙먼지 순으로 읽힌다.
        /// </summary>
        /// <param name="direction">날아온 방향. 파편은 그 반대쪽으로 튄다.</param>
        /// <param name="scale">1 = 일반행동 한 방. 작은 조각은 0.55쯤.</param>
        public static void Rock(Vector3 position, Vector3 direction, float scale = 1f, bool heavyDebris = false)
        {
            Color bright = new(1f, 0.88f, 0.56f, 1f);
            Color deep = new(0.44f, 0.27f, 0.18f, 0.78f);
            float travel = direction.sqrMagnitude > 0.0001f ? AngleOf(direction) : 0f;

            // 착탄 순간의 포커스 딤이 대비를 맡으므로 넓은 엄버 면은 남기지 않고 얇은 쐐기만 방향을 잡는다.
            SpriteFx.Play(VfxArt.ImpactStar, position, new Color(deep.r, deep.g, deep.b, 0.42f), false,
                new Vector2(0.11f, 0.05f) * S * scale, new Vector2(0.62f, 0.24f) * S * scale, 0.14f,
                travel, 0f, 0.003f, 0.30f, SpriteFx.Ease.OutCubic, Layer, Order + 4);
            Flash(VfxArt.SoftGlow, position, new Color(1f, 0.74f, 0.28f, 0.54f),
                0.06f * scale, 0.25f * scale, 0.095f, 0f, 0f, 5, fadeOutFrom: 0.04f);
            SpriteFx.Play(VfxArt.ImpactStar, position, new Color(bright.r, bright.g, bright.b, 0.62f), true,
                new Vector2(0.08f, 0.03f) * S * scale, new Vector2(0.42f, 0.14f) * S * scale, 0.095f,
                travel, 0f, 0.003f, 0.12f, SpriteFx.Ease.OutCubic, Layer, Order + 6);

            // 납작한 파편은 짧게 옆으로 흩어졌다 사라진다. 중력 낙하는 밝은 바닥에 색점이 남아 보이지 않게 막는다.
            VfxParticles.Burst(position, new ParticleSpec
            {
                Sprite = VfxArt.SandShard(Random.Range(0, 6)),
                StartColor = Color.white,
                EndColor = new Color(0.72f, 0.52f, 0.31f, 0f),
                Lifetime = heavyDebris ? new Vector2(0.24f, 0.38f) : new Vector2(0.18f, 0.31f),
                Speed = new Vector2(0.65f * S, 1.55f * S) * scale,
                Angle = travel - 18f,
                Spread = 58f,
                StartSize = (heavyDebris ? new Vector2(0.07f * S, 0.13f * S) : new Vector2(0.05f * S, 0.09f * S)) * scale,
                EndSizeScale = 0.50f,
                Gravity = 0f,
                Drag = 5.4f,
                Spin = new Vector2(100f, 360f),
                SpawnRadius = 0.05f * S,
            }, heavyDebris ? 5 : 3, Layer, Order + 2);

            // 늑대 발밑에 갈색 얼룩으로 남던 넓은 황토 원뿔은 제거하고 짧은 청회 먼지만 남긴다.
            float dustLife = heavyDebris ? 0.38f : 0.30f;
            SpriteFx.Play(VfxArt.DustCone, position + direction * 0.035f * S + Vector3.down * 0.035f * S,
                new Color(0.27f, 0.36f, 0.42f, heavyDebris ? 0.22f : 0.16f), false,
                new Vector2(0.20f, 0.07f) * S * scale,
                new Vector2(heavyDebris ? 0.68f : 0.50f, heavyDebris ? 0.19f : 0.15f) * S * scale,
                dustLife, travel - 8f, 0f, 0.01f, 0.30f, SpriteFx.Ease.OutCubic, Layer, Order - 4);

            if (scale >= 0.9f) CameraShake.Shake(0.04f * scale);
        }

        /// <summary>카드끼리 부딪히는 몸통 박치기. 충돌 별 · 부서지는 초승달 · 긁힌 불티.</summary>
        public static void Slam(Vector3 position, Vector3 direction, Color accent, float scale = 1f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("contact");
#endif
            Managers.AudioManager.PlayCue("common.hit.contact");
            float travel = direction.sqrMagnitude > 0.0001f ? AngleOf(direction) : 0f;
            Color.RGBToHSV(accent, out _, out float saturation, out _);
            Color impactAccent = saturation < 0.22f ? new Color(0.66f, 0.08f, 0.26f, 1f) : accent;
            Color deep = Color.Lerp(new Color(0.11f, 0.025f, 0.23f, 1f), impactAccent, 0.62f);
            deep.a = 0.88f;

            Effects.BattlefieldBackdrop.PushFocus(0.12f, 0.06f, 0.04f, 0.12f,
                new Color(0.18f, 0.23f, 0.42f, 1f), position);
            Effects.BattlefieldBackdrop.PulseFlash(new Color(0.94f, 0.88f, 1f), 0.025f, 0.16f);

            // 구매 손그림의 충돌 정점과 방향성 잔상이 무게를 만든다. 전체 화면 섬광에 의존하지 않는다.
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            CommonHandpaintFx.Contact(position + forward * .025f * S,direction,impactAccent,scale,Layer,Order+5);
            SpriteFx.Play(VfxSpace.Ring, position, new Color(deep.r, deep.g, deep.b, 0.26f), false,
                0.24f * S * scale, 1.15f * S * scale, 0.28f,
                0f, 0f, 0.004f, 0.30f, SpriteFx.Ease.OutCubic, Layer, Order + 3);
            SpriteFx.Play(VfxArt.ImpactStar, position, new Color(deep.r, deep.g, deep.b, 0.34f), false,
                new Vector2(0.16f, 0.07f) * S * scale, new Vector2(1.62f, 0.46f) * S * scale, 0.16f,
                travel, 0f, 0.003f, 0.28f, SpriteFx.Ease.OutCubic, Layer, Order + 7);
            SpriteFx.Play(VfxArt.ImpactStar, position, new Color(1f, 0.91f, 0.98f, 0.82f), true,
                new Vector2(0.12f, 0.035f) * S * scale, new Vector2(1.30f, 0.30f) * S * scale, 0.11f,
                travel, 0f, 0.002f, 0.10f, SpriteFx.Ease.OutCubic, Layer, Order + 8);

            Flash(VfxArt.SoftGlow, position, new Color(1f, 0.98f, 0.9f, 0.42f), 0.06f * scale, 0.36f * scale, 0.095f,
                0f, 0f, 6, fadeOutFrom: 0.08f);
            // 부딪힌 방향으로 휘는 초승달 — 힘이 어느 쪽으로 들어갔는지 보인다.
            Flash(VfxArt.Crescent, position, new Color(deep.r, deep.g, deep.b, 0.12f), 0.32f * scale, 0.82f * scale, 0.15f,
                travel - 90f, 0f, 1, additive: false, fadeOutFrom: 0.36f);
            Flash(VfxArt.Crescent, position, new Color(accent.r, accent.g, accent.b, 0.68f), 0.31f * scale, 0.82f * scale, 0.16f,
                travel - 90f, 0f, 2, fadeOutFrom: 0.18f);

            // 부딪힌 방향으로 튀는 불티. 속도 방향으로 늘여 그려 '긁혔다'가 보인다.
            VfxParticles.Burst(position, new ParticleSpec
            {
                Sprite = VfxSpace.SoftDot,
                Additive = true,
                StartColor = new Color(1f, 0.9f, 0.6f, 1f),
                EndColor = new Color(accent.r, accent.g, accent.b, 0f),
                Lifetime = new Vector2(0.18f, 0.35f),
                Speed = new Vector2(2.2f * S, 4.2f * S) * scale,
                Angle = travel,
                Spread = 55f,
                StartSize = new Vector2(0.03f * S, 0.05f * S),
                EndSizeScale = 0.4f,
                Drag = 4f,
                Stretch = 0.05f / S,
            }, Mathf.Max(4, Mathf.RoundToInt(6 * scale)), Layer, Order + 3);

            CameraShake.Shake(0.05f * scale);
        }

        /// <summary>원소 색 투사체의 착탄. 특정 모양이 없는 공격의 기본값이다.</summary>
        public static void ElementBurst(Vector3 position, Vector3 direction, Color color, float scale = 1f)
        {
            float travel = direction.sqrMagnitude > 0.0001f ? AngleOf(direction) : 0f;
            Color bright = Color.Lerp(color, Color.white, 0.25f);
            Color deep = new(color.r * 0.24f, color.g * 0.20f, color.b * 0.28f, 0.82f);

            // 짙은 알파 별과 고리가 먼저 윤곽을 만들고, 밝은 핵은 0.1초 안에 꺼진다.
            Flash(VfxArt.Starburst, position, deep, 0.20f * scale, 0.70f * scale, 0.19f,
                Random.Range(0f, 360f), 0f, 2, additive: false, fadeOutFrom: 0.30f);
            Flash(VfxArt.Shockwave, position, new Color(deep.r, deep.g, deep.b, 0.68f), 0.17f * scale, 0.88f * scale, 0.26f,
                0f, 0f, 0, additive: false, fadeOutFrom: 0.34f);
            Flash(VfxArt.SoftGlow, position, new Color(1f, 1f, 1f, 0.30f), 0.04f * scale, 0.22f * scale, 0.085f,
                0f, 0f, 7, fadeOutFrom: 0.06f);
            Flash(VfxArt.Starburst, position, new Color(bright.r, bright.g, bright.b, 0.58f), 0.15f * scale, 0.48f * scale, 0.13f,
                Random.Range(0f, 360f), 0f, 3);
            VfxParticles.Burst(position, new ParticleSpec
            {
                Sprite = VfxArt.Glint,
                Additive = true,
                StartColor = bright,
                EndColor = new Color(color.r, color.g, color.b, 0f),
                Lifetime = new Vector2(0.22f, 0.42f),
                Speed = new Vector2(1.0f * S, 2.4f * S) * scale,
                Angle = travel,
                Spread = 120f,
                StartSize = new Vector2(0.06f * S, 0.12f * S),
                EndSizeScale = 0.2f,
                Drag = 3.5f,
                Spin = new Vector2(90f, 300f),
            }, Mathf.Max(3, Mathf.RoundToInt(4 * scale)), Layer, Order + 3);

            CameraShake.Shake(0.014f * scale);
        }

        /// <summary>공허 화살 전용 착탄. 검보라 창흔과 두 잔상이 먼저 남고 작은 보랏빛 핵만 가산한다.</summary>
        public static void VoidPierce(Vector3 position, Vector3 direction, float scale = 1f)
        {
            float travel = direction.sqrMagnitude > 0.0001f ? AngleOf(direction) : 0f;
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            Color ink = new(0.08f, 0.025f, 0.13f, 0.9f);
            Color violet = new(0.50f, 0.22f, 0.68f, 0.68f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 echo = position - forward * (0.04f + i * 0.075f) * S * scale;
                SpriteFx.Play(VfxArt.Streak, echo, new Color(ink.r, ink.g, ink.b, 0.78f - i * 0.18f), false,
                    new Vector2(0.10f, 0.035f) * S * scale,
                    new Vector2(0.56f - i * 0.08f, 0.095f - i * 0.012f) * S * scale,
                    0.22f + i * 0.035f, travel + 180f, 0f, 0.004f, 0.38f,
                    SpriteFx.Ease.OutCubic, Layer, Order + i);
            }
            SpriteFx.Play(VfxArt.ImpactStar, position, violet, false,
                new Vector2(0.10f, 0.045f) * S * scale, new Vector2(0.58f, 0.19f) * S * scale,
                0.24f, travel, 0f, 0.004f, 0.38f, SpriteFx.Ease.OutCubic, Layer, Order + 4);
            Flash(VfxArt.SoftGlow, position, new Color(0.92f, 0.78f, 1f, 0.42f),
                0.04f * scale, 0.18f * scale, 0.10f, 0f, 0f, 7, fadeOutFrom: 0.08f);
            CameraShake.Shake(0.018f * scale);
        }

        /// <summary>
        /// 고정 피해가 덧붙는 순간. 원래 타격 위에 작게 겹치는 금빛 네 갈래 반짝임이다.
        /// 원래 타격을 가리면 안 되므로 작고 짧다.
        /// </summary>
        public static void TrueSpark(Vector3 position)
        {
            Vector3 offset = new(Random.Range(-0.18f, 0.18f) * S, Random.Range(-0.05f, 0.22f) * S, 0f);
            Flash(VfxArt.Glint, position + offset, new Color(0.38f, 0.22f, 0.08f, 0.72f), 0.48f, 0.12f, 0.32f,
                0f, 110f, 5, additive: false, fadeOutFrom: 0.38f);
            Flash(VfxArt.Glint, position + offset, new Color(1f, 0.93f, 0.62f, 0.86f), 0.34f, 0.08f, 0.26f,
                0f, 120f, 6, ease: SpriteFx.Ease.OutCubic, fadeOutFrom: 0.4f);
        }
    }
}
