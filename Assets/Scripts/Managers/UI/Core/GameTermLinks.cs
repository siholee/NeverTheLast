using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Managers.UI.Core
{
    /// <summary>설명 본문의 실제 TMP 링크를 포인터로 찾는다. 휠·드래그는 ScrollRect에 그대로 전달한다.</summary>
    public sealed class GameTermLinks : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerMoveHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        private TextMeshProUGUI _text;
        private string _rendered, _hoverLink;
        private bool _hovered;
        private Vector2 _point;
        private Camera _camera;
        private float _since;
        private bool _preferParentClick;
        public static void Attach(TextMeshProUGUI text, bool preferParentClick = false)
        {
            if (text == null) return;
            var links = text.GetComponent<GameTermLinks>() ?? text.gameObject.AddComponent<GameTermLinks>();
            links._text = text;
            links._preferParentClick |= preferParentClick;
        }
        private void LateUpdate()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_text.text != _rendered)
            {
                _rendered = GameGuideCatalog.LinkTerms(_text.text);
                _text.text = _rendered;
                bool links = _rendered.Contains("<link=");
                if (links) _text.richText = true;
                _text.raycastTarget = links;
            }
            if (!_hovered || UIDragSource.Current != null) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
                _point = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
            string link = LinkAt(_point, _camera);
            if (link != _hoverLink) { _hoverLink = link; _since = Time.unscaledTime; }
            if (link != null && link.StartsWith("term:") && Time.unscaledTime - _since >= 0.3f)
                GuideTermTooltip.Show(this, link.Substring(5), _point);
        }
        internal string LinkAt(Vector2 point, Camera camera = null)
        {
            int index = TMP_TextUtilities.FindIntersectingLink(_text, point, camera);
            return index >= 0 ? _text.textInfo.linkInfo[index].GetLinkID() : null;
        }
        public void OnPointerEnter(PointerEventData e) { _hovered = true; _point = e.position; _camera = e.enterEventCamera; }
        public void OnPointerMove(PointerEventData e) { _point = e.position; _camera = e.enterEventCamera; }
        public void OnPointerExit(PointerEventData e) { _hovered = false; _hoverLink = null; GuideTermTooltip.Release(this); }
        // 카드의 용어 호버는 유지하되 선택 입력은 카드에 넘긴다. Button의 down/up도 같은 경로로
        // 보내야 자식 텍스트와 부모의 클릭 수신기가 달라져 클릭이 취소되는 것을 막을 수 있다.
        public void OnPointerDown(PointerEventData e)
        {
            if (_preferParentClick && transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.pointerDownHandler);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (_preferParentClick && transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.pointerUpHandler);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (_preferParentClick && transform.parent != null &&
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.pointerClickHandler) != null) return;
            string link = LinkAt(e.position, e.pressEventCamera);
            if (link == null) return;
            if (link.StartsWith("guide:")) OpenArticle(link.Substring(6));
            else if (link.StartsWith("term:")) GuideTermTooltip.Show(this, link.Substring(5), e.position, immediatePin: true);
        }
        internal static void OpenArticle(string id)
        {
            GuideTermTooltip.CloseAll(); UITooltip.HideAny();
            if (Screens.WikiScreen.Active != null) Screens.WikiScreen.Active.ShowArticle(id);
            else GameManager.Instance?.uiManager?.ShowGuide(id);
        }
        private void OnDisable() { _hovered = false; _hoverLink = null; GuideTermTooltip.Release(this); }
    }
}
