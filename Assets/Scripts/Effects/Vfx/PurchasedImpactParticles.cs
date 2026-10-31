using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>Hit4의 구매 불티·파편과 Trail73의 붓결을 분리해 시차로 해체한다. 피해·상태·행동 대기는 소유하지 않는다.</summary>
    public sealed class PurchasedImpactParticles : MonoBehaviour
    {
        private static Material _material;
        private Unit _target,_source;
        private Vector3 _lastContact;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private readonly List<Vector3> _vertices=new(600);
        private readonly List<Vector3> _uv=new(600);
        private readonly List<Color> _colors=new(600);
        private readonly List<int> _triangles=new(900);
        private Color _tint;
        private float _age,_size,_opacity;
        private bool _seed,_ward,_centered,_solar,_verse;
        private const float Life=1.02f;
        internal float Age=>_age;
        internal Unit Recipient=>_target;

        public static void Play(Unit target,Vector3 direction,float weight,Color tint,float opacity,bool seed=false,bool ward=false,bool centered=false,Unit source=null)
        {
            if(target==null)return;
            var shader=Resources.Load<Shader>("Shaders/PurchasedImpactParticles");
            var spark=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Spark");
            var debris=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Debris");
            var veil=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Veil");
            if(shader==null||spark==null||debris==null||veil==null)return;
            if(_material==null)_material=new Material(shader){name="PurchasedHit4_Particles",hideFlags=HideFlags.HideAndDontSave};
            var root=new GameObject("PurchasedImpactParticles_Hit4");
            var fx=root.AddComponent<PurchasedImpactParticles>();fx._target=target;fx._source=source;fx._solar=source!=null&&source.ID is 120 or 220;fx._verse=source!=null&&source.ID is 2 or 3;
            fx._lastContact=fx._verse?VerseReaperFx.Point(target):fx._solar?PurchasedProjectileFx.ContactFor(source,target,true):centered?VfxSpace.BodyContactPoint(target):VfxSpace.ContactPoint(target);root.transform.position=fx._lastContact;
            root.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            fx._size=VfxSpace.CardSize*weight;fx._tint=(fx._solar||fx._verse||source!=null&&source.ID is 62 or 81 or 7 or 123 or 1 or 60 or 61 or 66)&&QualitySettings.activeColorSpace==ColorSpace.Linear?tint.linear:tint;
            fx._seed=seed;fx._ward=ward;fx._centered=centered;
            fx._opacity=Mathf.Clamp01(opacity)*(HitStop.PlaybackScale>2.01f?.22f:1f);
            fx._mesh=new Mesh{name="PurchasedHit4_ParticleMesh"};fx._mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=fx._mesh;
            fx._renderer=root.AddComponent<MeshRenderer>();fx._renderer.sharedMaterial=_material;
            fx._renderer.sortingLayerID=VfxSpace.SortingLayer(target);
            fx._renderer.sortingOrder=VfxSpace.OverlaySortingOrder+170;
            var block=new MaterialPropertyBlock();block.SetTexture("_Spark",spark);block.SetTexture("_Debris",debris);block.SetTexture("_Veil",veil);
            fx._renderer.SetPropertyBlock(block);fx._renderer.enabled=false;
        }

        private void Update()
        {
            if(Time.timeScale<=0f)return;
            _age+=Time.deltaTime;
            if(_age>=Life)Destroy(gameObject);
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0f||_renderer==null)return;
            var view=CardMotion.ViewOf(_target);
            if(_target!=null&&_target.isActive&&view?.PortraitRenderer!=null&&view.gameObject.activeInHierarchy&&view.PortraitRenderer.sprite!=null)
                _lastContact=_verse?VerseReaperFx.Point(_target):_solar?PurchasedProjectileFx.ContactFor(_source,_target,true):_centered?VfxSpace.BodyContactPoint(_target):VfxSpace.ContactPoint(_target);
            transform.position=_lastContact;
            _vertices.Clear();_uv.Clear();_colors.Clear();_triangles.Clear();
            // 먹빛 외연을 먼저 깔고 구매 원화의 밝은 중심을 한 번 합성한다. 가산 섬광은 없다.
            for(int layer=0;layer<2;layer++)
            {
                bool rim=layer==0;
                if(_ward)for(int i=0;i<8;i++)WardShard(i,rim);
                else
                {
                    for(int i=0;i<32;i++)Particle(i,rim);
                    for(int i=0;i<3;i++)Sweep(i,rim);
                }
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uv);_mesh.SetColors(_colors);_mesh.SetTriangles(_triangles,0);
            _mesh.RecalculateBounds();_renderer.enabled=_vertices.Count>0;
        }
        private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3f-2f*x);}
        // 전투 RNG를 소비하지 않는 장식용 고정 분포다. 테스트 전투의 시드를 고정하는 함수가 아니다.
        private static float Grain(int i,int salt)=>Mathf.Repeat(Mathf.Sin((i+3)*17.17f+(salt+2)*31.73f)*1367.13f,1f);
        private Color Paint(float alpha,bool rim,float light)
        {
            var color=rim?_tint*.18f:Color.Lerp(_tint,new Color(.96f,.99f,1f),light);
            color.a=alpha*_opacity*(rim?.64f:.86f);return color;
        }
        private void Particle(int index,bool rim)
        {
            bool streak=index<12;
            int wave=index%3;
            float born=.008f+wave*.057f+Grain(index,0)*.028f;
            float life=streak?.34f+Grain(index,1)*.21f:.46f+Grain(index,1)*.32f;
            bool earlyBurst=!streak&&index<20;
            // 모든 착탄은 작은 조각8개가 첫 렌더부터 핵의 바깥에 이미 갈라져 나온다.
            if(earlyBurst)born=-.032f-Grain(index,0)*.012f;
            if(_solar||_verse)life*=_solar?.50f:.70f;
            float phase=(_age-born)/life;if(phase<=0f||phase>=1f)return;
            float angle=(index*137.508f+Grain(index,2)*24f)*Mathf.Deg2Rad;
            var ray=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            // 진행 방향으로 조금 더 길게 흩어진다. 방사형 원 테두리를 만들지 않고 여백을 둔다.
            float distance=(.23f+Grain(index,3)*.40f)*(ray.x>0f?1.2f:.80f);
            float travel=1f-Mathf.Pow(1f-phase,2.6f);
            var position=ray*(.035f+distance*travel)+new Vector2(.12f*phase,Mathf.Sin(phase*Mathf.PI)*(.025f+Grain(index,4)*.07f));
            if(earlyBurst)position+=ray*(.12f+Grain(index,7)*.08f);
            if(_centered)position=ray*(.012f+distance*travel*(_solar?.30f:.72f));
            float fade=Smooth(phase/.08f)*(1f-Smooth(Mathf.InverseLerp(.30f,1f,phase)));
            float length=streak?Mathf.Lerp(.09f+Grain(index,5)*.15f,.022f,phase):.028f+Grain(index,5)*.035f;
            if(streak)length*=.60f+.40f*Grain(index,7);
            if(earlyBurst)length*=_seed?1.2f:1.15f;
            float width=streak?.014f+Grain(index,6)*.015f:length*(.65f+Grain(index,6)*.45f);
            if(_solar||_verse){length*=1.35f;width*=1.45f;}
            float shrink=1f-.60f*Smooth(phase);
            var tangent=(ray+new Vector2(.22f,0f)).normalized;
            if(streak)
            {
                float skew=(25f+Grain(index,8)*15f)*(index%2==0?-1f:1f)*Mathf.Deg2Rad;
                tangent=new Vector2(tangent.x*Mathf.Cos(skew)-tangent.y*Mathf.Sin(skew),tangent.x*Mathf.Sin(skew)+tangent.y*Mathf.Cos(skew));
            }
            Vector2 along=tangent*length*shrink*(rim?1.35f:1f),across=new Vector2(-tangent.y,tangent.x)*width*shrink*(rim?1.8f:1f);
            Color color=Paint(fade,rim,streak?.38f:.15f);
            int quadrant=index%4;
            bool shard=earlyBurst&&index%3==1;
            Rect uv=streak||shard?new Rect(0,0,1,1):new Rect((quadrant%2)*.5f,(quadrant/2)*.5f,.5f,.5f);
            Quad(position,along,across,uv,streak||shard?0f:1f,color);
        }
        // 보호막은 구매 Hit4 Romb3(Spark)의 작은 마름모 8개를 올린다. 공격 파편이나 피해를 발행하지 않는다.
        private void WardShard(int index,bool rim)
        {
            float phase=(_age-(.025f+index*.033f))/(.50f+index*.025f);
            if(phase<=0f||phase>=1f)return;
            float sign=index%2==0?-1f:1f;
            var point=new Vector2(sign*(.23f+index/2*.018f)+Mathf.Sin(phase*Mathf.PI)*sign*.04f,
                -.16f+phase*.64f+index/2*.035f);
            float fade=Smooth(phase/.16f)*(1f-Smooth(Mathf.InverseLerp(.38f,1f,phase)));
            float size=(.035f+Grain(index,3)*.025f)*(1f-.6f*phase);
            var along=new Vector2(.25f,1f).normalized*size*(rim?1.35f:1f);
            var across=new Vector2(-along.y,along.x)*.48f*(rim?1.5f:1f);
            Quad(point,along,across,new Rect(0,0,1,1),0f,Paint(fade,rim,.15f));
        }
        private void Sweep(int index,bool rim)
        {
            float born=.014f+index*.046f,life=.46f+index*.06f;
            if(_solar||_verse)life*=_solar?.60f:.75f;
            float phase=(_age-born)/life;if(phase<=0f||phase>=1f)return;
            float fade=Smooth(phase/.12f)*(1f-Smooth(Mathf.InverseLerp(.20f,1f,phase)));
            float angle=(-62f+index*67f)*Mathf.Deg2Rad;
            var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var normal=new Vector2(-direction.y,direction.x);
            const int segments=12;
            for(int s=0;s<segments;s++)
            {
                float u0=s/(float)segments,u1=(s+1f)/segments;
                Vector2 Point(float u)=>direction*(.055f+(.33f+phase*.20f)*u)+normal*(Mathf.Sin(u*Mathf.PI)*(.09f+index*.012f)*(1f-phase));
                var a=Point(u0);var b=Point(u1);var tangent=(b-a).normalized;
                var side=new Vector2(-tangent.y,tangent.x)*((_solar||_verse?.022f:.014f)*(rim?1.9f:1f))*(1f-phase);
                int start=_vertices.Count;
                Add(a-side,new Vector3(u0,0,2),Paint(fade*(1f-u0*.70f),rim,.18f));
                Add(a+side,new Vector3(u0,1,2),Paint(fade*(1f-u0*.70f),rim,.18f));
                Add(b+side,new Vector3(u1,1,2),Paint(fade*(1f-u1*.70f),rim,.18f));
                Add(b-side,new Vector3(u1,0,2),Paint(fade*(1f-u1*.70f),rim,.18f));
                Triangles(start);
            }
        }
        private void Quad(Vector2 point,Vector2 along,Vector2 across,Rect uv,float texture,Color color)
        {
            int start=_vertices.Count;
            Add(point-along-across,new Vector3(uv.xMin,uv.yMin,texture),color);
            Add(point-along+across,new Vector3(uv.xMin,uv.yMax,texture),color);
            Add(point+along+across,new Vector3(uv.xMax,uv.yMax,texture),color);
            Add(point+along-across,new Vector3(uv.xMax,uv.yMin,texture),color);Triangles(start);
        }
        private void Add(Vector2 point,Vector3 uv,Color color){_vertices.Add(point*_size);_uv.Add(uv);_colors.Add(color);}
        private void Triangles(int i){_triangles.Add(i);_triangles.Add(i+1);_triangles.Add(i+2);_triangles.Add(i);_triangles.Add(i+2);_triangles.Add(i+3);}
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);}
    }
}
