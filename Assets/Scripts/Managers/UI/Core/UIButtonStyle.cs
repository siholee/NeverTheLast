using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// 버튼의 켜짐/꺼짐을 <b>면과 글자를 함께 바꿔</b> 보여 준다.
    ///
    /// 예전에는 ColorTint의 disabledColor(회색 곱)만 썼다. 보조 버튼 면이 원래 회색이라 곱해도
    /// 거의 같은 회색이 되어, 훈련·휴식이 꺼졌는지 켜졌는지 구분되지 않는다는 QA가 있었다.
    /// 이제 꺼지면 면이 바닥으로 가라앉고(테두리 없음) 글자가 확실히 어두워진다.
    ///
    /// 화면 코드는 켜져 있는 동안 스프라이트·글자색을 마음대로 바꿔도 된다(탭 강조 등).
    /// 켜져 있을 때의 모습을 매 프레임 기억해 두었다가 꺼졌다 켜질 때 그대로 되돌린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonStyle : MonoBehaviour
    {
        private static Sprite _disabledSprite;

        private Button _button;
        private TMP_Text _label;
        private bool _shownEnabled = true;
        private Sprite _enabledSprite;
        private Color _enabledLabelColor;

        private static Sprite DisabledSprite => _disabledSprite != null
            ? _disabledSprite
            : _disabledSprite = UIShapes.CutCorner(8, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, UITheme.Divider, 1);

        public static void Attach(Button button)
        {
            if (button == null) return;
            UIButtonStyle style = button.GetComponent<UIButtonStyle>();
            if (style == null) style = button.gameObject.AddComponent<UIButtonStyle>();
            style._button = button;
            // 처음부터 꺼진 채로 만들어지는 버튼도 있다. 켜졌을 때 돌아갈 모습을 지금 잡아 둔다.
            style._enabledSprite = button.image != null ? button.image.sprite : null;
            style._label = button.GetComponentInChildren<TMP_Text>(true);
            if (style._label != null) style._enabledLabelColor = style._label.color;
        }

        private void LateUpdate()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button == null || _button.image == null) return;
            if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);

            bool on = _button.IsInteractable();
            if (on)
            {
                if (!_shownEnabled)
                {
                    _shownEnabled = true;
                    if (_enabledSprite != null && _button.image.sprite == DisabledSprite) _button.image.sprite = _enabledSprite;
                    // 화면 코드가 켜면서 글자색을 새로 칠했으면 그쪽이 맞다. 꺼진 색 그대로일 때만 되돌린다.
                    if (_label != null && _label.color == UITheme.TextDisabled) _label.color = _enabledLabelColor;
                }

                _enabledSprite = _button.image.sprite;
                if (_label != null) _enabledLabelColor = _label.color;
                return;
            }

            if (!_shownEnabled) return;
            _shownEnabled = false;
            _button.image.sprite = DisabledSprite;
            if (_label != null) _label.color = UITheme.TextDisabled;
        }
    }
}
