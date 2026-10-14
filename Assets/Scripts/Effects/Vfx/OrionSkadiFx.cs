using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using UnityEngine;
namespace Effects.Vfx
{
    /// <summary>같은 궁수 실루엣에서 금빛 화살비와 청빙 고드름을 구분한다. 판정은 중심 낙하에 한 번.</summary>
    public static class OrionSkadiFx
    {
        public static ProjectileStyle Arrow(bool ice = false, float size = 1f)
        {
            var color = ice ? new Color(.62f, .82f, .92f, .88f) : new Color(.88f, .75f, .49f, .88f);
            return new ProjectileStyle { Hovl = HovlFx.ForElement(ice ? BaseClasses.BaseEnums.UnitElement.Cryo : BaseClasses.BaseEnums.UnitElement.Geo, size * .55f),
                Body = VfxArt.Streak, BodyColor = color, BodySize = .055f * size, BodyAspect = 4.5f,
                BodyUnderlay = VfxArt.Streak, BodyUnderlayColor = new Color(.16f,.23f,.34f,.55f), BodyUnderlayScale = 1.3f,
                RibbonColor = new Color(color.r,color.g,color.b,.42f), RibbonWidth = .025f * size, RibbonTime = .12f,
                RibbonAdditive = false, GlowColor = new Color(color.r,color.g,color.b,.12f), GlowSize = .12f,
                Duration = .30f, Acceleration = 1.2f };
        }
        public static IEnumerator Star(Unit caster, Unit target, Action impact)
        {
            CardMotion.Throw(caster, target);
            yield return ProjectileFx.Fly(caster, target, Arrow(false, 1.8f), () =>
            { impact?.Invoke(); });
        }
        public static IEnumerator Rain(Unit caster, IReadOnlyList<Unit> targets, Action impact, bool ice = false)
        {
            if (caster == null || targets == null || targets.Count == 0) yield break;
            CardMotion.Throw(caster, targets[0]);
            bool landed = false;
            foreach (var target in targets)
            {
                if (target == null || !target.isActive) continue;
                Vector3 point = VfxSpace.WorldPoint(target);
                for (int arrow = 0; arrow < 3; arrow++)
                {
                    bool center = arrow == 1;
                    Vector3 origin = point + new Vector3((arrow - 1) * VfxSpace.CardSize * .22f, VfxSpace.CardSize * (1.25f + .10f * arrow), 0);
                    caster.StartCoroutine(ProjectileFx.FlyFrom(origin, target, Arrow(ice, center ? 1.5f : .8f), () =>
                    {
                        if (!center) return;
                        if (!landed) { landed = true; impact?.Invoke(); }
                    }, VfxSpace.SortingLayer(caster)));
                }
            }
            yield return new WaitForSeconds(.42f);
            if (!landed) impact?.Invoke();
        }
    }
}
