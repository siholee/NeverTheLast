using System;
using System.Collections;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>투사체 한 발의 모양과 궤적. 크기는 전부 카드 한 변 대비 비율이다.</summary>
    public sealed class ProjectileStyle
    {
        /// <summary>지정하면 Hovl Flash · Projectile · Hit이 주 연출이 되고 아래 스프라이트는 보조층만 맡는다.</summary>
        public HovlProjectileSet Hovl;
        public Sprite Body = VfxSpace.HardDot;
        public Color BodyColor = Color.white;
        public bool BodyAdditive;
        public float BodySize = 0.16f;
        /// <summary>몸체의 가로:세로 비. 1이면 정사각, 창끝 · 혜성은 가로로 길다.</summary>
        public float BodyAspect = 1f;
        /// <summary>몸체 그림이 바라보는 방향 보정(도). 머리가 왼쪽을 향한 텍스처면 180.</summary>
        public float BodyRotationOffset;

        /// <summary>밝은 배경에서 가산 몸체의 모양을 받치는 알파 외곽. 알파가 0이면 그리지 않는다.</summary>
        public Sprite BodyUnderlay;
        public Color BodyUnderlayColor = new(1f, 1f, 1f, 0f);
        public float BodyUnderlayScale = 1.22f;

        /// <summary>몸체 앞을 감싸는 공기막/원소 껍질. 알파가 0이면 그리지 않는다.</summary>
        public Sprite ShellSprite;
        public Color ShellColor = new(1f, 1f, 1f, 0f);
        public bool ShellAdditive = true;
        public float ShellSize = 0.28f;
        public float ShellAspect = 1f;
        public float ShellRotationOffset;

        /// <summary>몸체 뒤에 겹치는 빛무리. 알파가 0이면 그리지 않는다.</summary>
        public Color GlowColor = new(1f, 1f, 1f, 0f);
        public float GlowSize = 0.34f;
        /// <summary>빛무리 그림. 비우면 절차 원광을 쓴다.</summary>
        public Sprite GlowSprite;

        /// <summary>
        /// 끊기지 않는 궤적 띠(TrailRenderer). 알파가 0이면 달지 않는다. 점 꼬리만으로는 빠른 투사체의
        /// 길이 한 획으로 읽히지 않는다 — 원신 TCG의 원소 궤적처럼 선이 남아야 한다.
        /// </summary>
        public Color RibbonColor = new(1f, 1f, 1f, 0f);
        /// <summary>띠의 머리 쪽 두께(카드 대비).</summary>
        public float RibbonWidth = 0.08f;
        /// <summary>띠가 남는 시간(초).</summary>
        public float RibbonTime = 0.16f;
        public bool RibbonAdditive = true;

        /// <summary>비행 시간(초). 거리와 무관하게 고정해 박자를 맞춘다.</summary>
        public float Duration = 0.36f;
        /// <summary>포물선 높이(거리 대비). 0이면 직선이다.</summary>
        public float ArcHeight;
        /// <summary>
        /// 대상 카드 중심에서 벗어난 착탄점(카드 한 변 대비). 흡수·바닥 효과처럼 얼굴이 아닌
        /// 발밑으로 가야 하는 투사체만 지정하고, 일반 공격은 계속 정중앙을 쓴다.
        /// </summary>
        public Vector3 TargetOffset;
        /// <summary>몸체 회전 속도(도/초). 0이면 진행 방향을 바라본다.</summary>
        public float Spin;
        /// <summary>
        /// 가속 정도. 1이면 등속, 클수록 끝에서 빨라진다(던진 물체가 꽂히는 느낌).
        /// </summary>
        public float Acceleration = 1.15f;

        /// <summary>비행 중 흘리는 꼬리. 스프라이트가 없으면 흘리지 않는다.</summary>
        public ParticleSpec Trail;
        /// <summary>꼬리를 흘리는 간격(월드 거리).</summary>
        public float TrailSpacing = 0.45f;

        /// <summary>발사 순간 출발점의 연출. (위치, 진행 방향)</summary>
        public Action<Vector3, Vector3> OnLaunch;
        /// <summary>착탄 연출. (위치, 진행 방향)</summary>
        public Action<Vector3, Vector3> OnImpact;
    }

    /// <summary>
    /// 투사체. <b>카드 정중앙에서 출발해 카드 정중앙에 꽂힌다</b>(하스스톤 기준).
    ///
    /// 착탄점은 비행 내내 대상 카드를 따라간다 — 대상 카드가 맞아서 밀리거나 날아가는 중이어도
    /// 빗나가 보이지 않는다. 피해는 <b>착탄하는 순간</b> 콜백으로 들어간다. 미리 재 둔 시간으로
    /// 기다리면 대상이 먼저 쓰러져 빈자리로 날아가는 꼴이 된다.
    /// </summary>
    public static class ProjectileFx
    {
        /// <summary>기본 비접촉 공격용 Hovl 세트. 비가산 받침과 짧은 리본이 흰 전장에서 실루엣을 남긴다.</summary>
        public static ProjectileStyle ElementStyle(BaseEnums.UnitElement element, float scale = 1f)
        {
            HovlProjectileSet hovl = HovlFx.ForElement(element, scale);
            return new ProjectileStyle
            {
                Hovl = hovl,
                Body = null,
                BodyUnderlay = VfxArt.Streak,
                BodyUnderlayColor = hovl.UnderlayColor,
                BodyUnderlayScale = 1f,
                BodySize = hovl.UnderlaySize,
                BodyAspect = hovl.UnderlayAspect,
                RibbonColor = new Color(hovl.UnderlayColor.r, hovl.UnderlayColor.g, hovl.UnderlayColor.b,
                    hovl.UnderlayColor.a * 0.72f),
                RibbonWidth = 0.024f * scale,
                RibbonTime = 0.12f,
                RibbonAdditive = false,
                Duration = 0.30f,
                Acceleration = 1.28f,
            };
        }

        public static IEnumerator Fly(Unit from, Unit to, ProjectileStyle style, Action onImpact)
            => FlyFrom(VfxSpace.WorldPoint(from), to, style, onImpact, VfxSpace.SortingLayer(from));

        public static IEnumerator FlyFrom(Vector3 origin, Unit to, ProjectileStyle style, Action onImpact, int sortingLayer)
        {
            if (to == null || style == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            float cardSize = VfxSpace.CardSize;
            var root = new GameObject("Projectile");
            root.transform.position = origin;
            // 안전망 — 시전자가 쓰러지거나 스테이지가 다시 불려 이 코루틴이 도중에 멈추면 몸체가
            // 허공에 멈춘 채 남았다(Play 모드 확인). 정상 착탄보다 넉넉히 늦게 스스로 걷힌다.
            UnityEngine.Object.Destroy(root, Mathf.Max(0.05f, style.Duration) + 2f);

            HovlProjectileSet hovl = style.Hovl;
            SpriteRenderer glow = null;
            if (style.GlowColor.a > 0f)
            {
                glow = VfxSpace.NewSprite(root.transform, "Glow", style.GlowSprite != null ? style.GlowSprite : VfxSpace.Glow, style.GlowColor,
                    sortingLayer, VfxSpace.OverlaySortingOrder, additive: true);
                glow.transform.localScale = Vector3.one * style.GlowSize * cardSize;
            }
            SpriteRenderer underlay = null;
            if (style.BodyUnderlay != null && style.BodyUnderlayColor.a > 0f)
            {
                underlay = VfxSpace.NewSprite(root.transform, "BodyUnderlay", style.BodyUnderlay,
                    style.BodyUnderlayColor, sortingLayer, VfxSpace.OverlaySortingOrder, additive: false);
            }
            SpriteRenderer body = null;
            if (style.Body != null && style.BodyColor.a > 0f)
            {
                body = VfxSpace.NewSprite(root.transform, "Body", style.Body, style.BodyColor,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 1, style.BodyAdditive);
            }
            SpriteRenderer shell = null;
            if (style.ShellSprite != null && style.ShellColor.a > 0f)
            {
                shell = VfxSpace.NewSprite(root.transform, "Shell", style.ShellSprite, style.ShellColor,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 2, style.ShellAdditive);
            }
            TrailRenderer ribbon = null;
            if (style.RibbonColor.a > 0f)
            {
                Color tail = style.RibbonColor;
                tail.a = 0f;
                ribbon = VfxArt.NewTrail(root.transform, style.RibbonColor, tail, style.RibbonWidth * cardSize,
                    style.RibbonTime, sortingLayer, VfxSpace.OverlaySortingOrder - 1, style.RibbonAdditive);
            }

            Vector3 lastEnd = VfxSpace.WorldPoint(to) + style.TargetOffset * cardSize;
            Vector3 launchDirection = (lastEnd - origin).normalized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("projectile_launch");
#endif
            style.OnLaunch?.Invoke(origin, launchDirection);
            GameObject hovlProjectile = null;
            if (hovl != null)
            {
                HovlFx.PlayFlash(hovl, origin, launchDirection, cardSize,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 4);
                hovlProjectile = HovlFx.SpawnProjectile(hovl, origin, launchDirection,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 3);
            }

            float elapsed = 0f;
            // 비행체는 출발·중간·착탄의 세 위치가 있어야 방향이 읽힌다.
            float duration = VfxTiming.AtLeastFrames(Mathf.Max(0.05f, style.Duration), 3);
            float rotation = UnityEngine.Random.Range(0f, 360f);
            float trailDistance = 0f;
            Vector3 previous = origin;
            float bodySize = style.BodySize * cardSize;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.Pow(t, Mathf.Max(0.1f, style.Acceleration));

                if (to != null && to.isActive)
                    lastEnd = VfxSpace.WorldPoint(to) + style.TargetOffset * cardSize;
                Vector3 position = Arc(origin, lastEnd, style.ArcHeight, eased);
                root.transform.position = position;

                Vector3 delta = position - previous;
                if (style.Spin != 0f) rotation += style.Spin * Time.deltaTime;
                else if (delta.sqrMagnitude > 0.0001f) rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                if (body != null)
                    body.transform.localRotation = Quaternion.Euler(0f, 0f, rotation + style.BodyRotationOffset);
                if (underlay != null)
                    underlay.transform.localRotation = Quaternion.Euler(0f, 0f, rotation + style.BodyRotationOffset);
                if (shell != null)
                    shell.transform.localRotation = Quaternion.Euler(0f, 0f, rotation + style.ShellRotationOffset);

                // 나올 때 작게 튀어나와 금세 제 크기가 된다. 처음부터 다 큰 채로 나타나면 '생겼다'가 안 보인다.
                float pop = Mathf.Min(1f, t / 0.15f);
                float grow = bodySize * Mathf.Lerp(0.35f, 1f, EaseOutBack(pop));
                if (body != null)
                    body.transform.localScale = new Vector3(grow * style.BodyAspect, grow, 1f);
                if (underlay != null)
                    underlay.transform.localScale = new Vector3(grow * style.BodyAspect, grow, 1f) * style.BodyUnderlayScale;
                if (shell != null)
                {
                    float shellGrow = style.ShellSize * cardSize * Mathf.Lerp(0.2f, 1f, EaseOutBack(pop));
                    shell.transform.localScale = new Vector3(shellGrow * style.ShellAspect, shellGrow, 1f);
                }

                Vector3 visualDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : launchDirection;
                HovlFx.MoveProjectile(hovlProjectile, position, visualDirection,
                    hovl != null ? hovl.RotationOffset : 0f);

                if (style.Trail.Sprite != null)
                {
                    trailDistance += delta.magnitude;
                    while (trailDistance >= style.TrailSpacing)
                    {
                        trailDistance -= style.TrailSpacing;
                        ParticleSpec trail = style.Trail;
                        // 꼬리는 진행 반대 방향으로 흘러야 속도가 읽힌다.
                        if (delta.sqrMagnitude > 0.0001f)
                            trail.Angle = Mathf.Atan2(-delta.y, -delta.x) * Mathf.Rad2Deg;
                        VfxParticles.Burst(position, trail, 1, sortingLayer, VfxSpace.OverlaySortingOrder - 1);
                    }
                }

                previous = position;
                yield return null;
            }

            Vector3 direction = (lastEnd - Arc(origin, lastEnd, style.ArcHeight, 0.9f)).normalized;
            VfxArt.ReleaseTrail(ribbon);
            HovlFx.StopAndRelease(hovlProjectile, 0.02f);
            UnityEngine.Object.Destroy(root);
            Effects.BattlefieldBackdrop.PushFocus(0.20f, 0f, 0.08f, 0.20f,
                new Color(0.022f, 0.048f, 0.078f, 1f), lastEnd);
            Effects.BattlefieldBackdrop.PulseFlash(Color.white, 0.035f, 0.065f);
            if (hovl != null)
            {
                HovlFx.PlayHit(hovl, lastEnd, direction, cardSize,
                    sortingLayer, VfxSpace.OverlaySortingOrder + 5);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("impact");
#endif
            style.OnImpact?.Invoke(lastEnd, direction);
            onImpact?.Invoke();
        }

        /// <summary>두 점을 잇는 포물선. 높이는 거리에 비례하고 화면 위쪽으로 솟는다.</summary>
        public static Vector3 Arc(Vector3 from, Vector3 to, float heightRatio, float t)
        {
            Vector3 point = Vector3.LerpUnclamped(from, to, t);
            if (heightRatio != 0f)
                point.y += Vector3.Distance(from, to) * heightRatio * 4f * t * (1f - t);
            return point;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
