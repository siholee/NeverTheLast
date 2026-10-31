using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>구매 Trail73의 붓결로 연결 전격·뿔피리·지원을 구분한다. 판정과 행동 대기는 소유하지 않는다.</summary>
    public sealed class PurchasedRosterStrokeFx : MonoBehaviour
    {
        public enum Kind { Link, Horn, Support, Descent, Tide, Detonation, Verdict }
        private static Material _material;
        private Unit _caster,_target;
        private Kind _kind;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private readonly List<Vector3> _vertices=new();
        private readonly List<Vector3> _uv=new();
        private readonly List<Color> _colors=new();
        private readonly List<int> _triangles=new();
        private Vector3 _source,_point;
        private Color _tint;
        private float _age,_life,_card;
        internal float Age=>_age;
        internal Kind CueKind=>_kind;
        private bool LawStroke=>_kind is Kind.Descent or Kind.Tide or Kind.Detonation or Kind.Verdict;
        internal Unit Recipient=>_target;
        public static void Play(Unit caster,Unit target,Kind kind)
        {
            if(caster==null||target==null||!target.isActive)return;
            var view=CardMotion.ViewOf(target);
            if(view?.PortraitRenderer==null||view.PortraitRenderer.sprite==null||!view.gameObject.activeInHierarchy)return;
            var texture=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Veil");
            var shader=Resources.Load<Shader>("Shaders/PurchasedWeave");
            if(texture==null||shader==null)return;
            if(_material==null)_material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,name="PurchasedRosterTrail73"};
            var root=new GameObject("PurchasedRosterStroke_"+kind);
            var fx=root.AddComponent<PurchasedRosterStrokeFx>();
            fx._caster=caster;fx._target=target;fx._kind=kind;fx._card=VfxSpace.CardSize;
            fx._tint=ElementPalette.ColorFor(caster.Element);
            if(fx.LawStroke&&QualitySettings.activeColorSpace==ColorSpace.Linear)fx._tint=fx._tint.linear;
            fx._life=kind==Kind.Descent?.38f:kind==Kind.Tide?.48f:kind==Kind.Detonation?.46f:kind==Kind.Verdict?.40f:
                kind==Kind.Link?.38f:kind==Kind.Horn?.56f:.82f;
            fx._source=PurchasedArcherFx.EmissionPoint(caster,target);fx._point=fx.LawStroke?VfxSpace.BodyContactPoint(target):VfxSpace.ContactPoint(target);
            fx._mesh=new Mesh{name="PurchasedRosterStrokes"};fx._mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=fx._mesh;
            fx._renderer=root.AddComponent<MeshRenderer>();fx._renderer.sharedMaterial=_material;
            fx._renderer.sortingLayerID=VfxSpace.SortingLayer(target);fx._renderer.sortingOrder=VfxSpace.OverlaySortingOrder+175;
            var properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",texture);fx._renderer.SetPropertyBlock(properties);
            fx._renderer.enabled=false;
        }
        private void Update()
        {
            if(Time.timeScale<=0f)return;
            _age+=Time.deltaTime;
            if(_age>=_life)Destroy(gameObject);
        }
        private static bool Visible(Unit unit)
        {
            var view=CardMotion.ViewOf(unit);
            return unit!=null&&unit.isActive&&view?.PortraitRenderer!=null&&view.PortraitRenderer.sprite!=null&&view.gameObject.activeInHierarchy;
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0f||_renderer==null)return;
            if(Visible(_target))_point=LawStroke?VfxSpace.BodyContactPoint(_target):VfxSpace.ContactPoint(_target);
            if(Visible(_caster))_source=PurchasedArcherFx.EmissionPoint(_caster,_target);
            transform.position=_point;_vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();
            int strands=LawStroke?3:_kind==Kind.Link?3:_kind==Kind.Horn?3:5;
            for(int layer=0;layer<2;layer++)for(int strand=0;strand<strands;strand++)Stroke(strand,layer==0);
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();_renderer.enabled=_vertices.Count>0;
        }
        private static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3f-2f*t);}
        private void Stroke(int strand,bool rim)
        {
            const int segments=32;
            float local=Mathf.Clamp01(_age/_life-strand*.025f);
            float reveal=Smooth(local/(_kind==Kind.Descent?.20f:_kind==Kind.Link?.38f:.26f));
            for(int i=0;i<segments;i++)
            {
                float a=i/(float)segments,b=Mathf.Min((i+1)/(float)segments,reveal);
                if(a>=b)break;
                // 부위마다 풀리는 시차를 두어 전체 형상을 한 번에 끄지 않는다.
                float grain=Mathf.Repeat((i/4+strand*3)*.618034f,1f);
                float end=.80f+.20f*grain,start=.43f+.16f*grain;
                float alpha=Smooth(local/.08f)*(1f-Smooth((local-start)/(end-start)));
                if(alpha<.002f)continue;
                Vector3 p0=Point(a,local,strand),p1=Point(b,local,strand);
                var tangent=(p1-p0).normalized;var normal=new Vector3(-tangent.y,tangent.x,0f);
                float width=_card*(_kind==Kind.Descent?.016f:_kind==Kind.Tide?.020f:
                    _kind==Kind.Detonation?.012f:_kind==Kind.Verdict?.010f:_kind==Kind.Link?.009f:.012f)*(rim?(LawStroke?2.2f:2.7f):1f);
                float w0=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(a/reveal*Mathf.PI)),.55f);
                float w1=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(b/reveal*Mathf.PI)),.55f);
                Vector3 drift=Vector3.up*_card*Smooth((local-.50f)/.5f)*(.025f+.09f*grain);
                p0+=drift;p1+=drift;
                var color=rim?new Color(_tint.r*.15f,_tint.g*.15f,_tint.b*.15f):Color.Lerp(_tint,Color.white,.15f);
                color.a=alpha*(_kind==Kind.Link?.92f:.88f)*(HitStop.PlaybackScale>2.01f?.55f:1f);
                int n=_vertices.Count;_vertices.Add(p0-normal*w0);_vertices.Add(p0+normal*w0);_vertices.Add(p1-normal*w1);_vertices.Add(p1+normal*w1);
                float u=a*.9f+strand*.17f+local*.10f,v=b*.9f+strand*.17f+local*.10f,z=rim?1f:0f;
                _uv.Add(new Vector3(u,0,z));_uv.Add(new Vector3(u,1,z));_uv.Add(new Vector3(v,0,z));_uv.Add(new Vector3(v,1,z));
                for(int j=0;j<4;j++)_colors.Add(color);
                _triangles.Add(n);_triangles.Add(n+1);_triangles.Add(n+2);_triangles.Add(n+2);_triangles.Add(n+1);_triangles.Add(n+3);
            }
        }
        private Vector3 Point(float u,float phase,int strand)
        {
            if(_kind==Kind.Descent)
                return new Vector3(((strand-1)*.055f+Mathf.Sin(u*9f+strand)*.035f)*(1f-u)*_card,(1f-u)*.82f*_card,0f);
            if(_kind==Kind.Tide)
                return new Vector3((u-.5f)*1.04f*_card,(Mathf.Sin(u*Mathf.PI*2f+strand*.8f)*.08f+(strand-1)*.035f)*_card,0f);
            if(_kind==Kind.Detonation)
                return new Vector3(((u-.5f)*.65f+strand*.02f)*_card,
                    (Mathf.Sin((u+strand*.12f)*Mathf.PI)*.12f+(strand-1)*.06f+phase*.04f)*_card,0f);
            if(_kind==Kind.Verdict)
                return new Vector3((.17f+(strand-1)*.055f+u*.028f)*_card,(-.04f+u*.22f+(strand-1)*.025f)*_card,0f);
            if(_kind==Kind.Link)
            {
                Vector3 from=_source-_point;var direction=(-from).normalized;var normal=new Vector3(-direction.y,direction.x,0f);
                float jag=Mathf.Sin(u*Mathf.PI*8f+strand*.9f)*Mathf.Sin(u*Mathf.PI)*_card*.035f;
                return from*(1f-u)+normal*(jag+(strand-1)*.016f*_card);
            }
            if(_kind==Kind.Horn)
            {
                float angle=Mathf.Lerp(-1.10f,1.10f,u),radius=(.20f+strand*.14f+phase*.17f)*_card;
                return new Vector3(Mathf.Cos(angle)*radius-.18f*_card,Mathf.Sin(angle)*radius*.46f,0f);
            }
            float side=strand-2;
            return new Vector3((u-.5f)*_card*.96f,
                (-.16f+side*.055f+Mathf.Sin(u*Mathf.PI*2f+strand*.7f+phase)*.11f+phase*.10f)*_card,0f);
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);_mesh=null;}
    }
}
