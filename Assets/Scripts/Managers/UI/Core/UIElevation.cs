using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>독립된 그림자 형제를 표면의 레이아웃·표시·수명과 함께 관리한다.</summary>
    public sealed class UIElevation : MonoBehaviour
    {
        private Image _shadow;
        private RectTransform _source;
        private float _blur, _drop;
        private Color _shadowColor;
        public void Bind(Image shadow, float blur, float drop)
        {
            _shadow = shadow; _source = (RectTransform)transform; _blur = blur; _drop = drop;
            _shadowColor = shadow.color;
            Refresh();
        }
        private void LateUpdate() => Refresh();
        public void Synchronize() => Refresh();
        private void Refresh()
        {
            if (_shadow == null || _source == null) return;
            CanvasGroup group = GetComponent<CanvasGroup>();
            Color color = _shadowColor;
            color.a *= group != null ? group.alpha : 1f;
            _shadow.color = color;
            RectTransform rect = _shadow.rectTransform;
            rect.anchorMin = _source.anchorMin; rect.anchorMax = _source.anchorMax; rect.pivot = _source.pivot;
            rect.offsetMin = _source.offsetMin + new Vector2(-_blur, -_blur - _drop);
            rect.offsetMax = _source.offsetMax + new Vector2(_blur, _blur - _drop);
            rect.localScale = _source.localScale;
        }
        private void OnEnable() { if (_shadow != null) _shadow.gameObject.SetActive(true); }
        private void OnDisable() { if (_shadow != null) _shadow.gameObject.SetActive(false); }
        private void OnDestroy() { if (_shadow != null) Destroy(_shadow.gameObject); }
    }
}
