using System.Collections.Generic;
using Entities;
using Effects.Vfx.Presets;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>기존 독 거품 원화를 시차로 부풀리고 터뜨린다. 상태·피해·행동 콜백을 소유하지 않는다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PoisonBubblesFx : MonoBehaviour
    {
        private sealed class Bubble
        {
            public SpriteRenderer Body,Highlight,Rim;
            public readonly SpriteRenderer[] Drops=new SpriteRenderer[3];
            public readonly SpriteRenderer[] Fragments=new SpriteRenderer[2];
            public float Delay,Life,Size,X,Y,Phase,Hold;
            public bool WaitForImpact;
        }
        private Unit _target;
        private Bubble[] _bubbles;
        private Color _poison,_light;
        private float _startedAt,_scale,_popHold;
        private bool _tick,_gateConsumed,_impactAssigned;
        private object _pursuitCue;
        private FlipbookFx _pursuitImpact;
        private float _gateDeadline,_releaseAge=float.PositiveInfinity;
        private static readonly HashSet<PoisonBubblesFx> Active=new();
        private static Sprite _lip;
        private static MaterialPropertyBlock _liquidTint;

        private static void UseLiquidTint(SpriteRenderer renderer)
        {
            renderer.sharedMaterial=VfxSpace.CompositeAlpha;
            if(_liquidTint==null)
            {
                _liquidTint=new MaterialPropertyBlock();
                // 원화의 .30 암부를 그대로 곱해 회올리브가 되지 않게 중립 명도만 들어 올린다.
                // alpha와 원소색은 유지하고 금빛 배경 보정·가산광은 사용하지 않는다.
                _liquidTint.SetFloat("_Desaturate",1f);_liquidTint.SetFloat("_LiftStrength",0f);
                _liquidTint.SetFloat("_EdgeSoftness",0f);_liquidTint.SetFloat("_PaintContrast",0f);
                _liquidTint.SetFloat("_CoverageCoreStrength",0f);_liquidTint.SetFloat("_AlphaGain",1f);
            }
            renderer.SetPropertyBlock(_liquidTint);
        }

        // 원판을 축소해 지우는 대신, 두 개의 열린 곡면이 반대 방향으로 벌어진다.
        // 흰 텍스처의 곡선 메시를 공유하므로 별도 원화나 캐스트별 텍스처를 만들지 않는다.
        private static Sprite Lip
        {
            get
            {
                if(_lip!=null)return _lip;
                const int segments=20;
                var vertices=new Vector2[(segments+1)*2];var triangles=new ushort[segments*6];
                for(int i=0;i<=segments;i++)
                {
                    float angle=Mathf.Lerp(-70f,70f,i/(float)segments)*Mathf.Deg2Rad;
                    Vector2 direction=new(Mathf.Cos(angle),Mathf.Sin(angle));
                    vertices[i*2]=direction*.40f-new Vector2(.3184f,0f);
                    vertices[i*2+1]=direction*.50f-new Vector2(.3184f,0f);
                    if(i==segments)continue;
                    int k=i*6,v=i*2;
                    triangles[k]=(ushort)v;triangles[k+1]=(ushort)(v+1);triangles[k+2]=(ushort)(v+2);
                    triangles[k+3]=(ushort)(v+1);triangles[k+4]=(ushort)(v+3);triangles[k+5]=(ushort)(v+2);
                }
                var texture=Texture2D.whiteTexture;
                // OverrideGeometry는 월드 좌표가 아닌 rect의 픽셀 좌표를 받는다.
                // Sprite.bounds는 rect 전체이므로 곡면의 실제 범위를 rect에 맞춘다.
                Vector2 minimum=vertices[0],maximum=vertices[0];
                foreach(var point in vertices){minimum=Vector2.Min(minimum,point);maximum=Vector2.Max(maximum,point);}
                Vector2 extent=maximum-minimum;
                for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector2(
                    (vertices[i].x-minimum.x)/extent.x*texture.width,(vertices[i].y-minimum.y)/extent.y*texture.height);
                var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width,0,SpriteMeshType.FullRect);
                sprite.name="PoisonBubbleOpenLip";sprite.hideFlags=HideFlags.HideAndDontSave;
                try{sprite.OverrideGeometry(vertices,triangles);}
                catch{Object.Destroy(sprite);throw;}
                _lip=sprite;
                return _lip;
            }
        }

        public static void Play(Unit target,bool tick)
        {
            if(!HasPortrait(target))return;
            Sprite lip=Lip;
            var fx=new GameObject("PoisonBubblesFx").AddComponent<PoisonBubblesFx>();
            fx._target=target;fx._startedAt=Time.time;fx._scale=tick?.80f:1f;fx._tick=tick;
            // 자연 틱은 추격의 밝은 정점이 풀릴 때까지 차오른 채 흔들린 뒤 터진다.
            // 판정·추격·행동 대기와 무관한 장식 시간이다.
            fx._popHold=tick?.30f:0f;
            fx._poison=ElementPalette.ColorFor(BaseClasses.BaseEnums.UnitElement.Dendro);
            fx._light=Color.Lerp(fx._poison,new Color(.95f,1f,.84f),tick?.35f:.65f);
            fx._bubbles=new Bubble[tick?3:5];
            // 자연 틱의 기포 파열은 곧이어 터지는 추격 명중에 묻히지 않게 앞에서 마무리한다.
            int layer=VfxSpace.SortingLayer(target),order=VfxSpace.OverlaySortingOrder+(tick?196:62);
            var positions=new[]{new Vector2(-.23f,-.05f),new Vector2(.22f,.03f),new Vector2(-.02f,-.11f),new Vector2(-.12f,-.01f),new Vector2(.17f,-.06f)};
            for(int i=0;i<fx._bubbles.Length;i++)
            {
                var b=new Bubble{Delay=i*.14f,Life=i==2?.76f:i%2==0?.72f:.68f,
                    Size=i==2?.22f:i%2==0?.15f:.13f,X=positions[i].x,Y=positions[i].y,Phase=i*1.7f,Hold=fx._popHold};
                b.Rim=VfxSpace.NewSprite(fx.transform,"BubbleRim"+i,VfxSpace.Ring,Color.clear,layer,order-1);
                b.Body=VfxSpace.NewSprite(fx.transform,"BubbleBody"+i,VfxSpace.ToxicBubble,Color.clear,layer,order);
                UseLiquidTint(b.Body);
                b.Highlight=VfxSpace.NewSprite(fx.transform,"BubbleHighlight"+i,VfxSpace.BubbleHighlight,Color.clear,layer,order+1);
                for(int j=0;j<3;j++)
                {
                    b.Drops[j]=VfxSpace.NewSprite(fx.transform,"BubbleDrop"+i+"_"+j,VfxSpace.ToxicBubble,Color.clear,layer,order+2);
                    UseLiquidTint(b.Drops[j]);
                }
                for(int j=0;j<2;j++)b.Fragments[j]=VfxSpace.NewSprite(fx.transform,"BubbleLip"+i+"_"+j,lip,Color.clear,layer,order+1);
                fx._bubbles[i]=b;
            }
            Active.Add(fx);
            fx.Present(0f);
        }

        // 1·2배속의 읽기 대기는 서로 다르므로 고정 지연 대신 실제 추격 명중의 코어 퇴장을 따른다.
        // 이미 터진 기포는 되감지 않으며, 취소·누락된 명중도 게임 시간 2.5초 안에 풀린다.
        internal static void HoldForPursuit(Unit target,object cue)
        {
            if(cue==null)return;
            foreach(var fx in Active)
            {
                if(fx==null||!fx._tick||fx._target!=target||fx._gateConsumed)continue;
                float age=Time.time-fx._startedAt;bool waiting=false;
                foreach(var b in fx._bubbles)
                {
                    b.WaitForImpact=age-b.Delay<=b.Life*.63f+b.Hold;
                    waiting|=b.WaitForImpact;
                }
                if(!waiting)continue;
                fx._gateConsumed=true;fx._pursuitCue=cue;fx._gateDeadline=Time.time+2.5f;
            }
        }
        internal static void BindPursuitImpact(Unit target,object cue,FlipbookFx impact)
        {
            foreach(var fx in Active)
                if(fx!=null&&fx._target==target&&ReferenceEquals(fx._pursuitCue,cue)&&cue!=null)
                {fx._impactAssigned=true;fx._pursuitImpact=impact;}
        }
        internal static void CancelPursuit(object cue)
        {
            foreach(var fx in Active)
                if(fx!=null&&cue!=null&&ReferenceEquals(fx._pursuitCue,cue))fx.ReleasePursuit();
        }
        private void ReleasePursuit()
        {
            _releaseAge=Mathf.Max(0f,Time.time-_startedAt);_pursuitCue=null;_pursuitImpact=null;
        }
        private void OnDestroy()=>Active.Remove(this);

        private void LateUpdate()
        {
            if(!HasPortrait(_target)){Destroy(gameObject);return;}
            if(Time.timeScale<=0f)return;
            float age=Mathf.Max(0f,Time.time-_startedAt);
            if(_pursuitCue!=null&&(Time.time>=_gateDeadline||
                (_impactAssigned&&(_pursuitImpact==null||_pursuitImpact.ImpactCoreReleased))))ReleasePursuit();
            float end=0f;
            foreach(var b in _bubbles)
            {
                if(b.WaitForImpact)
                {
                    // 코어 퇴장 뒤에도 원래의 .14초 시차로 차례로 터진다.
                    b.Hold=Mathf.Max(b.Hold,Mathf.Min(age,_releaseAge+b.Delay)-b.Delay-b.Life*.63f);
                    if(age>=_releaseAge+b.Delay)b.WaitForImpact=false;
                }
                end=Mathf.Max(end,b.Delay+b.Life+b.Hold);
            }
            if(age>=end){Destroy(gameObject);return;}
            Present(age);
        }

        private void Present(float age)
        {
            transform.position=VfxSpace.ContactPoint(_target);
            float card=VfxSpace.CardSize*_scale;
            foreach(var b in _bubbles)
            {
                float rawAge=age-b.Delay,popAt=b.Life*.63f;
                float held=Mathf.Clamp(rawAge-popAt,0f,b.Hold);
                float t=(rawAge-held)/b.Life;
                bool visible=t>=0f&&t<1f;
                b.Body.enabled=b.Highlight.enabled=b.Rim.enabled=visible;
                foreach(var drop in b.Drops)drop.enabled=visible&&t>=.63f;
                foreach(var fragment in b.Fragments)fragment.enabled=visible&&t>=.63f;
                if(!visible)continue;
                float birth=Mathf.SmoothStep(0f,1f,t/.20f);
                float pop=Mathf.InverseLerp(.63f,1f,t);
                float intact=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.63f,.69f,t));
                float motionAge=Mathf.Min(rawAge,popAt+b.Hold)/b.Life;
                float wobble=Mathf.Sin(motionAge*14f+b.Phase)*.14f*birth;
                float rise=Mathf.Min(t,.63f);
                Vector3 center=new Vector3(b.X+Mathf.Sin(motionAge*9f+b.Phase)*.055f,b.Y+rise*.46f,0f)*card;
                float size=b.Size*card*Mathf.Lerp(.25f,1.12f,birth);
                float squeeze=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.53f,.63f,t));
                Vector2 shape=new(size*(1f+wobble+.12f*squeeze),size*(1f-wobble-.10f*squeeze));
                Paint(b.Body,center,shape,_poison,.90f*birth*intact);
                Paint(b.Highlight,center,shape*.94f,_light,.72f*birth*intact);
                float burst=Mathf.SmoothStep(0f,1f,pop/.10f)*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.50f,1f,pop)));
                Paint(b.Rim,center,Vector2.Lerp(shape*1.08f,Vector2.one*size*(1.08f+.80f*pop),1f-intact),
                    Color.Lerp(_poison,_light,1f-intact),.42f*birth*intact+.48f*burst*(1f-intact));
                for(int j=0;j<2;j++)
                {
                    float side=j==0?-1f:1f;
                    Vector3 point=center+new Vector3(side*(.32f+.85f*pop),.10f+.30f*pop,0f)*size;
                    Paint(b.Fragments[j],point,new Vector2(.40f,1.05f)*size*(1f+.35f*pop),_light,.90f*burst);
                    b.Fragments[j].transform.localRotation=Quaternion.Euler(0,0,(j==0?180f:0f)+side*(-12f+pop*42f));
                }
                for(int j=0;j<3;j++)
                {
                    float angle=(45f+j*65f+b.Phase*8f)*Mathf.Deg2Rad;
                    Vector3 direction=new(Mathf.Cos(angle),Mathf.Sin(angle),0f);
                    Vector3 point=center+direction*(b.Size*card*(.35f+pop*1.55f))-Vector3.up*(pop*pop*.12f*card);
                    Paint(b.Drops[j],point,new Vector2(.36f,.44f)*size,Color.Lerp(_poison,_light,.20f),.88f*burst);
                    b.Drops[j].transform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg-90f);
                }
            }
        }

        private static void Paint(SpriteRenderer renderer,Vector3 point,Vector2 size,Color color,float alpha)
        {
            renderer.transform.localPosition=point;
            Vector2 bounds=renderer.sprite.bounds.size;
            renderer.transform.localScale=new Vector3(size.x/Mathf.Max(.0001f,bounds.x),size.y/Mathf.Max(.0001f,bounds.y),1f);
            color.a=alpha;renderer.color=color;
        }
        private static bool HasPortrait(Unit target)
        {
            if(target==null||!target.isActive)return false;
            var view=CardMotion.ViewOf(target);
            return view!=null&&view.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null;
        }
    }
}
