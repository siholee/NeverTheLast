using Managers.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// 누를 수 있는 면(카드)에 마우스를 올리면 테두리가 떠오른다.
    ///
    /// Button이 아닌 카드(보상 · 상점 매대 · 이벤트 선택지)는 호버에 아무 반응이 없어,
    /// 그림만 큰 카드가 눌리는 것인지 장식인지 알 수 없었다. Spotlight의 카드처럼 올리면 면이
    /// 한 겹 떠오르는 반응을 준다 — 테두리가 짧게 번지고, 뗄 때 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIHoverRing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler
    {
        private const float FadeTime = 0.12f;

        private static Sprite _ring;

        private Image _image;
        private float _target;
        private float _value;
        private bool _pressed;
        private float _scale = 1f;
        private UIFeedbackScale _motionScale;
        private Sprite _surface;

        /// <summary>선택지처럼 포커스 구분이 필요한 면의 채움/헤어라인만 개별 지정한다.</summary>
        public void SetSurface(Sprite surface)
        {
            _surface=surface;
            if(_image!=null)_image.sprite=_surface!=null?_surface:Ring;
        }

        private static Sprite Ring => _ring != null
            ? _ring
            : _ring = UIShapes.RoundedRect(12, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.07f));

        private void EnsureImage()
        {
            if (_image != null) return;

            var go = new GameObject("HoverRing", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _image = go.GetComponent<Image>();
            _image.sprite = _surface!=null?_surface:Ring;
            _image.type = Image.Type.Sliced;
            _image.raycastTarget = false;
            UIBuild.Stretch(_image.rectTransform, 0f, 0f);
            _image.color = new Color(1f, 1f, 1f, 0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Button button = GetComponent<Button>();
            if (button != null && !button.IsInteractable()) return;
            EnsureImage();
            _image.transform.SetAsLastSibling();
            _target = 1f;
        }

        public void OnPointerExit(PointerEventData eventData) { _target=0f; _pressed=false; }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button!=PointerEventData.InputButton.Left || GetComponent<Button>()!=null) return;
            EnsureImage(); _pressed=true; _target=1f;
        }
        public void OnPointerUp(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)_pressed=false; }
        public void OnBeginDrag(PointerEventData e) { _pressed=false; _target=0f; }

        private void OnDisable()
        {
            _target = _value = 0f;
            _pressed=false; _scale=1f; _motionScale.Reset(transform);
            if (_image != null) _image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void Update()
        {
            if(GetComponent<Button>()==null)
            {
                float desired=_pressed?.986f:_target>0f?1.004f:1f;
                _scale=Mathf.Lerp(_scale,desired,1f-Mathf.Exp(-Time.unscaledDeltaTime*(_pressed?38f:22f)));
                _motionScale.Apply(transform,_scale);
            }
            if (_image == null) return;
            _value = Mathf.MoveTowards(_value, _pressed?1.55f:_target, Time.unscaledDeltaTime / FadeTime);
            _image.color = new Color(1f, 1f, 1f, _value);
        }
    }

    /// <summary>
    /// 창이 열릴 때 배경이 서서히 어두워지고 면이 살짝 커지며 떠오른다.
    ///
    /// 예전에는 창이 한 프레임에 툭 나타났다. 0.16초 남짓의 짧은 전환만으로도 "화면이 바뀌었다"가
    /// "위에 창이 떴다"로 읽힌다. 시간은 unscaledTime으로 센다 — 일시정지(timeScale 0) 중에 여는 메뉴도 움직여야 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPopIn : MonoBehaviour
    {
        private const float Duration = 0.24f;
        private const float StartScale = 0.975f;

        public RectTransform Panel;

        private CanvasGroup _group;
        private float _elapsed, _beganAt;
        private bool _firstUpdate;
        private UIFeedbackScale _panelScale;

        private void OnEnable()
        {
            if (_group == null) _group = UIBuild.Group(gameObject);
            _elapsed = 0f;_firstUpdate=true;
            Apply(0f);
        }

        private void Update()
        {
            if (_elapsed >= Duration) return;
            if(_firstUpdate){_firstUpdate=false;_beganAt=Time.realtimeSinceStartup;}
            _elapsed = Time.realtimeSinceStartup-_beganAt;
            Apply(Mathf.Clamp01(_elapsed / Duration));
        }

        private void OnDisable() { if(Panel!=null)_panelScale.Reset(Panel); }

        private void Apply(float t)
        {
            // ease-out cubic — 빠르게 떠서 부드럽게 멈춘다.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (_group != null) _group.alpha = eased;
            if (Panel != null) _panelScale.Apply(Panel, Mathf.Lerp(StartScale, 1f, eased));
        }
    }
    /// <summary>다른 레이아웃/등장 연출의 기본 배율을 보존하는 국소 피드백.</summary>
    internal struct UIFeedbackScale
    {
        private Transform _target;
        private Vector3 _base, _last;
        public void Apply(Transform target, float factor)
        {
            if(target==null)return;
            if(_target!=target || target.localScale!=_last)
            { _target=target; _base=target.localScale; }
            _last=_base*factor;
            target.localScale=_last;
        }
        public void Reset(Transform target)
        {
            if(target!=null && _target==target && target.localScale==_last)target.localScale=_base;
            _target=null;
        }
    }

    /// <summary>비교 카드가 순서대로 나타난다. 레이아웃/입력 영역은 움직이지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class UICardReveal : MonoBehaviour
    {
        public float Delay;
        public int Index { get; private set; }
        public void Replay() => Replay(Index);
        private CanvasGroup _group;
        private float _age, _beganAt;
        private bool _firstUpdate;
        private bool _captured, _wasInteractable, _wasBlocking;
        public static void Attach(GameObject target,int index)
        {
            var reveal=target.GetComponent<UICardReveal>();
            if(reveal==null)reveal=target.AddComponent<UICardReveal>();
            reveal.Replay(index);
        }
        public void Replay(int index)
        {
            Index=Mathf.Max(0,index); Delay=Mathf.Min(.325f,Index*.065f); _age=0f;_firstUpdate=true;
            if(_group==null)_group=UIBuild.Group(gameObject);
            if(!_captured)
            { _wasInteractable=_group.interactable; _wasBlocking=_group.blocksRaycasts; _captured=true; }
            _group.alpha=0f;_group.interactable=false;_group.blocksRaycasts=false;
            SynchronizeShadow();
            enabled=true;
        }
        private void SynchronizeShadow()
        {
            var elevation=GetComponent<UIElevation>();
            if(elevation!=null)elevation.Synchronize();
        }
        private void OnEnable() { Replay(); }
        private void OnDisable()
        {
            if(_group==null)return;
            _group.alpha=1f;
            SynchronizeShadow();
            if(_captured){_group.interactable=_wasInteractable;_group.blocksRaycasts=_wasBlocking;_captured=false;}
        }
        private void Update()
        {
            if(_firstUpdate){_firstUpdate=false;_beganAt=Time.realtimeSinceStartup;}
            _age=Time.realtimeSinceStartup-_beganAt;
            float t=Mathf.Clamp01((_age-Delay)/.24f);
            _group.alpha=t*t*(3f-2f*t);
            SynchronizeShadow();
            if(t>=1f)enabled=false;
        }
    }

}
