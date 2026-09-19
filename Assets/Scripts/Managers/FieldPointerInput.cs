using System.Collections.Generic;
using Entities;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using static BaseClasses.BaseEnums;

namespace Managers
{
    /// <summary>
    /// 전장 카드의 클릭 · 드래그 · 호버를 한 곳에서 판정한다.
    ///
    /// 예전에는 칸마다 Unity의 OnMouseDown/Drag/Up 메시지를 받고, UI 위인지를
    /// <c>EventSystem.IsPointerOverGameObject()</c>로 물었다. 이 프로젝트의 입력 모듈(InputSystemUIInputModule)은
    /// 그 질문에 <b>직전 프레임</b>의 호버 결과로 답한다. 그래서
    ///   · 하단 [전투 시작] 버튼을 누르면 그 아래 깔린 카드의 클릭으로도 처리되어 캐릭터 창이 "뒤늦게" 떴고,
    ///   · 카드 클릭은 0.15초(게임 시간, 배속을 따른다)만 눌러도 드래그로 넘어가 삼켜졌다(QA 두 번).
    ///
    /// 이제 누른 순간 같은 프레임에 UI 레이캐스트를 직접 쏘아 UI가 하나라도 맞으면 전장 입력을 받지 않는다.
    /// 클릭과 드래그는 <b>이동 거리만</b>으로 가른다 — 누른 칸에서 떼면 클릭, 일정 거리 이상 끌면 드래그다.
    /// </summary>
    public sealed class FieldPointerInput : MonoBehaviour
    {
        /// <summary>드래그로 보는 이동 거리(1080p 기준 px). 해상도에 비례해 늘린다.</summary>
        private const float DragThreshold = 12f;

        private static FieldPointerInput _instance;

        private readonly List<RaycastResult> _uiHits = new();
        private PointerEventData _pointer;

        private Cell _pressed;
        private Vector2 _pressPosition;
        private bool _dragging;
        private Cell _hovered;
#if ENABLE_INPUT_SYSTEM
        private bool _rawMouseHeld;
        private bool _hasRawMouseDown;
        private bool _hasRawMouseUp;
        private Vector2 _rawMouseDownPosition;
        private Vector2 _rawMouseUpPosition;
#endif

        private readonly struct PointerState
        {
            public readonly Vector2 Position;
            public readonly bool Down;
            public readonly bool Held;
            public readonly bool Up;
            public readonly bool IsTouch;
            public readonly bool Canceled;
            public readonly Vector2 Delta;
            public readonly bool HasExactPressPosition;

            public PointerState(Vector2 position, bool down, bool held, bool up, bool isTouch,
                bool canceled = false, Vector2 delta = default, bool hasExactPressPosition = false)
            {
                Position = position;
                Down = down;
                Held = held;
                Up = up;
                IsTouch = isTouch;
                Canceled = canceled;
                Delta = delta;
                HasExactPressPosition = hasExactPressPosition;
            }
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            _rawMouseHeld = mouse != null && mouse.leftButton.isPressed;
            UnityEngine.InputSystem.InputSystem.onEvent += OnInputEvent;
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.InputSystem.onEvent -= OnInputEvent;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnInputEvent(UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr,
            UnityEngine.InputSystem.InputDevice device)
        {
            if (device is not UnityEngine.InputSystem.Mouse mouse) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            if (!mouse.leftButton.ReadValueFromEvent(eventPtr, out float buttonValue)) return;
            bool held = buttonValue > 0.5f;
            Vector2 position = mouse.position.ReadValueFromEvent(eventPtr, out Vector2 eventPosition)
                ? eventPosition
                : mouse.position.ReadValue();
            if (held && !_rawMouseHeld)
            {
                _hasRawMouseDown = true;
                _rawMouseDownPosition = position;
            }
            else if (!held && _rawMouseHeld)
            {
                _hasRawMouseUp = true;
                _rawMouseUpPosition = position;
            }
            _rawMouseHeld = held;
        }
#endif

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GameObject("FieldPointerInput").AddComponent<FieldPointerInput>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            PointerState pointer = ReadPointer();
            Vector2 position = pointer.Position;
            bool overUi = IsOverUi(position);

            if (pointer.Down)
            {
                // 자동화 입력이나 낮은 프레임에서는 press/move/release가 한 Update에 합쳐질 수 있다.
                // 그때 최종 위치를 시작점으로 쓰면 거리 0의 클릭이 되어 캐릭터 창이 열린다.
                Vector2 downPosition = pointer.Up && pointer.HasExactPressPosition
                    ? position - pointer.Delta
                    : position;

                _pressed = IsOverUi(downPosition) ? null : CellWithUnitAt(downPosition);
                _pressPosition = downPosition;
                _dragging = false;
            }

            if (_pressed != null && pointer.Held && !_dragging)
            {
                float touchScale = pointer.IsTouch ? 1.5f : 1f;
                float threshold = DragThreshold * touchScale * Mathf.Max(1f, Screen.height / 1080f);
                if (Vector2.Distance(position, _pressPosition) > threshold && CanDrag(_pressed))
                {
                    SetHovered(null);
                    DragAndDropManager.Instance?.StartDrag(_pressed, position);
                    _dragging = DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging();
                }
            }

            if (pointer.Up)
            {
                // 누른 뒤 다음 프레임이 오기 전에 멀리 옮겨 뗀 빠른 끌기. 드래그가 시작될 틈이 없었으므로
                // 여기서 시작해 바로 놓는다 — 예전에는 클릭도 드래그도 아니게 되어 카드가 제자리로 돌아갔다(QA).
                if (!_dragging && _pressed != null && CanDrag(_pressed) &&
                    Vector2.Distance(position, _pressPosition) > DragThreshold *
                    (pointer.IsTouch ? 1.5f : 1f) * Mathf.Max(1f, Screen.height / 1080f))
                {
                    DragAndDropManager.Instance?.StartDrag(_pressed, position);
                    _dragging = DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging();
                }

                if (_dragging)
                {
                    if (pointer.Canceled || overUi) DragAndDropManager.Instance?.CancelDrag();
                    else DragAndDropManager.Instance?.EndDrag(position);
                }
                else if (_pressed != null && !overUi && CellWithUnitAt(position) == _pressed)
                {
                    Unit unit = UnitOf(_pressed);
                    if (unit != null) GameManager.Instance?.uiManager?.ShowUnitDetail(unit);
                }

                _pressed = null;
                _dragging = false;
            }

            if (_dragging && pointer.Held) DragAndDropManager.Instance?.UpdateDragPointer(position);

            // 호버 — 누르고 있거나 UI 위면 툴팁을 걷는다.
            bool busy = _dragging || pointer.Held;
            SetHovered(pointer.IsTouch || busy || overUi ? null : CellWithUnitAt(position));
            if (_hovered != null) _hovered.ShowHoverTooltip();
        }

