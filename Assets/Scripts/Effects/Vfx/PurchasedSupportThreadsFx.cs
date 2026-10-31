using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>구매 Trail73의 얇은 결로 지휘 연결·수혜·실제 회복을 그린다. 판정·대기·구독을 소유하지 않는다.</summary>
    [DefaultExecutionOrder(4500)]
    public sealed class PurchasedSupportThreadsFx : MonoBehaviour
    {
        public enum Kind { Order, Receive, Heal, Ward }
        private static Material _material;
        private Unit _caster,_target;
        private Kind _kind;
        private Vector3 _source,_point;
        private Color _tint;
        private float _age,_life,_card;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private readonly List<Vector3> _vertices=new(),_uv=new();
        private readonly List<Color> _colors=new();
        private readonly List<int> _triangles=new();
        internal Unit Recipient=>_target;
        internal float Age=>_age;
        internal Kind CueKind=>_kind;
        internal Color Tint=>_tint;
        private static bool Visible(Unit unit)
        {
            var view=unit!=null?CardMotion.ViewOf(unit):null;
            return unit!=null&&unit.isActive&&view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null&&view.gameObject.activeInHierarchy;
        }
        public static void Play(Unit caster,Unit target,Kind kind,bool prepareCaster=true)
        {
            if(!Visible(target))return;
            if(kind==Kind.Order&&(!Visible(caster)||caster==target))return;
            var texture=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Veil");
            var shader=Resources.Load<Shader>("Shaders/PurchasedWeave");
            if(texture==null||shader==null)return;
            if(_material==null)_material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,name="PurchasedSupportTrail73"};
            var root=new GameObject("PurchasedSupportThreads_"+kind);
            var fx=root.AddComponent<PurchasedSupportThreadsFx>();fx._caster=caster;fx._target=target;fx._kind=kind;
            fx._card=VfxSpace.CardSize;fx._life=VfxTiming.AtLeastFrames(kind==Kind.Order?.58f:kind==Kind.Ward?1.02f:.86f,6);
            fx._point=kind==Kind.Ward?VfxSpace.BodyContactPoint(target):VfxSpace.ContactPoint(target);fx._source=Visible(caster)?PurchasedArcherFx.EmissionPoint(caster,target):fx._point;
            fx._tint=kind==Kind.Ward?Managers.UI.Theme.UITheme.Shield:ElementPalette.ColorFor(caster!=null?caster.Element:target.Element);
            if(kind==Kind.Ward&&QualitySettings.activeColorSpace==ColorSpace.Linear)fx._tint=fx._tint.linear;
            fx._mesh=new Mesh{name="PurchasedSupportThreadsMesh"};fx._mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=fx._mesh;fx._renderer=root.AddComponent<MeshRenderer>();
            fx._renderer.sharedMaterial=_material;fx._renderer.sortingLayerID=VfxSpace.SortingLayer(target);
            fx._renderer.sortingOrder=VfxSpace.OverlaySortingOrder+176;fx._renderer.enabled=false;
            var properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",texture);fx._renderer.SetPropertyBlock(properties);
            if(prepareCaster&&kind==Kind.Heal&&Visible(caster))PurchasedArcherFx.RosterPreparation(caster,target);
            if(kind==Kind.Ward)PurchasedImpactParticles.Play(target,Vector3.right,.80f,fx._tint,.86f,ward:true);
        }
        private void Update()
        {
            if(Time.timeScale<=0f)return;
            _age+=Time.deltaTime;
            if(_age>=_life){Destroy(gameObject);return;}
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0f||_renderer==null)return;
            if(Visible(_target))_point=_kind==Kind.Ward?VfxSpace.BodyContactPoint(_target):VfxSpace.ContactPoint(_target);
            if(Visible(_caster))_source=PurchasedArcherFx.EmissionPoint(_caster,_target);
            transform.position=_point;_vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();
            float phase=Mathf.Clamp01(_age/_life);
            for(int layer=0;layer<2;layer++)
            {
                bool rim=layer==0;
                if(_kind==Kind.Ward)for(int panel=0;panel<3;panel++)WardPanel(phase,panel,rim);
                else if(_kind==Kind.Order)for(int strand=0;strand<2;strand++)Thread(phase,strand,rim,true);
                else
                {
                    if(_kind==Kind.Heal&&_caster!=null&&_caster!=_target)Thread(phase,0,rim,true);
                    for(int strand=0;strand<(_kind==Kind.Ward?4:3);strand++)Thread(phase,strand,rim,false);
                }
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();_renderer.enabled=_vertices.Count>0;
        }
        private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3f-2f*x);}
        private void WardPanel(float phase,int panel,bool rim)
        {
            float local=Mathf.Clamp01((phase-panel*.045f)/(1f-panel*.045f)),rise=Smooth(local/.27f);
            if(rise<.002f)return;
            float side=panel==0?0f:panel==1?-1f:1f;
            float center=side*.235f*_card,width=(panel==0?.19f:.145f)*_card;
            float height=(panel==0?.65f:.58f)*_card;
            Vector3 Shape(float u)
            {
                float y=(-.28f+u*height/_card)*_card;
                float half=width*(u<.17f?u/.17f:u>.82f?Mathf.Lerp(1f,.58f,(u-.82f)/.18f):1f);
                return new Vector3(center,y,half);
            }
            // 아래에서 솟는 세 면이 차례로 접혀 보호 형상을 만든다. 면과 테두리도 조각별로 먼저 풀린다.
            const int bands=24;
            for(int i=0;i<bands;i++)
            {
                float a=i/(float)bands,b=(i+1)/(float)bands;
                float grain=Mathf.Repeat((i/3+panel*5)*.618034f,1f);
                float opacity=Smooth(local/.10f)*(1f-Smooth((local-(.39f+.12f*grain))/(.30f+.16f*grain)));
                if(opacity<.002f)continue;
                Vector3 pa=Shape(a),pb=Shape(b);float yaw=panel==0?1f:.70f;
                float y0=-.28f*_card+(pa.y+.28f*_card)*rise,y1=-.28f*_card+(pb.y+.28f*_card)*rise;
                Vector3 drift=new Vector3(side*.04f,.045f)*_card*Smooth((local-.42f)/.56f);
                Color color=rim?new Color(_tint.r*.25f,_tint.g*.25f,_tint.b*.25f):Color.Lerp(_tint,Color.white,.12f);
                float alpha=opacity*(rim?.85f:.30f)*(HitStop.PlaybackScale>2.01f?.55f:1f);
                if(!rim)
                    WardQuad(new(center-pa.z*yaw,y0),new(center-pb.z*yaw,y1),new(center+pb.z*yaw,y1),new(center+pa.z*yaw,y0),color,alpha,a,b,drift,false);
                for(int edge=-1;edge<=1;edge+=2)
                {
                    Vector3 p0=new(center+edge*pa.z*yaw,y0),p1=new(center+edge*pb.z*yaw,y1);
                    Vector3 normal=new Vector3(-(p1-p0).y,(p1-p0).x).normalized;
                    float w=_card*(rim?.009f:.004f);
                    WardQuad(p0-normal*w,p1-normal*w,p1+normal*w,p0+normal*w,color,opacity*(rim?.84f:.88f),a,b,drift,rim);
                }
            }
        }
        private void WardQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float alpha,float u,float v,Vector3 drift,bool rim)
        {
            int n=_vertices.Count;_vertices.Add(a+drift);_vertices.Add(b+drift);_vertices.Add(c+drift);_vertices.Add(d+drift);
            _uv.Add(new(u*.9f,0,rim?1:0));_uv.Add(new(v*.9f,0,rim?1:0));_uv.Add(new(v*.9f,1,rim?1:0));_uv.Add(new(u*.9f,1,rim?1:0));
            color.a=alpha;for(int j=0;j<4;j++)_colors.Add(color);
            _triangles.Add(n);_triangles.Add(n+1);_triangles.Add(n+2);_triangles.Add(n);_triangles.Add(n+2);_triangles.Add(n+3);
        }
        private void Thread(float phase,int strand,bool rim,bool link)
        {
            const int segments=32;
            float delay=strand*.035f;
            float local=Mathf.Clamp01((phase-delay)/(1f-delay)),reveal=Smooth(local/.24f);
            if(reveal<=.001f)return;
            for(int i=0;i<segments;i++)
            {
                float a=i/(float)segments,b=Mathf.Min((i+1)/(float)segments,reveal);if(a>=b)break;
                float grain=Mathf.Repeat((i/4+strand*3)*.618034f,1f);
                float start=.42f+.14f*grain,end=.80f+.18f*grain;
                if(_kind==Kind.Ward){start=.30f+.28f*grain;end=.50f+.40f*grain;}
                float alpha=Smooth(local/.12f)*(1f-Smooth((local-start)/(end-start)));
                if(alpha<.002f)continue;
                Vector3 p0=Point(a,local,strand,link),p1=Point(b,local,strand,link);
                var direction=(p1-p0).normalized;var normal=new Vector3(-direction.y,direction.x,0f);
                Vector3 drift=(normal*(grain-.5f)*.12f+Vector3.up*(.04f+.06f*grain))*_card*Smooth((local-.50f)/.48f);
                p0+=drift;p1+=drift;
                float width=_card*(link?.011f:_kind==Kind.Ward?.026f:.014f)*(rim?1.65f:1f);
                if(_kind==Kind.Ward)width*=.60f+.60f*grain;
                float w0=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(a/reveal*Mathf.PI)),.65f);
                float w1=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(b/reveal*Mathf.PI)),.65f);
                var color=rim?new Color(_tint.r*.22f,_tint.g*.22f,_tint.b*.22f):Color.Lerp(_tint,Color.white,.25f);
                color.a=alpha*(HitStop.PlaybackScale>2.01f?.55f:.92f);
                int n=_vertices.Count;_vertices.Add(p0-normal*w0);_vertices.Add(p0+normal*w0);_vertices.Add(p1-normal*w1);_vertices.Add(p1+normal*w1);
                float u=a*.9f+strand*.19f+local*.10f,v=b*.9f+strand*.19f+local*.10f;
                float ink=rim?1f:0f;
                _uv.Add(new Vector3(u,0,ink));_uv.Add(new Vector3(u,1,ink));_uv.Add(new Vector3(v,0,ink));_uv.Add(new Vector3(v,1,ink));
                for(int j=0;j<4;j++)_colors.Add(color);
                _triangles.Add(n);_triangles.Add(n+1);_triangles.Add(n+2);_triangles.Add(n+2);_triangles.Add(n+1);_triangles.Add(n+3);
            }
        }
        private Vector3 Point(float u,float phase,int strand,bool link)
        {
            if(link)
            {
                Vector3 from=_source-_point,normal=new Vector3(-from.y,from.x,0f).normalized;
                return from*(1f-u)+normal*(Mathf.Sin(u*Mathf.PI)*_card*(.075f+strand*.018f));
            }
            if(_kind==Kind.Ward)
            {
                float sign=strand%2==0?-1f:1f;
                return new Vector3(sign*Mathf.Sin((.08f+u*.84f)*Mathf.PI)*(.24f+strand/2*.055f)*_card,
                    (-.18f+u*.62f+phase*.04f)*_card,0f);
            }
            float side=strand-1;
            return new Vector3((side*.15f+Mathf.Sin(u*Mathf.PI)*side*.085f+Mathf.Sin(u*5f+strand)*.025f)*_card,
                (-.14f+u*.44f+phase*.05f+Mathf.Sin(u*Mathf.PI*2f+strand)*.025f)*_card,0f);
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);_mesh=null;}
    }
}
