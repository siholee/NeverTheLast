using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Managers.UI.Core
{
    /// <summary>
    /// 버튼의 켜짐/꺼짐을 <b>면과 글자를 함께 바꿔</b> 보여 준다.
    ///
    /// 예전에는 ColorTint의 disabledColor(회색 곱)만 썼다. 보조 버튼 면이 원래 회색이라 곱해도
    /// 거의 같은 회색이 되어, 훈련·휴식이 꺼졌는지 켜졌는지 구분되지 않는다는 QA가 있었다.
    /// 밝은 화면에서는 꺼진 버튼도 형태를 유지하고, 글자 알파만 낮춰 고장 난 빈칸처럼 보이지 않게 한다.
    ///
    /// 화면 코드는 켜져 있는 동안 스프라이트·글자색을 마음대로 바꿔도 된다(탭 강조 등).
    /// 켜져 있을 때의 모습을 매 프레임 기억해 두었다가 꺼졌다 켜질 때 그대로 되돌린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private static Sprite _disabledSprite;

        /// <summary>
        /// 남색 제목 띠처럼 짙은 바탕 위 버튼. 호버 · 누름 막을 흰색으로 칠한다(글자가 없는 원형 닫기도 반응이 보이게).
        /// </summary>
        public bool LightFeedback;
        /// <summary>짙은 행동 버튼은 청색광으로 밝기를 올려 호버 중에도 군청 채도를 유지한다.</summary>
        public Color LightFeedbackTint=Color.white;
        private Image _feedback;
        private float _feedbackAmount, _feedbackTarget;
        private bool _hovered, _selected, _pressed;
        private float _pressScale = 1f;
        private UIFeedbackScale _motionScale;
        private void Feedback(float amount)
        {
            _feedbackTarget = _button != null && _button.IsInteractable() ? amount : 0f;
            if (_feedback != null || _feedbackTarget <= 0f) return;
            _feedback=UIBuild.Solid("InteractionFeedback",transform,Color.clear);
            // 버튼이 알약 · 원형이라 반응 막도 같은 반경을 쓴다. 각진 막이 원 밖으로 비어져 나오지 않는다.
            float radius = ((RectTransform)transform).rect.width <= 48f ? 20f : 10f;
            _feedback.sprite=UIShapes.RoundedRect(Mathf.RoundToInt(radius),Color.white);
            _feedback.type=Image.Type.Sliced;_feedback.raycastTarget=false;
            UIBuild.Stretch(_feedback.rectTransform);
        }
        public void OnPointerEnter(PointerEventData e){_hovered=true;Feedback(.075f);}
        public void OnPointerExit(PointerEventData e){_hovered=false;_pressed=false;Feedback(_selected ? .075f : 0f);}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left && _button != null && _button.IsInteractable()){_pressed=true;Feedback(.16f);}}
        public void OnPointerUp(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;_pressed=false;Feedback(_hovered || _selected ? .075f : 0f);}
        public void OnSelect(BaseEventData e){_selected=true;Feedback(.075f);}
        public void OnDeselect(BaseEventData e){_selected=false;Feedback(_hovered ? .075f : 0f);}
        private void OnDisable(){_hovered=_selected=_pressed=false;_pressScale=1f;_motionScale.Reset(transform);_feedbackAmount=_feedbackTarget=0f;if(_feedback!=null)_feedback.color=Color.clear;}


        private Button _button;
        private TMP_Text _label;
        private bool _shownEnabled = true;
        private Sprite _enabledSprite;
        private Color _enabledLabelColor;

        private static Sprite DisabledSprite => _disabledSprite != null
            ? _disabledSprite
            : _disabledSprite = UIShapes.RoundedRect(10, UITheme.SurfaceSunken, UITheme.Divider, 1);

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
            if(!on){_feedbackTarget=0f;_pressed=false;}
            float scaleTarget = _pressed ? .975f : on && (_hovered || _selected) ? 1.004f : 1f;
            _pressScale = Mathf.Lerp(_pressScale, scaleTarget, 1f-Mathf.Exp(-Time.unscaledDeltaTime*(_pressed?38f:22f)));
            _motionScale.Apply(transform, _pressScale);
            _feedbackAmount=Mathf.MoveTowards(_feedbackAmount,_feedbackTarget,Time.unscaledDeltaTime*2f);
            if(_feedback!=null){Color ink=LightFeedback?LightFeedbackTint:(_enabledLabelColor.grayscale>.6f && _enabledLabelColor.a>.5f)?Color.white:UITheme.Accent;ink.a=_feedbackAmount;_feedback.color=ink;}

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
