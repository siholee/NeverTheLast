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
    /// 암전 배경 + 가운데 컷코너 패널 + 제목을 제공하고,
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

        /// <summary>내용을 채울 영역. 제목 아래 전체를 차지한다.</summary>
        protected RectTransform Body { get; private set; }

        protected abstract string CanvasName { get; }
        protected abstract int SortingOrder { get; }
        protected abstract string Title { get; }

        /// <summary>패널이 차지하는 화면 비율. 화면마다 다르게 잡는다.</summary>
        protected virtual Vector2 AnchorMin => new(0.16f, 0.14f);
        protected virtual Vector2 AnchorMax => new(0.84f, 0.86f);

        /// <summary>배경을 눌러 닫을 수 있는지. 진행을 막아야 하는 화면은 false.</summary>
        protected virtual bool CloseOnBackdrop => false;

        public bool IsVisible => _root != null && _root.activeSelf;

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
            if (CloseOnBackdrop) UIBuild.OnClick(backdrop.gameObject, Hide);

            RectTransform safe = _root.AddComponent<UISafeArea>().Content;
            Image panel = UIBuild.Panel("Panel", safe, UITheme.Surface,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            // 관리 화면은 공간을 넉넉히 쓰고, 확인/결과 같은 짧은 대화만 작은 창으로 남긴다.
            bool workspace = AnchorMax.x - AnchorMin.x >= 0.60f;
            UIBuild.Anchor(panel.rectTransform,
                workspace ? new Vector2(0.025f, 0.025f) : AnchorMin,
                workspace ? new Vector2(0.975f, 0.975f) : AnchorMax);
            // 한 층 떠 있는 면 — 그림자를 깔고, 열릴 때 배경과 함께 떠오른다.
            UIBuild.Elevate(panel, 40, 0.6f, 14f);
            _root.AddComponent<UIPopIn>().Panel = panel.rectTransform;

            BuildHeader(panel.transform);

            Image rule = UIBuild.Divider("TitleRule", panel.transform);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 1f), Vector2.one);
            rule.rectTransform.offsetMin = new Vector2(UITheme.PanelPad, -88f);
            rule.rectTransform.offsetMax = new Vector2(-UITheme.PanelPad, -87f);

            Body = UIBuild.Container("Body", panel.transform);
            UIBuild.Stretch(Body, UITheme.PanelPad, UITheme.PanelPad);
            Body.offsetMax = new Vector2(-UITheme.PanelPad, -100f);

            Build();
            _root.SetActive(false);
        }

        /// <summary>
        /// 모든 모달이 같은 머리를 쓴다.
        ///   [짧은 틸 기준선] [라틴 대문자 마이크로 라벨]
        ///   [한글 제목]                         [옅은 틸 빛 번짐]
        ///
        /// 제목만 덩그러니 놓던 예전 머리와 달리, 어디를 보라는 신호(틸 기준선)와
        /// 화면의 성격(라틴 라벨)이 함께 붙는다. Porcelain Aurora의 공통 정보 위계다.
        /// </summary>
        private void BuildHeader(Transform panel)
        {
            // 오른쪽 위의 옅은 빛 번짐. 예전의 사선 빗금은 둥근 면과 어울리지 않고 글자 뒤에서 결이 떴다.
            // Radiant의 흐린 그라데이션처럼 면 모서리에만 차가운 빛을 준다.
            // 빛의 중심은 면 바깥 모서리에 두고, 면 안쪽으로 번진 부분만 보이게 잘라 낸다.
            RectTransform glowClip = UIBuild.Container("HeaderGlowClip", panel);
            // 작은 확인 창에서도 색 띠처럼 본문을 덮지 않도록, 우상단 38% 안에서만 번지게 한다.
            UIBuild.Anchor(glowClip, new Vector2(0.62f, 0.72f), new Vector2(1f, 1f), 2f, 2f);
            glowClip.gameObject.AddComponent<RectMask2D>();

            var glowObject = new GameObject("HeaderGlow", typeof(RectTransform), typeof(Image));
            glowObject.transform.SetParent(glowClip, false);
            var glow = glowObject.GetComponent<Image>();
            glow.sprite = UIShapes.Glow(128, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.08f));
            glow.raycastTarget = false;
            UIBuild.Pin(glow.rectTransform, new Vector2(1f, 1f), new Vector2(420f, 240f), new Vector2(150f, 110f));

            Image bar = UIBuild.Solid("HeaderBar", panel, UITheme.Accent);
            UIBuild.Pin(bar.rectTransform, new Vector2(0f, 1f), new Vector2(96f, 3f),
                new Vector2(UITheme.PanelPad, -18f));

            if (!string.IsNullOrEmpty(Caption))
            {
                TextMeshProUGUI caption = UIBuild.Label("Caption", panel, Caption,
                    UITheme.FontMicro, UITheme.TextMuted);
                caption.characterSpacing = 20f;
                UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 13f),
                    new Vector2(UITheme.PanelPad + 112f, -12f));
            }

            _title = UIBuild.Label("Title", panel, Title, UITheme.FontTitle, UITheme.TextPrimary);
            _title.characterSpacing = 5f;
            UIBuild.Pin(_title.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 30f),
                new Vector2(UITheme.PanelPad, -36f));
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
        /// 평소처럼 메뉴를 연다. 명시적인 취소가 있는 화면(훈련)은 이것을 덮어쓴다.
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
