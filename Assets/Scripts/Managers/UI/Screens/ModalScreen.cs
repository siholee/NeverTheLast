using System.Collections.Generic;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 전체 화면을 덮는 모달의 공통 뼈대.
    /// 청회색 배경 베일 + 유리 표면 + 제목을 제공하고,
    /// 실제 내용은 상속받은 화면이 <see cref="Body"/> 아래에 채운다.
    ///
    /// 지연 생성이라 처음 열릴 때까지 GameObject를 만들지 않는다.
    /// </summary>
    public abstract class ModalScreen
    {
        private GameObject _root;
        private TextMeshProUGUI _title;

        /// <summary>떠 있는 모달. 여는 순서대로 쌓인다. ESC가 가장 위의 것부터 닫는다.</summary>
        private static readonly List<ModalScreen> Open = new();
        /// <summary>전경 모달이 열려 있으면 뒤의 전투 조작·준비 독을 표시하지 않는다.</summary>
        internal static bool HasVisibleScreen
        {
            get
            {
                for (int i = Open.Count - 1; i >= 0; i--)
                    if (Open[i]?.IsVisible != true) Open.RemoveAt(i);
                return Open.Count > 0;
            }
        }

        /// <summary>내용을 채울 영역. 제목 아래 전체를 차지한다.</summary>
        protected RectTransform Body { get; private set; }

        protected abstract string CanvasName { get; }
        protected abstract int SortingOrder { get; }
        protected abstract string Title { get; }
        protected virtual Color BackdropTint => UITheme.Backdrop;

        /// <summary>패널이 차지하는 화면 비율. 화면마다 다르게 잡는다.</summary>
        protected virtual Vector2 AnchorMin => new(0.16f, 0.14f);
        protected virtual Vector2 AnchorMax => new(0.84f, 0.86f);

        /// <summary>
        /// 관리형 화면은 상단 바와 같은 좌우 끝선을 쓴다. 짧은 확인 창·메뉴만 중앙 폭을 유지한다.
        /// </summary>
        protected virtual bool AlignToScreenFrame => this is CharacterSelectScreen or ShopScreen or RewardScreen or WikiScreen or InventoryScreen;

        /// <summary>배경을 눌러 닫을 수 있는지. 진행을 막아야 하는 화면은 false.</summary>
        protected virtual bool CloseOnBackdrop => false;

        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>읽을 내용이 적은 창은 기준 해상도 높이만큼만 연다. 선택 흐름은 필요한 높이로 다시 펼친다.</summary>
        protected void FitContentHeight(float height)
        {
            if (Body == null || Body.parent is not RectTransform panel || panel.parent is not RectTransform safe) return;
            panel.anchorMin = new Vector2(AnchorMin.x, .5f);
            panel.anchorMax = new Vector2(AnchorMax.x, .5f);
            panel.sizeDelta = new Vector2(0, Mathf.Min(height, safe.rect.height * .92f));
            panel.anchoredPosition = Vector2.zero;
        }


        /// <summary>패널이 처음 만들어질 때 한 번 호출된다. 여기서 내용을 조립한다.</summary>
        protected abstract void Build();

        protected void EnsureBuilt()
        {
            if (_root != null) return;

            Canvas canvas = UIBuild.Canvas(CanvasName, SortingOrder);
            _root = new GameObject(GetType().Name, typeof(RectTransform));
            _root.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_root.GetComponent<RectTransform>());

            Image backdrop = UIBuild.Backdrop("Backdrop", _root.transform);
            backdrop.color=BackdropTint;
            if (CloseOnBackdrop) UIBuild.OnClick(backdrop.gameObject, Hide);

            RectTransform safe = _root.AddComponent<UISafeArea>().Content;
            // 창은 위 모서리를 남색 제목 띠와 같은 반경(16)으로 둥글린 종이 한 장이다. 테두리 대신 얕은 그림자로 뜬다.
            Image panel = UIBuild.Panel("Panel", safe, UITheme.Surface,
                UIShapes.Corner.All, 16, UITheme.Outline, 0);
            panel.sprite = UIShapes.RoundedRect(16, UITheme.Surface);
            // 관리 화면은 공간을 넉넉히 쓰고, 확인/결과 같은 짧은 대화만 작은 창으로 남긴다.
            bool workspace = AlignToScreenFrame;
            UIBuild.Anchor(panel.rectTransform,
                AlignToScreenFrame
                    ? new Vector2(0f, workspace ? 0.04f : AnchorMin.y)
                    : AnchorMin,
                AlignToScreenFrame
                    ? new Vector2(1f, workspace ? 0.96f : AnchorMax.y)
                    : AnchorMax);
            if (AlignToScreenFrame) UIBuild.ScreenFrameHorizontal(panel.rectTransform);
            // 한 층 떠 있는 면 — 그림자를 깔고, 열릴 때 배경과 함께 떠오른다.
            // 장문 내용은 단색 읽기 면이다. 유리는 탐색 헤더에만 쓴다.
            UIBuild.Elevate(panel, 24, .14f, 6f);
            _root.AddComponent<UIPopIn>().Panel = panel.rectTransform;

            UIArtDirection.ReadingSurface(panel.transform, UIArtDirection.ScreenIcon(CanvasName), false);
            BuildHeader(panel.transform);

            // 띠 아래 2px 선이 본문 경계를 맡는다. 예전의 별도 구분선은 선이 두 겹으로 보여 걷었다.
            Image rule = UIBuild.Divider("TitleRule", panel.transform);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 1f), Vector2.one);
            rule.rectTransform.offsetMin = new Vector2(UITheme.PanelPad, -64f);
            rule.rectTransform.offsetMax = new Vector2(-UITheme.PanelPad, -63f);
            rule.gameObject.SetActive(false);

            Body = UIBuild.Container("Body", panel.transform);
            UIBuild.Stretch(Body, UITheme.PanelPad, UITheme.PanelPad);
            Body.offsetMax = new Vector2(-UITheme.PanelPad, -76f);

            Build();
            _root.SetActive(false);
        }

        /// <summary>
        /// 남색 제목 띠에 기능 기호 · 제목 · 원형 닫기를 한 중심선(띠 위에서 32px)에 정렬한다.
        /// 기호 24px · 제목 46px 상자 · 닫기 40px 원의 세로 중심이 모두 같다.
        /// </summary>
        private void BuildHeader(Transform panel)
        {
            UIArtDirection.Header(panel, UIArtDirection.HeaderHeight);
            UIArtDirection.TitleGlyph(panel, UIArtDirection.ScreenIcon(CanvasName), UITheme.PanelPad);

            if (!string.IsNullOrEmpty(Caption))
            {
                TextMeshProUGUI caption = UIBuild.Label("Caption", panel, Caption,
                    UITheme.FontMicro, UITheme.TextMuted);
                caption.gameObject.SetActive(false); // 과도한 마이크로 장식은 제거한다.
                UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 13f),
                    new Vector2(UITheme.PanelPad + 18f, -15f));
            }

            _title = UIArtDirection.HeaderTitle(panel, "Title", Title, UITheme.PanelPad + 38f, 900f);
            if (CloseOnBackdrop) UIArtDirection.HeaderClose("HeaderClose", panel, Hide, UITheme.PanelPad - 6f);
        }

        /// <summary>제목 위에 얹는 라틴 대문자 라벨. 없으면 생략된다.</summary>
        protected virtual string Caption => null;

        protected void SetTitle(string text)
        {
            if (_title != null) _title.text = text;
        }

        public virtual void Show()
        {
            EnsureBuilt();
            _root.SetActive(true);
            Open.Remove(this);
            Open.Add(this);
        }

        public virtual void Hide()
        {
            if (_root != null) _root.SetActive(false);
            Open.Remove(this);
        }

        /// <summary>
        /// ESC를 받았을 때 할 일. 닫았으면 true.
        ///
        /// 기본은 <b>배경을 눌러 닫히는 화면이면 ESC로도 닫힌다</b>는 규칙이다. 두 입력이 같은 뜻이어야
        /// 한다. 진행을 막는 화면(보상 · 사건 · 전투 결과)은 false를 돌려주고, 그러면 ESC는
        /// 평소처럼 메뉴를 연다. 별도 취소 흐름이 필요한 화면은 이것을 덮어쓴다.
        /// </summary>
        protected virtual bool OnEscape()
        {
            if (!CloseOnBackdrop) return false;
            Hide();
            return true;
        }

        /// <summary>
        /// 가장 위(정렬 순서, 같으면 나중에 연 것)에 떠 있는 모달에 ESC를 넘긴다.
        /// <paramref name="minSortingOrder"/>보다 아래 층의 모달은 건드리지 않는다 —
        /// TAB 캐릭터 창 뒤에 깔린 상점이 ESC에 닫히면 무엇이 닫혔는지 보이지 않는다.
        /// </summary>
        public static bool TryEscapeTop(int minSortingOrder = int.MinValue)
        {
            Open.RemoveAll(screen => screen == null || !screen.IsVisible);

            ModalScreen top = null;
            foreach (ModalScreen screen in Open)
            {
                if (screen.SortingOrder < minSortingOrder) continue;
                if (top == null || screen.SortingOrder >= top.SortingOrder) top = screen;
            }

            return top != null && top.OnEscape();
        }
    }
}
