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
    /// 마우스를 올린 위젯의 자세한 정보를 띄우는 공용 툴팁.
    ///
    /// 카드 안에 제원을 전부 적으면 <b>일러스트를 넣을 자리가 없다.</b> 카드에는 그림과
    /// 이름·등급만 남기고, 숫자와 조건은 이쪽으로 옮긴다.
    ///
    /// 화면 전체에 하나만 존재한다. 캔버스는 다른 UI보다 위(<see cref="SortingOrder"/>)에 두어
    /// 모달 위에서도 가려지지 않는다.
    /// </summary>
    public sealed class UITooltip : MonoBehaviour
    {
        /// <summary>어떤 화면보다도 위. 모달(100)·HUD보다 크게 잡는다.</summary>
        private const int SortingOrder = 900;

        private const float MaxWidth = 400f;
        private const float PadX = 14f;
        private const float PadY = 12f;
        private const float CursorGap = 18f;

        private static UITooltip _instance;

        private RectTransform _panel;

        /// <summary>패널 뒤 그림자. 패널이 커서를 따라 움직이므로 매번 같이 옮긴다.</summary>
        private RectTransform _shadow;
        private const float ShadowBlur = 20f;
        private const float ShadowMargin = ShadowBlur + 6f;
        private RectTransform _canvasRect;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private Image _rarityStrip;
        private object _owner;
        private Vector2? _fixedScreenPoint;

        /// <summary>툴팁 한 줄. 라벨이 비면 본문만 있는 설명 줄이 된다.</summary>
        public readonly struct Line
        {
            public readonly string Label;
            public readonly string Value;
            public readonly Color Color;

            public Line(string label, string value, Color color)
            {
                Label = label;
                Value = value;
                Color = color;
            }

            public static Line Note(string text, Color color) => new(null, text, color);
        }

        private static UITooltip Instance
        {
            get
            {
                if (_instance != null) return _instance;

                Canvas canvas = UIBuild.Canvas("TooltipCanvas", SortingOrder);
                // 툴팁은 커서를 따라다니기만 한다. 레이캐스트를 먹으면 그 아래 위젯의
                // PointerExit이 발생해 툴팁이 깜빡인다.
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;

                _instance = canvas.gameObject.AddComponent<UITooltip>();
                _instance.Build(canvas);
                return _instance;
            }
        }

        private void Build(Canvas canvas)
        {
            _canvasRect = (RectTransform)canvas.transform;

            Image panel = UIBuild.Panel("Tooltip", canvas.transform, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            _panel = panel.rectTransform;
            _panel.pivot = new Vector2(0f, 1f);
            // ScreenPointToLocalPointInRectangle이 돌려주는 좌표는 캔버스 중심 기준이다.
            // 패널 앵커가 좌하단이면 같은 값을 anchoredPosition에 넣는 순간 캔버스 반 폭/높이만큼
            // 다시 밀려, 화면 하단의 슬롯에서 띄운 툴팁이 좌하단 밖으로 잘렸다.
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);

            Image shadow = UIBuild.Elevate(panel, (int)ShadowBlur, 0.6f, 6f);
            _shadow = shadow.rectTransform;
            _shadow.anchorMin = _shadow.anchorMax = new Vector2(0.5f, 0.5f);
            _shadow.pivot = new Vector2(0f, 1f);

            // 등급 띠는 둥근 모서리 안쪽에서 끝나야 모서리 곡선과 겹치지 않는다.
            _rarityStrip = UIBuild.Solid("Rarity", _panel, UITheme.Accent);
            _rarityStrip.rectTransform.anchorMin = new Vector2(0f, 1f);
            _rarityStrip.rectTransform.anchorMax = new Vector2(1f, 1f);
            _rarityStrip.rectTransform.pivot = new Vector2(0.5f, 1f);
            _rarityStrip.rectTransform.sizeDelta = new Vector2(-20f, 2f);
            _rarityStrip.rectTransform.anchoredPosition = new Vector2(0f, -1f);

            _title = UIBuild.Text("Title", _panel, "", UITheme.FontHeading, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            _body = UIBuild.Text("Body", _panel, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.TopLeft, wrap: true);

            _panel.gameObject.SetActive(false);
        }

        /// <summary>툴팁을 띄운다. <paramref name="owner"/>는 누가 띄웠는지 구분하는 표식이다.</summary>
        public static void Show(object owner, string title, IReadOnlyList<Line> lines, Color accent)
            => Show(owner, title, lines, accent, null);

        /// <summary>터치처럼 커서가 없는 입력에서는 지정한 화면 좌표에 고정해 띄운다.</summary>
        public static void ShowAt(object owner, string title, IReadOnlyList<Line> lines, Color accent,
            Vector2 screenPoint)
            => Show(owner, title, lines, accent, screenPoint);

        private static void Show(object owner, string title, IReadOnlyList<Line> lines, Color accent,
            Vector2? screenPoint)
        {
            UITooltip tip = Instance;
            tip._owner = owner;
            tip._fixedScreenPoint = screenPoint;
            tip._rarityStrip.color = accent;
            tip._title.text = title ?? "";
            tip._body.text = Compose(lines);
            tip._panel.gameObject.SetActive(true);
            tip._shadow.gameObject.SetActive(true);
            tip.Layout();
            tip.FollowCursor();
        }

        /// <summary>이 소유자가 띄운 툴팁만 닫는다. 다른 위젯이 이미 가져갔으면 두고 나간다.</summary>
        public static void Hide(object owner)
        {
            if (_instance == null || _instance._owner != owner) return;
            _instance._owner = null;
            _instance._fixedScreenPoint = null;
            _instance._panel.gameObject.SetActive(false);
            _instance._shadow.gameObject.SetActive(false);
        }

        private static string Compose(IReadOnlyList<Line> lines)
        {
            if (lines == null || lines.Count == 0) return "";

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) builder.Append('\n');
                Line line = lines[i];
                string hex = ColorUtility.ToHtmlStringRGB(line.Color);

                if (string.IsNullOrEmpty(line.Label))
                {
                    builder.Append($"<color=#{hex}>{line.Value}</color>");
                    continue;
                }

                string muted = ColorUtility.ToHtmlStringRGB(UITheme.TextMuted);
                builder.Append($"<color=#{muted}>{line.Label}</color>  <color=#{hex}>{line.Value}</color>");
            }
            return builder.ToString();
        }

        /// <summary>내용에 맞춰 패널 크기를 잡는다. 폭은 최대치를 넘지 않는다.</summary>
        private void Layout()
        {
            Rect safe = SafeCanvasRect();
            float maxWidth = Mathf.Min(MaxWidth, Mathf.Max(PadX * 2f + 1f, safe.width - ShadowMargin * 2f));
            float inner = maxWidth - PadX * 2f;

            _title.rectTransform.sizeDelta = new Vector2(inner, 0f);
            _body.rectTransform.sizeDelta = new Vector2(inner, 0f);

            Vector2 titleSize = _title.GetPreferredValues(_title.text, inner, 0f);
            Vector2 bodySize = _body.GetPreferredValues(_body.text, inner, 0f);
            bool hasBody = !string.IsNullOrEmpty(_body.text);

            float width = Mathf.Min(inner, Mathf.Max(titleSize.x, bodySize.x)) + PadX * 2f;
            float gap = hasBody ? 8f : 0f;
            float height = titleSize.y + gap + (hasBody ? bodySize.y : 0f) + PadY * 2f;

            _panel.sizeDelta = new Vector2(width, height);

            Place(_title.rectTransform, PadY, titleSize.y, width);
            Place(_body.rectTransform, PadY + titleSize.y + gap, bodySize.y, width);
            _body.gameObject.SetActive(hasBody);
        }

        private static void Place(RectTransform rect, float top, float height, float width)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width - PadX * 2f, height);
            rect.anchoredPosition = new Vector2(PadX, -top);
        }

        private void Update()
        {
            if (_panel != null && _panel.gameObject.activeSelf && !_fixedScreenPoint.HasValue) FollowCursor();
        }

        /// <summary>커서 오른쪽 아래에 붙이되, 화면 밖으로 나가면 반대편으로 접는다.</summary>
        private void FollowCursor()
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, _fixedScreenPoint ?? CursorScreenPosition(), null, out Vector2 local))
            {
                return;
            }

            Vector2 size = _panel.sizeDelta;
            Rect canvas = SafeCanvasRect();
            // 그림자까지 노치와 화면 밖으로 나가지 않게 패널 가용 영역을 안쪽으로 줄인다.
            canvas.xMin += ShadowMargin;
            canvas.xMax -= ShadowMargin;
            canvas.yMin += ShadowMargin;
            canvas.yMax -= ShadowMargin;
            float x = local.x + CursorGap;
            float y = local.y - CursorGap;

            if (x + size.x > canvas.xMax) x = local.x - CursorGap - size.x;
            if (y - size.y < canvas.yMin) y = local.y + CursorGap + size.y;

            _panel.anchoredPosition = new Vector2(
                Mathf.Clamp(x, canvas.xMin, Mathf.Max(canvas.xMin, canvas.xMax - size.x)),
                Mathf.Clamp(y, Mathf.Min(canvas.yMax, canvas.yMin + size.y), canvas.yMax));

            _shadow.sizeDelta = size + new Vector2(ShadowBlur * 2f, ShadowBlur * 2f);
            _shadow.anchoredPosition = _panel.anchoredPosition + new Vector2(-ShadowBlur, ShadowBlur - 6f);
        }

        private Rect SafeCanvasRect()
        {
            Rect safe = Screen.safeArea;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, safe.min, null, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, safe.max, null, out Vector2 max))
            {
                return _canvasRect.rect;
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Vector2 CursorScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    /// <summary>
    /// 위젯에 붙이는 호버 감지기. 내용은 지연 생성한다 —
    /// 목록의 모든 항목이 미리 문자열을 만들면 스크롤할 때마다 낭비가 크다.
    /// </summary>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler
    {
        private const float TouchHoldSeconds = 0.55f;
        private const float TouchMoveTolerance = 18f;

        private Func<string> _title;
        private Func<IReadOnlyList<UITooltip.Line>> _lines;
        private Color _accent = UITheme.Accent;
        private PointerEventData _touchEvent;
        private Vector2 _touchStart;
        private float _touchStartedAt;
        private bool _touchTooltipShown;

        /// <summary>호버하면 툴팁이 뜨도록 만든다. 이미 붙어 있으면 내용만 갈아 끼운다.</summary>
        public static TooltipTrigger Attach(GameObject target, Func<string> title,
            Func<IReadOnlyList<UITooltip.Line>> lines, Color accent)
        {
            if (target == null) return null;

            // 레이캐스트를 받는 그래픽이 있어야 포인터 이벤트가 들어온다.
            var graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;

            var trigger = target.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = target.AddComponent<TooltipTrigger>();

            trigger._title = title;
            trigger._lines = lines;
            trigger._accent = accent;
            return trigger;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsTouch(eventData)) return;
            if (_title == null) return;
            UITooltip.Show(this, _title(), _lines?.Invoke(), _accent);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!IsTouch(eventData)) UITooltip.Hide(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsTouch(eventData) || _title == null) return;
            _touchEvent = eventData;
            _touchStart = eventData.position;
            _touchStartedAt = Time.unscaledTime;
            _touchTooltipShown = false;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_touchEvent == null || eventData.pointerId != _touchEvent.pointerId) return;
            if ((eventData.position - _touchStart).sqrMagnitude > TouchMoveTolerance * TouchMoveTolerance)
                CancelTouch();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_touchEvent == null || eventData.pointerId != _touchEvent.pointerId) return;
            if (_touchTooltipShown) eventData.eligibleForClick = false;
            CancelTouch();
        }

        private void Update()
        {
            if (_touchEvent == null || _touchTooltipShown ||
                Time.unscaledTime - _touchStartedAt < TouchHoldSeconds) return;

            // EventSystem이 같은 PointerEventData로 click 여부를 결정하므로 길게 누른 입력은
            // Button/EventTrigger의 본 동작으로 이어지지 않는다.
            _touchEvent.eligibleForClick = false;
            _touchTooltipShown = true;
            UITooltip.ShowAt(this, _title(), _lines?.Invoke(), _accent, _touchStart);
        }

        private void CancelTouch()
        {
            if (_touchTooltipShown) UITooltip.Hide(this);
            _touchEvent = null;
            _touchTooltipShown = false;
        }

        private static bool IsTouch(PointerEventData eventData) => eventData != null && eventData.pointerId >= 0;

        private void OnDisable()
        {
            CancelTouch();
            UITooltip.Hide(this);
        }
    }
}
