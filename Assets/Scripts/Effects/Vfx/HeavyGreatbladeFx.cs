using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;
using UnityEngine;
namespace Effects.Vfx
{
    /// <summary>대검의 무게·정점·해체를 그리는 공통 순수 VFX. 피해 시계와 팔레트는 호출자가 소유한다.</summary>
        public sealed class HeavyGreatbladeFx : MonoBehaviour
        {
            // 원화의 독립 행 경계를 따라 전체 형상을 보존한다. 카드/얼굴 마스크나 그림 절단은 없다.
            private static readonly Vector4[] FallRects={new(0.000000000f,0.780701754f,0.250000000f,0.219298246f),new(0.250000000f,0.780701754f,0.250000000f,0.219298246f),new(0.500000000f,0.780701754f,0.250000000f,0.219298246f),new(0.750000000f,0.780701754f,0.250000000f,0.219298246f),new(0.000000000f,0.524720893f,0.250000000f,0.255980861f),new(0.250000000f,0.524720893f,0.250000000f,0.255980861f),new(0.500000000f,0.524720893f,0.250000000f,0.255980861f),new(0.750000000f,0.524720893f,0.250000000f,0.255980861f),new(0.000000000f,0.264752791f,0.250000000f,0.259968102f),new(0.250000000f,0.264752791f,0.250000000f,0.259968102f),new(0.500000000f,0.264752791f,0.250000000f,0.259968102f),new(0.750000000f,0.264752791f,0.250000000f,0.259968102f),new(0.000000000f,0.000000000f,0.250000000f,0.264752791f),new(0.250000000f,0.000000000f,0.250000000f,0.264752791f),new(0.500000000f,0.000000000f,0.250000000f,0.264752791f),new(0.750000000f,0.000000000f,0.250000000f,0.264752791f)};
            private static Material _material,_veilMaterial;
            private Mesh _mesh,_veil;
            private MeshRenderer _renderer,_veilRenderer;
            private MaterialPropertyBlock _properties;
            private readonly List<Vector3> _vertices=new(),_uv=new(),_veilVertices=new(),_veilUv=new();
            private readonly List<Color> _colors=new(),_veilColors=new();
            private readonly List<int> _triangles=new(),_veilTriangles=new();
            private Unit _caster;private Unit[] _targets;private Vector3 _point;private Color _tint;
            private float _age,_landAge=-1f,_card,_height,_angle,_lift;private bool _area;
            private bool _cancelled;
            private HeavySkyfallStageFx _stage;
            private Vector3 _impactPoint; private float _spanX,_spanY,_facing=1f,_strikeAngle;
            private Mesh _sweep; private MeshRenderer _sweepRenderer; private MaterialPropertyBlock _sweepProperties;
            private readonly List<Vector3> _sweepVertices=new(),_sweepUv=new();
            private readonly List<Color> _sweepColors=new(); private readonly List<int> _sweepTriangles=new();
            private Color _palette;
            public const int WindupSamples=48,ReleaseSamples=96;
            // 생성 시트의 경계 불량 4번 셀은 사용하지 않는다. 나머지 서로 다른 실제 원화15셀을 순서대로 재생한다.
            public const int ImpactFrameCount=15,FallFrameCount=16,SmokeFrameCount=16;
            public static HeavyGreatbladeFx BeginSlash(Unit caster,Unit target,Color? palette=null)
                =>Create(caster,new[]{target},false,false,palette);
            public static HeavyGreatbladeFx BeginSkyfall(Unit caster,IReadOnlyList<Unit> targets,bool empowered,Color? palette=null)
                =>Create(caster,targets,true,empowered,palette);
            private readonly List<(FlipbookFx fx,Vector3 origin,Vector3 velocity,float birth)> _smoke=new();
            private static HeavyGreatbladeFx Create(Unit caster,IReadOnlyList<Unit> targets,bool area,bool empowered,Color? palette)
            {
                if(caster==null||targets==null||targets.Count==0)return null;
                var live=targets.Where(x=>x!=null&&x.isActive&&CardMotion.ViewOf(x)?.PortraitRenderer?.sprite!=null).ToArray();
                if(caster==null||live.Length==0)return null;
                var art=Resources.Load<Texture2D>("VfxTex/Signature/HeavyFall44");
                var flame=Resources.Load<Texture2D>("VfxTex/Handpaint/SurtrFlame4");
                var grain=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Veil");var shader=Resources.Load<Shader>("Shaders/PurchasedWeave");var bodyShader=Resources.Load<Shader>("Shaders/HeavyGreatblade");
                if(art==null||flame==null||grain==null||shader==null||bodyShader==null)return null;
                if(_material==null)_material=new Material(bodyShader){name="HeavyGreatbladeArt",hideFlags=HideFlags.HideAndDontSave};
                if(_veilMaterial==null)_veilMaterial=new Material(shader){name="PurchasedGreatbladeTrail73",hideFlags=HideFlags.HideAndDontSave};
                var fx=new GameObject(area?"HeavyGreatblade_Area":"HeavyGreatblade_Strike").AddComponent<HeavyGreatbladeFx>();
                fx._caster=caster;fx._targets=live;fx._area=area;fx._card=VfxSpace.CardSize;
                fx.RefreshPoints();
                fx._facing=VfxSpace.AimFrom(caster,live[0]).FacingRight?1f:-1f;
                if(!area)
                {
                    Vector3 returnDirection=VfxSpace.BodyContactPoint(caster)-fx._point;
                    fx._strikeAngle=Mathf.Atan2(returnDirection.y,returnDirection.x)*Mathf.Rad2Deg-90f;
                }
                fx._palette=palette??ElementPalette.ColorFor(caster.Element);
                fx._height=area?Mathf.Max((empowered?3.5f:3.2f)*fx._card,fx._spanY+1.35f*fx._card):1.95f*fx._card;
                if(area&&Camera.main!=null)
                {
                    float top=Camera.main.ViewportToWorldPoint(new Vector3(.5f,.92f,Camera.main.WorldToViewportPoint(fx._point).z)).y;
                    fx._height=Mathf.Min(fx._height,Mathf.Max(1.35f*fx._card,top-fx._point.y-.08f*fx._card));
                }
                fx._tint=fx._palette;if(QualitySettings.activeColorSpace==ColorSpace.Linear)fx._tint=fx._tint.linear;
                fx._mesh=new Mesh{name="HeavyGreatbladeTextured"};fx._mesh.MarkDynamic();fx.gameObject.AddComponent<MeshFilter>().sharedMesh=fx._mesh;
                fx._renderer=fx.gameObject.AddComponent<MeshRenderer>();fx._renderer.sharedMaterial=_material;
                fx._renderer.sortingLayerID=VfxSpace.SortingLayer(live[0]);fx._renderer.sortingOrder=VfxSpace.OverlaySortingOrder+180;
                var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",art);props.SetTexture("_NoiseTex",grain);props.SetTexture("_FlameTex",flame);props.SetColor("_EdgeColor",fx._palette);props.SetVectorArray("_FallRects",FallRects);fx._properties=props;fx._renderer.SetPropertyBlock(props);fx._renderer.enabled=false;
                var child=new GameObject("PurchasedGreatbladeVeil");child.transform.SetParent(fx.transform,false);fx._veil=new Mesh{name="PurchasedGreatbladeVeil"};fx._veil.MarkDynamic();child.AddComponent<MeshFilter>().sharedMesh=fx._veil;
                fx._veilRenderer=child.AddComponent<MeshRenderer>();fx._veilRenderer.sharedMaterial=_veilMaterial;fx._veilRenderer.sortingLayerID=fx._renderer.sortingLayerID;fx._veilRenderer.sortingOrder=fx._renderer.sortingOrder+1;
                props=new MaterialPropertyBlock();props.SetTexture("_MainTex",grain);fx._veilRenderer.SetPropertyBlock(props);fx._veilRenderer.enabled=false;
                var sweepObject=new GameObject("PurchasedHeavyCleave6");sweepObject.transform.SetParent(fx.transform,false);
                fx._sweep=new Mesh{name="HeavyGreatbladeCleave"};fx._sweep.MarkDynamic();sweepObject.AddComponent<MeshFilter>().sharedMesh=fx._sweep;
                fx._sweepRenderer=sweepObject.AddComponent<MeshRenderer>();fx._sweepRenderer.sharedMaterial=_material;
                fx._sweepRenderer.sortingLayerID=fx._renderer.sortingLayerID;fx._sweepRenderer.sortingOrder=fx._renderer.sortingOrder+5;
                fx._sweepProperties=new MaterialPropertyBlock();fx._sweepProperties.SetTexture("_MainTex",Resources.Load<Texture2D>("VfxTex/Signature/HeavyImpact43"));
                fx._sweepProperties.SetColor("_EdgeColor",fx._palette);fx._sweepProperties.SetFloat("_Layer",2f);fx._sweepRenderer.enabled=false;
                if(area)fx._stage=HeavySkyfallStageFx.Begin(fx.transform,fx._impactPoint,fx._palette);
                return fx;
            }
            public void Land()
            {
                if(_landAge>=0f)return;
                _landAge=_age;
                _stage?.Land(_impactPoint);
                // 판정은 기존 호출자가 소유한다. 하나의 착지에서 낮은 폭발과 뒤따르는 연기만 연다.
                Color tint=_palette;
                float scale=_area?1f:.66f,alpha=HitStop.PlaybackScale>2.01f?.42f:.92f;
                int layer=_renderer.sortingLayerID;
                var hitStyle=new FlipbookCompositeStyle{
                    BlendFrames=true,ContinuousBodyBlend=true,BlendFraction=.90f,SampleRate=120f,CoreLastFrame=13,
                    PeakAlpha=alpha,GlowAlpha=0f,Desaturate=1f,PaletteWhiteMix=.22f,CoverageCoreStrength=.26f,AlphaGain=2.2f,
                    StartScale=.88f,CrestScale=1.18f,EndScale=1.36f,CrestAtFraction=.06f,ReleaseEndFraction=1f,
                    FadeInFraction=0f,FadeOutFraction=.64f,RequiredVisibleFrame=7,StartRotationDegrees=-7f,
                    GroundLightSizeInCards=Vector2.zero,ContactShadowSizeInCards=Vector2.zero,SceneLightStrength=0f,BrightBackdropLiftStrength=0f};
                var burst=FlipbookFx.Play("VfxTex/PurchasedArcher/Hit",12,8,new Vector2(.509f,.492f),_impactPoint,
                    FitNormalSize(new Vector2(_area?Mathf.Max(3.7f,_spanX/_card+1.8f):2.6f,_area?Mathf.Max(2.3f,_spanY/_card+.65f):1.4f)*scale*_card,new Vector2(.509f,.492f),_impactPoint,1.36f),tint,false,false,false,Enumerable.Repeat(1f/120f,96).ToArray(),
                    layer,VfxSpace.OverlaySortingOrder+184,style:hitStyle,startFrame:7,frameCount:96);
                if(burst!=null)burst.gameObject.name="PurchasedGreatbladeLanding";
                // 같은 연기 세 장을 포개지 않고 낮은 충격연기에서 좌우 말림으로 시차를 둔다.
                StartCoroutine(LandingSmoke(scale,layer,tint));
            }
            private IEnumerator LandingSmoke(float scale,int layer,Color tint)
            {
                yield return new WaitForSeconds(.045f);
                Vector3 origin=_point+Vector3.down*.08f*_card;
                var smokeStyle=new FlipbookCompositeStyle{
                    BlendFrames=true,BlendFraction=.90f,SampleRate=120f,PeakAlpha=HitStop.PlaybackScale>2.01f?.28f:.68f,
                    GlowAlpha=0f,Desaturate=1f,PaintContrast=1f,CoverageCoreStrength=0f,
                    StartScale=.88f,CrestScale=1.06f,EndScale=1.26f,CrestAtFraction=.25f,
                    FadeInFraction=.03f,FadeOutFraction=.45f,
                    GroundLightSizeInCards=Vector2.zero,ContactShadowSizeInCards=Vector2.zero,SceneLightStrength=0f,BrightBackdropLiftStrength=0f};
                var fx=FlipbookFx.Play("VfxTex/Signature/HeavySmoke44",4,4,new Vector2(.5f,.075f),origin,
                    FitNormalSize(new Vector2(_area?4.1f:2.1f,_area?1.85f:1.2f)*_card,new Vector2(.5f,.075f),origin,1.32f),
                    new Color(.66f,.64f,.62f,1f),false,false,false,Enumerable.Repeat(1.4f/16f,16).ToArray(),
                    layer,VfxSpace.OverlaySortingOrder+177,style:smokeStyle,frameCount:16);
                if(fx!=null){fx.gameObject.name="AuthoredGreatbladeSmoke16";_smoke.Add((fx,origin,Vector3.up*.18f*_card,_age));}
            }

