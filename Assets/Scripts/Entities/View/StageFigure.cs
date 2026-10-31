using System;
using System.Collections.Generic;
using Effects.Vfx;
using Managers;
using Managers.Stage;
using Managers.UI.Theme;
using UnityEngine;

namespace Entities.View
{
    /// <summary>시안 sdBox·sdParts의 스탠딩 2분할 프록시. 원점은 발바닥이다.</summary>
    [DisallowMultipleComponent]
    public sealed class StageFigure : MonoBehaviour
    {
        public const float HeadScale = 1.5f, BodyScaleY = .5f, BodyScaleX = .8f;
        public static float BaseWorldHeight => StageLayout.BodyHeightPx * StageLayout.WorldHeight / StageLayout.HeightPx;
        private static StageFigureDataList _data;
        private static readonly Dictionary<Sprite, Rect> VisibleHeads = new();
        private readonly List<Material> _materials = new();
        private SpriteRenderer _head, _lower, _shadow, _ring;
        private Sprite _source;
        private StageFigureData _profile;
        private float _height, _lowerHeight, _headImageHeight;
        private SdFrameRig _frames;
        public SdFrameRig Frames => _frames != null && _frames.Ready ? _frames : null;
        public Sprite StandingSource => _source;
        public SpriteRenderer Lower => _lower;
        public SpriteRenderer Head => _head;
        public bool Deform => Frames == null && (_profile == null || _profile.deform);
        public bool Calibrated { get; private set; }
        public string SpriteKey => _source != null ? _source.name : "";
        public float BodyWorldHeight => _height * transform.lossyScale.y;
        public Vector3 ChestWorld => Frames != null ? Frames.ChestWorld : transform.TransformPoint(Vector3.up * (_lowerHeight * .8f));
        public Vector3 HandWorld => Frames != null ? Frames.HandWorld : ChestWorld;
        public Bounds LowerWorldBounds => _lower.bounds;
        public Bounds VisibleHeadWorldBounds
        {
            get
            {
                if (Frames != null) return Frames.HeadBounds;
                if (!Deform || !VisibleHeads.TryGetValue(_source, out Rect uv)) return HeadWorldBounds;
                Rect rect = _source.textureRect;
                float ppu = _source.pixelsPerUnit;
                Vector2 origin = new((rect.xMin-_source.rect.xMin-_source.pivot.x)/ppu,
                    (rect.yMin-_source.rect.yMin-_source.pivot.y)/ppu);
                var bounds = new Bounds(); bool first = true;
                for (int ix=0;ix<2;ix++)
                    for (int iy=0;iy<2;iy++)
                    {
                        float x=ix==0?uv.xMin:uv.xMax,y=iy==0?uv.yMin:uv.yMax;
                        Vector3 p = new(origin.x+x*rect.width/ppu, origin.y+(1f-y)*rect.height/ppu, 0f);
                        if (_head.flipX) p.x=-p.x;
                        p=_head.transform.TransformPoint(p);
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                    }
                return bounds;
            }
        }
        public Bounds HeadWorldBounds
        {
            get
            {
                if (Frames != null) return Frames.HeadBounds;
                if (!Deform) return _lower.bounds;
                Vector4 ellipse = _head.sharedMaterial.GetVector("_Ellipse");
                Sprite sprite = _head.sprite;
                Rect rect = sprite.textureRect;
                float ppu = sprite.pixelsPerUnit;
                Vector2 origin = new((rect.xMin - sprite.rect.xMin - sprite.pivot.x) / ppu,
                    (rect.yMin - sprite.rect.yMin - sprite.pivot.y) / ppu);
                Vector3 center = new(origin.x + rect.width / ppu * ellipse.x,
                    origin.y + rect.height / ppu * (1f - ellipse.y), 0f);
                if (_head.flipX) center.x = -center.x;
                var bounds = new Bounds(_head.transform.TransformPoint(center), Vector3.zero);
                Vector3 radius = new(rect.width / ppu * ellipse.z, rect.height / ppu * ellipse.w, 0f);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        bounds.Encapsulate(_head.transform.TransformPoint(center + new Vector3(x * radius.x, y * radius.y, 0f)));
                return bounds;
            }
        }

