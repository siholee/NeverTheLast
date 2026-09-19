using System;
using System.Collections.Generic;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// 코드로 조립한 UI에 끌어다 놓기를 붙이는 최소 장치.
    ///
    /// <see cref="UIDragSource"/>를 집을 수 있는 것에, <see cref="UIDropTarget"/>을
    /// 놓을 수 있는 곳에 붙인다. 끌고 다니는 동안에는 유령이 커서를 따라가고,
    /// <b>받을 수 있는 자리에만 앰버 테두리가 켜진다</b> — 발더스 게이트에서 아이템을 들면
    /// 넣을 수 있는 칸이 빛나는 것과 같은 신호다.
    ///
    /// <b>UGUI의 IBeginDragHandler 계열을 쓰지 않는다.</b> 이 프로젝트의 EventSystem에서는
    /// 클릭(IPointerDown)은 오는데 드래그 이벤트가 오지 않았다. 입력 모듈 설정에 기대는 대신
    /// 눌린 지점만 받아 두고 <see cref="UIDragRuntime"/>이 직접 매 프레임 커서를 따라간다.
    /// 놓는 순간의 판정도 EventSystem의 레이캐스트를 직접 돌려서 한다.
    ///
    /// 짐(payload)은 object 그대로 넘긴다. 화면마다 필요한 정보가 달라
    /// 여기서 형태를 정해 두면 오히려 걸리적거린다.
    /// </summary>
    public sealed class UIDragSource : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>이 손잡이가 들고 있는 것. 놓는 쪽이 해석한다.</summary>
        public object Payload;

        /// <summary>유령에 쓸 이름.</summary>
        public string Label;

        /// <summary>유령 테두리 색. 보통 등급 색을 쓴다.</summary>
        public Color Tint = UITheme.Outline;

        /// <summary>지금 끌고 있는 짐. 아무것도 끌고 있지 않으면 null.</summary>
        public static object Current => UIDragRuntime.Payload;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Payload == null) return;
            UIDragRuntime.Arm(this, eventData.position, eventData.pointerId);
        }
    }

    /// <summary>
    /// 놓을 수 있는 자리. <see cref="CanAccept"/>가 true를 준 짐만 <see cref="Accept"/>로 넘어온다.
    /// 끌기가 시작되면 받을 수 있는 자리만 스스로 테두리를 켠다.
    /// </summary>
    public sealed class UIDropTarget : MonoBehaviour
    {
        public Func<object, bool> CanAccept;
        public Action<object> Accept;

        /// <summary>강조할 때 갈아 끼울 스프라이트. 둘 다 있어야 강조한다.</summary>
        public Sprite IdleSprite;
        public Sprite HighlightSprite;

        private static readonly List<UIDropTarget> Active = new();
        private Image _image;

        private void OnEnable()
        {
            Active.Add(this);
            _image = GetComponent<Image>();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            SetHighlighted(false);
        }

        internal bool TryAccept(object payload)
        {
            if (payload == null) return false;
            if (CanAccept != null && !CanAccept(payload)) return false;

            Accept?.Invoke(payload);
            return true;
        }

        /// <summary>끌기가 시작됐다. 이 짐을 받을 수 있는 자리만 켠다.</summary>
        internal static void BeginDrag(object payload)
        {
            foreach (UIDropTarget target in Active)
            {
                target.SetHighlighted(target.CanAccept == null || target.CanAccept(payload));
            }
        }

        internal static void EndDrag()
        {
            foreach (UIDropTarget target in Active) target.SetHighlighted(false);
        }

        private void SetHighlighted(bool on)
        {
            if (_image == null || HighlightSprite == null || IdleSprite == null) return;
            _image.sprite = on ? HighlightSprite : IdleSprite;
        }
    }

    /// <summary>
    /// 끌기를 실제로 굴리는 한 개짜리 진행자. 필요할 때 스스로 만들어진다.
    ///
    /// 눌린 것을 기억해 두었다가 커서가 문턱을 넘어 움직이면 그때부터 끌기로 친다.
    /// 버튼을 놓으면 그 자리에서 레이캐스트를 돌려 <see cref="UIDropTarget"/>을 찾는다.
    /// </summary>
    internal sealed class UIDragRuntime : MonoBehaviour
    {
        /// <summary>이만큼 움직여야 클릭이 아니라 끌기로 본다(px).</summary>
        private const float DragThreshold = 8f;

        private static UIDragRuntime _instance;
        private static UIDragSource _armed;
        private static Vector2 _armedAt;
        private static Vector2 _lastPointer;
        private static int _pointerId;

        public static object Payload { get; private set; }

        private static GameObject _ghost;
        private static RectTransform _ghostRect;
        private static Canvas _ghostCanvas;

        private static readonly List<RaycastResult> Hits = new();

        /// <summary>눌렸다. 아직 끌기는 아니다 — 문턱을 넘어야 시작한다.</summary>
        internal static void Arm(UIDragSource source, Vector2 screenPoint, int pointerId)
        {
            EnsureInstance();
            _armed = source;
            _armedAt = screenPoint;
            _lastPointer = screenPoint;
            _pointerId = pointerId;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var go = new GameObject("UIDragRuntime");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UIDragRuntime>();
        }

        private void Update()
        {
            bool held = TryReadPointer(_pointerId, out Vector2 pointer);
            if (held) _lastPointer = pointer;
            else pointer = _lastPointer;

            if (Payload == null)
            {
                if (_armed == null) return;

                // 버튼을 뗐거나 손잡이가 사라졌으면 그냥 클릭이었다.
                if (!held || _armed == null)
                {
                    _armed = null;
                    return;
                }

                float threshold = _pointerId >= 0 ? DragThreshold * 2f : DragThreshold;
                if ((pointer - _armedAt).sqrMagnitude < threshold * threshold) return;

                StartDrag(pointer);
                return;
            }

            MoveGhost(pointer);
            if (!held) Drop(pointer);
        }

        private static bool TryReadPointer(int pointerId, out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            if (pointerId >= 0)
            {
                UnityEngine.InputSystem.Touchscreen screen = UnityEngine.InputSystem.Touchscreen.current;
                if (screen != null)
                {
                    foreach (UnityEngine.InputSystem.Controls.TouchControl touch in screen.touches)
                    {
                        if (!touch.press.isPressed || touch.touchId.ReadValue() != pointerId) continue;
                        position = touch.position.ReadValue();
                        return true;
                    }

                    // 입력 모듈의 포인터 ID가 기기의 touchId와 다를 수 있어 활성 주 터치를 보조로 쓴다.
                    if (screen.primaryTouch.press.isPressed)
                    {
                        position = screen.primaryTouch.position.ReadValue();
                        return true;
                    }
                }

                position = _armedAt;
                return false;
            }

            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null && mouse.leftButton.isPressed;
#else
            if (pointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != pointerId) continue;
                    position = touch.position;
                    return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                }

                position = _armedAt;
                return false;
            }

            position = Input.mousePosition;
            return Input.GetMouseButton(0);