            private void Update()
            {
                if(Time.timeScale<=0f)return;_age+=Time.deltaTime;
                if((_caster==null||!_caster.isActive||_caster.isControlled)&&_landAge<0f){_cancelled=true;_landAge=_age;_stage?.Cancel();}
                foreach(var s in _smoke)if(s.fx!=null)
                {
                    float t=_age-s.birth;
                    s.fx.transform.position=s.origin+s.velocity*(1f-Mathf.Exp(-t*3.5f))/3.5f;
                }
                if((_landAge>=0f&&_age-_landAge>1.75f)||_age>3f)Destroy(gameObject);
            }
            private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3f-2f*x);}
            private void RefreshPoints()
            {
                var points=_targets.Where(t=>t!=null&&t.isActive&&CardMotion.ViewOf(t)?.PortraitRenderer?.sprite!=null).Select(VfxSpace.BodyContactPoint).ToArray();
                if(points.Length==0)return;
                _impactPoint=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;
                _spanX=points.Max(p=>p.x)-points.Min(p=>p.x);_spanY=points.Max(p=>p.y)-points.Min(p=>p.y);
                _point=_area?new Vector3(_impactPoint.x,points.Min(p=>p.y),_impactPoint.z):points[0];
            }
            private void LateUpdate()
            {
                if(Time.timeScale<=0f||_renderer==null)return;
                bool landed=_landAge>=0f;float after=landed?_age-_landAge:0f;
                float travel=Mathf.Clamp01(_age/(_area?.47f:.16f));
                float fall=SampleMotion(travel,false);
                float recoil=landed?Mathf.Exp(-after/.055f):0f;
                float breakup=landed?SampleMotion(after/.58f,true):0f;
                _lift=landed?-.045f*_card:(1f-fall)*(_area?_height/_card*.44f:.65f)*_card;
                _angle=_area?Mathf.Lerp(9f,-6f,fall)*_facing:_strikeAngle+Mathf.Lerp(-75f,0f,fall)*_facing;
                if(landed)_angle=_area?-6f*_facing:_strikeAngle;
                if(!landed)
                {
                    RefreshPoints();
                }
                transform.position=_point;_vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();
                _veilVertices.Clear();_veilUv.Clear();_veilColors.Clear();_veilTriangles.Clear();
                Quaternion turn=Quaternion.Euler(0,0,_angle);
                float height=_height*Mathf.Lerp(_area?.54f:.72f,1f,Smooth(fall));
                float width=(_area?Mathf.Max(_spanX+1.25f*_card,height*.86f):height*.92f)*(1f+recoil*.20f);
                float speedAlpha=HitStop.PlaybackScale>2.01f?.52f:1f;
                float entry=Smooth(_age/(_area?.12f:.055f));
                Vector3 offset=new Vector3((1f-fall)*-.22f*_card,_lift);
                // 그려진 칼날의 전체 알파를 유지한다. 착지 뒤에는 원화 조각이 시차로 벌어지며 해체된다.
                const int bands=8,columns=2;
                Vector3 Shape(float x,float y)
                {
                    float bend=Mathf.Sin(y*Mathf.PI)*height*.045f*(1f-fall);
                    return turn*new Vector3((x-.53f)*width+bend,(y-.065f)*height)+offset;
                }
                void Piece(Vector2 a,Vector2 b,Vector2 c,int piece)
                {
                    Vector2 center=(a+b+c)/3f;
                    float delay=.070f+(piece%5)*.016f;
                    float t=landed?Mathf.Max(0f,after-delay):0f;
                    float spread=Smooth(t/.40f);
                    float direction=center.x<.5f?-1f:1f;
                    Vector3 shift=new Vector3(direction*(.06f+(piece%3)*.028f)*spread,
                        -.15f*spread+Mathf.Sin(piece*2.1f)*.03f*spread)*_card;
                    Quaternion spin=Quaternion.Euler(0,0,direction*(8f+piece%4*5f)*spread);
                    Vector3 pivot=Shape(center.x,center.y);
                    int n=_vertices.Count;
                    void Vertex(Vector2 uv)
                    {
                        _vertices.Add(pivot+spin*(Shape(uv.x,uv.y)-pivot)+shift);
                        _uv.Add(new Vector3(uv.x,uv.y));_colors.Add(Color.white);
                    }
                    Vertex(a);Vertex(b);Vertex(c);
                    _triangles.Add(n);_triangles.Add(n+1);_triangles.Add(n+2);
                }
                for(int row=0;row<bands;row++)for(int col=0;col<columns;col++)
                {
                    float x0=col/(float)columns,x1=(col+1)/(float)columns,y0=row/(float)bands,y1=(row+1)/(float)bands;
                    int piece=(row*columns+col)*2;
                    Piece(new(x0,y0),new(x0,y1),new(x1,y0),piece);
                    Piece(new(x1,y0),new(x0,y1),new(x1,y1),piece+1);
                }
                _properties.SetFloat("_Opacity",entry*speedAlpha*(_cancelled?1f-Smooth(after/.14f):.92f));
                _properties.SetFloat("_Phase",_age);
                _properties.SetFloat("_BodyFrame",landed?15f:15f*Smooth(travel));
                _properties.SetFloat("_Dissolve",breakup);
                _properties.SetFloat("_Highlight",landed?1f-Smooth((after-.12f)/.18f):Mathf.Pow(fall,3f)*.15f);
                _properties.SetFloat("_Fall",fall);_renderer.SetPropertyBlock(_properties);
                for(int strand=0;strand<3;strand++)for(int i=0;i<40;i++)
                {
                    float a=i/40f,b=(i+1)/40f,t=(a+b)*.5f;
                    float tail=landed?1f-Smooth((after-(.035f+strand*.032f))/.40f):Smooth((travel-.40f)/.30f);
                    float alpha=entry*speedAlpha*tail*Mathf.Sin(t*Mathf.PI)*.64f;
                    if(alpha<.002f)continue;
                    Vector3 Point(float u)=>turn*new Vector3((strand-1)*.075f*_height+Mathf.Sin(u*Mathf.PI)*(.10f+strand*.035f)*_height,
                        u*_height*(landed?1.10f:Mathf.Lerp(.55f,1.18f,fall)))+offset+Vector3.up*(strand*.055f*_card);
                    Vector3 p=Point(a),q=Point(b),normal=new Vector3(-(q-p).y,(q-p).x).normalized;
                    float w=_card*(_area?.032f:.020f)*Mathf.Sin(t*Mathf.PI)*(1f-breakup*.55f);
                    int n=_veilVertices.Count;_veilVertices.Add(p-normal*w);_veilVertices.Add(q-normal*w);_veilVertices.Add(q+normal*w);_veilVertices.Add(p+normal*w);
                    _veilUv.Add(new(a,0));_veilUv.Add(new(b,0));_veilUv.Add(new(b,1));_veilUv.Add(new(a,1));
                    var color=_tint;color.a=alpha;for(int j=0;j<4;j++)_veilColors.Add(color);
                    _veilTriangles.Add(n);_veilTriangles.Add(n+1);_veilTriangles.Add(n+2);_veilTriangles.Add(n);_veilTriangles.Add(n+2);_veilTriangles.Add(n+3);
                }
                FitNormalVertices(_vertices,_veilVertices);
                DrawSweep(landed,after,fall,speedAlpha);
                _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);_mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();_renderer.enabled=_vertices.Count>0;
                _veil.Clear();_veil.SetVertices(_veilVertices);_veil.SetUVs(0,_veilUv);_veil.SetColors(_veilColors);_veil.SetTriangles(_veilTriangles,0);_veil.RecalculateBounds();_veilRenderer.enabled=_veilVertices.Count>0;
            }
            private void DrawSweep(bool landed,float after,float fall,float speedAlpha)
            {
                if(_cancelled){_sweepRenderer.enabled=false;return;}
                float appear=landed?1f-Smooth((after-.95f)/.25f):0f;
                if(_area&&!landed)appear=0f;
                _sweepVertices.Clear();_sweepUv.Clear();_sweepColors.Clear();_sweepTriangles.Clear();
                Vector3 pivot=Vector3.zero;
                float w=_area?Mathf.Max(4.1f*_card,_spanX+2.5f*_card):2.45f*_card;
                float h=_area?2.75f*_card:1.75f*_card;
                float grow=landed?1f+.22f*Smooth(after/.23f):.75f+.25f*fall;
                var rotation=Quaternion.identity;
                Vector3[] corners={new(-.5f,-.14f),new(-.5f,.86f),new(.5f,.86f),new(.5f,-.14f)};
                for(int i=0;i<4;i++){_sweepVertices.Add(pivot+rotation*Vector3.Scale(corners[i],new Vector3(w,h,1f))*grow);_sweepColors.Add(Color.white);}
                _sweepUv.Add(new(0,0));_sweepUv.Add(new(0,1));_sweepUv.Add(new(1,1));_sweepUv.Add(new(1,0));
                _sweepTriangles.AddRange(new[]{0,1,2,0,2,3});
                FitNormalVertices(_sweepVertices);
                _sweep.Clear();_sweep.SetVertices(_sweepVertices);_sweep.SetUVs(0,_sweepUv);_sweep.SetColors(_sweepColors);_sweep.SetTriangles(_sweepTriangles,0);_sweep.RecalculateBounds();
                _sweepProperties.SetFloat("_CleaveFrame",landed?(after<.14f?Mathf.Lerp(0f,4f,after/.14f):Mathf.Lerp(4f,14f,Smooth((after-.14f)/.82f))):fall*2f);
                _sweepProperties.SetFloat("_Opacity",appear*speedAlpha*.90f);_sweepProperties.SetFloat("_Highlight",landed?1f-Smooth((after-.12f)/.18f):0f);
                _sweepRenderer.SetPropertyBlock(_sweepProperties);_sweepRenderer.enabled=appear>.001f;
            }
            private Vector2 FitNormalSize(Vector2 size,Vector2 pivot,Vector3 origin,float growth)
            {
                if(_area||Camera.main==null)return size;
                Camera camera=Camera.main;float depth=camera.WorldToViewportPoint(origin).z;
                Vector3 lower=camera.ViewportToWorldPoint(new Vector3(.012f,.012f,depth))-origin;
                Vector3 upper=camera.ViewportToWorldPoint(new Vector3(.988f,.988f,depth))-origin;
                float scale=Mathf.Min(1f,upper.x/Mathf.Max(.001f,size.x*(1f-pivot.x)*growth),
                    -lower.x/Mathf.Max(.001f,size.x*pivot.x*growth),upper.y/Mathf.Max(.001f,size.y*(1f-pivot.y)*growth),
                    -lower.y/Mathf.Max(.001f,size.y*pivot.y*growth));
                return size*Mathf.Clamp01(scale);
            }
            // 접점을 고정하고 일반 검격 전체를 화면 안에 맞춘다. HUD/얼굴 마스크가 아니다.
            private void FitNormalVertices(List<Vector3> body,List<Vector3> trail=null)
            {
                if(_area||Camera.main==null||body.Count==0)return;
                Camera camera=Camera.main;float depth=camera.WorldToViewportPoint(transform.position).z;
                Vector3 lower=camera.ViewportToWorldPoint(new Vector3(.012f,.012f,depth))-transform.position;
                Vector3 upper=camera.ViewportToWorldPoint(new Vector3(.988f,.988f,depth))-transform.position;
                float scale=1f;
                void Include(List<Vector3> points)
                {
                    if(points==null)return;
                    foreach(Vector3 point in points)
                    {
                        if(point.x>0f)scale=Mathf.Min(scale,upper.x/point.x);else if(point.x<0f)scale=Mathf.Min(scale,lower.x/point.x);
                        if(point.y>0f)scale=Mathf.Min(scale,upper.y/point.y);else if(point.y<0f)scale=Mathf.Min(scale,lower.y/point.y);
                    }
                }
                Include(body);Include(trail);scale=Mathf.Clamp01(scale);
                if(scale>=1f)return;
                for(int i=0;i<body.Count;i++)body[i]*=scale;
                if(trail!=null)for(int i=0;i<trail.Count;i++)trail[i]*=scale;
            }
            private static readonly float[] WindupFrames=BuildMotion(WindupSamples,false),ReleaseFrames=BuildMotion(ReleaseSamples,true);
            private static float[] BuildMotion(int count,bool release)
            {
                var frames=new float[count];for(int i=0;i<count;i++){float t=i/(float)(count-1);frames[i]=release?Smooth(t):(t<.45f?.12f*Smooth(t/.45f):.12f+.88f*Mathf.Pow((t-.45f)/.55f,3f));}return frames;
            }
            private static float SampleMotion(float t,bool release)
            {
                var frames=release?ReleaseFrames:WindupFrames;float f=Mathf.Clamp01(t)*(frames.Length-1);int a=Mathf.FloorToInt(f),b=Mathf.Min(a+1,frames.Length-1);return Mathf.Lerp(frames[a],frames[b],f-a);
            }
            private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_veil!=null)Destroy(_veil);if(_sweep!=null)Destroy(_sweep);_mesh=null;_veil=null;_sweep=null;}
        }


}
