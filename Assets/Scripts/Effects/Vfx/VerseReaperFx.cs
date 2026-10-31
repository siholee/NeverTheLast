using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>시·사바흐의 구매 검격과 작은 분산 착탄. 전투 판정·행동 대기는 호출자가 소유한다.</summary>
    internal static class VerseReaperFx
    {
        internal static bool OwnPart(string id,Unit caster)
            => caster!=null && (caster.ID==2 && (id?.StartsWith("cast.shi.")==true || id?.StartsWith("hit.shi.")==true)
                || caster.ID==3 && (id?.StartsWith("cast.sabah.")==true || id?.StartsWith("hit.sabah.")==true));

        internal static void Palette(Unit caster,ref Color body,ref Color core)
        {
            float alpha=body.a,lightAlpha=core.a;
            body=ElementPalette.ColorFor(caster.Element);body.a=alpha;
            core=Color.Lerp(ElementPalette.ColorFor(caster.Element),Color.white,.22f);core.a=lightAlpha;
        }

        internal static Color Edge(Unit caster)
        {var color=ElementPalette.ColorFor(caster.Element)*.18f;color.a=.92f;return color;}

        internal static Vector3 Point(Unit target)
        {
            var view=target!=null?CardMotion.ViewOf(target):null;
            if(view?.PortraitRenderer==null||view.PortraitRenderer.sprite==null)return VfxSpace.WorldPoint(target);
            Vector3 point=VfxSpace.BodyContactPoint(target);
            point.y=Mathf.Max(VfxSpace.PortraitHudTop(target)+.055f*VfxSpace.CardSize,point.y-.16f*VfxSpace.CardSize);
            return view.SnapPortraitContactPoint(point,VfxSpace.PortraitHudTop(target));
        }

        internal static void Contact(Unit caster,Unit target,float weight=.55f,bool particles=false)
        {
            if(caster==null||caster.ID is not (2 or 3)||!caster.isActive||caster.isControlled||target==null||!target.isActive)return;
            PurchasedArcherFx.RosterContact(caster,target,weight,elementalInk:true);
            // 만종은 Bonus/Distributed라 공통 직접 피격 분산층에서 제외된다. 그 경로만 수동으로 한 번 그린다.
            if(particles)PurchasedImpactParticles.Play(target,VfxSpace.AimFrom(caster,target).Direction,
                weight,ElementPalette.ColorFor(caster.Element),.76f,centered:true,source:caster);
        }
    }
}
