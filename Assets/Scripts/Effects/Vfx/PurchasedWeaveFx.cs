using System;
using System.Collections.Generic;
using Entities;
using UnityEngine;
namespace Effects.Vfx
{
    /// <summary>구매 Trail73의 농담을 가는 실과 먹빛 테두리로 재사용한다. 강화·피해·행동 시간은 소유하지 않는다.</summary>
    public sealed class PurchasedWeaveFx : MonoBehaviour
    {
        public enum Kind { Cast,Receive,Catalysis }
        private static Material _material;
        private Unit _caster,_target;
        private Kind _kind;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private readonly List<Vector3> _vertices=new();
        private readonly List<Vector3> _uv=new();
        private readonly List<Color> _colors=new();
        private readonly List<int> _triangles=new();
        private Vector3 _lastTarget,_lastSource;
        private float _age,_delay,_life,_card;
        private Color _tint;
        internal Unit Recipient=>_target;
        internal float Phase=>Mathf.Clamp01((_age-_delay)/_life);
        internal int PhaseIndex=>Mathf.FloorToInt(Phase*59);
        internal bool IsVisible=>_renderer!=null&&_renderer.enabled;
        public static void Play(Unit caster,Unit target,Kind kind)
        {
            if(target==null||!target.isActive)return;
            var texture=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Veil");
            var shader=Resources.Load<Shader>("Shaders/PurchasedWeave");
            if(texture==null||shader==null)return;
            if(_material==null)_material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,name="PurchasedTrail73_Weave"};
            var root=new GameObject("PurchasedWeave_"+kind);
            var fx=root.AddComponent<PurchasedWeaveFx>();fx._caster=caster;fx._target=target;fx._kind=kind;
            fx._card=VfxSpace.CardSize;fx._life=VfxTiming.AtLeastFrames(kind==Kind.Catalysis?.82f:.94f,6);
            fx._delay=kind==Kind.Receive&&caster!=null?.06f:0f;fx._tint=ElementPalette.ColorFor("Electro");
            fx._lastTarget=VfxSpace.ContactPoint(target);
            fx._lastSource=caster!=null?PurchasedArcherFx.EmissionPoint(caster,target):fx._lastTarget;
            fx._mesh=new Mesh{name="PurchasedWeaveThreads"};fx._mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=fx._mesh;fx._renderer=root.AddComponent<MeshRenderer>();
            fx._renderer.sharedMaterial=_material;fx._renderer.sortingLayerID=VfxSpace.SortingLayer(target);
            fx._renderer.sortingOrder=VfxSpace.OverlaySortingOrder+176;fx._renderer.enabled=false;
            var properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",texture);fx._renderer.SetPropertyBlock(properties);
            if(kind==Kind.Cast)CardMotion.ViewOf(target)?.PlayRangedWindup(Vector3.right,.12f);
        }
        private void Update()
        {
            if(Time.timeScale<=0f)return;
            _age+=Time.deltaTime;
            if(_age>=_delay+_life){Destroy(gameObject);return;}
            if(_age<_delay&&(_target==null||!_target.isActive)){Destroy(gameObject);return;}
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0f||_renderer==null)return;
            if(_age<_delay){_renderer.enabled=false;return;}
            var view=CardMotion.ViewOf(_target);
            if(_target!=null&&_target.isActive&&view?.PortraitRenderer!=null&&view.gameObject.activeInHierarchy&&view.PortraitRenderer.sprite!=null)
                _lastTarget=VfxSpace.ContactPoint(_target);
            var sourceView=CardMotion.ViewOf(_caster);
            if(_caster!=null&&_caster.isActive&&sourceView?.PortraitRenderer!=null&&sourceView.gameObject.activeInHierarchy&&sourceView.PortraitRenderer.sprite!=null)
                _lastSource=PurchasedArcherFx.EmissionPoint(_caster,_target);
            transform.position=_lastTarget;
            _vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();
            float phase=Phase;int strands=_kind==Kind.Catalysis?3:_kind==Kind.Cast?4:5;
            for(int layer=0;layer<2;layer++)
            {
                bool rim=layer==0;
                if(_kind==Kind.Receive&&_caster!=null&&_caster!=_target)
                    Ribbon(phase,rim,-1,true);
                for(int strand=0;strand<strands;strand++)Ribbon(phase,rim,strand,false);
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();_renderer.enabled=_vertices.Count>0;
        }
        private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3f-2f*x);}
        private static float Grain(int strand,int piece)=>Mathf.Repeat(Mathf.Sin((strand+5)*17.17f+(piece+2)*31.73f)*1367.13f,1f);
        private void Ribbon(float phase,bool rim,int strand,bool link)
        {
            const int segments=28;
            float local=Mathf.Clamp01(phase-Mathf.Max(0,strand)*.012f);
            float reveal=Smooth(local/.20f);if(reveal<=.001f)return;
            for(int i=0;i<segments;i++)
            {
                float a=i/(float)segments,b=(i+1)/(float)segments;
                if(a>=reveal)break;b=Mathf.Min(b,reveal);
                int piece=i/4;float scatter=Grain(strand,piece);
                float ending=link?.72f+.24f*(1f-a):.82f+.16f*scatter;
                float fadeStart=link?.42f+.16f*(1f-a):.46f+.06f*scatter;
                float fade=1f-Smooth((local-fadeStart)/(ending-fadeStart));
                float alpha=Smooth(local/.11f)*fade*(link?.96f:_kind==Kind.Catalysis?.80f:.96f);
                if(alpha<.002f)continue;
                Vector3 p0=Point(a,local,strand,link),p1=Point(b,local,strand,link);
                Vector3 tangent=(p1-p0).normalized,normal=new(-tangent.y,tangent.x,0f);
                float unthread=Smooth((local-.52f)/.46f);
                Vector3 drift=(normal*(scatter-.5f)*.23f+Vector3.up*(.035f+.075f*scatter))*_card*unthread;
                p0+=drift;p1+=drift;
                float width=(link?.009f:.0105f)*_card*(rim?(link?2.8f:_kind==Kind.Catalysis?2.05f:2.4f):1f);
                if(link)width*=Mathf.Lerp(.35f,1f,fade);
                float w0=width*Taper(a,reveal,strand),w1=width*Taper(b,reveal,strand);
                float darkness=_kind==Kind.Catalysis?.23f:.18f;
                var color=rim?new Color(_tint.r*darkness,_tint.g*darkness,_tint.b*darkness,alpha):Color.Lerp(_tint,Color.white,.32f);
                color.a=alpha;
                int n=_vertices.Count;
                _vertices.Add(p0-normal*w0);_vertices.Add(p0+normal*w0);
                _vertices.Add(p1-normal*w1);_vertices.Add(p1+normal*w1);
                float offset=(Mathf.Max(0,strand)*.17f)+local*.11f;
                float inkBoost=rim&&_kind!=Kind.Catalysis?1f:0f;
                _uv.Add(new Vector3(a*.9f+offset,0,inkBoost));_uv.Add(new Vector3(a*.9f+offset,1,inkBoost));
                _uv.Add(new Vector3(b*.9f+offset,0,inkBoost));_uv.Add(new Vector3(b*.9f+offset,1,inkBoost));
                for(int j=0;j<4;j++)_colors.Add(color);
                _triangles.Add(n);_triangles.Add(n+1);_triangles.Add(n+2);
                _triangles.Add(n+2);_triangles.Add(n+1);_triangles.Add(n+3);
            }
        }
        private static float Taper(float u,float reveal,int strand)
            =>Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*u/reveal)),.65f)*(.83f+.17f*Mathf.Sin(u*23f+strand*1.7f));
        private Vector3 Point(float u,float phase,int strand,bool link)
        {
            if(link)
            {
                Vector3 start=_lastSource-_lastTarget;
                return Vector3.Lerp(start,Vector3.zero,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*_card*.065f;
            }
            float side=(strand-(_kind==Kind.Cast?1.5f:_kind==Kind.Catalysis?1f:2f));
            float x=(u-.5f)*_card*(_kind==Kind.Cast?1.12f:1.02f);
            float curl=Mathf.Sin(u*Mathf.PI*2f+strand*.9f+phase*.7f)*.105f;
            float y=(_kind==Kind.Cast?-.10f:-.12f)+side*.060f+curl;
            float gather=1f-.25f*Smooth(phase/.22f);
            y*=gather;
            return new Vector3(x,y*_card,0f);
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);_mesh=null;}
    }
}
