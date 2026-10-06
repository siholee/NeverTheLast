using System.Linq;
using BaseClasses;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 파티 공용 보급과 코드 도구. <b>한 파티가 함께 쓰는 물건</b>이라 누구의 장비 칸도, 중량도 없다.
    ///
    /// 전투 비약과 승급의 인장은 <b>여기서 쓴다</b>. 코드는 Tab에서 무료 드래그로 옮긴다(Detail_17 §6).
    /// 카드를 누르면 준비 페이즈에서만 동작하고, 코드 도구는 보상 화면과 같은 선택 흐름(<see cref="RewardCodeFlow"/>)이
    /// 보급 목록을 대신해 열린다. 흐름이 끝나거나 취소되면 목록이 다시 그려진다.
    /// 예전 4×3 고정 격자는 열세 번째 종류부터 잘렸다. 지금은 같은 리듬의 격자를 세로로 굴린다.
    /// </summary>
    public sealed class InventoryScreen : ModalScreen
    {
        private const int Columns = 4;

        /// <summary>카드 한 장의 높이(기준 해상도 px). 960×540에서도 이름 · 효과 · 사용 줄이 들어가는 높이다.</summary>
        private const float CardHeight = 300f;

        protected override string CanvasName => "InventoryCanvas";
        protected override int SortingOrder => 73;
        protected override string Title => "공용 보급";
        protected override string Caption => "PARTY SUPPLY";
        protected override Vector2 AnchorMin => new(0.12f, 0.10f);
        protected override Vector2 AnchorMax => new(0.88f, 0.90f);
        protected override bool CloseOnBackdrop => true;

        private RectTransform _list;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _guide;
        private TextMeshProUGUI _notice;
        private Button _close;
        private RewardCodeFlow _codeFlow;

        /// <summary>마지막 사용 결과. 목록을 다시 그려도 남아야 "왜 안 됐는지"가 읽힌다.</summary>
        private string _noticeText = "";

        internal const string GuideText =
            "파티 전체가 함께 쓰는 보급품입니다. 전투 비약은 한 전투에 1개, 승급의 인장은 준비 중 사용. 코드는 Tab에서 무료로 드래그 이동합니다. " +
            "식량은 긴 휴식이 알아서 가져갑니다.";

        internal const string EmptyText =
            "공용 보급이 비어 있습니다.\n전투 보상의 소모품 · 적 처치 드랍과 상점의 '공용 보급' 탭(식량 · 정화의 성수)에서 채울 수 있습니다.";

        protected override void Build()
        {
            _summary = UIBuild.Text("Summary", Body, "", UITheme.FontReadBody, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_summary.rectTransform, new Vector2(0f, 0.935f), Vector2.one);

            _guide = UIBuild.Text("Guide", Body, GuideText, UITheme.FontReadSmall, UITheme.TextSecondary,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_guide.rectTransform, new Vector2(0f, 0.85f), new Vector2(1f, 0.935f), 12f, 0f);

            // 사용 결과 · 사유 줄. 요약 줄에 섞으면 길어져 두 줄로 꺾이므로 아래에 따로 둔다.
            _notice = UIBuild.Text("Notice", Body, "", UITheme.FontReadSmall, UITheme.Accent,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_notice.rectTransform, new Vector2(0f, 0.79f), new Vector2(1f, 0.85f), 12f, 0f);

            _list = UIBuild.Container("Items", Body);
            UIBuild.Anchor(_list, new Vector2(0.01f, 0.10f), new Vector2(0.99f, 0.785f));

            _close = UIBuild.Button("Close", Body, "닫기", Hide, primary: true, fontSize: UITheme.FontReadSmall);
            UIBuild.Anchor(_close.image.rectTransform, new Vector2(0.40f, 0.005f), new Vector2(0.60f, 0.085f));

            _codeFlow = new RewardCodeFlow(Body, ShowItems, SetTitle, "공용 보급으로 돌아가기");
        }

        public override void Show()
        {
            base.Show();
            _noticeText = "";
            ShowItems();
        }

        /// <summary>코드 흐름이 끝났거나 취소됐을 때 돌아오는 자리. 목록과 위아래 줄을 되살린다.</summary>
        private void ShowItems()
        {
            SetTitle(Title);
            _codeFlow?.Hide();
            SetChromeVisible(true);
            Refresh();
        }

        /// <summary>선택 판은 Body 전체를 덮으므로 흐름 중에는 겹치는 줄을 숨긴다.</summary>
        private void SetChromeVisible(bool visible)
        {
            _list.gameObject.SetActive(visible);
            _summary.gameObject.SetActive(visible);
            _guide.gameObject.SetActive(visible);
            _notice.gameObject.SetActive(visible);
            _close.gameObject.SetActive(visible);
        }

        private static bool InPreparation => GameManager.Instance?.gameState == BaseEnums.GameState.Preparation;

        private static bool IsUsable(ItemData item) => item != null && (item.IsTonic || item.IsCodeTool);

        private void Refresh()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) _list.GetChild(i).gameObject.SetActive(false);
            UIBuild.Clear(_list);
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            var stacks = inventory?.SharedStacks().ToList() ?? new();

            string summary = $"식량 {inventory?.FoodUnits ?? 0} / {GameManager.LongRestFoodCost}   ·   " +
                             $"보급품 {stacks.Count}종 {inventory?.ItemIdsInHand?.Count ?? 0}개   ·   골드 {inventory?.Gold ?? 0:N0}";
            // 써 둔 비약은 목록에서 이미 빠졌으므로 여기서 보이지 않으면 "썼는지"를 알 길이 없다.
            int pending = GameManager.Instance?.PendingTonicItemId ?? 0;
            if (pending > 0)
            {
                string tonic = inventory?.Find(pending)?.name ?? $"비약 {pending}";
                summary += $"   ·   <color=#{ColorUtility.ToHtmlStringRGB(UITheme.Positive)}>다음 전투 비약: {tonic}</color>";
            }

            _summary.richText = true;
            _summary.text = summary;
            _notice.text = _noticeText;

            if (stacks.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _list, EmptyText, UITheme.FontReadBody, UITheme.TextMuted,
                    TextAlignmentOptions.Center, wrap: true);
                UIBuild.Stretch(empty.rectTransform, 40f, 0f);
                UIScaledText.KeepReadable(Body);
                return;
            }

            RectTransform content = UIBuild.ScrollArea("ItemScroll", _list, out ScrollRect scroll);
            UIBuild.Stretch((RectTransform)scroll.transform);
            float width = 1f / Columns;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemData item = stacks[i].item;
                int count = stacks[i].count;
                int column = i % Columns;
                int row = i / Columns;
                bool usable = IsUsable(item);

                Image card = UIBuild.Panel($"Item_{item.id}", content, usable ? UITheme.SurfaceRaised : UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 8, usable && InPreparation ? UITheme.Accent : UITheme.Outline, 1);
                card.rectTransform.anchorMin = new Vector2(column * width, 1f);
                card.rectTransform.anchorMax = new Vector2((column + 1) * width, 1f);
                card.rectTransform.pivot = new Vector2(0.5f, 1f);
                card.rectTransform.offsetMin = new Vector2(8f, -(row + 1) * CardHeight + 8f);
                card.rectTransform.offsetMax = new Vector2(-14f, -row * CardHeight);

                Image strip = UIBuild.Solid("Rarity", card.transform, UITheme.Rarity(Mathf.Max(1, item.rarity)));
                strip.raycastTarget = false;
                UIBuild.Anchor(strip.rectTransform, new Vector2(0f, 0.975f), Vector2.one, 8f, 0f);

                Sprite art = ItemTooltip.LoadArt(item);
                if (art != null)
                {
                    var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artObject.transform.SetParent(card.transform, false);
                    Image image = artObject.GetComponent<Image>();
                    image.sprite = art;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    UIBuild.Anchor(image.rectTransform, new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.95f));
                }
                else
                {
                    TextMeshProUGUI fallback = UIBuild.Text("FallbackArt", card.transform,
                        item.IsFood ? "FOOD" : item.IsTonic ? "TONIC" : item.IsCodeTool ? "CODE TOOL" : "ITEM",
                        UITheme.FontReadHeading, UITheme.Accent, TextAlignmentOptions.Center);
                    fallback.characterSpacing = 12f;
                    UIBuild.Anchor(fallback.rectTransform, new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.92f));
                }

                TextMeshProUGUI name = UIBuild.Text("Name", card.transform, item.name,
                    UITheme.FontReadBody, UITheme.TextPrimary, TextAlignmentOptions.Center);
                name.fontStyle = FontStyles.Bold;
                name.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(name.rectTransform, new Vector2(0.03f, 0.51f), new Vector2(0.97f, 0.63f));

                TextMeshProUGUI effect = UIBuild.Text("Effect", card.transform, ItemTooltip.EffectLine(item),
                    UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.Top, wrap: true);
                effect.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(effect.rectTransform, new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.51f));

                TextMeshProUGUI meta = UIBuild.Text("Meta", card.transform, MetaLine(item, count),
                    UITheme.FontReadSmall, usable && InPreparation ? UITheme.AccentDim : UITheme.TextMuted,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(meta.rectTransform, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.17f));
                ItemTooltip.Attach(card.gameObject, item);

                if (usable)
                {
                    ItemData captured = item;
                    UIBuild.OnClick(card.gameObject, () => OnUse(captured));
                }
            }

            int rows = (stacks.Count + Columns - 1) / Columns;
            content.sizeDelta = new Vector2(0f, rows * CardHeight + 8f);
            // 이 창의 22 이상 글자에만 가독 하한을 건다(다른 화면의 자동 맞춤 규칙은 그대로).
            UIScaledText.KeepReadable(Body);
        }

        /// <summary>비약 · 코드 도구는 눌러서 쓴다는 것을 카드 위에서 알린다. 준비 밖에서는 이유를 대신 적는다.</summary>
        private static string MetaLine(ItemData item, int count)
        {
            if (item.IsFood) return $"×{count}   ·   긴 휴식에 사용";
            if (IsUsable(item)) return InPreparation ? $"×{count}   ·   눌러서 사용" : $"×{count}   ·   준비 중에 사용";
            return $"×{count}";
        }

        // ── 사용 ─────────────────────────────────────────────────────

        private void OnUse(ItemData item)
        {
            GameManager game = GameManager.Instance;
            if (game == null || !IsUsable(item)) return;

            if (!InPreparation)
            {
                Notify($"{item.name} — 준비 중에만 쓸 수 있습니다.");
                return;
            }

            if (item.IsTonic)
            {
                bool used = game.TryUseTonic(item.id, out string reason);
                Notify(used ? $"{item.name} — 다음 전투에 적용됩니다." : $"{item.name} — {reason}");
                return;
            }

            if (item.IsSeal)
            {
                SetChromeVisible(false);
                bool opened = _codeFlow.StartSeal(item, (unit, fromId, toId) =>
                {
                    bool done = game.UseSeal(item.id, unit, fromId, toId);
                    _noticeText = done
                        ? $"{item.name} — {unit.UnitName}의 {RewardCodeFlow.NameOf(fromId)}을(를) {RewardCodeFlow.NameOf(toId)}(으)로 올렸습니다."
                        : $"{item.name} — 쓸 수 없어 그대로 두었습니다.";
                    ShowItems();
                });
                if (!opened) Notify($"{item.name} — 올릴 수 있는 코드가 없습니다. (한 단계 위 코드가 있어야 하고, 인장의 최대 티어 T{item.sealMaxTier} 이하여야 합니다)");
                return;
            }

            if (item.transferScroll)
            {
                Hide();
                game.uiManager?.ShowCodex();
                return; // 구형 저장의 두루마리는 쓰지 않는다. 드래그 이동으로 대체됐다.
            }

            Notify($"{item.name} — 아직 쓸 수 없는 도구입니다.");
        }

        /// <summary>사용 결과나 막힌 사유를 남기고 목록을 되살린다. 흐름을 열지 못한 경우도 여기로 온다.</summary>
        private void Notify(string message)
        {
            _noticeText = message;
            ShowItems();
        }
    }
}