        /// <summary>Stage 실루엣도 원래 조각·마스크·자세를 복제한다. 원본의 수명과 재질을 공유하지 않는다.</summary>
        internal static SpriteRenderer CopySlice(SpriteRenderer source, Transform parent, string name,
            Color color, int layer, int order, bool additive = false)
        {
            var copy = VfxSpace.NewSprite(parent, name, source.sprite, color, layer, order);
            copy.flipX = source.flipX; copy.flipY = source.flipY;
            copy.transform.position = source.transform.position;
            copy.transform.rotation = source.transform.rotation;
            copy.transform.localScale = source.transform.lossyScale;
            copy.localBounds = source.localBounds;
            var material = new Material(source.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_Brightness", 1f);
            material.SetFloat("_DstBlend", additive ? 1f : 10f);
            copy.sharedMaterial = material;
            copy.gameObject.AddComponent<StageSliceCopy>().OwnedMaterial = material;
            return copy;
        }

        internal SpriteRenderer[] CopySlices(Transform parent, string name, Color color, int layer, int order, bool additive = false)
        {
            var lower = CopySlice(_lower, parent, name + "Lower", color, layer, order, additive);
            return Deform
                ? new[] { lower, CopySlice(_head, parent, name + "Head", color, layer, order + 1, additive) }
                : new[] { lower };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetData() { _data = null; VisibleHeads.Clear(); }

        public void Configure(Sprite source, Unit unit, Transform ground)
        {
            if (_lower == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/StageFigureSlice");
                if (shader == null) throw new InvalidOperationException("StageFigureSlice 셰이더 누락");
                _lower = NewSlice("Lower", shader);
                _head = NewSlice("Head", shader);
                _shadow = VfxSpace.NewSprite(ground, "StageShadow", VfxSpace.SoftDot, Color.black, 0, 0);
                _ring = VfxSpace.NewSprite(ground, "StageActionRing", UIShapes.Disc(128, Color.white, .92f), Color.clear, 0, 0);
            }
            _data ??= DataManager.LoadStageFigureDataList();
            _source = source;
            _profile = _data.figures.Find(item => item.sprite == source.name);
            Calibrated = _profile != null;
            _profile ??= _data.@default;
            float rank = unit != null && (unit.UnitTier == "elite" || unit.UnitTier == "boss") ? 1.15f : 1f;
            _height = BaseWorldHeight * rank;
            if (_frames == null) _frames = gameObject.AddComponent<SdFrameRig>();
            if (_frames.Configure(_profile.sd, unit, _lower, _height))
            {
                _lowerHeight = _height;
                _head.enabled = false;
                SetVisible(true);
                return;
            }
            float neck = _profile.chin + .012f;
            bool flip = (unit != null && unit.IsEnemy) == (_profile.facing == "right");
            if (!_profile.deform)
            {
                // 비인간형은 전신 한 장을 등방 축소한다. Lower/chest 소비자 계약은 유지한다.
                _lowerHeight = _height;
                LayoutSlice(_lower, 0f, 1f, _height, _height, 0f, flip, false);
                _head.enabled = false;
                SetVisible(true);
                return;
            }
            _head.enabled = true;
            float hi = _height / ((neck - _profile.top) * HeadScale + (_profile.feet - neck) * BodyScaleY);
            _lowerHeight = (_profile.feet - neck) * hi * BodyScaleY;
            _headImageHeight = hi * HeadScale;
            LayoutSlice(_lower, neck, _profile.feet, hi * BodyScaleX, hi * BodyScaleY, 0f, flip, false);
            LayoutSlice(_head, 0f, neck, hi * HeadScale, hi * HeadScale, _lowerHeight, flip, true);
            CacheVisibleHead();
            SetVisible(true);
        }

        // 보존 띠의 머리카락·얼굴은 타원보다 위로 나올 수 있다. 접점 프로필 없이 실제 Head 알파만 측정한다.
        private void CacheVisibleHead()
        {
            if (VisibleHeads.ContainsKey(_source)) return;
            Texture2D texture = _source.texture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, temporary); RenderTexture.active=temporary;
                read.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); read.Apply();
                Color32[] pixels=read.GetPixels32(); Rect rect=_source.textureRect;
                Vector4 ellipse=_head.sharedMaterial.GetVector("_Ellipse");
                float keep=_head.sharedMaterial.GetFloat("_KeepAboveV"), fade=_head.sharedMaterial.GetFloat("_AboveFadeV");
                float Smooth(float a,float b,float value) { float t=Mathf.InverseLerp(a,b,value);return t*t*(3f-2f*t); }
                Vector2 min=Vector2.one,max=Vector2.zero;
                for(int y=0;y<(int)rect.height;y++) for(int x=0;x<(int)rect.width;x++)
                {
                    if(pixels[((int)rect.y+y)*texture.width+(int)rect.x+x].a<24)continue;
                    Vector2 p=new((x+.5f)/rect.width,1f-(y+.5f)/rect.height);
                    if(p.y>_profile.chin+.012f)continue;
                    float radius=new Vector2((p.x-ellipse.x)/ellipse.z,(p.y-ellipse.y)/ellipse.w).magnitude;
                    float mask=1f-Smooth(.7f,1f,radius);
                    float hair=(1f-Smooth(keep,keep+fade,p.y))*(1f-Smooth(ellipse.z,ellipse.z*1.3f,Mathf.Abs(p.x-ellipse.x)));
                    if(Mathf.Max(mask,hair)<.08f)continue;
                    min=Vector2.Min(min,p);max=Vector2.Max(max,p);
                }
                if(max.x>min.x && max.y>min.y) VisibleHeads[_source]=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);Destroy(read); }
        }

        private SpriteRenderer NewSlice(string name, Shader shader)
        {
            var renderer = VfxSpace.NewSprite(transform, name, null, Color.white, 0, 0);
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            renderer.sharedMaterial = material;
            _materials.Add(material);
            return renderer;
        }

        private void LayoutSlice(SpriteRenderer renderer, float from, float to, float widthScale,
            float heightScale, float bottom, bool flip, bool head)
        {
            renderer.sprite = _source;
            renderer.flipX = flip;
            Rect rect = _source.textureRect;
            float ppu = _source.pixelsPerUnit;
            float w = rect.width / ppu, h = rect.height / ppu;
            Vector2 origin = new((rect.xMin - _source.rect.xMin - _source.pivot.x) / ppu,
                (rect.yMin - _source.rect.yMin - _source.pivot.y) / ppu);
            float sx = widthScale / h, sy = heightScale / h;
            float faceX = origin.x + w * _profile.cx;
            renderer.transform.localScale = new Vector3(sx, sy, 1f);
            renderer.transform.localPosition = new Vector3((flip ? faceX : -faceX) * sx,
                bottom - (origin.y + h * (1f - to)) * sy, 0f);
            // 시각적으로 잘린 몸통만 bounds로 노출한다. 같은 원본 sprite·UV는 두 조각이 공유한다.
            renderer.localBounds = new Bounds(new Vector3(flip ? -(origin.x + w * .5f) : origin.x + w * .5f,
                origin.y + h * (1f - (from + to) * .5f), 0f), new Vector3(w, h * (to - from), .01f));
            Material material = renderer.sharedMaterial;
            material.SetVector("_SpriteUV", new Vector4(rect.xMin / _source.texture.width, rect.yMin / _source.texture.height,
                rect.width / _source.texture.width, rect.height / _source.texture.height));
            material.SetVector("_ClipV", new Vector4(from, to, 0f, 0f));
            // 타원 x 반경도 이미지 높이 기준이다. UV x에는 원화 가로세로 비를 보정한다.
            float span = _profile.chin - _profile.top;
            material.SetVector("_Ellipse", new Vector4(_profile.cx, (_profile.top + _profile.chin) * .5f,
                .95f * span * h / w, .74f * span));
            material.SetFloat("_Head", head ? 1f : 0f);
            material.SetFloat("_Feather", .3f);
            material.SetFloat("_KeepAboveV", _profile.top + 10f / (heightScale * StageLayout.HeightPx / StageLayout.WorldHeight));
            material.SetFloat("_AboveFadeV", 26f / (heightScale * StageLayout.HeightPx / StageLayout.WorldHeight));
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (_shadow != null) _shadow.enabled = visible;
            if (_ring != null && !visible) _ring.enabled = false;
        }

        public void ApplyDepth(int order, int layer)
        {
            if (_lower == null) return;
            foreach (SpriteRenderer renderer in new[] { _shadow, _ring, _lower, _head }) renderer.sortingLayerID = layer;
            _shadow.sortingOrder = order - 2; _ring.sortingOrder = order - 1;
            _lower.sortingOrder = _head.sortingOrder = order;
            Vector3 position = _head.transform.localPosition; position.z = -.001f;
            _head.transform.localPosition = position;
        }

        public void ApplyAppearance(Color color, float lift, bool acting, Color element, bool focused = false)
        {
            if (_lower == null) return;
            _lower.color = _head.color = color;
            _lower.sharedMaterial.SetFloat("_Brightness", focused ? 1.12f : 1f);
            _head.sharedMaterial.SetFloat("_Brightness", focused ? 1.12f : 1f);
            float px = StageLayout.WorldHeight / StageLayout.HeightPx;
            float spread = 1f + .35f * Mathf.Min(2f, lift);
            _shadow.transform.localScale = new Vector3(150f * px * spread, 30f * px * spread, 1f);
            _shadow.color = new Color(0f, 0f, 0f, .38f * color.a / spread);
            _ring.transform.localScale = new Vector3(150f * px, 30f * px, 1f);
            _ring.color = new Color(element.r, element.g, element.b, .8f * color.a);
            _ring.enabled = acting && gameObject.activeInHierarchy;
            if (!Deform) return;
            float imagePixels = _headImageHeight * Mathf.Abs(transform.lossyScale.y) * StageLayout.HeightPx / StageLayout.WorldHeight;
            _head.sharedMaterial.SetFloat("_KeepAboveV", _profile.top + 10f / imagePixels);
            _head.sharedMaterial.SetFloat("_AboveFadeV", 26f / imagePixels);
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials) if (material != null) Destroy(material);
            if (_shadow != null) Destroy(_shadow.gameObject);
            if (_ring != null) Destroy(_ring.gameObject);
        }
    }

    internal sealed class StageSliceCopy : MonoBehaviour
    {
        internal Material OwnedMaterial;
        private void OnDestroy() { if (OwnedMaterial != null) Destroy(OwnedMaterial); }
    }
}
