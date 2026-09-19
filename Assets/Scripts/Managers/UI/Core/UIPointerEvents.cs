using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Managers.UI.Core
{
    /// <summary>
    /// 클릭 · 호버만 받는 가벼운 포인터 수신기. EventTrigger 대신 쓴다.
    ///
    /// EventTrigger는 휠 · 드래그를 포함한 모든 포인터 인터페이스를 구현한다. 그래서 스크롤 목록 안의
    /// 카드에 붙이면 휠 · 드래그가 카드에서 멈추고 부모 ScrollRect까지 올라가지 않는다 — 캐릭터 선택창에서
    /// 초상화 위에 커서를 두면 휠이 먹지 않던 원인이다. 여기서는 필요한 두 인터페이스만 구현해
    /// 나머지 이벤트는 그대로 부모로 흘려보낸다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPointerEvents : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler
    {
        public event Action Clicked;
        public event Action Entered;
        public event Action Exited;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        public static UIPointerEvents On(GameObject target)
        {
            UIPointerEvents existing = target.GetComponent<UIPointerEvents>();
            return existing != null ? existing : target.AddComponent<UIPointerEvents>();
        }
    }
}

namespace Managers.UI.Core
{
    /// <summary>
    /// 포인터가 터치인지 가린다.
    ///
    /// 예전에는 <c>pointerId &gt;= 0</c>이면 터치로 봤다. 구형 StandaloneInputModule은 마우스에 음수 ID를
    /// 주지만, 이 프로젝트의 InputSystemUIInputModule은 마우스에도 <b>장치 ID(양수)</b>를 준다.
    /// 그래서 마우스가 터치로 읽혀 TAB 창의 장비 끌기가 시작하자마자 끊기고(터치스크린이 없으니
    /// "손을 뗐다"로 판정), 마우스 호버 툴팁도 뜨지 않았다. 새 입력 시스템은 포인터 종류를 직접 알려 준다.
    /// </summary>
    public static class UIPointerKind
    {
        public static bool IsTouch(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (eventData == null) return false;
#if ENABLE_INPUT_SYSTEM
            if (eventData is UnityEngine.InputSystem.UI.ExtendedPointerEventData extended)
                return extended.pointerType == UnityEngine.InputSystem.UI.UIPointerType.Touch;
#endif
            return eventData.pointerId >= 0;
        }

        /// <summary>터치 기기가 주는 손가락 번호. 터치가 아니면 -1.</summary>
        public static int TouchId(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!IsTouch(eventData)) return -1;
#if ENABLE_INPUT_SYSTEM
            if (eventData is UnityEngine.InputSystem.UI.ExtendedPointerEventData extended)
                return extended.touchId;
#endif
            return eventData.pointerId;
        }
    }
}

namespace Managers.UI.Core
{
    /// <summary>
    /// 떠 있는 카드(누르면 뜨는 상세)를 바깥을 누르면 닫는다.
    ///
    /// TAB 창의 장비 상세 카드는 다른 탭으로 옮기거나 창을 닫아야만 사라졌다. 판정은 누르는 순간에
    /// 하고 그 클릭을 가로채지 않는다 — 다른 칸을 누르면 이 카드가 닫히고 그 칸의 동작이 그대로 이어진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIDismissOnOutsideClick : MonoBehaviour
    {
        /// <summary>닫을 때 부를 것. 비어 있으면 오브젝트를 끈다.</summary>
        public System.Action OnDismiss;

        private void Update()
        {
            if (!TryReadPress(out Vector2 point)) return;

            var rect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.rootCanvas.worldCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, point, camera)) return;

            if (OnDismiss != null) OnDismiss();
            else gameObject.SetActive(false);
        }

        private static bool TryReadPress(out Vector2 point)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                point = mouse.position.ReadValue();
                return true;
            }

            UnityEngine.InputSystem.Touchscreen screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen != null && screen.primaryTouch.press.wasPressedThisFrame)
            {
                point = screen.primaryTouch.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                point = Input.mousePosition;
                return true;
            }
#endif
            point = default;
            return false;
        }
    }
}
