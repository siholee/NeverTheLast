using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Entities;
using Effects.Vfx.Presets;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>동일 구매 세트의 완성 프리팹을 베이크해 재생한다. 피해·행동 대기는 기존 슬롯이 소유한다.</summary>
    public static class PurchasedArcherFx
    {
        private sealed class Volley
        {
            public readonly List<Arrow> Arrows=new();
            public readonly List<FlipbookFx> Casts=new();
            public bool Cancelled,Area,Pursuit,Seed,Arc;
            public Color Tint;
            public Vector3 Origin;
            public Vector3 ContactDirection;
            public float StartedAt;
        }
        private sealed class Arrow
        {
            public Unit Target;
            public FlipbookFx Paint;
            public Vector3 Point;
            public Vector3 Tangent;
            public bool Landed;
            public PurchasedArrowEnvelope Envelope;
        }
        private static readonly ConditionalWeakTable<Unit,Volley> Volleys=new();
        private static readonly float[] Delays=FrameDelays();
        private static readonly float[] ImpactDelays=ImpactFrameDelays();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static int EmittedArrows { get; private set; }
        internal static int ResolvedContacts { get; private set; }
#endif
        private static float[] FrameDelays(float crest=1f/60f)
        {var frames=new float[48];for(int i=0;i<frames.Length;i++)frames[i]=1f/60f;frames[3]=crest;return frames;}
        private static float[] ImpactFrameDelays()
        {
            var original=FrameDelays(.12f);
            original[1]=original[2]=original[4]=.025f;original[5]=.035f;original[6]=.16f;
            var frames=new float[96];
            // 흰 정점은 .11초 안에 끝내고 남은 코어는 .25초까지 부위별로 푼다.
            for(int i=0;i<14;i++)frames[i]=i<6?1f/120f:i<8?.030f:i<10?.018f:i<12?.022f:.030f;
            float previousCore=0f,core=0f;
            for(int i=0;i<14;i++){previousCore+=original[i/2]*.5f;core+=frames[i];}
            // 줄인 정점 시간은 실제 구매 파편82셀에 분배해 장식의 총 수명은 보존한다.
            for(int i=14;i<frames.Length;i++)frames[i]=1f/90f+(previousCore-core)/(frames.Length-14);
            return frames;
        }

        public static bool SupportsProjectile(string key)=>key is "atalante.normal" or "atalante.ultimate" or
            "neith.normal" or "nephthys.normal" or "nephthys.ultimate";
        public static bool IsArea(string key)=>key is "atalante.ultimate" or "nephthys.ultimate";

        public static void Cast(Unit caster,IReadOnlyList<Unit> targets,string key)
        {
            if(caster==null||targets==null||targets.Count==0)return;
            if(Volleys.TryGetValue(caster,out var old))
            {
                PoisonBubblesFx.CancelPursuit(old);
                old.Cancelled=true;foreach(var arrow in old.Arrows)Release(arrow);
                foreach(var oldCast in old.Casts)if(oldCast!=null)Object.Destroy(oldCast.gameObject);
                Volleys.Remove(caster);
            }
            var volley=new Volley{StartedAt=Time.time,Area=IsArea(key),Seed=caster.ID==164,Arc=caster.ID==166,
                Tint=ElementPalette.ColorFor(caster.Element),Pursuit=
                caster.ID==165&&Managers.GameManager.Instance?.ActionScheduler?.ActingUnit==caster&&
                Managers.GameManager.Instance?.ActionScheduler?.ActingKind==Managers.ActionScheduler.ActionKind.Additional};Volleys.Add(caster,volley);
            float card=VfxSpace.CardSize;
            Vector3 origin=Origin(caster,targets[0]);
            volley.Origin=origin;
            float angle=Angle(origin,VfxSpace.ContactPoint(targets[0]));
            BattlefieldBackdrop.PushFocus(volley.Area?.28f:.20f,.04f,.16f,.24f,
                caster.ID==165?new Color(.07f,.16f,.04f):new Color(volley.Tint.r*.10f,volley.Tint.g*.20f,volley.Tint.b*.10f),origin,VfxSpace.ContactPoint(targets[0]));
            var slot=VfxComposer.Slot(key,"hit");var part=VfxDefinitionCatalog.Part(slot?.partId);
            int peak=slot!=null&&slot.peakFrame>=0?slot.peakFrame:part?.peakFrame??3;
            float arrival=VfxComposer.Offset(key,"hit")+(slot!=null&&slot.impactDelay>=0?slot.impactDelay:
                Mathf.Max(0,peak-(part?.startFrame??0))/Mathf.Max(1f,part?.fps??30f));
            float releaseAt=Mathf.Clamp(arrival-.12f,.08f,.22f);
            var view=CardMotion.ViewOf(caster);
            view?.PlayRangedWindup(VfxSpace.AimFrom(caster,targets[0]).Direction,releaseAt);
            view?.PulseVfxLight(volley.Tint,.35f,releaseAt+.14f);
            var cast=PlayPreparation(caster,targets[0],angle,volley.Area?1.2f:1f,releaseAt);
            volley.Casts.Add(cast);
            VfxRunner.Run(ReleaseFlash(caster,volley,angle,releaseAt));
            int targetIndex=0;
            foreach(Unit target in targets)
            {
                if(target==null||!target.isActive)continue;
                if(volley.Pursuit)PoisonBubblesFx.HoldForPursuit(target,volley);
                int count=volley.Seed?(volley.Area?3:1):volley.Arc||volley.Pursuit?1:volley.Area?5:3;
                for(int i=0;i<count;i++)
                {
                    var arrow=new Arrow{Target=target};volley.Arrows.Add(arrow);
                    VfxRunner.Run(Fly(caster,volley,arrow,i,Mathf.Max(.14f,arrival),targetIndex));
                }
                targetIndex++;
            }
        }
        private static float Angle(Vector3 from,Vector3 to)=>Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg;
        private static FlipbookFx PlayPreparation(Unit caster,Unit target,float angle,float weight,float releaseAt)
        {
            var element=ElementPalette.ColorFor(caster.Element);
            var style=new FlipbookCompositeStyle
            {
                BlendFrames=true,PeakAlpha=.90f,AlphaGain=1.35f,Desaturate=1f,CoverageCoreStrength=.65f,
                GlowAlpha=.10f,GlowColor=Color.Lerp(element,Color.white,.6f),GlowThreshold=.82f,
                StartRotationDegrees=angle,StartScale=.72f,CrestScale=1f,EndScale=1.06f,
                CrestAtFraction=.25f,FadeInFraction=0f,FadeOutFraction=.44f,
                GroundLightSizeInCards=Vector2.zero,ContactShadowSizeInCards=Vector2.zero,
                SceneLightStrength=0f,BrightBackdropLiftStrength=0f,RequiredVisibleFrame=2,
            };
            var cast=FlipbookFx.Play("VfxTex/PurchasedArcher/Cast",8,3,new Vector2(.50f,.50f),
                EmissionPoint(caster,target),Vector2.one*VfxSpace.CardSize*3f*weight,element,
                false,false,false,PreparationDelays(releaseAt),VfxSpace.SortingLayer(caster),VfxSpace.OverlaySortingOrder+161,
                style:style,frameCount:20);
            if(cast==null)return null;
            PurchasedArcherCastAnchor.Bind(cast,caster,target);
            return cast.BindRecipient(caster);
        }
        private static float[] PreparationDelays(float releaseAt)
        {
            var frames=new float[20];for(int i=0;i<frames.Length;i++)frames[i]=1f/60f;
            // 시전의 방향성 정점을 당김 동안 유지한다. 피해와 행동의 도착 시간은 바꾸지 않는다.
            frames[2]=Mathf.Max(1f/60f,releaseAt-.05f);return frames;
        }
        private static IEnumerator ReleaseFlash(Unit caster,Volley volley,float angle,float releaseAt)
        {
            yield return new WaitForSeconds(releaseAt);
            if(volley.Cancelled||caster==null||!caster.isActive||volley.Arrows.TrueForAll(a=>a.Landed))yield break;
            volley.Origin=EmissionPoint(caster,volley.Arrows[0].Target);
            var flash=Play("Cast",volley.Origin,Vector2.one*VfxSpace.CardSize*(volley.Area?1.35f:1.25f),angle,caster,166,.15f,volley.Tint);
            if(flash!=null){PurchasedArcherCastAnchor.Bind(flash,caster,volley.Arrows[0].Target);flash.BindRecipient(caster);}
            volley.Casts.Add(flash);
        }
        private static Vector3 Origin(Unit caster,Unit target)=>EmissionPoint(caster,target);
        /// <summary>투명 여백·배지 바깥이 아닌 실제 원화의 가슴과 손앞에서 모으고 발사한다.</summary>
        public static Vector3 EmissionPoint(Unit caster,Unit target)
        {
            float side=VfxSpace.AimFrom(caster,target).Direction.x>=0?1f:-1f;
            var view=CardMotion.ViewOf(caster);
            if (view?.StageProxy != null) return VfxSpace.CastPoint(caster, target);
            if(view?.PortraitRenderer!=null)
            {
                Bounds visible=view.VisiblePortraitBounds();
                // 지팡이 원화의 발사점은 낮은 몸통 오른쪽이 아니라 실제 좌측 상단 끝이다.
                if(caster.ID==83&&view.PortraitRenderer.sprite!=null)
                    return new Vector3(view.PortraitRenderer.flipX?visible.max.x-visible.size.x*.16f:visible.min.x+visible.size.x*.16f,
                        visible.max.y-visible.size.y*.12f,0f);
                return new Vector3(visible.center.x+side*visible.size.x*.34f,
                    Mathf.Lerp(VfxSpace.PortraitHudTop(caster),visible.max.y,.22f),0f);
            }
            return VfxSpace.CastPoint(caster,target);
        }
        private static IEnumerator Fly(Unit caster,Volley volley,Arrow arrow,int index,float arrival,int targetIndex)
        {
            float delay=volley.Area?.012f+index*.004f+targetIndex*.002f:index*.006f;
            yield return new WaitForSeconds(delay);
            if(volley.Cancelled||arrow.Landed||caster==null||!caster.isActive||arrow.Target==null||!arrow.Target.isActive)yield break;
            float card=VfxSpace.CardSize,lane=volley.Arc||volley.Pursuit||volley.Seed&&!volley.Area?0f:index-(volley.Seed?1f:volley.Area?2f:1f);
            float weight=volley.Seed?.68f:volley.Arc?.9f:volley.Pursuit?1.15f:volley.Area?(index==2?1.1f:.82f):index==1?1f:.85f;
            Vector3 direction=(VfxSpace.ContactPoint(arrow.Target)-volley.Origin).normalized;
            Vector3 normal=new Vector3(-direction.y,direction.x,0f);
            Vector3 bend=normal.y<0f?-normal:normal;
            Vector3 landing=normal*lane*card*.035f;
            Vector3 origin=volley.Origin+normal*lane*.055f*card;
            Vector3 end=VfxSpace.ContactPoint(arrow.Target)+landing;
            // 시전에서 대상까지 바로 뻗는 작은 호. 높이 솟거나 도착 직전 수직으로 꺾지 않는다.
            float arc=card*((volley.Area?.10f:.045f)+lane*.035f);
            arrow.Point=origin;
            arrow.Paint=Play("Flight",origin,new Vector2(.48f,1.7f)*card*weight,Angle(origin,end),arrow.Target,162,.80f,volley.Tint);
            if(arrow.Paint==null)yield break;
            arrow.Paint.StretchEnvelope(Vector2.one,Vector2.one,Vector2.one);
            arrow.Envelope=PurchasedArrowEnvelope.Attach(arrow.Paint,arrow.Target,card,weight,volley.Pursuit,volley.Area,volley.Tint,volley.Seed,volley.Arc);
            arrow.Paint.Follow(()=>arrow.Point,Vector3.zero,lateFollow:true).BindRecipient(arrow.Target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmittedArrows++;
#endif
            float duration=Mathf.Max(.06f,arrival-delay),age=0f;
            float flight=Mathf.Min(duration,.12f);
            float charge=duration-flight;
            var body=arrow.Paint.transform.Find("Body").GetComponent<SpriteRenderer>();
            body.enabled=false;
            try
            {
                while(!volley.Cancelled&&!arrow.Landed&&caster!=null&&caster.isActive&&arrow.Target!=null&&arrow.Target.isActive&&arrow.Paint!=null&&age<duration)
                {
                    if(Time.timeScale<=0f){yield return null;continue;}
                    // 생성 코루틴의 한 프레임 지연을 보상하고 현재 렌더 구간의 중간 위치를 그린다.
                    age=Mathf.Max(0f,Time.time-volley.StartedAt-delay+Mathf.Min(.02f,Time.deltaTime*.5f));
                    float travel=Mathf.Clamp01((age-charge)/flight);
                    if(travel<=0f)origin=EmissionPoint(caster,arrow.Target)+normal*lane*.055f*card;
                    float t=Mathf.Lerp(travel,travel*travel,.75f),u=1f-t;
                    end=VfxSpace.ContactPoint(arrow.Target)+landing;
                    Vector3 control=Vector3.Lerp(origin,end,.28f)+bend*arc;
                    Vector3 approach=Vector3.Lerp(origin,end,.74f)+bend*arc*.35f;
                    Vector3 position=u*u*u*origin+3*u*u*t*control+3*u*t*t*approach+t*t*t*end;
                    Vector3 tangent=3*u*u*(control-origin)+6*u*t*(approach-control)+3*t*t*(end-approach);
                    arrow.Tangent=tangent.normalized;
                    arrow.Point=position;
                    arrow.Paint.AimDegrees(Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);
                    arrow.Paint.transform.position=position;
                    body.enabled=!volley.Seed&&travel>0f&&(position-origin).sqrMagnitude>card*card*.04f;
                    arrow.Envelope.Advance(age,travel,position,index);
                    yield return null;
                }
                while(Time.timeScale<=0f&&!volley.Cancelled&&!arrow.Landed&&caster!=null&&caster.isActive&&arrow.Target!=null&&arrow.Target.isActive&&arrow.Paint!=null)
                    yield return null;
                if(body!=null)body.enabled=false;
                float waiting=0f;
                while(!volley.Cancelled&&!arrow.Landed&&caster!=null&&caster.isActive&&arrow.Target!=null&&arrow.Target.isActive&&arrow.Paint!=null&&waiting<.25f)
                {
                    if(Time.timeScale<=0f){yield return null;continue;}
                    arrow.Point=VfxSpace.ContactPoint(arrow.Target)+landing;arrow.Paint.transform.position=arrow.Point;
                    arrow.Envelope.HoldContact(1f-.75f*waiting/.25f);
                    waiting+=Time.deltaTime;yield return null;
                }
            }
            finally{Release(arrow);}
        }
        public static void Impact(Unit caster,Unit target)
        {
            if(target==null)return;
            Volley volley=null;
            if(caster!=null&&Volleys.TryGetValue(caster,out volley))
            {
                foreach(var cast in volley.Casts)if(cast!=null)Object.Destroy(cast.gameObject);
                volley.Casts.Clear();
                volley.ContactDirection=Vector3.zero;
                foreach(var arrow in volley.Arrows)if(arrow.Target==target)
                {
                    if(volley.ContactDirection.sqrMagnitude<.001f&&arrow.Tangent.sqrMagnitude>.001f)volley.ContactDirection=arrow.Tangent;
                    arrow.Landed=true;Release(arrow);
                }
            }
            float weight=volley?.Seed==true?(volley.Area?1.2f:.85f):volley?.Area==true?1.5f:volley?.Pursuit==true?1.15f:1f;
            FlipbookFx hit;
            try{hit=Contact(caster,target,weight,volley?.Tint??ElementPalette.ColorFor(caster?.Element??"Dendro"));}
            finally{if(volley!=null)volley.ContactDirection=Vector3.zero;}
            if(volley?.Pursuit==true)PoisonBubblesFx.BindPursuitImpact(target,volley,hit);
            if(volley?.Area==true)hit?.StretchEnvelope(new Vector2(.65f,1.35f),new Vector2(1.35f,.85f),new Vector2(1.38f,.88f));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolvedContacts++;
#endif
        }
        private static FlipbookFx Contact(Unit caster,Unit target,float weight,Color tint,float opacity=1f)
        {
            float card=VfxSpace.CardSize;
            Vector3 point=VfxSpace.ContactPoint(target);
            // 논리 격자의 축이 아니라 실제 원화 사이의 비행 접선으로 착탄 붓결을 잇는다.
            Vector3 direction=caster!=null?VfxSpace.AimBetween(EmissionPoint(caster,target),point).Direction:Vector3.right;
            if(caster!=null&&Volleys.TryGetValue(caster,out var arrival)&&arrival.ContactDirection.sqrMagnitude>.001f)
                direction=arrival.ContactDirection;
            // 구매 정점 뒤에 같은 Hit4의 작은 불티·파편을 독립 수명으로 흩뜨린다.
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            var hit=Play("Hit",point,Vector2.one*card*2.45f*weight,angle,target,164,1.34f,tint,opacity);
            bool seed=caster!=null&&caster.ID==164;
            if(seed)hit?.UseSeedImpact();
            PurchasedImpactParticles.Play(target,direction,weight,tint,opacity,seed);
            // 네이트 한 발의 밝은 정점 뒤에는 밀린 프레임도 두 셀씩 읽히게 한다. 다른 구매 명중은 유지한다.
            if(caster!=null&&Volleys.TryGetValue(caster,out var contactVolley)&&contactVolley.Arc)hit?.LimitCoreSteps(2);
            // 긴 파편 꼬리 도중 카드가 회수되면 마지막 유효 접점에서 마무리한다.
            Vector3 FollowContact()
            {
                var view=CardMotion.ViewOf(target);
                if(target!=null&&target.isActive&&view!=null&&view.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null)point=VfxSpace.ContactPoint(target);
                return point;
            }
            hit?.Follow(FollowContact,Vector3.zero,lateFollow:true).BindRecipient(target);
            return hit;
        }

        /// <summary>구매 명중의 조밀한 원화를 다른 직접 공격에 재사용한다. 파편은 공통 피해 경로가 한 번만 소유한다.</summary>
        internal static void RosterContact(Unit caster,Unit target,float weight=.52f,bool elementalInk=false)
        {
            if(caster==null||target==null||!target.isActive)return;
            bool solar=elementalInk&&caster.ID is 120 or 220;
            if(solar)weight*=1.14f;
            bool paletteInk=solar||elementalInk&&caster.ID is 2 or 3 or 62 or 81 or 7 or 123 or 1 or 60 or 61 or 66 or 63 or 64 or 65;
            // 이 두 근접 타격은 피해 때 생성한다. 빈 초기 셀을 타격 정지에 붙잡지 않고 구매 정점부터 해체한다.
            bool immediateCore=elementalInk&&caster.ID is 62 or 81 or 7 or 123 or 1 or 60 or 61 or 66 or 63 or 64 or 65;
            Vector3 point=caster.ID is 2 or 3?VerseReaperFx.Point(target):PurchasedProjectileFx.ContactFor(caster,target,elementalInk);
            Color tint=ElementPalette.ColorFor(caster.Element);float angle=VfxSpace.AimFrom(caster,target).AngleDegrees;
            var ink=elementalInk?Play("Hit",point,Vector2.one*VfxSpace.CardSize*2.45f*weight*1.14f,
                angle,target,163,1.34f,new Color(tint.r*.14f,tint.g*.14f,tint.b*.14f,1f),.92f,paletteInk,immediateCore):null;
            var hit=Play("Hit",point,Vector2.one*VfxSpace.CardSize*2.45f*weight,
                angle,target,164,1.34f,tint,elementalInk?.98f:.78f,paletteInk,immediateCore);
            hit?.Follow(()=>
            {
                var view=CardMotion.ViewOf(target);
                if(target!=null&&target.isActive&&view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null)
                    point=caster.ID is 2 or 3?VerseReaperFx.Point(target):PurchasedProjectileFx.ContactFor(caster,target,elementalInk);
                return point;
            },Vector3.zero,lateFollow:true).BindRecipient(target);
            ink?.Follow(()=>
            {
                var view=CardMotion.ViewOf(target);
                if(target!=null&&target.isActive&&view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null)
                    point=caster.ID is 2 or 3?VerseReaperFx.Point(target):PurchasedProjectileFx.ContactFor(caster,target,elementalInk);
                return point;
            },Vector3.zero,lateFollow:true).BindRecipient(target);
        }

        internal static FlipbookFx RosterFlight(Unit caster,Unit target,Vector3 point,float weight)
            => Play("Flight",point,new Vector2(.48f,1.7f)*VfxSpace.CardSize*weight,
                VfxSpace.AimFrom(caster,target).AngleDegrees,target,162,.80f,ElementPalette.ColorFor(caster.Element));

        /// <summary>실제로 그림이 있는 구매 시전1~7셀을 손앞에 재생한다. 행동/피해 대기를 늘리지 않는다.</summary>
        internal static void RosterPreparation(Unit caster,Unit target,bool attack=false)
        {
            if(caster==null||!caster.isActive)return;
            var casterView=CardMotion.ViewOf(caster);
            if(casterView?.PortraitRenderer!=null&&casterView.PortraitRenderer.sprite==null)return;
            Color tint=ElementPalette.ColorFor(caster.Element);
            bool invocation=caster.ID==167;
            bool strong=caster.ID is 22 or 83 or 120 or 220 or 62 or 81 or 7 or 123 or 1 or 60 or 61 or 66 or 63 or 64 or 65;
            if(invocation)PurchasedRosterStrokeFx.Play(caster,caster,PurchasedRosterStrokeFx.Kind.Support);
            var style=new FlipbookCompositeStyle
            {
                BlendFrames=true,BlendFraction=.65f,Desaturate=1f,CoverageCoreStrength=attack&&caster.ID is 120 or 220 or 62 or 81 or 7 or 123 or 1 or 60 or 61 or 66 or 63 or 64 or 65?.06f:strong?.60f:.15f,
                PeakAlpha=invocation||strong?1f:.86f,GlowAlpha=0f,AlphaGain=strong?2f:1.2f,RequiredVisibleFrame=3,
                StartScale=.82f,CrestScale=1f,EndScale=1.08f,CrestAtFraction=.28f,
                FadeInFraction=.04f,FadeOutFraction=.45f,StartRotationDegrees=VfxSpace.AimFrom(caster,target).AngleDegrees,
                GroundLightSizeInCards=Vector2.zero,ContactShadowSizeInCards=Vector2.zero,
                SceneLightStrength=0f,BrightBackdropLiftStrength=0f,
            };
            if(strong)
            {
                var ink=FlipbookFx.Play("VfxTex/PurchasedArcher/Cast",8,3,new Vector2(.5f,.5f),
                    EmissionPoint(caster,target),Vector2.one*VfxSpace.CardSize*3.25f,new Color(tint.r*.12f,tint.g*.12f,tint.b*.12f,1f),false,false,false,
                    new[]{.025f,.035f,.040f,.045f,.045f,.040f,.035f,.030f},VfxSpace.SortingLayer(caster),
                    VfxSpace.OverlaySortingOrder+168,style:style,startFrame:1,frameCount:8);
                if(ink!=null)PurchasedArcherCastAnchor.Bind(ink,caster,target);
            }
            var fx=FlipbookFx.Play("VfxTex/PurchasedArcher/Cast",8,3,new Vector2(.5f,.5f),
                EmissionPoint(caster,target),Vector2.one*VfxSpace.CardSize*(invocation?3.40f:strong?2.95f:2.20f),tint,false,false,false,
                new[]{.025f,.035f,.040f,.045f,.045f,.040f,.035f,.030f},VfxSpace.SortingLayer(caster),
                VfxSpace.OverlaySortingOrder+169,style:style,startFrame:1,frameCount:8);
            if(fx!=null)PurchasedArcherCastAnchor.Bind(fx,caster,target);
        }

        public static bool TryContact(string key,Unit caster,Unit target)
        {
            if(!SupportsProjectile(key)&&key is not ("nephthys.passive" or "neith.passive"))return false;
            if(target==null)return true;
            if(key=="neith.passive"){PurchasedWeaveFx.Play(caster,target,PurchasedWeaveFx.Kind.Catalysis);return true;}
            if(key=="nephthys.passive")
            {
                // 승인된 자연 틱 거품은 재설정하지 않는다. 작은 개화만 먼저 끝내 추격의 대기 소유권도 보존한다.
                var hit=Contact(caster,target,.70f,ElementPalette.ColorFor("Dendro"),.62f);
                hit?.StretchEnvelope(new Vector2(.72f,1.05f),new Vector2(1.05f,1.18f),new Vector2(1.22f,1.1f));
                return true;
            }
            Impact(caster,target);return true;
        }

        public static void WeaveCast(Unit caster)=>PurchasedWeaveFx.Play(caster,caster,PurchasedWeaveFx.Kind.Cast);
        public static void WeaveReceive(Unit target,Unit caster=null)
        {
            // 자신의 강화는 시전 결이 이미 소유한다. 다른 아군만 손앞에서 이어지는 수신 결을 그린다.
            if(caster!=null&&caster==target)return;
            PurchasedWeaveFx.Play(caster,target,PurchasedWeaveFx.Kind.Receive);
        }
        private static void Release(Arrow arrow)
        {
            arrow.Envelope?.Dispose();arrow.Envelope=null;
            if(arrow.Paint!=null){arrow.Paint.gameObject.SetActive(false);Object.Destroy(arrow.Paint.gameObject);}
            arrow.Paint=null;
        }
        private static FlipbookFx Play(string name,Vector3 point,Vector2 size,float angle,Unit owner,int order,float life,Color element,float opacity=1f,bool paletteInk=false,bool immediateCore=false)
        {
            var style=new FlipbookCompositeStyle
            {
                // 2배 초과는 결과 식별용으로 명중광만 낮춘다. hit-stop의 순간 배율이 아닌 복구 배율을 쓴다.
                BlendFrames=name=="Hit",ContinuousBodyBlend=name=="Hit",BlendFraction=.90f,AllowHighSpeedSkipping=name=="Hit",
                CoreLastFrame=13,SampleRate=name=="Hit"?120f:60f,
                PeakAlpha=(name=="Flight"?.36f:name=="Hit"&&Effects.HitStop.PlaybackScale>2.01f?.22f:1f)*opacity,GlowAlpha=0f,
                Desaturate=paletteInk||name=="Flight"?1f:0f,PaletteWhiteMix=paletteInk?.06f:.65f,CoverageCoreStrength=name=="Flight"?.35f:paletteInk?.06f:1f,AlphaGain=name=="Flight"?1f:2.1f,EdgeSoftness=0f,
                StartStretch=name=="Flight"?new Vector2(.35f,1f):Vector2.one,
                CrestStretch=name=="Flight"?new Vector2(.35f,1f):Vector2.one,
                EndStretch=name=="Flight"?new Vector2(.35f,1f):Vector2.one,
                StartRotationDegrees=angle,StartScale=name=="Flight"?1f:.82f,CrestScale=name=="Flight"?1f:paletteInk?1.65f:1.2f,EndScale=name=="Hit"?(paletteInk?.96f:1.25f):name=="Flight"?1f:.42f,
                // 실제 120Hz 베이크96셀을 보간한다. 전체 형상을 접어 없애지 않는다.
                CrestAtFraction=name=="Hit"?.06f:.18f,ReleaseEndFraction=name=="Hit"?1f:.65f,
                FadeInFraction=0f,FadeOutFraction=paletteInk?.68f:.32f,RequiredVisibleFrame=name=="Flight"?-1:7,
                GroundLightSizeInCards=Vector2.zero,ContactShadowSizeInCards=Vector2.zero,
                SceneLightStrength=0f,BrightBackdropLiftStrength=0f,
            };
            // 상단 행 셀3의 HDR 핵(260.67,260.17)을 Unity 하단 원점으로 환산한다.
            Vector2 pivot=name=="Flight"?new Vector2(.5f,.5f):new Vector2(.509f,.492f);
            bool dense=name!="Flight";
            var fx=FlipbookFx.Play("VfxTex/PurchasedArcher/"+(name=="Cast"?"Hit":name),dense?12:8,dense?8:6,pivot,point,size,element,
                false,name=="Flight",false,name=="Cast"?new[]{1f/120f,1f/120f,1f/120f,1f/120f,1f/120f,1f/120f,.0325f,.0325f,.025f,.025f}:name=="Hit"?ImpactDelays:Delays,VfxSpace.SortingLayer(owner),VfxSpace.OverlaySortingOrder+order,style:style,
                frameCount:name=="Cast"?10:name=="Hit"?96:Mathf.Min(48,Mathf.CeilToInt(life*60)),startFrame:name=="Flight"?3:name=="Hit"?(immediateCore?7:2):4);
            return fx;
        }
    }
}
