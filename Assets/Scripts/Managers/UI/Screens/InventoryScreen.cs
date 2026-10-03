using System.Linq;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>모든 아이템을 한곳에서 보여 주는 파티 공용 인벤토리.</summary>
    public sealed class InventoryScreen : ModalScreen
    {
        protected override string CanvasName => "InventoryCanvas";
        protected override int SortingOrder => 73;
        protected override string Title => "공용 인벤토리";
        protected override string Caption => "PARTY INVENTORY";
        protected override Vector2 AnchorMin => new(0.14f, 0.14f);
        protected override Vector2 AnchorMax => new(0.86f, 0.86f);
        protected override bool CloseOnBackdrop => true;

        private RectTransform _list;
        private TextMeshProUGUI _summary;

        protected override void Build()
        {
            _summary = UIBuild.Text("Summary", Body, "", UITheme.FontBody, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_summary.rectTransform, new Vector2(0f, 0.90f), Vector2.one);

            _list = UIBuild.Container("Items", Body);
            UIBuild.Anchor(_list, new Vector2(0.02f, 0.12f), new Vector2(0.98f, 0.88f));

            Button close = UIBuild.Button("Close", Body, "닫기", Hide, primary: true);
            UIBuild.Anchor(close.image.rectTransform, new Vector2(0.40f, 0.01f), new Vector2(0.60f, 0.09f));
        }

        public override void Show()
        {
            base.Show();
            Refresh();
        }

        private void Refresh()
        {
            UIBuild.Clear(_list);
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            var stacks = inventory?.SharedStacks().ToList() ?? new();
            _summary.text = $"식량 {inventory?.FoodUnits ?? 0} / {GameManager.LongRestFoodCost}   ·   " +
                            $"아이템 {inventory?.ItemIdsInHand?.Count ?? 0}개   ·   골드 {inventory?.Gold ?? 0:N0}";

            if (stacks.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _list,
                    "공용 인벤토리가 비어 있습니다.", UITheme.FontBody, UITheme.TextMuted,
                    TextAlignmentOptions.Center);
                UIBuild.Stretch(empty.rectTransform);
                return;
            }

            const int columns = 4;
            // 한 줄뿐이어도 카드가 화면 높이를 통째로 먹지 않는다. 4열 × 3행을 고정 리듬으로 쓰고
            // 아이템이 적은 초반에는 위에서부터 차분히 채운다.
            const int visibleRows = 3;
            float height = 1f / visibleRows;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemData item = stacks[i].item;
                int count = stacks[i].count;
                int column = i % columns;
                int row = i / columns;
                float width = 1f / columns;
                float top = 1f - row * height;

                Image card = UIBuild.Panel($"Item_{item.id}", _list, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
                UIBuild.Anchor(card.rectTransform,
                    new Vector2(column * width, top - height), new Vector2((column + 1) * width, top), 8f, 8f);

                Sprite art = ItemTooltip.LoadArt(item);
                if (art != null)
                {
                    var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artObject.transform.SetParent(card.transform, false);
                    Image image = artObject.GetComponent<Image>();
                    image.sprite = art;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    UIBuild.Anchor(image.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.90f));
                }
                else
                {
                    TextMeshProUGUI fallback = UIBuild.Text("FallbackArt", card.transform,
                        item.IsFood ? "CAMP\nSUPPLY" : "ITEM", UITheme.FontHeading, UITheme.Accent,
                        TextAlignmentOptions.Center);
                    fallback.characterSpacing = 12f;
                    UIBuild.Anchor(fallback.rectTransform, new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.88f));
                }

                TextMeshProUGUI name = UIBuild.Text("Name", card.transform, item.name,
                    UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(name.rectTransform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.36f));
                TextMeshProUGUI meta = UIBuild.Text("Meta", card.transform,
                    item.IsFood ? $"×{count}   ·   식량 +{item.foodValue * count}" : $"×{count}",
                    UITheme.FontCaption, UITheme.Accent, TextAlignmentOptions.Center);
                UIBuild.Anchor(meta.rectTransform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.18f));
                ItemTooltip.Attach(card.gameObject, item, null);
            }
        }
    }
}
