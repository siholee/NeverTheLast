using Helpers;
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
        protected override Vector2 AnchorMin => new(0.08f, 0.12f);
        protected override Vector2 AnchorMax => new(0.92f, 0.88f);
        protected override bool CloseOnBackdrop => true;

        private TextMeshProUGUI _shortMeta;
        private TextMeshProUGUI _longMeta;
        private TextMeshProUGUI _message;
        private Button _shortButton;
        private Button _longButton;

        protected override void Build()
        {
            var host=UIBuild.Container("CampHost",Body);
            UIBuild.Anchor(host,Vector2.zero,new Vector2(.26f,1));
            Image standing=UIBuild.Solid("HimikoStanding",host,Color.white);
            standing.sprite=SpriteResource.LoadStanding("HIMIKO_STANDING");
            standing.enabled=standing.sprite!=null;
            standing.type=Image.Type.Simple; standing.preserveAspect=true; standing.raycastTarget=false;
            Image floor=UIBuild.Solid("HostFloor",host,Color.white);
            floor.sprite=UIShapes.Glow(128,new Color(UITheme.Accent.r,UITheme.Accent.g,UITheme.Accent.b,.15f));
            floor.type=Image.Type.Simple;floor.raycastTarget=false;
            UIBuild.Anchor(floor.rectTransform,new Vector2(.12f,.235f),new Vector2(.88f,.275f));
            floor.transform.SetAsFirstSibling();
            UIBuild.Anchor(standing.rectTransform,new Vector2(0,.24f),new Vector2(1,.98f));
            var name=UIBuild.Text("HostName",host,"히미코",26,UITheme.TextPrimary,TextAlignmentOptions.Midline);
            name.fontStyle=FontStyles.Bold;
            UIBuild.Anchor(name.rectTransform,new Vector2(0,.16f),new Vector2(1,.24f));
            var line=UIBuild.Text("HostLine",host,"잠시 쉬어가세요.\n다음 여정을 준비할 시간이에요.",20,UITheme.TextSecondary,TextAlignmentOptions.Midline,true);
            UIBuild.Anchor(line.rectTransform,new Vector2(0,.03f),new Vector2(1,.15f),12,0);
            BuildChoice("ShortRest", "짧은 휴식", "SHORT REST",
                "살아 있는 파티원의 최대 체력 50% 회복\n긴 휴식 사이 최대 2회",
                0.29f, 0.635f, out _shortMeta, out _shortButton, UseShortRest, UIIcon.Rest);
            BuildChoice("LongRest", "긴 휴식", "LONG REST",
                "쓰러진 아군 부활 · 체력과 마나 완전 회복\n짧은 휴식 횟수 복구",
                0.655f, 1f, out _longMeta, out _longButton, UseLongRest, UIIcon.Supply);

            _message = UIBuild.Text("Message", Body, "", UITheme.FontCaption, UITheme.Danger,
                TextAlignmentOptions.Midline, wrap: true);
            ScreenKit.BottomBand(_message.rectTransform, 0f, 30f, 40f, 40f);
        }

        /// <summary>
        /// 휴식 하나 = 종이 위 흰 카드 한 장. 위에서부터 기호 원 + 제목 / 효과 두 줄 / 남은 횟수 · 식량 / 주 행동.
        /// 두 카드의 같은 요소는 같은 높이에 선다(카드 위 끝에서 잰 px).
        /// </summary>
        private void BuildChoice(string key, string title, string caption, string body,
            float left, float right, out TextMeshProUGUI meta, out Button button, System.Action action, UIIcon glyph)
        {
            Image panel = UIBuild.Solid(key, Body, Color.white);
            panel.sprite = UIShapes.RoundedRect(10, Color.white);
            UIArtDirection.ReadingSurface(panel.transform, glyph);
            panel.type = Image.Type.Sliced;
            UIBuild.Anchor(panel.rectTransform, new Vector2(left, 0.0f), new Vector2(right, 1f), 0f, 0f);
            panel.rectTransform.offsetMin = new Vector2(0f, 36f);

            Image disc = UIArtDirection.Emblem("Glyph", panel.transform, glyph);
            UIBuild.Pin(disc.rectTransform, new Vector2(0f, 1f), new Vector2(56f, 56f), new Vector2(16f, -12f));

            TextMeshProUGUI cap = UIBuild.Label("Caption", panel.transform, caption,
                UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.Center);
            cap.gameObject.SetActive(false);
            UIBuild.Anchor(cap.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.92f));

            // 제목 상자(18~62)의 중심 40px = 기호 원(18~62)의 중심.
            TextMeshProUGUI name = UIBuild.Text("Title", panel.transform, title, 26f,
                UITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            name.fontStyle = FontStyles.Bold;
            PlaceTop(name.rectTransform, 44, 18, 80, 22);

            TextMeshProUGUI description = UIBuild.Text("Description", panel.transform, body,
                UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.MidlineLeft, wrap: true);
            PlaceTop(description.rectTransform,100,108,28,28);

            meta = UIBuild.Text("Meta", panel.transform, "", UITheme.FontHeading,
                UITheme.Accent, TextAlignmentOptions.MidlineLeft, wrap: true);
            meta.fontStyle = FontStyles.Bold;
            PlaceTop(meta.rectTransform,44,232,28,28);

            button = UIBuild.Button("Use", panel.transform, "휴식하기", action, primary: true, fontSize:22);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(0f, 0), new Vector2(1f, 0), 22f, 0f);
            button.image.rectTransform.sizeDelta = new Vector2(-44f, 48);
            button.image.rectTransform.pivot = new Vector2(.5f, 0f);
            button.image.rectTransform.anchoredPosition = new Vector2(0, 22);
        }

        private static void PlaceTop(RectTransform rect, float height, float top, float left, float right)
        {
            UIBuild.Anchor(rect, new Vector2(0, 1), Vector2.one);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }

        public override void Show()
        {
            base.Show();
            // 안내자 원화와 두 휴식 선택지를 넉넉한 높이에서 비교한다.
            FitContentHeight(720);
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