#endif
        }

        private static void StartDrag(Vector2 pointer)
        {
            Payload = _armed.Payload;
            BuildGhost(_armed);
            MoveGhost(pointer);
            UIDropTarget.BeginDrag(Payload);
        }

        private static void Drop(Vector2 pointer)
        {
            object payload = Payload;

            // 받는 쪽이 화면을 다시 그리며 이 오브젝트들을 지울 수 있으니 먼저 정리한다.
            Finish();

            EventSystem events = EventSystem.current;
            if (events == null) return;

            var data = new PointerEventData(events) { position = pointer };
            Hits.Clear();
            events.RaycastAll(data, Hits);

            foreach (RaycastResult hit in Hits)
            {
                if (hit.gameObject == null) continue;

                // 칸 안의 글자에 맞을 수도 있으니 부모까지 훑는다.
                UIDropTarget target = hit.gameObject.GetComponentInParent<UIDropTarget>();
                if (target == null) continue;
                if (target.TryAccept(payload)) return;
            }
        }

        private static void Finish()
        {
            Payload = null;
            _armed = null;
            UIDropTarget.EndDrag();
            if (_ghost != null) _ghost.SetActive(false);
        }

        private static void BuildGhost(UIDragSource source)
        {
            Canvas canvas = source.GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) return;

            if (_ghost == null || _ghostCanvas != canvas)
            {
                if (_ghost != null) Destroy(_ghost);

                _ghostCanvas = canvas;
                Image body = UIBuild.Panel("DragGhost", canvas.transform, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 6, UITheme.Accent, 2);
                body.raycastTarget = false;
                _ghost = body.gameObject;
                _ghostRect = body.rectTransform;
                _ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
                _ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
                _ghostRect.pivot = new Vector2(0.5f, 0.5f);
                _ghostRect.sizeDelta = new Vector2(170f, 38f);

                TextMeshProUGUI label = UIBuild.Text("Label", _ghost.transform, "",
                    UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Stretch(label.rectTransform, 6f, 4f);
                label.overflowMode = TextOverflowModes.Ellipsis;
            }

            _ghost.SetActive(true);
            _ghost.transform.SetAsLastSibling();

            var text = _ghost.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = source.Label ?? "";

            var image = _ghost.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = UIShapes.CutCorner(6, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, source.Tint, 2);
            }
        }

        private static void MoveGhost(Vector2 pointer)
        {
            if (_ghostRect == null || _ghostCanvas == null) return;

            // 오버레이 캔버스에서는 카메라가 null이어야 화면 좌표가 그대로 먹힌다.
            Camera camera = _ghostCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _ghostCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_ghostCanvas.transform, pointer, camera, out Vector2 local))
            {
                _ghostRect.anchoredPosition = local;
            }
        }
    }
}
