using System.Collections.Generic;
using Entities;
using Helpers;
using Managers;
using Managers.Stage;
using Managers.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Effects.Vfx
{
    /// <summary>Stage 초월기의 예고 시작에 도는 장식. 판정·코루틴 완료·입력은 소유하지 않는다.</summary>
    public sealed class UltimateCutIn : MonoBehaviour
    {
        private static UltimateCutIn _instance;
        private RectTransform _band, _art, _title, _lines;
        private CanvasGroup _group, _bandAlpha, _artAlpha, _titleAlpha;
        private Image _dim, _flash, _portrait, _glow;
        private TMP_Text _name, _caption;
        private TMP_FontAsset _serif;
        private float _start, _duration;
        private Unit _caster;
        private bool _fitWhole;
        public float Progress => _duration > 0f ? Mathf.Clamp01((Time.time - _start) / _duration) : 1f;
        public Vector3 FaceWorld => _art != null ? _art.position : Vector3.zero;
        public RectTransform Band => _band;
        public RectTransform Art => _art;
        public static UltimateCutIn Current => _instance;
        public static bool IsShowing(Unit caster) => _instance != null && _instance._caster == caster &&
            _instance._group != null && _instance._group.alpha > 0f && _instance.Progress < 1f;
        public static float Window(Unit caster) => caster?.ActiveUltimateCode?.IsAutoCast == true &&
            !caster.ActiveUltimateCode.IsPersistent ? ActionPresentation.FullPreviewSeconds : 0f;
        public static void Stop(Unit caster)
        {
            if(_instance!=null&&_instance._caster==caster&&_instance._group!=null)_instance._group.alpha=0f;
        }

        public static void Play(Unit caster)
        {
            if (VisualModeState.Current != VisualMode.Stage || caster == null || Combat.CombatRandom.SeedState != 0u) return;
            float window = Window(caster);
            if (window < .5f) return;
            try
            {
                if (_instance == null)
                {
                    var go = new GameObject("UltimateCutIn");
                    _instance = go.AddComponent<UltimateCutIn>(); _instance.Build();
                }
                _instance.Begin(caster, Mathf.Min(1.15f, window));
            }
            catch(System.Exception error)
            {
                // 장식 초기화 실패가 기존 행동 실행 이벤트 밖으로 전파되지 않게 한다.
                Debug.LogException(error);
                if(_instance!=null){Destroy(_instance.gameObject);_instance=null;}
            }
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(x, -y); return r;
        }
        private static Image Picture(RectTransform r, Color color)
        {
            var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 1f;
            _group = gameObject.AddComponent<CanvasGroup>(); _group.blocksRaycasts = _group.interactable = false;
            Unit.AnyDamageIncoming += OnFirstDamage;
            Unit.AnyUnitDied += OnDeath;
            _dim = Picture(Rect("Dim", transform, -2000, -2000, 6000, 5000), new Color(.025f,.04f,.09f,0f));
            _band = Rect("Band", transform, 960, 465, 2080, 250);
            _band.pivot = new Vector2(.5f,.5f);
            Picture(_band, new Color(.035f,.065f,.13f,.98f));
            _band.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _bandAlpha = _band.gameObject.AddComponent<CanvasGroup>();
            _glow = Picture(Rect("ElementGlow", _band, 180, -60, 700, 370), Color.white);
            var texture = new Texture2D(128, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y=0;y<64;y++) for (int x=0;x<128;x++)
                texture.SetPixel(x,y,new Color(1,1,1, Mathf.Pow(Mathf.Clamp01(1f-new Vector2((x-63.5f)/64f,(y-31.5f)/32f).magnitude),2f)*.7f));
            texture.Apply(); _glow.sprite=Sprite.Create(texture,new Rect(0,0,128,64),new Vector2(.5f,.5f));
            _lines = Rect("SpeedLines", _band, 0, 0, 3000, 250);
            for (int i=0;i<26;i++) Picture(Rect("Line",_lines,i*130,0,i%2==0?2:1,250),new Color(1,1,1,.07f));
            _art = Rect("Standing", _band,480,112,1,1100);
            _portrait=Picture(_art,Color.white); _artAlpha=_art.gameObject.AddComponent<CanvasGroup>();
            _title=Rect("Title",_band,860,64,1100,160); _title.localRotation=Quaternion.Euler(0,0,-6);
            _title.pivot=new Vector2(.5f,.5f);
            _titleAlpha=_title.gameObject.AddComponent<CanvasGroup>();
            _caption=UIBuild.Text("Caption",_title,"",18,new Color(1f,.90f,.63f));
            _caption.rectTransform.anchorMin=_caption.rectTransform.anchorMax=_caption.rectTransform.pivot=new Vector2(0,1);
            _caption.rectTransform.sizeDelta=new Vector2(1100,32);
            _name=UIBuild.Text("Skill",_title,"",78,Color.white);
            _name.rectTransform.anchorMin=_name.rectTransform.anchorMax=_name.rectTransform.pivot=new Vector2(0,1);
            _name.rectTransform.sizeDelta=new Vector2(1100,110); _name.rectTransform.anchoredPosition=new Vector2(0,-44);
            _name.fontSize=78; _name.fontStyle=FontStyles.Bold; _name.enableAutoSizing=false;
            _name.alignment=_caption.alignment=TextAlignmentOptions.TopLeft;
            // OS 명조를 사용한다. 한글 명조가 없는 플랫폼은 공통 한글 폰트로 안전하게 표시한다.
            if (System.Array.Exists(Font.GetOSInstalledFontNames(), n=>n=="Batang" || n=="바탕"))
            {
                _serif=TMP_FontAsset.CreateFontAsset("Batang","Regular",90);
                if(_serif!=null)
                {
                    _serif.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                    _serif.fallbackFontAssetTable.Add(_name.font); _name.font=_serif;
                }
            }
            for(int i=0;i<2;i++) Picture(Rect("Edge",_band,0,i==0?10:239,2080,1),new Color(1f,.92f,.74f,.75f));
            _flash=Picture(Rect("SoftFlash",transform,-2000,-2000,6000,5000),new Color(1,1,1,0));
            _group.alpha=0;
        }
        private void Begin(Unit caster, float duration)
        {
            Sprite source=CardMotion.ViewOf(caster)?.StageProxy?.StandingSource;
            if(source==null)return;
            var data=DataManager.LoadStageFigureDataList();
            var p=data.figures.Find(f=>f.sprite==source.name)??data.@default;
            _portrait.sprite=source;
            bool nonHuman=!p.deform && p.sd==null;
            _fitWhole=nonHuman && p.cutinFace==null;
            // 비인간형 얼굴판은 긴 귀·턱도 들어오도록 확대를 줄인다. 인간형/SD의 기존 얼굴 규칙은 유지한다.
            float height=_fitWhole?250f:nonHuman?600f:1100f;
            _art.sizeDelta=new Vector2(height*source.rect.width/source.rect.height,height);
            _art.pivot=_fitWhole?new Vector2(.5f,.5f):nonHuman?
                new Vector2(p.cutinFace.x,1f-p.cutinFace.y):new Vector2(p.cx,1f-(p.top+p.chin)*.5f);
            // 전체판은 띠와 나란히 둬 모서리까지 250px 안에 남긴다.
            _art.localRotation=Quaternion.Euler(0,0,_fitWhole?0:-6);
            _name.text=caster.ActiveUltimateCode.CodeName; _caption.text=$"{caster.UnitName} · 초월기";
            _glow.color=ElementPalette.ColorFor(caster.Element);
            _caster=caster; _duration=duration; _start=Time.time; _group.alpha=1f;
            Update();
        }
        private void Update()
        {
            if (_group==null || _group.alpha==0) return;
            if(VisualModeState.Current!=VisualMode.Stage || _caster==null || !_caster.isActive ||
                GameManager.Instance?.gameState!=BaseClasses.BaseEnums.GameState.RoundInProgress)
            { _group.alpha=0; return; }
            float t=Progress;
            if(t>=1f){_group.alpha=0;return;}
            float fade=1f-Mathf.InverseLerp(.86f,1f,t);
            _dim.color=new Color(.025f,.04f,.09f,.5f*Mathf.Min(t/.12f,fade));
            _bandAlpha.alpha=Mathf.Min(t/.1f,fade);
            _band.localRotation=Quaternion.Euler(0,0,6);
            _band.localScale=new Vector3(1,Mathf.Lerp(.08f,1,Mathf.SmoothStep(0,1,t/.1f))*Mathf.Lerp(.05f,1,fade),1);
            _artAlpha.alpha=Mathf.Min(t/.22f,fade);
            // 얼굴 고정점을 피벗으로 쓴다. 정점 이후의 작은 이동도 띠 안에서 끝난다.
            float slide=t<.22f?Mathf.Lerp(-140,0,Mathf.SmoothStep(0,1,t/.22f)):Mathf.Lerp(0,26,Mathf.InverseLerp(.22f,.86f,t));
            _art.anchoredPosition=new Vector2(480+slide,_fitWhole?-125:-112);
            _art.localScale=Vector3.one*(_fitWhole?1f:Mathf.Lerp(1.06f,1,Mathf.Clamp01(t/.22f)));
            _titleAlpha.alpha=Mathf.Clamp01((t-.14f)/.22f)*fade;
            _title.anchoredPosition=new Vector2(860+550+Mathf.Lerp(80,0,Mathf.SmoothStep(0,1,(t-.14f)/.22f)),-64-80);
            _lines.anchoredPosition=new Vector2(-520*t,0);
            _flash.color=new Color(1,1,1,.035f*Mathf.Clamp01(1f-Mathf.Abs(t-.11f)/.035f));
        }
        private void OnDestroy()
        {
            Unit.AnyDamageIncoming -= OnFirstDamage;
            Unit.AnyUnitDied -= OnDeath;
            if(_instance==this)_instance=null;
            if(_serif!=null)Destroy(_serif);
            if(_glow?.sprite!=null){Destroy(_glow.sprite.texture);Destroy(_glow.sprite);}
        }
        private void OnFirstDamage(Unit target, BaseClasses.DamageContext context)
        {
            // 같은 프레임의 선후에도 대응하는 관측이다. 기존 콜백에는 개입하지 않는다.
            if(context.Attacker==_caster&&context.CodeType==BaseClasses.BaseEnums.CodeType.Ultimate&&_group!=null)
                _group.alpha=0;
        }
        private void OnDeath(Unit unit, Unit attacker) => Stop(unit);
    }
}
