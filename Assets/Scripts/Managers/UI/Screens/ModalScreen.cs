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

            Image panel = UIBuild.Panel("Panel", _root.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            UIBuild.Anchor(panel.rectTransform, AnchorMin, AnchorMax);

            BuildHeader(panel.transform);

            Image rule = UIBuild.Divider("TitleRule", panel.transform);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.88f),
                UITheme.PanelPad, 0f);
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 1f);

            Body = UIBuild.Container("Body", panel.transform);
            UIBuild.Anchor(Body, new Vector2(0f, 0f), new Vector2(1f, 0.88f),
                UITheme.PanelPad, UITheme.PanelPad);

            Build();
            _root.SetActive(false);
        }

        /// <summary>
        /// 모든 모달이 같은 머리를 쓴다.
        ///   [앰버 세로 막대] [라틴 대문자 마이크로 라벨]
        ///                    [한글 제목]        [우측 사선 빗금]
        ///
        /// 제목만 덩그러니 놓던 예전 머리와 달리, 어디를 보라는 신호(앰버 막대)와
        /// 화면의 성격(라틴 라벨)이 함께 붙는다. 명일방주식 정보 위계다.
        /// </summary>
        private void BuildHeader(Transform panel)
        {
            // 오른쪽 위 장식. 면을 채우지 않으면서 머리와 몸통을 갈라 준다.
            Image stripes = UIBuild.Solid("HeaderStripes", panel, Color.white);
            stripes.sprite = UIShapes.DiagonalStripes(
                12, new Color(1f, 1f, 1f, 0.030f), new Color(1f, 1f, 1f, 0f));
            stripes.type = Image.Type.Tiled;
            stripes.color = Color.white;
            UIBuild.Anchor(stripes.rectTransform, new Vector2(0.62f, 0.88f), new Vector2(1f, 1f));
            stripes.raycastTarget = false;

            Image bar = UIBuild.Solid("HeaderBar", panel, UITheme.Accent);
            UIBuild.Pin(bar.rectTransform, new Vector2(0f, 1f), new Vector2(3f, 26f),
                new Vector2(UITheme.PanelPad, -22f));

            if (!string.IsNullOrEmpty(Caption))
            {
                TextMeshProUGUI caption = UIBuild.Label("Caption", panel, Caption,
                    UITheme.FontMicro, UITheme.TextMuted);
                caption.characterSpacing = 20f;
                UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 13f),
                    new Vector2(UITheme.PanelPad + 12f, -18f));
            }

            _title = UIBuild.Label("Title", panel, Title, UITheme.FontTitle, UITheme.TextPrimary);
            _title.characterSpacing = 5f;
            UIBuild.Pin(_title.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 30f),
                new Vector2(UITheme.PanelPad + 12f, -34f));
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
        }

        public virtual void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
