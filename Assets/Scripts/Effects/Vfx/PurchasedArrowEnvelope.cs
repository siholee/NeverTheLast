using System;
using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>구매 Trail73의 바람 결이 짧은 화살을 감싸고, 실제 이동 이력으로만 꼬리를 남긴다.</summary>
    public sealed class PurchasedArrowEnvelope : MonoBehaviour, IDisposable
    {
        private static Sprite _veilSprite;
        private static Material _material;
        private Transform _root;
        private SpriteRenderer _veil;
        private SpriteRenderer _nucleus;
        private SpriteRenderer _nucleusGlow;
        private Color _glowColor;
        private Color _tint;
        private MeshRenderer _ribbon;
        private Mesh _mesh;
        private float _card,_weight;
        private bool _arc,_contrast,_saturated;
        private Vector3 _emission;
        private readonly List<Vector3> _track=new(),_vertices=new();
        private readonly List<float> _times=new();
        private readonly List<Vector2> _uvs=new();
        private readonly List<int> _triangles=new();
        private MaterialPropertyBlock _properties;
        private SpriteRenderer[] _pursuitVeils;
        private MaterialPropertyBlock _pursuitProperties;

        public static PurchasedArrowEnvelope Attach(FlipbookFx arrow,Unit target,float card,float weight,bool pursuit=false,bool area=false,
            Color? tint=null,bool seed=false,bool arc=false,bool contrast=false,bool saturated=false)
        {
            var envelope=arrow.gameObject.AddComponent<PurchasedArrowEnvelope>();
            envelope._contrast=contrast;envelope._saturated=saturated;
            envelope.Initialize(arrow,target,card,weight,pursuit,area,tint??ElementPalette.ColorFor("Dendro"),seed,arc);return envelope;
        }
        private void Initialize(FlipbookFx arrow,Unit target,float card,float weight,bool pursuit,bool area,Color tint,bool seed,bool arc)
        {
            _properties=new MaterialPropertyBlock();
            _root=arrow.transform;_card=card;_weight=weight;_emission=_root.position;_tint=tint;_arc=arc;
            if(_veilSprite==null)
            {
                var texture=Resources.Load<Texture2D>("VfxTex/PurchasedArcher/Veil");
                if(texture==null)return;
                // 흐르는 UV와 작은 발광 핵은 원본 alpha 바깥도 사용한다. Tight 메시로 자르지 않는다.
                _veilSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.78f,.5f),texture.width,0,SpriteMeshType.FullRect);
                _veilSprite.name="PurchasedTrail73_Veil";_veilSprite.hideFlags=HideFlags.HideAndDontSave;
            }
            if(_material==null)
                _material=new Material(Shader.Find("NeverTheLast/ArcherEnvelope")){hideFlags=HideFlags.HideAndDontSave};
            Color element=tint;
            _veil=VfxSpace.NewSprite(_root,"ElementVeil",_veilSprite,element,VfxSpace.SortingLayer(target),VfxSpace.OverlaySortingOrder+165);
            _veil.color=element;_veil.sharedMaterial=_material;
            _veil.transform.localScale=new Vector3(card*1.15f*weight,card*.42f*weight/_veilSprite.bounds.size.y,1f);
            // 다른 화살의 반투명 바람이 선두 핵을 덮지 않게 모든 바람/리본보다 앞에 분리한다.
            _nucleus=VfxSpace.NewSprite(_root,"ElementNucleus",_veilSprite,element,VfxSpace.SortingLayer(target),VfxSpace.OverlaySortingOrder+168);
            _nucleus.sharedMaterial=_material;_nucleus.transform.localScale=_veil.transform.localScale;
            _nucleusGlow=VfxSpace.NewSprite(_root,"NucleusGlow",VfxSpace.SoftDot,element,VfxSpace.SortingLayer(target),VfxSpace.OverlaySortingOrder+169,true);
            _nucleusGlow.transform.localScale=new Vector3(card*.10f*weight,card*.035f*weight,1f);
            if(pursuit)
            {
                _nucleus.transform.localScale*=1.35f;_nucleusGlow.transform.localScale*=1.35f;
                _pursuitProperties=new MaterialPropertyBlock();_pursuitVeils=new SpriteRenderer[2];
                for(int i=0;i<_pursuitVeils.Length;i++)
                {
                    var wake=VfxSpace.NewSprite(_root,"PursuitWind"+i,_veilSprite,element,
                        VfxSpace.SortingLayer(target),VfxSpace.OverlaySortingOrder+165);
                    wake.sharedMaterial=_material;
                    wake.transform.localScale=Vector3.Scale(_veil.transform.localScale,new Vector3(.78f,1.15f,1f));
                    wake.transform.localPosition=new Vector3(-card*.10f,card*(i==0?-.055f:.055f),0f);
                    wake.transform.localRotation=Quaternion.Euler(0f,0f,i==0?-8f:8f);
                    wake.enabled=false;_pursuitVeils[i]=wake;
                }
            }
            if(area)
            {
                // 광역의 연속 핵이 독 구슬 줄로 읽히지 않게 진행축을 따르는 빛줄기로 편다.
                _nucleus.transform.localScale=Vector3.Scale(_nucleus.transform.localScale,new Vector3(1.8f,.65f,1f));
                _nucleusGlow.transform.localScale=Vector3.Scale(_nucleusGlow.transform.localScale,new Vector3(2.2f,.55f,1f));
            }
            if(seed)
            {
                // 독의 씨앗은 굵은 화살대 없이 짧은 잎눈과 부드러운 꼬리로 날아간다. 상태 거품은 착탄 뒤 별도로 난다.
                _veil.transform.localScale=Vector3.Scale(_veil.transform.localScale,new Vector3(.70f,.85f,1f));
                _nucleus.transform.localScale=Vector3.Scale(_nucleus.transform.localScale,new Vector3(.48f,1.35f,1f));
                _nucleusGlow.transform.localScale=Vector3.Scale(_nucleusGlow.transform.localScale,new Vector3(.75f,1.5f,1f));
            }
            if(arc)
            {
                _veil.transform.localScale=Vector3.Scale(_veil.transform.localScale,new Vector3(1.15f,.85f,1f));
                _nucleus.transform.localScale=Vector3.Scale(_nucleus.transform.localScale,new Vector3(1.25f,1.125f,1f));
            }
            if(_saturated)
            {
                // 밝은 배경에서 탈색되는 가는 타원 대신 원소색의 굵은 바람 결과 작은 핵을 남긴다.
                _veil.transform.localScale=Vector3.Scale(_veil.transform.localScale,new Vector3(1f,1.65f,1f));
                _nucleus.transform.localScale=Vector3.Scale(_nucleus.transform.localScale,new Vector3(1f,1.45f,1f));
            }
            _glowColor=_nucleusGlow.color;
            var ribbon=new GameObject("PurchasedRibbon");ribbon.transform.SetParent(_root,false);
            _mesh=new Mesh{name="PurchasedArrowTrail"};_mesh.MarkDynamic();
            ribbon.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _ribbon=ribbon.AddComponent<MeshRenderer>();_ribbon.sharedMaterial=_material;
            _ribbon.sortingLayerID=VfxSpace.SortingLayer(target);_ribbon.sortingOrder=VfxSpace.OverlaySortingOrder+163;
            _ribbon.enabled=false;_veil.enabled=false;_nucleus.enabled=false;_nucleusGlow.enabled=false;
        }

        // 호출 주체가 기존 비행 코루틴이므로 일시정지·배속·취소 수명도 같은 경로를 따른다.
        public void Advance(float age,float travel,Vector3 point,int index)
        {
            if(_veil==null)return;
            bool visible=travel>0f;_veil.enabled=visible;_nucleus.enabled=visible;_nucleusGlow.enabled=visible;_ribbon.enabled=visible;
            if(!visible){_emission=point;return;}
            _properties.SetFloat("_Phase",age*4f+index*.19f);
            float arrival=_saturated?Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.40f,.90f,travel)):1f;
            _properties.SetFloat("_Opacity",Mathf.Clamp01(age*30f)*(_saturated?Mathf.Lerp(.38f,1f,arrival):1f));
            _nucleusGlow.color=new Color(_glowColor.r,_glowColor.g,_glowColor.b,_glowColor.a*Mathf.Clamp01(age*30f)*arrival);
            _properties.SetFloat("_Travel",travel);
            _properties.SetFloat("_TailExtent",Mathf.Min(.68f,Vector3.Distance(_emission,point)/(_card*1.15f*_weight)));
            _properties.SetColor("_Tint",_tint);
            _properties.SetFloat("_ArcProfile",_arc?1f:0f);
            _properties.SetTexture("_MainTex",_veilSprite.texture);
            _properties.SetFloat("_Ribbon",0f);_properties.SetFloat("_Nucleus",0f);_veil.SetPropertyBlock(_properties);
            if(_pursuitVeils!=null)UpdatePursuitVeils(age,point,index);
            _properties.SetFloat("_Nucleus",1f);_nucleus.SetPropertyBlock(_properties);
            _properties.SetFloat("_Nucleus",0f);
            _properties.SetFloat("_Ribbon",1f);_ribbon.SetPropertyBlock(_properties);
            if(travel<=0f)return;
            _track.Add(point);_times.Add(age);
            while(_track.Count>2&&(age-_times[0]>(_contrast?.11f:.065f)||_track.Count>(_contrast?20:12)))
            {_track.RemoveAt(0);_times.RemoveAt(0);}
            int count=_track.Count;if(count<2)return;
            _vertices.Clear();_uvs.Clear();_triangles.Clear();
            for(int i=0;i<count;i++)
            {
                float u=i/(float)(count-1);
                Vector3 tangent=_track[Mathf.Min(i+1,count-1)]-_track[Mathf.Max(i-1,0)];
                Vector3 side=new Vector3(-tangent.y,tangent.x,0f).normalized*_card*(_saturated?.100f:.060f)*_weight*Mathf.Pow(u,.65f);
                _vertices.Add(_root.InverseTransformPoint(_track[i]-side));
                _vertices.Add(_root.InverseTransformPoint(_track[i]+side));
                _uvs.Add(new Vector2(u,0));_uvs.Add(new Vector2(u,1));
                if(i==0)continue;int k=i*2;
                _triangles.Add(k-2);_triangles.Add(k-1);_triangles.Add(k);
                _triangles.Add(k);_triangles.Add(k-1);_triangles.Add(k+1);
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetUVs(0,_uvs);
            _mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();
        }

        // 논리 명중이 렌더 한 프레임 늦어져도 넓은 꼬리는 접고 작은 접점 광만 이어 준다.
        public void HoldContact(float opacity)
        {
            if(_veil==null)return;
            if(_pursuitVeils!=null)foreach(var wake in _pursuitVeils)wake.enabled=false;
            _veil.enabled=false;_ribbon.enabled=false;_nucleus.enabled=true;_nucleusGlow.enabled=true;
            _properties.SetFloat("_Ribbon",0f);_properties.SetFloat("_Nucleus",1f);
            _properties.SetFloat("_Opacity",Mathf.Clamp01(opacity));_nucleus.SetPropertyBlock(_properties);
            _nucleusGlow.color=new Color(_glowColor.r,_glowColor.g,_glowColor.b,_glowColor.a*Mathf.Clamp01(opacity));
        }

        private void UpdatePursuitVeils(float age,Vector3 point,int index)
        {
            // 단발 추격에서도 분리되는 구매 바람 두 겹. 실제 이동 거리만큼 나온 뒤에 펼친다.
            bool visible=Vector3.Distance(_emission,point)>_card*.24f;
            _veil.GetPropertyBlock(_pursuitProperties);
            for(int i=0;i<_pursuitVeils.Length;i++)
            {
                _pursuitVeils[i].enabled=visible;
                _pursuitProperties.SetFloat("_Phase",age*4f+index*.19f+(i+1)*.37f);
                _pursuitProperties.SetFloat("_Opacity",.90f);
                _pursuitVeils[i].SetPropertyBlock(_pursuitProperties);
            }
        }

        public void Dispose()
        {
            if(_mesh!=null)UnityEngine.Object.Destroy(_mesh);
            _mesh=null;
        }
        // 시전 취소·라운드 정리에서 부모만 제거되어도 비관리 GPU 메시를 회수한다.
        private void OnDestroy()=>Dispose();
    }
}
