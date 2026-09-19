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
    public sealed class UIHoverRing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float FadeTime = 0.12f;

        private static Sprite _ring;

        private Image _image;
        private float _target;
        private float _value;

        private static Sprite Ring => _ring != null
            ? _ring
            : _ring = UIShapes.CutCornerOutline(10,
                new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.62f), 1);

        private void EnsureImage()
        {
            if (_image != null) return;

            var go = new GameObject("HoverRing", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _image = go.GetComponent<Image>();
            _image.sprite = Ring;
            _image.type = Image.Type.Sliced;
            _image.raycastTarget = false;
            UIBuild.Stretch(_image.rectTransform, -2f, -2f);
            _image.color = new Color(1f, 1f, 1f, 0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureImage();
            _image.transform.SetAsLastSibling();
            _target = 1f;
        }

        public void OnPointerExit(PointerEventData eventData) => _target = 0f;

        private void OnDisable()
        {
            _target = _value = 0f;
            if (_image != null) _image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void Update()
        {
            if (_image == null || Mathf.Approximately(_value, _target)) return;
            _value = Mathf.MoveTowards(_value, _target, Time.unscaledDeltaTime / FadeTime);
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
        private const float Duration = 0.16f;
        private const float StartScale = 0.965f;

        public RectTransform Panel;

        private CanvasGroup _group;
        private float _elapsed;

        private void OnEnable()
        {
            if (_group == null) _group = UIBuild.Group(gameObject);
            _elapsed = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_elapsed >= Duration) return;
            _elapsed += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(_elapsed / Duration));
        }

        private void Apply(float t)
        {
            // ease-out cubic — 빠르게 떠서 부드럽게 멈춘다.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (_group != null) _group.alpha = eased;
            if (Panel != null) Panel.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, eased);
        }
    }
}
