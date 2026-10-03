using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>짧은 휴식과 식량을 쓰는 긴 휴식을 나란히 비교한다.</summary>
    public sealed class RestScreen : ModalScreen
    {
        protected override string CanvasName => "RestCanvas";
        protected override int SortingOrder => 74;
        protected override string Title => "휴식";
        protected override string Caption => "CAMP";
        protected override Vector2 AnchorMin => new(0.20f, 0.22f);
        protected override Vector2 AnchorMax => new(0.80f, 0.78f);
        protected override bool CloseOnBackdrop => true;

        private TextMeshProUGUI _shortMeta;
        private TextMeshProUGUI _longMeta;
        private TextMeshProUGUI _message;
        private Button _shortButton;
        private Button _longButton;

        protected override void Build()
        {
            BuildChoice("ShortRest", "짧은 휴식", "SHORT REST",
                "살아 있는 파티원의 최대 체력 50% 회복\n긴 휴식 사이 최대 2회",
                0.02f, 0.49f, out _shortMeta, out _shortButton, UseShortRest);
            BuildChoice("LongRest", "긴 휴식", "LONG REST",
                "쓰러진 아군 부활 · 체력과 마나 완전 회복\n짧은 휴식 횟수 복구",
                0.51f, 0.98f, out _longMeta, out _longButton, UseLongRest);

            _message = UIBuild.Text("Message", Body, "", UITheme.FontCaption, UITheme.Danger,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_message.rectTransform, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.12f));
        }

        private void BuildChoice(string key, string title, string caption, string body,
            float left, float right, out TextMeshProUGUI meta, out Button button, System.Action action)
        {
            Image panel = UIBuild.Panel(key, Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            UIBuild.Anchor(panel.rectTransform, new Vector2(left, 0.15f), new Vector2(right, 0.96f), 6f, 0f);

            TextMeshProUGUI cap = UIBuild.Label("Caption", panel.transform, caption,
                UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.Center);
            cap.characterSpacing = 18f;
            UIBuild.Anchor(cap.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.92f));

            TextMeshProUGUI name = UIBuild.Text("Title", panel.transform, title, UITheme.FontTitle,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(name.rectTransform, new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.82f));

            TextMeshProUGUI description = UIBuild.Text("Description", panel.transform, body,
                UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(description.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.63f));

            meta = UIBuild.Text("Meta", panel.transform, "", UITheme.FontHeading,
                UITheme.Accent, TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(meta.rectTransform, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.35f));

            button = UIBuild.Button("Use", panel.transform, "휴식하기", action, primary: true);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(0.16f, 0.04f), new Vector2(0.84f, 0.18f));
        }

        public override void Show()
        {
            base.Show();
            Refresh();
        }

        private void Refresh()
        {
            GameManager game = GameManager.Instance;
            int charges = game?.ShortRestCharges ?? 0;
            int food = game?.inventoryManager?.FoodUnits ?? 0;
            _shortMeta.text = $"남은 횟수  {charges} / {GameManager.MaxShortRestCharges}";
            _longMeta.text = $"식량  {food} / {GameManager.LongRestFoodCost}";
            _shortButton.interactable = game?.CanRest == true && charges > 0;
            _longButton.interactable = game?.CanRest == true && food >= GameManager.LongRestFoodCost;
        }

        private void UseShortRest()
        {
            if (GameManager.Instance?.TryShortRest() == true) return;
            _message.text = "짧은 휴식 횟수가 남아 있지 않습니다.";
            Refresh();
        }

        private void UseLongRest()
        {
            if (GameManager.Instance?.TryLongRest() == true) return;
            _message.text = $"긴 휴식에는 식량 {GameManager.LongRestFoodCost}이 필요합니다.";
            Refresh();
        }
    }
}