        private PointerState ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            if (_hasRawMouseDown || _hasRawMouseUp)
            {
                bool down = _hasRawMouseDown;
                bool up = _hasRawMouseUp;
                Vector2 position = up ? _rawMouseUpPosition : _rawMouseDownPosition;
                Vector2 delta = down && up ? _rawMouseUpPosition - _rawMouseDownPosition : Vector2.zero;
                _hasRawMouseDown = false;
                _hasRawMouseUp = false;
                return new PointerState(position, down, _rawMouseHeld, up, false,
                    false, delta, down && up);
            }

            UnityEngine.InputSystem.Touchscreen touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && (touch.primaryTouch.press.isPressed ||
                                  touch.primaryTouch.press.wasPressedThisFrame ||
                                  touch.primaryTouch.press.wasReleasedThisFrame))
            {
                bool canceled = touch.primaryTouch.phase.ReadValue() ==
                                UnityEngine.InputSystem.TouchPhase.Canceled;
                return new PointerState(touch.primaryTouch.position.ReadValue(),
                    touch.primaryTouch.press.wasPressedThisFrame,
                    touch.primaryTouch.press.isPressed,
                    touch.primaryTouch.press.wasReleasedThisFrame, true, canceled);
            }

            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return new PointerState(Vector2.zero, false, false, false, false);
            return new PointerState(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame, false);
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                return new PointerState(touch.position, touch.phase == TouchPhase.Began,
                    touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled,
                    touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled, true,
                    touch.phase == TouchPhase.Canceled);
            }

            return new PointerState(Input.mousePosition, Input.GetMouseButtonDown(0),
                Input.GetMouseButton(0), Input.GetMouseButtonUp(0), false);
#endif
        }

        private void SetHovered(Cell cell)
        {
            if (_hovered == cell) return;
            if (_hovered != null) _hovered.HideHoverTooltip();
            _hovered = cell;
        }

        /// <summary>아군만, 준비 페이즈에만 옮길 수 있다. 벤치 규칙은 DragAndDropManager가 다시 본다.</summary>
        private static bool CanDrag(Cell cell)
        {
            Unit unit = UnitOf(cell);
            if (unit == null || unit.IsEnemy) return false;
            return GameManager.Instance != null && GameManager.Instance.gameState == GameState.Preparation;
        }

        private static Unit UnitOf(Cell cell)
        {
            if (cell == null || !cell.isOccupied || cell.unit == null) return null;
            Unit unit = cell.unit.GetComponent<Unit>();
            return unit != null && unit.isActive ? unit : null;
        }

        /// <summary>화면 좌표 아래의 칸 중 살아 있는 유닛이 선 칸.</summary>
        private static Cell CellWithUnitAt(Vector2 screen)
        {
            Camera camera = Camera.main;
            if (camera == null) return null;

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            foreach (Collider2D hit in hits)
            {
                // ??는 쓰지 않는다 — 유니티 객체의 null 검사를 건너뛴다(UIBuild.Ensure 참조).
                Cell cell = hit.GetComponent<Cell>();
                if (cell == null) cell = hit.GetComponentInParent<Cell>();
                if (cell != null && cell.gameObject.activeInHierarchy && UnitOf(cell) != null) return cell;
            }

            return null;
        }

        /// <summary>
        /// 이 프레임의 포인터 위치에 UI 그래픽이 하나라도 있는가.
        /// 입력 모듈의 캐시를 묻지 않고 직접 레이캐스트한다 — 캐시는 한 프레임 늦다.
        /// </summary>
        private bool IsOverUi(Vector2 screen)
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;

            _pointer ??= new PointerEventData(system);
            _pointer.Reset();
            _pointer.position = screen;
            _uiHits.Clear();
            system.RaycastAll(_pointer, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
