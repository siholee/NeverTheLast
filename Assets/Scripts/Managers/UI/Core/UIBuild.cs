using System;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// UI 요소를 코드로 조립하는 헬퍼.
    /// 모든 화면이 이 함수들만 거쳐 위젯을 만들기 때문에,
    /// 나중에 시판 UI 에셋(Modern UI Pack / Heat 등)을 도입하면
    /// 각 함수의 "내부"만 그 에셋의 프리팹 생성으로 바꾸면 화면 코드는 그대로 둘 수 있다.
    /// </summary>
    public static class UIBuild
    {
        // ── 캔버스 / 컨테이너 ────────────────────────────────────────

        /// <summary>이름으로 오버레이 캔버스를 찾거나 만든다. sortingOrder가 클수록 위에 그려진다.</summary>
        public static Canvas Canvas(string name, int sortingOrder)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Canvas found = existing.GetComponent<Canvas>();
                if (found != null) return found;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            // 0.5 = 가로/세로 변화를 같은 비중으로 반영. 모바일 세로↔가로 대응에 무난하다.
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>클릭이 동작하려면 씬에 EventSystem이 하나는 있어야 한다.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        /// <summary>그래픽 없는 빈 컨테이너. 레이아웃 묶음용.</summary>
        public static RectTransform Container(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 세로 스크롤 영역. 돌려준 content 아래를 위에서부터 채우고, 다 채운 뒤
        /// content의 높이(<c>sizeDelta.y</c>)를 호출한 쪽이 직접 정한다.
        /// 배치는 돌려받은 <paramref name="scroll"/>의 RectTransform으로 잡는다.
        ///
        /// <b>안에 넣는 항목의 클릭은 <see cref="OnClick"/>이 아니라 Button으로 받는다.</b>
        /// EventTrigger는 휠 이벤트까지 먹어 버려, 항목 위에서 굴리면 목록이 움직이지 않는다.
        /// </summary>
        public static RectTransform ScrollArea(string name, Transform parent, out ScrollRect scroll)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                typeof(ScrollRect));
            go.transform.SetParent(parent, false);

            // 투명해도 레이캐스트는 받아야 빈 곳에서 굴려도 휠이 들어온다.
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);

            RectTransform content = Container("Content", go.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = go.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        // ── 패널 ─────────────────────────────────────────────────────

        /// <summary>컷코너 패널. 이 UI의 기본 면(surface).</summary>
        public static Image Panel(string name, Transform parent, Color fill,
            UIShapes.Corner corners = UIShapes.Corner.Diagonal, int cut = 10,
            Color outline = default, int outlineWidth = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.CutCorner(cut, fill, corners, outline, outlineWidth);
            image.type = Image.Type.Sliced;
            // 스프라이트에 색이 이미 구워져 있으므로 tint는 흰색으로 둔다.
            image.color = Color.white;
            return image;
        }

        /// <summary>단색 사각형. 구분선·배경처럼 모양이 필요 없을 때.</summary>
        public static Image Solid(string name, Transform parent, Color fill)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.Solid(Color.white);
            image.type = Image.Type.Sliced;
            image.color = fill;
            return image;
        }

        /// <summary>전체 화면 암전. 뒤 클릭을 막는 역할도 한다.</summary>
        public static Image Backdrop(string name, Transform parent)
        {
            Image image = Solid(name, parent, UITheme.Backdrop);
            Stretch(image.rectTransform);
            return image;
        }

        // ── 텍스트 ───────────────────────────────────────────────────

        public static TextMeshProUGUI Text(string name, Transform parent, string content,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = content;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.raycastTarget = false; // 텍스트가 아래 버튼 클릭을 막지 않도록
            return label;
        }

        /// <summary>대문자 + 자간 넓힘. 명일방주 UI의 라벨 표기 습관.</summary>
        public static TextMeshProUGUI Label(string name, Transform parent, string content,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            TextMeshProUGUI label = Text(name, parent, content, fontSize, color, alignment);
            label.characterSpacing = 8f;
            return label;
        }

        // ── 버튼 ─────────────────────────────────────────────────────

        /// <summary>
        /// 컷코너 버튼. 색 전이는 Unity 기본 ColorTint를 쓰되,
        /// 스프라이트에 색이 구워져 있으므로 tint를 곱해 명도만 흔든다.
        /// </summary>
        public static Button Button(string name, Transform parent, string text, Action onClick,
            bool primary = false, float fontSize = UITheme.FontBody)
        {
            Color fill = primary ? UITheme.Accent : UITheme.SurfaceRaised;
            Color textColor = primary ? UITheme.TextOnAccent : UITheme.TextPrimary;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.CutCorner(8, fill, UIShapes.Corner.Diagonal);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(() => onClick());

            if (!string.IsNullOrEmpty(text))
            {
                TextMeshProUGUI label = Text("Label", go.transform, text, fontSize, textColor,
                    TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 8f, 4f);
            }

            return button;
        }

        /// <summary>아이콘(문자) 하나만 들어가는 정사각 버튼. HUD 우상단 배속/인벤/설정용.</summary>
        public static Button IconButton(string name, Transform parent, string glyph, Action onClick,
            string tooltip = null)
        {
            Button button = Button(name, parent, glyph, onClick, false, UITheme.FontHeading);
            if (!string.IsNullOrEmpty(tooltip)) button.gameObject.name = $"{name}_{tooltip}";
            return button;
        }

        // ── 게이지 ───────────────────────────────────────────────────

        /// <summary>
        /// 가로 게이지. 반환된 Image의 fillAmount를 0~1로 조절해 쓴다.
        /// track(배경)은 자동 생성되며 fill만 반환한다.
        /// </summary>
        public static Image Bar(string name, Transform parent, Color fillColor, Color trackColor,
            int cut = 0)
        {
            Image track = cut > 0
                ? Panel($"{name}Track", parent, trackColor, UIShapes.Corner.None, cut)
                : Solid($"{name}Track", parent, trackColor);

            var go = new GameObject($"{name}Fill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(track.transform, false);
            var fill = go.GetComponent<Image>();
            fill.sprite = UIShapes.Solid(Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = fillColor;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            return fill;
        }

        /// <summary>
        /// 원형 프로그레스. fillAmount 0~1이 12시 방향에서 시계방향으로 찬다.
        /// 라운드 진행 타이머처럼 "남은 양"을 보여줄 때 쓴다.
        /// </summary>
        public static Image RadialBar(string name, Transform parent, Color fillColor, Color trackColor,
            int diameter = 64, float innerRatio = 0.72f)
        {
            var trackGo = new GameObject($"{name}Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(parent, false);
            var track = trackGo.GetComponent<Image>();
            track.sprite = UIShapes.Disc(diameter, trackColor, innerRatio);
            track.raycastTarget = false;

            var fillGo = new GameObject($"{name}Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = UIShapes.Disc(diameter, Color.white, innerRatio);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
            fill.color = fillColor;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            return fill;
        }

        /// <summary>가로 구분선.</summary>
        public static Image Divider(string name, Transform parent)
        {
            return Solid(name, parent, UITheme.Divider);
        }

        // ── RectTransform 배치 ───────────────────────────────────────

        /// <summary>부모를 꽉 채운다.</summary>
        public static RectTransform Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
            return rect;
        }

        /// <summary>앵커 비율로 배치(0~1). 부모 크기에 비례해 늘어난다.</summary>
        public static RectTransform Anchor(RectTransform rect, Vector2 min, Vector2 max,
            float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
            return rect;
        }

        /// <summary>
        /// 한 점에 고정하고 픽셀 크기를 준다. 모서리에 붙는 HUD 요소에 적합하다.
        /// pivot/anchor가 같으므로 offset은 그 모서리 기준 이동량이 된다.
        /// </summary>
        public static RectTransform Pin(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        // ── 상호작용 ─────────────────────────────────────────────────

        /// <summary>임의의 오브젝트에 클릭 반응을 붙인다(카드 전체를 누르게 할 때).</summary>
        public static void OnClick(GameObject target, Action callback)
        {
            if (target == null || callback == null) return;
            EventTrigger trigger = Ensure<EventTrigger>(target);
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        /// <summary>CanvasGroup을 보장한다. 페이드/입력차단 제어용.</summary>
        public static CanvasGroup Group(GameObject target)
        {
            return Ensure<CanvasGroup>(target);
        }

        /// <summary>
        /// 컴포넌트를 보장한다. 없으면 붙인다.
        ///
        /// <b><c>??</c>를 쓰면 안 된다.</b> 널 병합 연산자는 참조 동등성만 보므로
        /// UnityEngine.Object의 수명 검사(파괴된 객체를 null로 취급하는 <c>==</c> 오버로드)를
        /// 건너뛴다. 그 결과 "있는 것처럼 보이지만 실제로는 없는" 컴포넌트를 그대로 돌려주고,
        /// 나중에 접근하는 쪽에서 MissingComponentException이 난다.
        /// (행동서열 슬롯의 CanvasGroup이 실제로 이 경로로 깨졌다.)
        /// </summary>
        private static T Ensure<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        /// <summary>자식을 전부 지운다. 목록을 다시 그릴 때.</summary>
        public static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
