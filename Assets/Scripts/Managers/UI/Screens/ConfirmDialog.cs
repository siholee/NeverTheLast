using System;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 한 가지만 되묻는 작은 모달. "이대로 진행할까요?" 수준의 확인에만 쓴다.
    ///
    /// 화면마다 팝업을 새로 만들지 않도록 하나를 만들어 돌려 쓴다.
    /// 진행을 막는 물음이라 배경을 눌러 닫히지 않는다 — 확인이든 취소든 눌러야 한다.
    /// </summary>
    public class ConfirmDialog : ModalScreen
    {
        private static ConfirmDialog _instance;

        protected override string CanvasName => "ConfirmCanvas";

        /// <summary>어떤 화면보다도 위 — ESC 메뉴의 불러오기·나가기도 이 창으로 되묻는다.</summary>
        protected override int SortingOrder => UITheme.LayerConfirm;

        protected override string Title => "확인";
        protected override string Caption => "CONFIRM";
        protected override Vector2 AnchorMin => new(0.27f, 0.32f);
        protected override Vector2 AnchorMax => new(0.73f, 0.68f);

        private TextMeshProUGUI _message;
        private Button _confirm;
        private Button _cancel;
        private Action _onConfirm;

        /// <summary>물음을 띄운다. 확인을 누르면 <paramref name="onConfirm"/>이 실행된다.</summary>
        public static void Ask(string title, string message, string confirmText, Action onConfirm)
        {
            _instance ??= new ConfirmDialog();
            _instance.Open(title, message, confirmText, onConfirm);
        }

        protected override void Build()
        {
            _message = UIBuild.Text("Message", Body, "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(_message.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 1f));

            const float width = 180f;
            const float gap = 12f;

            _confirm = UIBuild.Button("Confirm", Body, "확인", Accept, primary: true);
            UIBuild.Pin(_confirm.image.rectTransform, new Vector2(1f, 0f), new Vector2(width, 64f),
                new Vector2(-width * 0.5f, 34f));

            _cancel = UIBuild.Button("Cancel", Body, "취소", Hide);
            UIBuild.Pin(_cancel.image.rectTransform, new Vector2(1f, 0f), new Vector2(width, 64f),
                new Vector2(-width * 1.5f - gap, 34f));
        }

        private void Open(string title, string message, string confirmText, Action onConfirm)
        {
            EnsureBuilt();
            _onConfirm = onConfirm;
            SetTitle(string.IsNullOrWhiteSpace(title) ? "확인" : title);
            _message.text = message ?? "";

            TextMeshProUGUI label = _confirm.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = string.IsNullOrWhiteSpace(confirmText) ? "확인" : confirmText;

            Show();
        }

        private void Accept()
        {
            Action callback = _onConfirm;
            _onConfirm = null;
            Hide();
            callback?.Invoke();
        }

        public override void Hide()
        {
            _onConfirm = null;
            base.Hide();
        }

        /// <summary>ESC는 취소와 같다. 배경으로는 닫히지 않지만 키보드로 물러설 길은 있어야 한다.</summary>
        protected override bool OnEscape()
        {
            Hide();
            return true;
        }
    }
}
