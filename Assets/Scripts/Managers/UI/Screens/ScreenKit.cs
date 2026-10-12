using System.Collections.Generic;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 관리 화면(상점 · 보상 · 파티 · 선택 · 자료실 · 메뉴)이 함께 쓰는 작은 조립 부품.
    ///
    /// 등급색이 군청 한 가지로 통일된 뒤 티어 T1~T5가 화면에서 구분되지 않았다(2026-10-08 검수 D8 · D13).
    /// 색을 되돌리지 않고 <b>눈금 수와 T 번호</b>로 읽게 한다 — 흑백으로 찍어도 차이가 남는다.
    /// 공통 Theme/Core는 root 소유라 화면 전용 부품만 여기에 둔다.
    /// </summary>
    internal static class ScreenKit
    {
        private const int TickCount = 5;

        /// <summary>눈금 한 개의 폭과 간격(기준 해상도 px). 960×540에서도 1px 이상 남는 값이다.</summary>
        private const float TickWidth = 4f;
        private const float TickGap = 3f;

        private static readonly Color TickOff = new(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.16f);

        /// <summary>배지 전체 폭. 눈금 다섯 + 간격 + "T5" 글자.</summary>
        internal static float TierBadgeWidth(float height) =>
            TickCount * TickWidth + (TickCount - 1) * TickGap + 8f + height * 1.5f;

        /// <summary>
        /// 티어 배지 — 높이가 오르는 눈금 다섯 개 중 티어만큼 채우고, 옆에 굵은 "T3"을 붙인다.
        /// 반환한 사각형은 크기만 정해져 있다. 자리는 호출한 쪽이 <see cref="PinTopLeft"/> 등으로 잡는다.
        /// 글자 오브젝트 이름은 "TierText" — 검증의 잘림 금지 목록("Tier")과 섞이지 않는다.
        /// </summary>
        internal static RectTransform TierBadge(Transform parent, int tier, float height = 26f)
        {
            tier = Mathf.Clamp(tier, 1, TickCount);
            RectTransform root = UIBuild.Container("TierBadge", parent);
            root.sizeDelta = new Vector2(TierBadgeWidth(height), height);

            for (int i = 0; i < TickCount; i++)
            {
                Image tick = UIBuild.Solid($"Tick{i + 1}", root, i < tier ? UITheme.Accent : TickOff);
                tick.raycastTarget = false;
                RectTransform rect = tick.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                // 오른쪽으로 갈수록 높아지는 막대 — 신호 세기처럼 "몇 칸"이 모양으로 읽힌다.
                rect.sizeDelta = new Vector2(TickWidth, TickWidth);
                rect.localRotation = Quaternion.Euler(0, 0, 45f);
                rect.anchoredPosition = new Vector2(i * (TickWidth + TickGap), 0f);
            }

            float textLeft = TickCount * TickWidth + (TickCount - 1) * TickGap + 8f;
            TextMeshProUGUI label = UIBuild.Text("TierText", root, $"T{tier}", height * 0.82f, UITheme.Accent,
                TextAlignmentOptions.MidlineLeft);
            label.fontStyle = FontStyles.Bold;
            UIBuild.Stretch(label.rectTransform);
            // TMP의 실제 줄 높이는 글자 크기의 약 1.4배다. 배지 높이를 넘겨도 잘리지 않게 위아래로 여유를 준다.
            label.rectTransform.offsetMin = new Vector2(textLeft, -height * 0.3f);
            label.rectTransform.offsetMax = new Vector2(0f, height * 0.3f);
            return root;
        }

        /// <summary>왼쪽 위 모서리 기준으로 고정한다. y는 위에서 아래로 내려가는 양이다.</summary>
        internal static RectTransform PinTopLeft(RectTransform rect, float left, float top)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            return rect;
        }

        /// <summary>부모 폭을 좌우 여백만 남기고 채우는 위쪽 띠. 높이는 픽셀이다.</summary>
        internal static RectTransform TopBand(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>부모 폭을 채우는 아래쪽 띠. bottom은 아래 끝에서 올라오는 양이다.</summary>
        internal static RectTransform BottomBand(RectTransform rect, float bottom, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
            return rect;
        }

        /// <summary>
        /// 코드 아이콘 타일 — 옅은 군청 바탕 위에 선 아이콘 하나. 아이콘이 카드 절반을 차지하던
        /// 자리표시자 느낌(D10)을 걷고, 이름 옆의 작은 표지로만 쓴다.
        /// </summary>
        internal static Image IconTile(Transform parent, UIIcon icon, float size, string name = "IconTile")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image tile = go.GetComponent<Image>();
            tile.sprite = UIShapes.RoundedRect(8, UITheme.AccentFaint);
            tile.type = Image.Type.Sliced;
            tile.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            Image glyph = UIBuild.Icon("Icon", tile.transform, icon, UITheme.Accent);
            UIBuild.Stretch(glyph.rectTransform, size * 0.2f, size * 0.2f);
            return tile;
        }

        /// <summary>선택 표지 — 군청 2px 외곽선. 나머지 테두리(1px 헤어라인)와 두께로 갈린다.</summary>
        internal static Sprite SelectedSurface(Color fill) =>
            UIShapes.RoundedRect(8, fill, UITheme.Accent, 2);

        /// <summary>평상 면 — 1px 헤어라인. <see cref="UIShapes.Surface"/>는 테두리를 25%로 낮춰 거의 보이지 않는다.</summary>
        internal static Sprite IdleSurface(Color fill) =>
            UIShapes.RoundedRect(8, fill, new Color(UITheme.Outline.r, UITheme.Outline.g, UITheme.Outline.b, 0.55f), 1);

        /// <summary>
        /// 탭 — 목록 행 · 타일과 같은 선택 문법(<see cref="UIArtDirection.Tab"/>): 옅은 군청 면 + 글자 폭의 3px 막대.
        /// 예전 D10의 "두 겹 표지"는 회색 블록 위에 탭 전체 폭 밑줄이 겹친 것이었다. 지금 면은 군청 10%이고
        /// 막대는 글자 아래에만 놓여 한 덩어리 표지로 읽힌다.
        /// </summary>
        internal static void FlatTab(Button button, bool selected)
        {
            if (button == null) return;
            UIArtDirection.Tab(button, selected);
        }

        /// <summary>
        /// 스크롤 목록 아래 끝을 바탕색으로 녹이는 띠. 마지막 줄이 칼로 자른 듯 반만 보이면
        /// "끝"인지 "더 있다"인지 읽히지 않는다(D8 · D11 · D12). 더 굴릴 것이 있을 때만 켜진다.
        /// 막대도 6px · 짙게 바꿔 더 있다는 신호를 준다.
        /// </summary>
        internal static void ScrollFade(ScrollRect scroll, Color background, float height = 28f)
        {
            if (scroll == null) return;
            RectTransform viewport = (RectTransform)scroll.transform;
            Color clear = new(background.r, background.g, background.b, 0f);
            Image fade = UIBuild.Solid("BottomFade", viewport, Color.white);
            fade.sprite = UIShapes.VerticalGradient(32, clear, background);
            fade.type = Image.Type.Simple;
            fade.raycastTarget = false;
            BottomBand(fade.rectTransform, 0f, height, 0f, 10f);
            fade.transform.SetAsLastSibling();

            if (scroll.verticalScrollbar != null)
            {
                RectTransform bar = (RectTransform)scroll.verticalScrollbar.transform;
                bar.sizeDelta = new Vector2(6f, bar.sizeDelta.y);
                bar.SetAsLastSibling();
                if (scroll.verticalScrollbar.targetGraphic is Image handle)
                    handle.color = new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.55f);
            }

            ScrollFadeView view = viewport.GetComponent<ScrollFadeView>();
            if (view == null) view = viewport.gameObject.AddComponent<ScrollFadeView>();
            view.Scroll = scroll;
            view.Fade = fade;
        }

        /// <summary>
        /// 초상 원본에서 머리 위 여백을 남긴 위쪽 창을 잘라 낸다(가로/세로 = <paramref name="aspect"/>).
        /// <see cref="UIPortraitCrop.Face"/>는 아래 57%를 잘라 작은 HUD용 얼굴만 남기므로, 타일처럼 큰 칸에서는
        /// 머리카락 위가 잘렸다(D12). 원본 · GUID는 그대로 두고 스프라이트만 새로 만든다.
        /// </summary>
        internal static Sprite PortraitWindow(Sprite source, float aspect)
        {
            if (source == null) return null;
            string key = $"{source.GetInstanceID()}_{aspect:0.000}";
            if (Windows.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Rect r = source.rect;
            float width = r.width;
            float height = width / Mathf.Max(0.1f, aspect);
            if (height > r.height)
            {
                height = r.height;
                width = height * aspect;
            }
            // 위쪽 2%만 비우고 그 아래를 쓴다. 정사각 초상의 머리는 상단 15~25%에서 시작한다.
            float top = r.yMax - r.height * 0.02f;
            float y = Mathf.Max(r.y, top - height);
            float x = r.x + (r.width - width) * 0.5f;
            Sprite window = Sprite.Create(source.texture, new Rect(x, y, width, Mathf.Min(height, top - y)),
                new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            window.name = source.name + ".Window";
            window.hideFlags = HideFlags.HideAndDontSave;
            Windows[key] = window;
            return window;
        }

        private static readonly Dictionary<string, Sprite> Windows = new();

        /// <summary>키캡 배지 — "ESC" 같은 단축키를 1px 외곽선 안에 적는다. 부제 대신 쓴다(D16).</summary>
        internal static RectTransform KeyCap(Transform parent, string key, Color ink)
        {
            Image cap = UIBuild.Solid("KeyCap", parent, Color.white);
            cap.sprite = UIShapes.RoundedRect(5, Color.clear, new Color(ink.r, ink.g, ink.b, 0.6f), 1);
            cap.type = Image.Type.Sliced;
            cap.raycastTarget = false;
            cap.rectTransform.sizeDelta = new Vector2(Mathf.Max(44f, key.Length * 11f + 18f), 26f);
            TextMeshProUGUI label = UIBuild.Text("Key", cap.transform, key, UITheme.FontMicro, ink,
                TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            UIBuild.Stretch(label.rectTransform, 2f, -4f);
            return cap.rectTransform;
        }
    }

    /// <summary>스크롤 아래 페이드를 "더 굴릴 것이 남았을 때"만 켠다.</summary>
    internal sealed class ScrollFadeView : MonoBehaviour
    {
        public ScrollRect Scroll;
        public Image Fade;

        private void LateUpdate()
        {
            if (Scroll == null || Fade == null || Scroll.content == null) return;
            RectTransform viewport = (RectTransform)Scroll.transform;
            bool overflow = Scroll.content.rect.height > viewport.rect.height + 2f;
            bool show = overflow && Scroll.verticalNormalizedPosition > 0.01f;
            if (Fade.enabled != show) Fade.enabled = show;
        }
    }
}
