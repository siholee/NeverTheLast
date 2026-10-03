using System;
using System.Collections;
using Entities;
using UnityEngine;

namespace Effects.Vfx.Presets
{
    /// <summary>
    /// 교체 전 시의 청록 비행 참격. 카드 중앙에서 대상 중앙으로 호를 그려 날고 착탄 프레임에 한 번 판정한다.
    /// 새 캐릭터가 투사체형 검기를 필요로 할 때 한 줄로 재사용하도록 보관한다.
    /// </summary>
    public static class FlyingSlashFx
    {
        public static IEnumerator Play(Unit caster, Unit target, Action onImpact, float scale = 1f)
        {
            if (caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 origin = VfxSpace.WorldPoint(caster);
            Vector3 end = VfxSpace.WorldPoint(target);
            Vector3 direction = (end - origin).normalized;
            float size = VfxSpace.CardSize;
            int layer = VfxSpace.SortingLayer(caster);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            BattlefieldBackdrop.PushFocus(0.46f, 0.025f, 0.12f, 0.22f,
                new Color(0.025f, 0.18f, 0.20f), origin, end);
            SpriteFx.Play(VfxArt.Streak, origin - direction * size * 0.04f,
                new Color(0.018f, 0.19f, 0.22f, 0.70f), false,
                new Vector2(size * 0.18f, size * 0.035f), new Vector2(size * 0.92f, size * 0.10f),
                0.16f, angle + 180f, 0f, 0.01f, 0.30f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 1);
            SpriteFx.Play(VfxArt.Streak, origin, new Color(0.84f, 1f, 0.98f, 0.96f), true,
                new Vector2(size * 0.10f, size * 0.018f), new Vector2(size * 0.64f, size * 0.040f),
                0.11f, angle + 180f, 0f, 0f, 0.35f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 2);

            var style = new ProjectileStyle
            {
                Body = VfxArt.ShiTealSlash,
                BodyColor = Color.white,
                BodyAdditive = false,
                BodySize = 0.74f * scale,
                BodyAspect = 1.18f,
                BodyRotationOffset = -44f,
                BodyUnderlay = VfxArt.ShiTealSlash,
                BodyUnderlayColor = new Color(0.01f, 0.15f, 0.18f, 0.34f),
                BodyUnderlayScale = 1.08f,
                RibbonColor = new Color(0.02f, 0.36f, 0.38f, 0.42f),
                RibbonWidth = 0.030f * scale,
                RibbonTime = 0.13f,
                RibbonAdditive = false,
                Duration = 0.24f,
                ArcHeight = 0.24f,
                Acceleration = 1.62f,
                OnImpact = (point, travel) => Impact(point, travel, layer, scale),
            };

            yield return ProjectileFx.Fly(caster, target, style, onImpact);
        }

        private static void Impact(Vector3 point, Vector3 direction, int layer, float scale)
        {
            float size = VfxSpace.CardSize * scale;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            BattlefieldBackdrop.PulseFlash(new Color(0.84f, 1f, 0.98f), 0.075f, 0.08f);
            SpriteFx.Play(VfxArt.ShiTealSlash, point, new Color(0.02f, 0.22f, 0.24f, 0.52f), false,
                new Vector2(size * 0.32f, size * 0.32f), new Vector2(size * 1.15f, size * 1.15f),
                0.19f, angle + 134f, 0f, 0f, 0.34f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 3);
            SpriteFx.Play(VfxArt.Streak, point, new Color(0.84f, 1f, 0.98f, 0.96f), true,
                new Vector2(size * 0.28f, size * 0.025f), new Vector2(size * 1.10f, size * 0.055f),
                0.13f, angle + 90f, 0f, 0f, 0.42f, SpriteFx.Ease.OutCubic,
                layer, VfxSpace.OverlaySortingOrder + 5);
            SpriteFx.Play(VfxArt.ImpactStar, point, new Color(0.05f, 0.82f, 0.78f, 0.94f), true,
                size * 0.10f, size * 0.52f, 0.14f, angle, 0f, 0f, 0.40f,
                SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 6);
        }
    }
}
