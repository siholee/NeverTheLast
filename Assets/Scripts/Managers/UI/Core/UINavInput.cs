using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Managers.UI.Core
{
    /// <summary>
    /// 키보드·패드 메뉴 조작을 한 프레임 단위로 읽는다. 화면이 매 프레임 <see cref="Poll"/>을 부르고
    /// 결과 필드를 본다.
    ///
    ///   이동 — WASD · 방향키 · 패드 십자키 · 왼쪽 스틱
    ///   결정 — Space · Enter · 패드 A(South)
    ///   취소 — Backspace · 패드 B(East)
    ///   정보 — Q · 패드 Y(North)
    ///   시작 — 패드 Start
    ///
    /// ESC는 일부러 넣지 않았다. ESC는 어느 화면에서든 인게임 메뉴가 받는다.
    ///
    /// 방향은 누른 순간 한 번, 계속 누르고 있으면 잠시 뒤부터 일정 간격으로 반복한다.
    /// 시간은 unscaled로 센다 — 메뉴가 떠 있으면 timeScale이 0이다.
    /// </summary>
    public sealed class UINavInput
    {
        private const float RepeatDelay = 0.35f;
        private const float RepeatInterval = 0.09f;
        private const float StickThreshold = 0.5f;

        /// <summary>이번 프레임의 이동. 한 번에 한 축만 움직인다. 위가 +y다.</summary>
        public Vector2Int Move { get; private set; }

        public bool Submit { get; private set; }
        public bool Cancel { get; private set; }
        public bool Info { get; private set; }
        public bool Start { get; private set; }

        /// <summary>이번 프레임에 무엇이든 눌렸는가. 마우스와 키보드 중 무엇을 쓰는지 가를 때 쓴다.</summary>
        public bool Any => Move != Vector2Int.zero || Submit || Cancel || Info || Start;

        private Vector2Int _held;
        private float _repeatAt;

        public void Poll()
        {
            Vector2Int held = ReadHeld();
            Vector2Int move = Vector2Int.zero;
            float now = Time.unscaledTime;

            if (held != _held)
            {
                if (held != Vector2Int.zero) move = held;
                _repeatAt = now + RepeatDelay;
            }
            else if (held != Vector2Int.zero && now >= _repeatAt)
            {
                move = held;
                _repeatAt = now + RepeatInterval;
            }

            _held = held;
            Move = move;
            ReadButtons();
        }

        /// <summary>화면이 열릴 때 부른다. 여는 데 쓴 키가 곧바로 결정으로 읽히지 않게.</summary>
        public void Reset()
        {
            _held = ReadHeld();
            _repeatAt = Time.unscaledTime + RepeatDelay;
            Move = Vector2Int.zero;
            Submit = Cancel = Info = Start = false;
        }

#if ENABLE_INPUT_SYSTEM
        private static Vector2Int ReadHeld()
        {
            int x = 0;
            int y = 0;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (pad.dpad.left.isPressed || stick.x < -StickThreshold) x -= 1;
                if (pad.dpad.right.isPressed || stick.x > StickThreshold) x += 1;
                if (pad.dpad.up.isPressed || stick.y > StickThreshold) y += 1;
                if (pad.dpad.down.isPressed || stick.y < -StickThreshold) y -= 1;
            }

            return Axis(x, y);
        }

        private void ReadButtons()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;

            Submit = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame ||
                                           keyboard.enterKey.wasPressedThisFrame ||
                                           keyboard.numpadEnterKey.wasPressedThisFrame))
                     || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            Cancel = (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame)
                     || (pad != null && pad.buttonEast.wasPressedThisFrame);
            Info = (keyboard != null && keyboard.qKey.wasPressedThisFrame)
                   || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            Start = pad != null && pad.startButton.wasPressedThisFrame;
        }
#else
        private static Vector2Int ReadHeld()
        {
            int x = 0;
            int y = 0;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1;
            return Axis(x, y);
        }

        private void ReadButtons()
        {
            Submit = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                     Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton0);
            Cancel = Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1);
            Info = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton3);
            Start = Input.GetKeyDown(KeyCode.JoystickButton7);
        }
#endif

        /// <summary>대각선은 가로를 우선한다. 격자에서 두 칸이 한꺼번에 움직이면 어디로 갔는지 놓친다.</summary>
        private static Vector2Int Axis(int x, int y)
        {
            x = Mathf.Clamp(x, -1, 1);
            y = Mathf.Clamp(y, -1, 1);
            return x != 0 ? new Vector2Int(x, 0) : new Vector2Int(0, y);
        }
    }
}
