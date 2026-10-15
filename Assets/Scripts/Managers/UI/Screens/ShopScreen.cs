using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseClasses;
using Codes.Base;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 준비 페이즈 상점. <b>매대가 아니라 사람과 거래한다.</b>
    ///
    /// 왼쪽에 행상인이 서고 오른쪽이 물건이다. 맨 위 줄이 지갑 · 시세 스테이지 · 남은 구매 횟수,
    /// 그 아래가 탭이다. 열면 <b>코드</b> 탭부터 보인다 — 상점에서 가장 오래 고민하는 물건이 보상 코드이기 때문이다.
    /// <list type="bullet">
    /// <item>코드 — 생존 · 제어 저항 같은 구멍 메우기 보상 코드(<c>shopFixed</c>) 세 장을 크게. 화력 코드는 팔지 않는다(Detail_17 §5.2)</item>
    /// <item>회복 · 성장 — 회복약 · 부활약 · 사탕. <c>90_rewards.yaml</c>을 그대로 쓴다</item>
    /// <item>공용 보급 — 식량과 정화의 성수. 공용 보급(인벤토리)에 들어간다</item>
    /// <item>판매 — 주운 귀중품을 골드로 바꾼다. 구매 한도를 쓰지 않는다</item>
    /// </list>
    /// 예전 매대는 고정 칸 격자라 진열이 칸 수를 넘으면 뒤쪽 물건에 손이 닿지 않았다(회복 · 성장 11종 중 뒤쪽). 지금은 목록이 굴러간다.
    ///
    /// 파는 물건과 그 효과는 보상 풀과 같은 데이터를 쓴다 — 상점에서 산 회복약과
    /// 보상으로 받은 회복약이 다르게 동작하면 안 되기 때문이다. 가격만 상점이 정하고
    /// (<see cref="RewardManager.ShopPrice"/>), 효과 적용은 <see cref="RewardManager.ApplyRewardEffect"/>가 그대로 처리한다.
    /// 대상이 필요한 물건은 대상(코드는 덮어쓸 칸까지)을 다 고른 뒤 마지막 확정에서만 값을 치르고,
    /// 그 순간 준비 페이즈 · 구매 횟수 · 골드 · 대상을 다시 확인한다.
    /// </summary>
    public class ShopScreen : ModalScreen
    {
        private const int SupplyOnDisplay = 6;

        /// <summary>목록 한 줄의 높이(기준 해상도 px). 이름 24 · 설명 22의 실제 줄 높이(약 34 + 31)와 위아래 여백이 들어간다.</summary>
        private const float RowHeight = 100f;

        /// <summary>탭 줄 · 안내 줄 · 바닥 줄의 높이(기준 해상도 px).</summary>
        private const float TabRowHeight = 44f;
        private const float GuideHeight = 56f;
        private const float FooterHeight = 48f;

        /// <summary>제목 띠 안 지갑 줄의 오른쪽 끝(닫기 원 40px + 간격을 비운다).</summary>
        private const float HeaderWalletRight = 86f;

        /// <summary>코드 카드 안 여백과 바닥 행(가격 · 구매) 높이.</summary>
        private const float CardPad = 16f;
        private const float BuyHeight = 44f;

        /// <summary>행상인 자산 키. 아직 그림이 없으면 이름표만 선다.</summary>
        private const string MerchantStanding = "LAVOISIER_STANDING";
        private const string MerchantName = "라부아지에";

        // 값은 저장 · 검증이 숫자로 가리키므로 순서를 바꾸지 않는다. 화면에 늘어선 순서는 TabOrder가 정한다.
        private enum Tab
        {
            Consumable,
            Supply,
            Code,
            Sell,
        }

        private static readonly (Tab Tab, string Label)[] TabOrder =
        {
            (Tab.Code, "코드"),
            (Tab.Consumable, "회복 · 성장"),
            (Tab.Supply, "공용 보급"),
            (Tab.Sell, "판매"),
        };

        protected override Color BackdropTint => new Color(.77f,.84f,.92f,.98f);
        protected override string CanvasName => "ShopCanvas";
        protected override int SortingOrder => 72;
        protected override bool AlignToScreenFrame => false;
        protected override string Title => "상점";
        protected override string Caption => "SHOP";
        protected override Vector2 AnchorMin => new(0.03f, 0.04f);
        protected override Vector2 AnchorMax => new(0.97f, 0.96f);
        protected override bool CloseOnBackdrop => true;

        private readonly Dictionary<Tab, Button> _tabButtons = new();

        private List<RewardDef> _consumables = new();
        private List<RewardDef> _supplies = new();
        private List<RewardDef> _codes = new();
        private int _supplyStage = -1;

        private Tab _tab = Tab.Code;

        private RectTransform _merchant;
        private RectTransform _goodsRoot;
        private RectTransform _content;
        private TextMeshProUGUI _wallet;
        private TextMeshProUGUI _stageInfo;
        private TextMeshProUGUI _purchases;
        private TextMeshProUGUI _guide;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _merchantLine;
        private Image _merchantArt;
        private Image _coin;
        private Button _sellAll;
        private UnitTargetPicker _targetPicker;
        private RewardCodeFlow _codeFlow;
        private RewardDef _pendingGood;

        /// <summary>검증용 — 지금 보이는 탭 이름(enum 이름).</summary>
        internal string ActiveTabName => _tab.ToString();

        private static int CurrentStage => Mathf.Max(1, GameManager.Instance?.RoundManager?.Stage ?? 1);

        private static InventoryManager Inventory => GameManager.Instance?.inventoryManager;

        protected override void Build()
        {
            BuildMerchant();

            _goodsRoot = UIBuild.Container("ShopGoods", Body);
            UIBuild.Anchor(_goodsRoot, new Vector2(0.195f, 0f), Vector2.one);

            BuildTabs();
            BuildStatusBar();

            // 탭 줄 아래 한 줄 — 왼쪽은 탭 안내(최대 두 줄), 오른쪽은 방금 한 거래의 결과. 두 줄 18px의 실제 줄 높이(약 26px × 2)를 다 받는다.
            _guide = UIBuild.Text("TabGuide", _goodsRoot, "", UITheme.FontBody, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineLeft, wrap: true);
            ScreenKit.TopBand(_guide.rectTransform, TabRowHeight + 10f, GuideHeight, 4f, 0f);
            _guide.rectTransform.anchorMax = new Vector2(0.60f, 1f);

            // 매대는 안내 줄 아래에서 창 바닥까지 쓴다. 예전에는 바닥에 결과 한 줄(48+12px)을 늘 비워 두어
            // 거래 전에는 카드 아래가 빈 띠로 남았다(2026-10-08 1920 검수). 결과는 위 안내 줄 오른쪽으로 옮겼다.
            _content = UIBuild.Container("Content", _goodsRoot);
            UIBuild.Stretch(_content);
            _content.offsetMin = new Vector2(0f, 0f);
            _content.offsetMax = new Vector2(0f, -(TabRowHeight + GuideHeight + 22f));

            // 거래 결과 — 안내 줄 오른쪽 끝선에 군청 굵게. 닫기는 머리의 X · ESC · 바깥 누르기로 충분하다(D10).
            _message = UIBuild.Text("Message", _goodsRoot, "", UITheme.FontReadSmall, UITheme.Accent,
                TextAlignmentOptions.MidlineRight, wrap: true);
            ScreenKit.TopBand(_message.rectTransform, TabRowHeight + 10f, GuideHeight, 0f, 4f);
            _message.rectTransform.anchorMin = new Vector2(0.62f, 1f);
            _message.fontStyle = FontStyles.Bold;

            // 판매 탭에서만 — 매대 아래 오른쪽. 이때만 매대 바닥을 버튼 높이만큼 올린다(Refresh).
            _sellAll = UIBuild.Button("SellAll", _goodsRoot, "귀중품 전부 팔기", OnSellAll, false, UITheme.FontReadSmall);
            UIBuild.Pin(_sellAll.image.rectTransform, new Vector2(1f, 0f), new Vector2(260f, 44f), Vector2.zero);

            _targetPicker = new UnitTargetPicker(Body, OnTargetPicked, ShowGoods, "매대로 돌아가기");
            // 코드는 받을 아군 → (칸이 차면) 버릴 코드 순으로 판을 번갈아 연다. 보상 화면과 같은 흐름이다.
            _codeFlow = new RewardCodeFlow(Body, ShowGoods, SetTitle, "매대로 돌아가기");
        }

        /// <summary>왼쪽 기둥 — 행상인. 그림이 아직 없으면 이름표와 대사만 남는다.</summary>
        private void BuildMerchant()
        {
            _merchant = UIBuild.Container("Merchant", Body);
            UIBuild.Anchor(_merchant, Vector2.zero, new Vector2(0.18f, 1f));

            // 행상인 뒤의 옅은 빛과 발밑 그림자 — 흰 종이에 원화가 떠 보이지 않고 서 있게 한다(파티 창 원화와 같은 문법).
            Image halo = UIBuild.Solid("Halo", _merchant, Color.white);
            halo.sprite = UIShapes.Glow(128, new Color(0.62f, 0.80f, 0.95f, 0.30f));
            halo.type = Image.Type.Simple; halo.raycastTarget = false;
            UIBuild.Anchor(halo.rectTransform, new Vector2(-0.1f, 0.40f), new Vector2(1.1f, 1.02f));
            Image floor = UIBuild.Solid("Floor", _merchant, Color.white);
            floor.sprite = UIShapes.Glow(128, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.12f));
            floor.type = Image.Type.Simple; floor.raycastTarget = false;
            UIBuild.Anchor(floor.rectTransform, new Vector2(0.14f, 0.335f), new Vector2(0.86f, 0.395f));

            var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artObject.transform.SetParent(_merchant, false);
            _merchantArt = artObject.GetComponent<Image>();
            _merchantArt.preserveAspect = true;
            _merchantArt.raycastTarget = false;
            UIBuild.Anchor(_merchantArt.rectTransform, new Vector2(0f, 0.36f), new Vector2(1f, 0.98f));

            Sprite standing = SpriteResource.LoadStanding(MerchantStanding);
            _merchantArt.sprite = standing;
            _merchantArt.enabled = standing != null;

            TextMeshProUGUI name = UIBuild.Text("Name", _merchant, MerchantName, UITheme.FontReadHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Midline);
            name.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.35f));

            _merchantLine = UIBuild.Text("Line", _merchant, "", UITheme.FontReadSmall, UITheme.TextSecondary,
                TextAlignmentOptions.Top, wrap: true);
            UIBuild.Anchor(_merchantLine.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.27f), 8f, 0f);
        }

        /// <summary>
        /// 탭 줄 오른쪽 — 보유 골드와 남은 구매. 예전에는 탭 위 회색 막대에 셋을 같은 무게로 늘어놓아
        /// 남은 구매가 오른쪽 끝에 떨어져 있었다(D10). 사기 전에 보는 두 값만 탭과 같은 줄에 붙이고,
        /// 시세 설명은 안내 줄 오른쪽으로 내린다.
        /// </summary>
        /// <summary>
        /// 지갑 · 남은 구매는 남색 제목 띠 오른쪽에 둔다 — 게임의 재화 표시 자리다. 받을 아군을 고르는 동안(매대가 숨는 동안)에도
        /// 그대로 보여 무엇을 얼마에 사는지 놓치지 않는다. 세 요소의 세로 중심은 띠 중심선(32px)이다.
        /// 시세 설명은 탭 줄 오른쪽 끝에 붙인다.
        /// </summary>
        private void BuildStatusBar()
        {
            Transform header = Body.parent;
            float y = -(UIArtDirection.HeaderMidline - 20f);

            _purchases = UIBuild.Text("Purchases", header, "", UITheme.FontReadSmall, UIArtDirection.HeaderInkSoft,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(_purchases.rectTransform, new Vector2(1f, 1f), new Vector2(190f, 40f), new Vector2(-HeaderWalletRight, y));

            Image rule = UIBuild.Solid("WalletRule", header, new Color(1f, 1f, 1f, 0.28f));
            rule.raycastTarget = false;
            UIBuild.Pin(rule.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 24f),
                new Vector2(-(HeaderWalletRight + 190f + 16f), -(UIArtDirection.HeaderMidline - 12f)));

            _wallet = UIBuild.Text("Wallet", header, "", UITheme.FontReadHeading, UITheme.TextOnAccent,
                TextAlignmentOptions.MidlineRight);
            _wallet.fontStyle = FontStyles.Bold;
            UIBuild.Pin(_wallet.rectTransform, new Vector2(1f, 1f), new Vector2(220f, 40f),
                new Vector2(-(HeaderWalletRight + 190f + 32f), y));

            _coin = UIBuild.Icon("WalletCoin", header, UIIcon.Coin, UITheme.TextOnAccent);
            UIBuild.Pin(_coin.rectTransform, new Vector2(1f, 1f), new Vector2(24f, 24f),
                new Vector2(-(HeaderWalletRight + 190f + 32f + 120f), -(UIArtDirection.HeaderMidline - 12f)));

            _stageInfo = UIBuild.Text("Stage", _goodsRoot, "", UITheme.FontBody, UITheme.TextMuted,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(_stageInfo.rectTransform, new Vector2(1f, 1f), new Vector2(420f, TabRowHeight), new Vector2(-4f, 0f));
        }

        /// <summary>지갑 글자 폭에 맞춰 동전 기호를 바로 왼쪽(간격 8px)에 붙인다.</summary>
        private void PlaceCoin()
        {
            if (_coin == null || _wallet == null) return;
            float width = Mathf.Min(_wallet.preferredWidth, _wallet.rectTransform.sizeDelta.x);
            _coin.rectTransform.anchoredPosition = new Vector2(
                -(HeaderWalletRight + 190f + 32f + width + 8f), -(UIArtDirection.HeaderMidline - 12f));
        }

        /// <summary>탭 넷은 왼쪽부터 같은 폭으로. 선택 표지는 2px 밑줄 하나뿐이다(<see cref="ScreenKit.FlatTab"/>).</summary>
        private void BuildTabs()
        {
            const float tabWidth = 176f;
            for (int i = 0; i < TabOrder.Length; i++)
            {
                Tab tab = TabOrder[i].Tab;
                Button button = UIBuild.Button($"Tab{tab}", _goodsRoot, TabOrder[i].Label, () => SwitchTo(tab),
                    false, UITheme.FontReadSmall);
                UIBuild.Pin(button.image.rectTransform, new Vector2(0f, 1f), new Vector2(tabWidth, TabRowHeight),
                    new Vector2(i * (tabWidth + 8f), 0f));
                _tabButtons[tab] = button;
            }

            Image rule = UIBuild.Divider("TabRule", _goodsRoot);
            rule.raycastTarget = false;
            ScreenKit.TopBand(rule.rectTransform, TabRowHeight, 1f);
        }

        private bool _codeShelfRevealed;
        private int _codePage;
        private const int CodePageSize = 3;

        public override void Show()
        {
            EnsureBuilt();
            _consumables = GameManager.Instance?.rewardManager?.BuildShopGoods() ?? new List<RewardDef>();
            EnsureSupplyStock();
            _codes = BuildCodeStock();
            _message.text = "";
            _tab = Tab.Code;
            _codeShelfRevealed = false;
            _codePage = 0;
            ShowGoods();
            base.Show();
        }

        private void EnsureSupplyStock()
        {
            int stage = CurrentStage;
            if (_supplyStage == stage && _supplies.Count > 0) return;

            List<RewardDef> pool = GameManager.Instance?.rewardManager?.BuildShopItems()
                                   ?? new List<RewardDef>();
            var random = new System.Random(stage * 7919);
            _supplies = pool.OrderBy(_ => random.Next()).Take(SupplyOnDisplay).ToList();
            _supplyStage = stage;
        }

        /// <summary>
        /// 상점 고정 코드. 구현이 안 된 코드는 사도 효과가 없으므로 진열하지 않는다 —
        /// 보상 풀(<see cref="RewardManager.BuildCodePool"/>)과 같은 기준이다.
        /// </summary>
        private static List<RewardDef> BuildCodeStock()
        {
            return RewardCodeCatalog.All()
                .Where(entry => entry.shopFixed && Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.id))
                .Where(entry => !entry.requiresToughness || Combat.AttackForms.PartyUnits().Any(hero =>
                    hero.ActivePassiveCodesResolved().Any(code => code is Codes.Passive.OrpheusCheatedDeath)))
                .Select(RewardManager.BuildCodeReward)
                .ToList();
        }

        // ── 매대 ─────────────────────────────────────────────────────

        private void SwitchTo(Tab tab)
        {
            _tab = tab;
            _message.text = "";
            ShowGoods();
        }

        private void ShowGoods()
        {
            _pendingGood = null;
            if (_codeFlow != null) _codeFlow.Note = null;
            SetTitle(Title);
            _targetPicker?.SetActive(false);
            _codeFlow?.Hide();
            SetShelfVisible(true);
            Refresh();
        }

        /// <summary>선택 판은 Body 전체를 덮는다. 흐름 중에는 행상인과 매대를 함께 숨겨 겹치지 않게 한다.</summary>
        private void SetShelfVisible(bool visible)
        {
            if (_goodsRoot != null) _goodsRoot.gameObject.SetActive(visible);
            if (_merchant != null) _merchant.gameObject.SetActive(visible);
        }

        private List<RewardDef> CurrentGoods => _tab switch
        {
            Tab.Supply => _supplies,
            Tab.Code => _codes,
            Tab.Sell => BuildSellCards(),
            _ => _consumables,
        };

        /// <summary>주머니를 매대 카드 모양으로 바꾼다. 값은 주울 때 확정된 것을 그대로 쓴다.</summary>
        private List<RewardDef> BuildSellCards()
        {
            var goods = new List<RewardDef>();
            IReadOnlyList<ValuableHolding> pouch = Inventory?.Valuables;
            if (pouch == null) return goods;

            ItemDataList items = GameManager.Instance?.itemDataList;
            for (int i = 0; i < pouch.Count; i++)
            {
                ItemData data = items?.items?.FirstOrDefault(item => item != null && item.id == pouch[i].itemId);
                goods.Add(new RewardDef
                {
                    id = $"valuable_{i}",
                    displayName = data?.name ?? $"귀중품 {pouch[i].itemId}",
                    description = "팔아서 골드로 바꾼다. 구매 횟수를 쓰지 않는다.",
                    tier = Mathf.Clamp(data?.rarity ?? 1, 1, 5),
                    goldCost = pouch[i].gold,
                    item = data,
                });
            }

            foreach(var owner in RewardCodeFlow.Party())
            foreach(var holding in owner.OwnedRewardCodeInstances)
            {
                int id=holding.codeId;
                goods.Add(new RewardDef
                {
                    id="sell_code_"+holding.instanceId,codeId=id,saleOwner=owner,saleInstanceId=holding.instanceId,
                    displayName=RewardCodeFlow.NameOf(id)+" · "+owner.UnitName,
                    description=Combat.RewardCodeInventory.ChargeSummary(holding)+"판매하면 이 코드의 효과와 저장량을 내어줍니다.",
                    tier=RewardCodeCatalog.TierOf(id),goldCost=Combat.RewardCodeInventory.SalePrice(holding)
                });
            }
            return goods;
        }

        private void Refresh()
        {
            // 매대와 대상 선택은 동일한 전면 공간을 사용한다.
            FitContentHeight(994f);
            int stage = CurrentStage;
            int gold = Inventory?.Gold ?? 0;
            int left = GameManager.Instance?.ShopPurchasesLeft ?? 0;

            _wallet.text = $"{gold:N0} G";
            PlaceCoin();
            _stageInfo.text = _tab == Tab.Sell
                ? $"귀중품 {Inventory?.Valuables?.Count ?? 0}점 · 코드 {RewardCodeFlow.Party().Sum(u=>u.OwnedRewardCodeInstances.Count())}장"
                : $"{stage}층 시세 · 기본가 × {stage}";
            _purchases.text = $"남은 구매 {left} / {GameManager.MaxShopPurchases}";
            _purchases.color = left > 0 ? UIArtDirection.HeaderInkSoft : new Color(1f, 1f, 1f, 0.42f);
            _guide.text = TabGuide(_tab);

            foreach (KeyValuePair<Tab, Button> pair in _tabButtons) ScreenKit.FlatTab(pair.Value, pair.Key == _tab);

            _sellAll.gameObject.SetActive(_tab == Tab.Sell);
            _content.offsetMin = new Vector2(0f, _tab == Tab.Sell ? 56f : 0f);
            _sellAll.interactable = (Inventory?.Valuables?.Count ?? 0) > 0;
            _merchantLine.text = Banter();

            ClearNow(_content);
            if (_tab == Tab.Code) BuildCodeShelf(stage);
            else BuildListShelf(stage);
            // 이 창의 22 이상 글자에만 가독 하한을 건다(다른 화면의 자동 맞춤 규칙은 그대로).
            UIScaledText.KeepReadable(Body);
        }

        private static string TabGuide(Tab tab) => tab switch
        {
            Tab.Code => "생존 · 지원 · 처치 연계 코드를 구매할 수 있습니다.\n받을 아군을 고르면 결제합니다. 활성 7칸이 차면 보관함에 들어갑니다.",
            Tab.Supply => "공용 보급에 들어가는 물건이다. 식량은 긴 휴식(40)에, 정화의 성수는 공용 보급에서 써 다음 전투 1회에 걸린다.",
            Tab.Sell => "귀중품과 보상 코드를 판매합니다. 축전 저장량은 가격에 반영됩니다. 구매 횟수는 쓰지 않습니다.",
            _ => "산 자리에서 바로 쓰는 회복약 · 부활약 · 사탕이다. 쓸 대상이 없으면 살 수 없다.",
        };

        /// <summary>
        /// 값. 소모품은 <c>90_rewards.yaml</c>의 기본가를, 공용 보급은 <c>40_items.yaml</c>의
        /// <c>shopPrice</c>를 쓰는데 둘 다 <see cref="RewardDef.goldCost"/>에 실려 온다.
        /// </summary>
        private static int ShopPriceOf(RewardDef good, int stage) => RewardManager.ShopPrice(good, stage);

        private string Banter()
        {
            if (_tab == Tab.Sell)
            {
                int count = (Inventory?.Valuables?.Count ?? 0) + RewardCodeFlow.Party().Sum(u=>u.OwnedRewardCodeInstances.Count());
                return count == 0
                    ? "파실 물건은 없네요… 그래도 들러주신 건 좋았어요."
                    : $"{count}점이네요! 감정은 정확해요. 저, 이런 건 잘하거든요…";
            }

            if (_tab == Tab.Code)
            {
                return _codes.Count == 0
                    ? "코드는 아직 안 들여왔어요. 곧 구해 올게요…!"
                    : "필요한 코드를 고르세요.\n좋은 배합은 준비에서 시작되죠.";
            }

            if (_tab == Tab.Supply)
            {
                return _supplies.Count == 0
                    ? "아직 물건이 안 들어왔어요. 제가 잊은 건 아니고요…!"
                    : "긴 휴식은 식량 40이 필요해요. 한 묶음씩 챙겨 두세요.";
            }

            return (GameManager.Instance?.ShopPurchasesLeft ?? 0) <= 0
                ? "이번 구매는 여기까지예요. 다음 준비 때도 저 잊지 마세요…"
                : "가격은 스테이지에 따라 올라가요. 설명 잘한다고요? 헤헤…";
        }

        /// <summary>살 수 없는 이유(매대에 표시). 살 수 있으면 null이다.</summary>
        private static string BuyBlockReason(RewardDef good)
        {
            if (good == null) return null;
            if (GameManager.Instance == null || !GameManager.Instance.CanOpenShop) return "준비 중에만 구매";
            if (GameManager.Instance.ShopPurchasesLeft <= 0) return "구매 한도";

            int price = RewardManager.ShopPrice(good, CurrentStage);
            if ((Inventory?.Gold ?? 0) < price) return "골드 부족";

            // 받을 아군이 없는 코드는 값만 받고 아무 일도 없게 된다. 사기 전에 막는다.
            if (good.IsCodeReward &&
                !RewardCodeFlow.Party().Any(unit => unit.CanReceiveRewardCode(good.codeId, out _, out _, out _)))
                return "받을 아군 없음";

            // 쓸 대상이 없는 약은 사도 아무 일이 일어나지 않는다. 사기 전에 막는다.
            if (good.IsRevivalReward && !HasFallenAlly()) return "되살릴 아군 없음";
            if (good.IsHealingReward && !HasInjuredAlly()) return "다친 아군 없음";

            return null;
        }

        /// <summary>선택 판 안내 아래 거래 요약 — "가격 1,800 G · 보유 20,000 G · 남은 구매 2/2 · 고르고 나면 결제".</summary>
        private static string TradeNote(RewardDef good)
            => $"가격 {RewardManager.ShopPrice(good, CurrentStage):N0} G  ·  보유 {Inventory?.Gold ?? 0:N0} G  ·  " +
               $"남은 구매 {GameManager.Instance?.ShopPurchasesLeft ?? 0}/{GameManager.MaxShopPurchases}  ·  고르면 그때 결제";

        /// <summary>
        /// 마지막 확정 직전의 재검사. 선택 판을 여는 동안 상태 · 골드 · 횟수 · 칸이 바뀌었을 수 있다.
        /// 여기서 걸리면 값을 받지 않는다.
        /// </summary>
        private static string ConfirmBlockReason(RewardDef good, Unit target, int overwriteCodeId, int price)
        {
            GameManager game = GameManager.Instance;
            if (game == null || Inventory == null || game.rewardManager == null) return "상점을 쓸 수 없음";
            if (!game.CanOpenShop) return "준비 중에만 구매";
            if (game.ShopPurchasesLeft <= 0) return "구매 한도";
            if (Inventory.Gold < price) return "골드 부족";

            if (good.IsCodeReward)
            {
                if (target == null || !RewardCodeFlow.Party().Contains(target)) return "받을 아군 없음";
                if (!target.CanReceiveRewardCode(good.codeId, out _, out bool needsOverwrite, out string reason)) return reason;
                if (needsOverwrite && (overwriteCodeId <= 0 || !target.RewardCodeIds.Contains(overwriteCodeId)))
                    return "버릴 코드를 고르지 않음";
                return null;
            }

            if (good.RequiresTargetSelection && (target == null || !RewardScreen.TargetCandidates(good).Contains(target)))
                return "대상이 맞지 않음";
            if (good.IsRevivalReward && !good.RequiresTargetSelection && !HasFallenAlly()) return "되살릴 아군 없음";
            if (good.IsHealingReward && !good.RequiresTargetSelection && !HasInjuredAlly()) return "다친 아군 없음";
            return null;
        }

        private static bool HasFallenAlly()
        {
            return GridManager.Instance?.heroList?.Any(RewardManager.IsValidReviveTarget) == true;
        }

        private static bool HasInjuredAlly()
        {
            return GridManager.Instance?.heroList?.Any(hero =>
                RewardManager.IsValidHealingTarget(hero) && hero.HpCurr < hero.HpMax) == true;
        }

        private void OnPick(int index)
        {
            if (_tab == Tab.Sell)
            {
                Sell(index);
                return;
            }

            List<RewardDef> goods = CurrentGoods;
            if (index < 0 || index >= goods.Count) return;

            RewardDef good = goods[index];
            string blocked = BuyBlockReason(good);
            if (blocked != null)
            {
                _message.text = $"{good.displayName} — {blocked}";
                Refresh();
                return;
            }

            if (good.IsCodeReward)
            {
                _pendingGood = good;
                FitContentHeight(994f);
                SetShelfVisible(false);
                // 선택 판이 지갑 줄을 덮으므로 무엇을 얼마에 사는지를 안내문 아래에 적는다. 결제는 여전히 마지막 확정의 재검사를 거친다.
                _codeFlow.Note = TradeNote(good);
                // 판을 여는 데 실패(받을 아군 없음)하면 매대를 그대로 둔다.
                bool opened = _codeFlow.StartReceive(good.codeId, good.displayName,
                    (unit, overwrite) => BuyCode(good, unit, overwrite));
                if (!opened)
                {
                    ShowGoods();
                    _message.text = $"{good.displayName} — 받을 아군 없음";
                }
                return;
            }

            if (good.RequiresTargetSelection)
            {
                List<Unit> candidates = RewardScreen.TargetCandidates(good);
                if (candidates.Count == 0) return;

                _pendingGood = good;
                SetShelfVisible(false);
                SetTitle($"{good.displayName} — 대상 선택");
                _targetPicker.Show(candidates, $"{RewardScreen.TargetGuide(good)}\n{TradeNote(good)}",
                    unit => RewardScreen.TargetPreview(good, unit));
                return;
            }

            Buy(good, null);
        }

        private void OnTargetPicked(Unit target)
        {
            if (_pendingGood == null) return;

            RewardDef good = _pendingGood;
            _pendingGood = null;
            Buy(good, target);
            ShowGoods();
        }

        /// <summary>
        /// 값을 치르고 효과를 적용한다. 확정 직전 재검사에 걸리면 값을 받지 않고,
        /// 효과가 대상 문제로 불발되면 <b>골드를 되돌려 준다</b> — 값만 받고 아무 일도 일어나지 않는 경우를 남기지 않는다.
        /// </summary>
        private void Buy(RewardDef good, Unit target)
        {
            InventoryManager inventory = Inventory;
            RewardManager rewards = GameManager.Instance?.rewardManager;
            int price = RewardManager.ShopPrice(good, CurrentStage);
            string blocked = ConfirmBlockReason(good, target, 0, price);
            if (blocked != null)
            {
                _message.text = $"{good.displayName} — {blocked}. 값을 치르지 않았습니다.";
                Refresh();
                return;
            }

            if (!inventory.TrySpendGold(price))
            {
                _message.text = $"{good.displayName} — 골드가 부족합니다 ({price:N0})";
                Refresh();
                return;
            }

            if (!rewards.ApplyRewardEffect(good, target))
            {
                inventory.AddGold(price);
                _message.text = $"{good.displayName} — 사용할 수 없어 값을 돌려받았습니다.";
                Refresh();
                return;
            }

            GameManager.Instance.NotifyShopPurchase();
            _message.text = $"{good.displayName} 구매 — {price:N0} 골드를 썼습니다.";
            Refresh();
        }

        /// <summary>
        /// 코드를 산다. 대상과 덮어쓸 코드는 고른 뒤이므로 <b>골드는 마지막에 확정</b>한다 —
        /// 재검사에 걸리거나 적용이 실패하면(그 사이 칸이 바뀌는 등) 값을 받지 않고 구매 횟수도 쓰지 않는다.
        /// </summary>
        private void BuyCode(RewardDef good, Unit target, int overwriteCodeId)
        {
            _pendingGood = null;
            InventoryManager inventory = Inventory;
            RewardManager rewards = GameManager.Instance?.rewardManager;
            int price = RewardManager.ShopPrice(good, CurrentStage);
            string blocked = ConfirmBlockReason(good, target, overwriteCodeId, price);
            if (blocked != null)
            {
                ShowGoods();
                _message.text = $"{good.displayName} — {blocked}. 값을 치르지 않았습니다.";
                return;
            }

            string result;
            if (!inventory.TrySpendGold(price))
            {
                result = $"{good.displayName} — 골드가 부족합니다 ({price:N0})";
            }
            else if (!rewards.ApplyRewardEffect(good, target, overwriteCodeId))
            {
                inventory.AddGold(price);
                result = $"{good.displayName} — 받을 수 없어 값을 돌려받았습니다.";
            }
            else
            {
                GameManager.Instance.NotifyShopPurchase();
                result = overwriteCodeId > 0
                    ? $"{good.displayName} → {target.UnitName} ({RewardCodeFlow.NameOf(overwriteCodeId)} 교체) — {price:N0} 골드를 썼습니다."
                    : $"{good.displayName} → {target.UnitName} — {price:N0} 골드를 썼습니다.";
            }

            ShowGoods();
            _message.text = result;
        }

        // ── 판매 ─────────────────────────────────────────────────────

        /// <summary>귀중품 하나를 판다. <b>구매 한도를 쓰지 않는다</b> — 파는 것은 사는 것이 아니다.</summary>
        private void Sell(int index)
        {
            if(GameManager.Instance?.gameState!=BaseClasses.BaseEnums.GameState.Preparation)return;
            var good=CurrentGoods.ElementAtOrDefault(index);if(good==null)return;
            if(good.saleOwner!=null)
            {
                if(Combat.RewardCodeInventory.TrySell(good.saleOwner,good.saleInstanceId,out int paid))
                    _message.text=$"{good.displayName} 판매 — {paid:N0} 골드를 받았습니다.";
            }
            else if(Inventory.TrySellValuable(index,out int gold))
                _message.text=$"{good.displayName} 판매 — {gold:N0} 골드를 받았습니다.";
            GameManager.Instance.runManager?.SaveCurrentRun();
            Refresh();
        }

        private void OnSellAll()
        {
            int count = Inventory?.Valuables?.Count ?? 0;
            if (count == 0) return;

            int gold = Inventory.SellAllValuables();
            _message.text = $"귀중품 {count}점 판매 — {gold:N0} 골드를 받았습니다.";
            Refresh();
        }

        // ── 코드 매대 ────────────────────────────────────────────────

        /// <summary>
        /// 상점 코드 세 장을 나란히 크게. 효과 전문 · 태그 · 상위 코드 · 가격 · 아군별로 받을 수 있는지(칸 · 덮어쓰기 · 보유)를
        /// 카드 한 장에 다 적는다. 카드를 누르면 받을 아군 선택으로 넘어간다.
        /// </summary>
        /// <remarks>
        /// 2026-10-08 검수 D10 — 카드의 39%를 범용 선 아이콘이 차지해 자리표시자처럼 보였고, 가격 · 구매가 가장 약했다.
        /// 지금은 위에서부터 [아이콘 64 · 티어 눈금 · 이름] → 태그 → 효과 전문(카드의 절반) → 받을 동료 →
        /// [가격 · 구매] 순으로 결정 정보가 커진다. 위치는 카드 위/아래 끝에서 잰 픽셀이라 해상도가 바뀌어도 줄 간격이 같다.
        /// </remarks>
        private void BuildCodeShelf(int stage)
        {
            if (_codes.Count == 0) return;
            bool revealShelf = !_codeShelfRevealed;
            _codeShelfRevealed = true;
            List<Unit> party = RewardCodeFlow.Party();
            int gold = Inventory?.Gold ?? 0;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(_codes.Count / (float)CodePageSize));
            _codePage = Mathf.Clamp(_codePage, 0, pageCount - 1);
            int start = _codePage * CodePageSize;
            int visible = Mathf.Min(CodePageSize, _codes.Count - start);
            var pageEffects = new List<TextMeshProUGUI>();
            float width = 1f / CodePageSize;
            float left = (CodePageSize - visible) * width * .5f;
            if (pageCount > 1)
            {
                Button previous = UIBuild.Button("CodePagePrevious", _content, "이전", () => { _codePage--; Refresh(); }, false, UITheme.FontReadSmall);
                UIBuild.Pin(previous.image.rectTransform, new Vector2(.5f, 0f), new Vector2(100f, 42f), new Vector2(-116f, 16f));
                previous.interactable = _codePage > 0;
                var label = UIBuild.Text("CodePageNumber", _content, $"{_codePage + 1} / {pageCount}", UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.Center);
                UIBuild.Pin(label.rectTransform, new Vector2(.5f, 0f), new Vector2(100f, 42f), new Vector2(0f, 16f));
                Button next = UIBuild.Button("CodePageNext", _content, "다음", () => { _codePage++; Refresh(); }, false, UITheme.FontReadSmall);
                UIBuild.Pin(next.image.rectTransform, new Vector2(.5f, 0f), new Vector2(100f, 42f), new Vector2(116f, 16f));
                next.interactable = _codePage < pageCount - 1;
            }
            for (int i = start; i < start + visible; i++)
            {
                RewardDef good = _codes[i];
                int index = i;
                string blocked = BuyBlockReason(good);
                int tier = Mathf.Max(1, good.tier);
                int price = RewardManager.ShopPrice(good, stage);

                // 종이 위 흰 카드 한 장(한 겹). 테두리 대신 헤어라인과 얕은 그림자로 떠서 "집어 드는 물건"으로 읽힌다.
                Image card = UIBuild.Panel("CodeGood_" + i, _content, UITheme.SurfaceRaised, cut: 12);
                card.sprite = UIArtDirection.Sheet(12);
                float height=Mathf.Min(560f,_content.rect.height-(pageCount > 1 ? 72f : 12f));
                UIBuild.Anchor(card.rectTransform,new Vector2(left+(i-start)*width,.5f),new Vector2(left+(i-start+1)*width,.5f));
                card.rectTransform.sizeDelta=new Vector2(-20,height);
                card.rectTransform.anchoredPosition=new Vector2(0f, pageCount > 1 ? 24f : 0f);
                UIBuild.Elevate(card, 10, .08f, 3f);

                UIBuild.OnClick(card.gameObject, () => OnPick(index));
                // 구매·대상 선택 복귀는 기존 매대를 즉시 갱신한다. 처음 열 때만 순차 리빌한다.
                if (revealShelf) UICardReveal.Attach(card.gameObject,i);
                Transform root = card.transform;

                // 머리 — 아이콘 원(44) 오른쪽에 이름(위)과 티어 눈금(아래)을 쌓아 두 줄 덩어리의 중심을 원의 중심에 맞춘다.
                Image iconTile = UIArtDirection.Emblem("CodeTile", root, UIArtDirection.CodeIcon(good.codeId), tier);
                ScreenKit.PinTopLeft(iconTile.rectTransform, CardPad - 2f, CardPad - 2f);
                iconTile.rectTransform.sizeDelta = new Vector2(64f, 64f);
                TextMeshProUGUI name = UIBuild.Text("Name", root, good.displayName, 24f, UITheme.TextPrimary);
                name.fontStyle = FontStyles.Bold;
                ScreenKit.TopBand(name.rectTransform, CardPad, 32f, CardPad + 76f, CardPad);
                ScreenKit.PinTopLeft(ScreenKit.TierBadge(root, tier, 20f), CardPad + 76f, CardPad + 34f);

                // 태그는 티어 눈금과 같은 줄 오른쪽 — 머리를 두 줄(이름 / 티어 · 태그)로 끝낸다.
                TextMeshProUGUI tags = UIBuild.Text("Tags", root, TagText(good.codeId), UITheme.FontBody, UITheme.TextMuted);
                tags.overflowMode = TextOverflowModes.Ellipsis;
                ScreenKit.TopBand(tags.rectTransform, CardPad + 30f, 28f, CardPad + 76f + ScreenKit.TierBadgeWidth(20f) + 10f, CardPad);

                Image headRule = UIBuild.Divider("HeadRule", root);
                headRule.raycastTarget = false;
                ScreenKit.TopBand(headRule.rectTransform, CardPad + 62f, 1f, CardPad, CardPad);

                // 효과 전문 — 카드에서 가장 넓은 칸. 아래 받을 동료 · 구매 행 위를 모두 쓴다.
                TextMeshProUGUI effect = UIBuild.Text("Effect", root, CodeCatalog.Summary(good.description),
                    22f, UITheme.TextPrimary, TextAlignmentOptions.TopLeft, true);
                effect.richText = false;
                effect.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Stretch(effect.rectTransform);
                effect.rectTransform.offsetMin = new Vector2(CardPad, CardPad + BuyHeight + 48f);
                effect.rectTransform.offsetMax = new Vector2(-CardPad, -(CardPad + 76f));
                pageEffects.Add(effect);

                BuildReceivers(root, good.codeId, party);

                Image footRule = UIBuild.Divider("FootRule", root);
                footRule.raycastTarget = false;
                ScreenKit.BottomBand(footRule.rectTransform, CardPad + BuyHeight + 14f, 1f, CardPad, CardPad);

                // 가격은 굵은 군청. 골드가 모자라면 위험색으로 바꿔 구매 칸의 사유와 짝을 맞춘다.
                TextMeshProUGUI priceLabel = UIBuild.Text("Price", root, price.ToString("N0") + " G", UITheme.FontReadHeading,
                    gold < price ? UITheme.Danger : UITheme.Accent);
                priceLabel.fontStyle = FontStyles.Bold;
                ScreenKit.BottomBand(priceLabel.rectTransform, CardPad, BuyHeight, CardPad, CardPad + 136f);

                BuyState(root, blocked ?? "구매", blocked == null, new Vector2(120f, BuyHeight), new Vector2(-CardPad, CardPad), onClick: () => OnPick(index));
                TooltipTrigger.Attach(card.gameObject, () => good.displayName, () => CodeTooltipLines(good), UITheme.Accent);
            }
            // 같은 페이지의 가장 긴 본문을 기준으로 여백을 나눠 첫 줄의 읽기선을 맞춘다.
            float textWidth = Mathf.Max(1f, _content.rect.width * width - 20f - CardPad * 2f);
            float longest = pageEffects.Max(text => text.GetPreferredValues(text.text, textWidth, Mathf.Infinity).y);
            foreach (var text in pageEffects)
                text.margin = new Vector4(0f, Mathf.Min(32f, Mathf.Max(0f, (text.rectTransform.rect.height - longest) * .5f)), 0f, 0f);
        }

        /// <summary>
        /// 받을 수 있는 동료 — 수와 얼굴. 받을 수 없는 아군의 얼굴은 흐리게 둔다. 이유 문장은 선택 판이 보인다.
        /// 판정은 구매 흐름과 같은 <see cref="Unit.CanReceiveRewardCode"/>다.
        /// </summary>
        private static void BuildReceivers(Transform root, int codeId, List<Unit> party)
        {
            int count = party.Count(unit => unit.CanReceiveRewardCode(codeId, out _, out _, out _));
            TextMeshProUGUI head = UIBuild.Text("ReceiversHead", root, $"적용 {count}명",
                UITheme.FontBody, count > 0 ? UITheme.TextPrimary : UITheme.TextMuted);
            ScreenKit.BottomBand(head.rectTransform, CardPad + BuyHeight + 16f, 34f, CardPad, CardPad + 5 * 38f);

            for (int i = 0; i < party.Count && i < 5; i++)
            {
                Unit unit = party[i];
                bool ok = unit.CanReceiveRewardCode(codeId, out _, out _, out _);
                Image backing = UIBuild.Panel($"Receiver_{unit.UnitName}", root, new Color(.14f, .20f, .30f, .10f));
                backing.sprite = UIShapes.RoundedRect(17, Color.white);
                backing.color = new Color(.14f, .20f, .30f, .35f);
                backing.raycastTarget = false;
                backing.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                Image face = UIBuild.Panel("Face", backing.transform, Color.white);
                face.type = Image.Type.Simple;
                face.sprite = UIPortraitCrop.Face(SpriteResource.LoadPortrait(unit.PortraitPath), includeCrown: true);
                face.enabled = face.sprite != null;
                face.preserveAspect = true;
                face.raycastTarget = false;
                face.color = ok ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                UIBuild.Stretch(face.rectTransform);
                RectTransform rect = backing.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(34f, 34f);
                rect.anchoredPosition = new Vector2(-CardPad - (party.Count - 1 - i) * 38f, CardPad + BuyHeight + 16f);
            }
        }

        /// <summary>아군마다 짧은 한 줄 — 받을 수 있으면 칸 수와 새 칸 · 교체 · 하위 대체, 없으면 이유. 자세한 문구는 선택 판이 보인다.</summary>
        private static string ReceiverLines(int codeId, List<Unit> party)
        {
            var sb = new StringBuilder();
            foreach (Unit unit in party)
            {
                bool ok = unit.CanReceiveRewardCode(codeId, out _, out _, out _);
                string line = RewardCodeFlow.ReceiveShort(codeId, unit);
                sb.Append(CodeText.Paint(CodeText.Plain(line), ok ? UITheme.TextPrimary : UITheme.TextMuted)).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string TagText(int codeId)
            => string.Join(" · ", UnitTagCatalog.Resolve(RewardCodeCatalog.TagsOf(codeId)).Select(tag => tag.Name));

        private static IReadOnlyList<UITooltip.Line> CodeTooltipLines(RewardDef good)
        {
            List<UITooltip.Line> lines = RewardCodeFlow.TooltipLines(good.codeId);
            lines.Add(UITooltip.Line.Note("받을 아군을 고르고, 활성 칸이 차면 보관함으로 받는다. 값은 마지막 확정에서 치른다.",
                UITheme.TextSecondary));
            return lines;
        }

        // ── 목록 매대(회복 · 성장 / 공용 보급 / 판매) ─────────────────

        /// <summary>
        /// 한 줄에 물건 하나 — 그림 · 이름 · 설명 · 값 · 구매 상태. 진열이 몇 개든 목록이 굴러가므로 모두 손이 닿는다.
        /// </summary>
        private void BuildListShelf(int stage)
        {
            List<RewardDef> goods = CurrentGoods;
            bool selling = _tab == Tab.Sell;
            if (goods.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _content,
                    selling ? "판매할 귀중품이나 보상 코드가 없습니다." : "진열된 물건이 없습니다.",
                    UITheme.FontReadBody, UITheme.TextMuted, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Stretch(empty.rectTransform, 24f, 0f);
                return;
            }

            RectTransform list = UIBuild.ScrollArea("GoodsScroll", _content, out ScrollRect scroll);
            UIBuild.Stretch((RectTransform)scroll.transform);
            int gold = Inventory?.Gold ?? 0;
            for (int i = 0; i < goods.Count; i++)
            {
                RewardDef good = goods[i];
                int index = i;
                string blocked = selling ? null : BuyBlockReason(good);
                int price = selling ? good.goldCost : ShopPriceOf(good, stage);

                // 살 수 없는 줄은 버튼 모양을 버리고 줄 전체를 흐리게 둔다 — 사유가 버튼처럼 보이면 누를 수 있는 것으로 읽혔다(D11).
                Image row = UIBuild.Panel($"Good_{i}", list, UITheme.SurfaceRaised, UIShapes.Corner.All, 8);
                row.sprite = blocked == null ? UIArtDirection.Sheet(10) : ScreenKit.IdleSurface(UITheme.SurfaceSunken);
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.sizeDelta = new Vector2(-20f, RowHeight - 10f);
                row.rectTransform.anchoredPosition = new Vector2(-6f, -i * RowHeight);
                UIBuild.OnClick(row.gameObject, () => OnPick(index));
                if (blocked != null) UIBuild.Group(row.gameObject).alpha = 0.62f;

                var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(row.transform, false);
                Image art = artObject.GetComponent<Image>();
                art.preserveAspect = true;
                art.raycastTarget = false;
                Sprite sprite = good.codeId > 0 ? UILineIcons.Get(UIArtDirection.CodeIcon(good.codeId)) : LoadArt(good);
                bool fallback=sprite==null;
                art.sprite=sprite??UILineIcons.Get(UIIcon.Supply);
                art.enabled=true;
                art.color=good.codeId>0||fallback?UITheme.Accent:Color.white;
                UIBuild.Pin(art.rectTransform, new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(20f, 0f));

                // 이름 줄 — 같은 병 그림이 여럿이라 티어 눈금을 이름 앞에 붙여 등급을 모양으로 가른다.
                // 줄 높이 90 기준 — 이름 줄 중심 28px, 설명 줄 중심 62px로 두 줄 덩어리가 행 중심(45)에 선다.
                // 티어 눈금은 이름 줄과 같은 중심선이다.
                RectTransform badge = ScreenKit.TierBadge(row.transform, Mathf.Max(1, good.tier), 20f);
                ScreenKit.PinTopLeft(badge, 104f, 18f);
                float nameLeft = 104f + ScreenKit.TierBadgeWidth(20f) + 10f;
                TextMeshProUGUI name = UIBuild.Text("Name", row.transform, good.displayName, UITheme.FontReadBody,
                    UITheme.TextPrimary);
                name.fontStyle = FontStyles.Bold;
                name.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0.62f, 1f));
                name.rectTransform.offsetMin = new Vector2(nameLeft, -50f);
                name.rectTransform.offsetMax = new Vector2(0f, -6f);

                TextMeshProUGUI desc = UIBuild.Text("Desc", row.transform, DescriptionOf(good),
                    UITheme.FontReadSmall, UITheme.TextSecondary);
                desc.richText = false;
                desc.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(desc.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 0f));
                desc.rectTransform.offsetMin = new Vector2(104f, 10f);
                desc.rectTransform.offsetMax = new Vector2(0f, 46f);

                Color priceInk = blocked == null ? UITheme.Accent : gold < price && !selling ? UITheme.Danger : UITheme.TextMuted;
                TextMeshProUGUI priceLabel = UIBuild.Text("Price", row.transform, $"{price:N0} G", UITheme.FontReadHeading,
                    priceInk, TextAlignmentOptions.MidlineRight);
                priceLabel.fontStyle = FontStyles.Bold;
                UIBuild.Anchor(priceLabel.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f));
                priceLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
                priceLabel.rectTransform.offsetMax = new Vector2(-228f, 0f);

                if (blocked == null)
                    BuyState(row.transform, selling ? "판매" : "구매", true, new Vector2(180f, 52f), new Vector2(-20f, 0f), 0.5f, () => OnPick(index));
                else
                {
                    TextMeshProUGUI reason = UIBuild.Text("State", row.transform, blocked, UITheme.FontReadSmall,
                        UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                    UIBuild.Pin(reason.rectTransform, new Vector2(1f, 0.5f), new Vector2(200f, 52f), new Vector2(-20f, 0f));
                }

                if (good.item != null) ItemTooltip.Attach(row.gameObject, good.item);
                else if (good.saleOwner != null)
                    TooltipTrigger.Attach(row.gameObject, () => good.displayName, () =>
                    {
                        var lines = RewardCodeFlow.TooltipLines(good.codeId);
                        lines.Add(UITooltip.Line.Note(good.description, UITheme.TextSecondary));
                        return lines;
                    }, UITheme.Accent);
            }

            list.sizeDelta = new Vector2(0f, goods.Count * RowHeight + 8f);
            ScreenKit.ScrollFade(scroll, UITheme.Surface);
        }

        /// <summary>목록 줄의 설명. 공용 보급 물건은 효과 문구를, 나머지는 데이터 설명을 쓴다.</summary>
        private static string DescriptionOf(RewardDef good)
        {
            if(good.saleOwner!=null)
            {
                var instance=good.saleOwner.OwnedRewardCodeInstances.FirstOrDefault(x=>x.instanceId==good.saleInstanceId);
                string charge=Combat.RewardCodeInventory.ChargeBrief(instance);
                return charge.Length>0 ? charge : "효과를 해제하고 골드로 교환합니다.";
            }
            if (good.item != null && !good.item.IsValuable)
            {
                string effect = ItemTooltip.EffectLine(good.item);
                if (effect.Length > 0) return effect;
            }
            return good.description ?? "";
        }

        /// <summary>
        /// 구매 칸. 살 수 있으면 군청으로 채운 주 행동(전투 시작 · 계속하기와 같은 문법),
        /// 막혔으면 채움 없이 1px 선 안에 사유만 흐리게 적는다. 카드 · 줄 전체가 클릭을 받으므로 이 칸은 표지다.
        /// 글자 이름 "State"는 검증이 사유 문자열을 읽는 자리다.
        /// </summary>
        private static void BuyState(Transform parent, string text, bool actionable, Vector2 size, Vector2 offset,
            float anchorY = 0f, System.Action onClick = null)
        {
            Image band = actionable && onClick != null
                ? UIBuild.Button("StateBand", parent, null, onClick, true).image
                : UIBuild.Solid("StateBand", parent, Color.white);
            // 공통 버튼 문법과 같은 알약 — 주 행동은 남색으로 채우고, 막힌 사유는 헤어라인 알약 안에 흐리게.
            band.sprite = actionable
                ? UIArtDirection.ActionPlate(true)
                : UIShapes.RoundedRect(10, Color.clear, UITheme.Outline, 1);
            band.type = Image.Type.Sliced;
            band.raycastTarget = actionable && onClick != null;
            UIBuild.Pin(band.rectTransform, new Vector2(1f, anchorY), size, offset);
            TextMeshProUGUI state = UIBuild.Text("State", band.transform, text, UITheme.FontReadSmall,
                actionable ? UITheme.TextOnAccent : UITheme.TextMuted, TextAlignmentOptions.Midline);
            state.fontStyle = actionable ? FontStyles.Bold : FontStyles.Normal;
            // 22px 글자의 실제 줄 높이(약 31px)가 52px 칸 안에 넉넉히 들어간다. 좌우만 8px 비운다.
            UIBuild.Stretch(state.rectTransform, 8f, 0f);
            if(band.TryGetComponent<Button>(out var button))
            {
                UIButtonStyle.Attach(button);
                var style=button.GetComponent<UIButtonStyle>();
                style.LightFeedback=true;
                style.LightFeedbackTint=new Color(.28f,.44f,.72f,1f);
            }
        }

        /// <summary>
        /// 다시 그릴 때 옛 자식을 먼저 끈다. <see cref="UIBuild.Clear"/>의 Destroy는 프레임 끝까지 미뤄지므로
        /// 같은 프레임에 새로 만든 같은 이름의 항목과 겹쳐 잡히지 않게 한다.
        /// </summary>
        private static void ClearNow(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) parent.GetChild(i).gameObject.SetActive(false);
            UIBuild.Clear(parent);
        }

        /// <summary>소모품은 <c>artPath</c>를, 공용 아이템은 <c>icon</c>을 본다.</summary>
        private static Sprite LoadArt(RewardDef good)
        {
            if (good.item != null) return ItemTooltip.LoadArt(good.item);
            return string.IsNullOrWhiteSpace(good.artPath) ? null : Resources.Load<Sprite>(good.artPath);
        }
    }
}
