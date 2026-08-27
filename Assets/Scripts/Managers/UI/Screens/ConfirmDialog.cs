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

        /// <summary>준비 페이즈 패널(40)이나 다른 모달(70대)보다 위에 떠야 한다.</summary>
        protected override int SortingOrder => 90;

        protected override string Title => "확인";
        protected override string Caption => "CONFIRM";
        protected override Vector2 AnchorMin => new(0.30f, 0.36f);
        protected override Vector2 AnchorMax => new(0.70f, 0.64f);

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

            const float width = 150f;
            const float gap = 12f;

            _confirm = UIBuild.Button("Confirm", Body, "확인", Accept, primary: true);
            UIBuild.Pin(_confirm.image.rectTransform, new Vector2(1f, 0f), new Vector2(width, 46f),
                new Vector2(-width * 0.5f, 24f));

            _cancel = UIBuild.Button("Cancel", Body, "취소", Hide);
            UIBuild.Pin(_cancel.image.rectTransform, new Vector2(1f, 0f), new Vector2(width, 46f),
                new Vector2(-width * 1.5f - gap, 24f));
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
    }
}
