using System;
using System.Collections;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>구매 원소 외피를 기존 비행 시간에 맞춘다. 피해·상태 판정은 전달받은 콜백만 소유한다.</summary>
    public static class PurchasedProjectileFx
    {
        // B06의 낮은 흉부 접점만 보정한다. 승인된 B05/기존 원소 접점은 그대로 둔다.
        internal static Vector3 ContactFor(Unit caster,Unit target,bool elementalInk)
        {
            Vector3 point=elementalInk?VfxSpace.BodyContactPoint(target):VfxSpace.ContactPoint(target);
            if(!elementalInk||caster==null||caster.ID is not (120 or 220))return point;
            var view=CardMotion.ViewOf(target);
            if(view?.PortraitRenderer==null||view.PortraitRenderer.sprite==null)return point;
            point.y=Mathf.Max(VfxSpace.PortraitHudTop(target)+.055f*VfxSpace.CardSize,point.y-.16f*VfxSpace.CardSize);
            return view.SnapPortraitContactPoint(point,VfxSpace.PortraitHudTop(target));
        }

        public static IEnumerator Fly(Unit caster,Unit target,float seconds,Action onImpact,
            float weight=.85f,Unit bounceFrom=null,Vector3? lastBouncePoint=null,bool paintContact=true,
            bool minimumFlightFrames=true,bool elementalInk=false)
        {
            if(caster==null||target==null){onImpact?.Invoke();yield break;}
            float card=VfxSpace.CardSize;
            float duration=minimumFlightFrames?VfxTiming.AtLeastFrames(Mathf.Max(.05f,seconds),3):Mathf.Max(.05f,seconds);
            float flight=Mathf.Min(elementalInk?.16f:.12f,duration),charge=duration-flight,age=0f;
            Vector3 ContactOrCenter(Unit unit)
            {
                var portrait=unit!=null?CardMotion.ViewOf(unit)?.PortraitRenderer:null;
                return portrait!=null&&portrait.sprite!=null
                    ?ContactFor(caster,unit,elementalInk):VfxSpace.WorldPoint(unit);
            }
            Vector3 Origin()
            {
                var view=bounceFrom!=null?CardMotion.ViewOf(bounceFrom):null;
                if(bounceFrom!=null||lastBouncePoint.HasValue)
                    return bounceFrom!=null&&bounceFrom.isActive&&view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null
                        ?VfxSpace.ContactPoint(bounceFrom):lastBouncePoint??ContactOrCenter(bounceFrom);
                if(caster==null)return lastBouncePoint??Vector3.zero;
                return PurchasedArcherFx.EmissionPoint(caster,target);
            }
            Vector3 origin=Origin(),end=ContactOrCenter(target),point=origin;
            var paint=PurchasedArcherFx.RosterFlight(caster,target,point,weight);
            // 비행 그림의 .35 가로 압축을 외피까지 상속하면 빛덩어리가 한 픽셀 선으로 줄어든다.
            if(elementalInk)paint?.StretchEnvelope(Vector2.one,Vector2.one,Vector2.one);
            var body=paint?.transform.Find("Body")?.GetComponent<SpriteRenderer>();
            var envelope=paint!=null?PurchasedArrowEnvelope.Attach(paint,target,card,weight*(elementalInk?1.15f:1f),
                tint:ElementPalette.ColorFor(caster.Element),arc:elementalInk,contrast:elementalInk,
                saturated:caster.ID is 120 or 220):null;
            paint?.Follow(()=>point,Vector3.zero,lateFollow:true).BindRecipient(target);
            if(paint!=null)UnityEngine.Object.Destroy(paint.gameObject,duration+2f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(onImpact!=null)Core.VerifyEvents.Raise("projectile_launch");
#endif
            try
            {
                // 기존 ProjectileFx와 같은 deltaTime 누적으로 도착 시각을 보존한다.
                while(age<duration)
                {
                    if(Time.timeScale<=0f){yield return null;continue;}
                    age+=Time.deltaTime;
                    if(age<=charge)origin=Origin();
                    var view=target!=null?CardMotion.ViewOf(target):null;
                    if(target!=null&&target.isActive&&view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null)
                        end=ContactFor(caster,target,elementalInk);
                    float travel=Mathf.Clamp01((age-charge)/flight),t=Mathf.Lerp(travel,travel*travel,.75f);
                    Vector3 direction=end-origin;Vector3 normal=new Vector3(-direction.y,direction.x,0f).normalized;
                    point=Vector3.Lerp(origin,end,t)+normal*(Mathf.Sin(t*Mathf.PI)*card*.035f);
                    Vector3 tangent=direction+normal*(Mathf.Cos(t*Mathf.PI)*card*.035f*Mathf.PI);
                    if(paint!=null)
                    {
                        paint.transform.position=point;
                        paint.AimDegrees(Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);
                        if(body!=null)body.enabled=false;
                        var sourceView=caster!=null?CardMotion.ViewOf(caster):null;
                        bool visible=caster!=null&&caster.isActive&&sourceView?.PortraitRenderer!=null&&sourceView.PortraitRenderer.sprite!=null&&
                            target!=null&&target.isActive&&
                            view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null&&view.gameObject.activeInHierarchy;
                        paint.gameObject.SetActive(visible);
                        if(visible)envelope?.Advance(age,travel,point,0);
                    }
                    yield return null;
                }
            }
            finally
            {
                envelope?.Dispose();
                if(paint!=null){paint.gameObject.SetActive(false);UnityEngine.Object.Destroy(paint.gameObject);}
            }
            var endView=target!=null?CardMotion.ViewOf(target):null;
            if(paintContact&&caster!=null&&caster.isActive&&target!=null&&target.isActive&&
                endView?.PortraitRenderer!=null&&endView.PortraitRenderer.sprite!=null&&endView.gameObject.activeInHierarchy)
                PurchasedArcherFx.RosterContact(caster,target,weight*(elementalInk?.80f:.70f),elementalInk);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(onImpact!=null)Core.VerifyEvents.Raise("impact");
#endif
            onImpact?.Invoke();
        }
    }
}
