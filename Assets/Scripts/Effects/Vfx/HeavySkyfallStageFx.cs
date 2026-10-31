using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>초월 낙하의 환경 전환. 피해·카메라·HUD·턴 시계는 소유하지 않는다.</summary>
    public sealed class HeavySkyfallStageFx : MonoBehaviour
    {
        private Mesh _mesh; private Material _material; private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties; private Camera _camera;
        private float _age, _landAge=-1f, _cancelAge=-1f; private Vector3 _impact;
        public float Age => _age;
        public bool HasLanded => _landAge>=0f;
        public static HeavySkyfallStageFx Begin(Transform owner, Vector3 impact, Color palette)
        {
            var texture=Resources.Load<Texture2D>("VfxTex/Signature/SurtrDoomscape51");
            var shader=Resources.Load<Shader>("Shaders/HeavySkyfallStage");
            if(texture==null||shader==null||Camera.main==null)return null;
            var fx=new GameObject("HeavySkyfall_Environment").AddComponent<HeavySkyfallStageFx>();
            fx.transform.SetParent(owner,false);fx._camera=Camera.main;fx._impact=impact;
            fx._mesh=new Mesh{name="HeavySkyfallFullscreen"};fx._mesh.MarkDynamic();
            fx.gameObject.AddComponent<MeshFilter>().sharedMesh=fx._mesh;
            fx._material=new Material(shader){name="HeavySkyfallEnvironment",hideFlags=HideFlags.HideAndDontSave};
            fx._renderer=fx.gameObject.AddComponent<MeshRenderer>();fx._renderer.sharedMaterial=fx._material;
            // 원래 배경 위/카드 아래의 환경층이다. 공격 원화는 기존 CombatVfx 앞 정렬을 유지한다.
            fx._renderer.sortingLayerID=SortingLayer.layers[0].id;fx._renderer.sortingOrder=-680;
            fx._properties=new MaterialPropertyBlock();fx._properties.SetTexture("_MainTex",texture);
            fx._properties.SetColor("_ElementColor",palette);fx._renderer.SetPropertyBlock(fx._properties);
            return fx;
        }
        public void Land(Vector3 impact){if(_landAge>=0f||_cancelAge>=0f)return;_landAge=_age;_impact=impact;}
        public void Cancel(){if(_cancelAge<0f)_cancelAge=_age;}
        private void LateUpdate()
        {
            if(Time.timeScale<=0f||_camera==null||_mesh==null)return;
            _age+=Time.deltaTime;
            float after=_landAge<0f?-1f:_age-_landAge;
            float entry=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(_age/.18f));
            float exit=after<0f?1f:1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((after-.96f)/.54f));
            if(_cancelAge>=0f)exit*=1f-Mathf.Clamp01((_age-_cancelAge)/.18f);
            float depth=_camera.WorldToViewportPoint(_impact).z;
            Vector3[] points={new(0f,0f,depth),new(0f,1f,depth),new(1f,1f,depth),new(1f,0f,depth)};
            for(int i=0;i<4;i++)points[i]=transform.InverseTransformPoint(_camera.ViewportToWorldPoint(points[i]));
            _mesh.Clear();_mesh.vertices=points;_mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            _mesh.triangles=new[]{0,1,2,0,2,3};_mesh.RecalculateBounds();
            Vector3 origin=_camera.WorldToViewportPoint(_impact);
            _properties.SetVector("_Origin",new Vector4(origin.x,origin.y,_camera.aspect,0f));
            _properties.SetFloat("_Age",_age);_properties.SetFloat("_After",after);
            _properties.SetFloat("_Opacity",entry*exit);_renderer.SetPropertyBlock(_properties);
            if(exit<=0f)Destroy(gameObject);
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
