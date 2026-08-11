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

            _title = UIBuild.Label("Title", panel.transform, Title, UITheme.FontTitle,
                UITheme.TextPrimary);
            UIBuild.Anchor(_title.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 1f),
                UITheme.PanelPad, 10f);

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
