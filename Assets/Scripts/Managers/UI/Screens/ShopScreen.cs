using System.Collections.Generic;
using System.Linq;
using BaseClasses;
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
    /// 왼쪽에 행상인이 서고 오른쪽이 물건이다. 예전에는 카드 열 장만 떠 있어서
    /// "무엇을 사는 곳"인지는 알아도 "누구에게 사는 곳"인지가 없었다.
    ///
    /// 세 갈래다.
    /// <list type="bullet">
    /// <item>소모품 — 회복약·부활약·강화제·사탕. <c>90_rewards.yaml</c>을 그대로 쓴다</item>
    /// <item>장비 — 테마를 타지 않는 범용 장비. 스테이지마다 진열이 바뀐다</item>
    /// <item>판매 — 주워 온 귀중품을 골드로 바꾼다. 구매 한도를 쓰지 않는다</item>
    /// </list>
    ///
    /// 파는 물건과 그 효과는 보상 풀과 같은 데이터를 쓴다 — 상점에서 산 회복약과
    /// 보상으로 받은 회복약이 다르게 동작하면 안 되기 때문이다. 가격만 상점이 정하고
    /// (<see cref="RewardManager.ShopPrice"/>), 효과 적용은
    /// <see cref="RewardManager.ApplyRewardEffect"/>가 그대로 처리한다.
    /// </summary>
    public class ShopScreen : ModalScreen
    {
        private const int Columns = 4;
        private const int Rows = 2;
        private const int Slots = Columns * Rows;

        /// <summary>장비 매대에 한 번에 진열하는 수. 전부 늘어놓으면 고를 것이 없어진다.</summary>
        private const int EquipmentOnDisplay = 6;

        /// <summary>행상인 자산 키. 아직 그림이 없으면 이름표만 선다.</summary>
        private const string MerchantStanding = "MERCHANT_STANDING";
        private const string MerchantName = "행상인";

        private enum Tab
        {
            Consumable,
            Equipment,
            Sell,
        }

        protected override string CanvasName => "ShopCanvas";
        protected override int SortingOrder => 72;
        protected override string Title => "상점";
        protected override string Caption => "SHOP";
        protected override Vector2 AnchorMin => new(0.08f, 0.14f);
        protected override Vector2 AnchorMax => new(0.92f, 0.86f);
        protected override bool CloseOnBackdrop => true;

        private readonly List<Card> _cards = new();
        private readonly List<Button> _tabButtons = new();
        private readonly List<RectTransform> _tabMarks = new();

        private List<RewardDef> _consumables = new();
        private List<RewardDef> _equipment = new();
        private int _equipmentStage = -1;

        private Tab _tab = Tab.Consumable;

        private RectTransform _goodsRoot;
        private TextMeshProUGUI _wallet;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _merchantLine;
        private Image _merchantArt;
        private Button _sellAll;
        private UnitTargetPicker _targetPicker;
        private RewardDef _pendingGood;

        private static int CurrentStage => Mathf.Max(1, GameManager.Instance?.RoundManager?.Stage ?? 1);

        private static InventoryManager Inventory => GameManager.Instance?.inventoryManager;

        protected override void Build()
        {
            BuildMerchant();

            _goodsRoot = UIBuild.Container("ShopGoods", Body);
            UIBuild.Anchor(_goodsRoot, new Vector2(0.26f, 0f), Vector2.one);

            _wallet = UIBuild.Text("Wallet", _goodsRoot, "", UITheme.FontBody, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_wallet.rectTransform, new Vector2(0f, 0.92f), Vector2.one);

            BuildTabs();

            for (int i = 0; i < Slots; i++)
            {
                _cards.Add(new Card(_goodsRoot, i, OnPick));
            }

            _message = UIBuild.Text("Message", _goodsRoot, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_message.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.16f));

            _sellAll = UIBuild.Button("SellAll", _goodsRoot, "전부 팔기", OnSellAll);
            UIBuild.Anchor(_sellAll.GetComponent<RectTransform>(),
                new Vector2(0.04f, 0.01f), new Vector2(0.30f, 0.09f));

            Button close = UIBuild.Button("Close", _goodsRoot, "닫기", Hide, primary: true);
            UIBuild.Anchor(close.GetComponent<RectTransform>(),
                new Vector2(0.40f, 0.01f), new Vector2(0.60f, 0.09f));

            _targetPicker = new UnitTargetPicker(Body, OnTargetPicked, ShowGoods, "매대로 돌아가기");
        }

        /// <summary>왼쪽 기둥 — 행상인. 그림이 아직 없으면 이름표와 대사만 남는다.</summary>
        private void BuildMerchant()
        {
            RectTransform column = UIBuild.Container("Merchant", Body);
            UIBuild.Anchor(column, Vector2.zero, new Vector2(0.24f, 1f));

            var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artObject.transform.SetParent(column, false);
            _merchantArt = artObject.GetComponent<Image>();
            _merchantArt.preserveAspect = true;
            _merchantArt.raycastTarget = false;
            UIBuild.Anchor(_merchantArt.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.96f));

            Sprite standing = SpriteResource.LoadStanding(MerchantStanding);
            _merchantArt.sprite = standing;
            _merchantArt.enabled = standing != null;

            TextMeshProUGUI name = UIBuild.Text("Name", column, MerchantName, UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.29f));

            _merchantLine = UIBuild.Text("Line", column, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.Top, wrap: true);
            UIBuild.Anchor(_merchantLine.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.19f), 8f, 0f);
        }

        private void BuildTabs()
        {
            (Tab tab, string label)[] tabs =
            {
                (Tab.Consumable, "소모품"),
                (Tab.Equipment, "장비"),
                (Tab.Sell, "판매"),
            };

            float width = 1f / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                Tab tab = tabs[i].tab;
                Button button = UIBuild.Button($"Tab{tab}", _goodsRoot, tabs[i].label, () => SwitchTo(tab));
                UIBuild.Anchor(button.GetComponent<RectTransform>(),
                    new Vector2(i * width, 0.84f), new Vector2((i + 1) * width, 0.91f), 6f, 0f);
                _tabButtons.Add(button);

                Image mark = UIBuild.Solid($"Tab{tab}Mark", _goodsRoot, UITheme.Accent);
                UIBuild.Anchor(mark.rectTransform,
                    new Vector2(i * width, 0.825f), new Vector2((i + 1) * width, 0.838f), 6f, 0f);
                _tabMarks.Add(mark.rectTransform);
            }
        }

        public override void Show()
        {
            EnsureBuilt();
            _consumables = GameManager.Instance?.rewardManager?.BuildShopGoods() ?? new List<RewardDef>();
            EnsureEquipmentStock();
            _message.text = "";
            _tab = Tab.Consumable;
            ShowGoods();
            base.Show();
        }

        /// <summary>
        /// 장비 진열. <b>스테이지마다 한 번만 뽑는다.</b>
        /// 열 때마다 다시 굴리면 닫았다 여는 것만으로 원하는 물건이 나올 때까지 돌릴 수 있다.
        /// </summary>
        private void EnsureEquipmentStock()
        {
            int stage = CurrentStage;
            if (_equipmentStage == stage && _equipment.Count > 0) return;

            List<RewardDef> pool = GameManager.Instance?.rewardManager?.BuildShopEquipment()
                                   ?? new List<RewardDef>();
            var random = new System.Random(stage * 7919);
            _equipment = pool.OrderBy(_ => random.Next()).Take(EquipmentOnDisplay).ToList();
            _equipmentStage = stage;
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
            SetTitle(Title);
            _targetPicker?.SetActive(false);
            if (_goodsRoot != null) _goodsRoot.gameObject.SetActive(true);
            Refresh();
        }

        private List<RewardDef> CurrentGoods => _tab switch
        {
            Tab.Equipment => _equipment,
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
                    description = "팔아서 골드로 바꾼다.",
                    tier = Mathf.Clamp(data?.rarity ?? 1, 1, 5),
                    goldCost = pouch[i].gold,
                });
            }

            return goods;
        }

        private void Refresh()
        {
            int stage = CurrentStage;
            int gold = Inventory?.Gold ?? 0;
            int left = GameManager.Instance?.ShopPurchasesLeft ?? 0;

            _wallet.text = _tab == Tab.Sell
                ? $"보유 골드 {gold:N0}   ·   주머니 {Inventory?.Valuables?.Count ?? 0}점" +
                  $"   ·   전부 팔면 {Inventory?.ValuableTotalGold ?? 0:N0} G"
                : $"보유 골드 {gold:N0}   ·   남은 구매 {left} / {GameManager.MaxShopPurchases}" +
                  $"   ·   {stage}스테이지 시세";

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                _tabMarks[i].gameObject.SetActive(i == (int)_tab);
            }

            _sellAll.gameObject.SetActive(_tab == Tab.Sell);
            _sellAll.interactable = (Inventory?.Valuables?.Count ?? 0) > 0;
            _merchantLine.text = Banter();

            List<RewardDef> goods = CurrentGoods;
            for (int i = 0; i < _cards.Count; i++)
            {
                RewardDef good = i < goods.Count ? goods[i] : null;
                int price = good == null
                    ? 0
                    : _tab == Tab.Sell ? good.goldCost : ShopPriceOf(good, stage);
                _cards[i].Bind(good, price, _tab == Tab.Sell ? null : BuyBlockReason(good), _tab == Tab.Sell);
            }
        }

        /// <summary>
        /// 값. 소모품은 <c>90_rewards.yaml</c>의 기본가를, 장비는 <c>40_items.yaml</c>의
        /// <c>shopPrice</c>를 쓰는데 둘 다 <see cref="RewardDef.goldCost"/>에 실려 온다.
        /// </summary>
        private static int ShopPriceOf(RewardDef good, int stage) => RewardManager.ShopPrice(good, stage);

        private string Banter()
        {
            if (_tab == Tab.Sell)
            {
                int count = Inventory?.Valuables?.Count ?? 0;
                return count == 0
                    ? "팔 만한 건 안 가져왔군. 다음에 오게."
                    : $"{count}점인가. 값은 자네가 주운 자리에서 이미 정해졌네.";
            }

            if (_tab == Tab.Equipment)
            {
                return _equipment.Count == 0
                    ? "오늘은 물건이 안 들어왔네."
                    : "숙련이 없으면 무게만 지고 가는 거야. 확인하고 사게.";
            }

            return (GameManager.Instance?.ShopPurchasesLeft ?? 0) <= 0
                ? "오늘 몫은 여기까지. 다음 준비 때 다시 오게."
                : "값은 스테이지를 타네. 늦게 산다고 싸지진 않아.";
        }

        /// <summary>살 수 없는 이유. 살 수 있으면 null이다.</summary>
        private static string BuyBlockReason(RewardDef good)
        {
            if (good == null) return null;
            if ((GameManager.Instance?.ShopPurchasesLeft ?? 0) <= 0) return "구매 한도";

            int price = RewardManager.ShopPrice(good, CurrentStage);
            if ((Inventory?.Gold ?? 0) < price) return "골드 부족";

            // 쓸 대상이 없는 약은 사도 아무 일이 일어나지 않는다. 사기 전에 막는다.
            if (good.IsRevivalReward && !HasFallenAlly()) return "되살릴 아군 없음";
            if (good.IsHealingReward && !HasInjuredAlly()) return "다친 아군 없음";

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
                return;
            }

            if (good.RequiresTargetSelection)
            {
                List<Unit> candidates = RewardScreen.TargetCandidates(good);
                if (candidates.Count == 0) return;

                _pendingGood = good;
                _goodsRoot.gameObject.SetActive(false);
                SetTitle($"{good.displayName} — 대상 선택");
                _targetPicker.Show(candidates, RewardScreen.TargetGuide(good),
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
        /// 값을 치르고 효과를 적용한다. 효과가 대상 문제로 불발되면 <b>골드를 되돌려 준다</b> —
        /// 값만 받고 아무 일도 일어나지 않는 경우를 남기지 않는다.
        /// </summary>
        private void Buy(RewardDef good, Unit target)
        {
            InventoryManager inventory = Inventory;
            RewardManager rewards = GameManager.Instance?.rewardManager;
            if (inventory == null || rewards == null) return;

            int price = RewardManager.ShopPrice(good, CurrentStage);
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

        // ── 판매 ─────────────────────────────────────────────────────

        /// <summary>귀중품 하나를 판다. <b>구매 한도를 쓰지 않는다</b> — 파는 것은 사는 것이 아니다.</summary>
        private void Sell(int index)
        {
            IReadOnlyList<ValuableHolding> pouch = Inventory?.Valuables;
            if (pouch == null || index < 0 || index >= pouch.Count) return;

            string name = CurrentGoods.ElementAtOrDefault(index)?.displayName ?? "귀중품";
            if (Inventory.TrySellValuable(index, out int gold))
            {
                _message.text = $"{name} 판매 — {gold:N0} 골드를 받았습니다.";
            }

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

        // ── 매대 카드 ────────────────────────────────────────────────

        private sealed class Card
        {
            private readonly GameObject _root;
            private readonly Image _rarityStrip;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _description;
            private readonly Image _art;
            private readonly TextMeshProUGUI _price;
            private readonly TextMeshProUGUI _state;

            public Card(Transform parent, int index, System.Action<int> onPick)
            {
                Image panel = UIBuild.Panel($"Good{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
                _root = panel.gameObject;

                int column = index % Columns;
                int row = index / Columns;
                float width = 1f / Columns;
                // 위쪽은 지갑 줄과 탭이, 아래쪽은 안내문과 버튼이 쓴다.
                const float top = 0.80f;
                const float bottom = 0.18f;
                float height = (top - bottom) / Rows;
                float cardTop = top - row * height;
                UIBuild.Anchor(panel.rectTransform,
                    new Vector2(column * width, cardTop - height),
                    new Vector2((column + 1) * width, cardTop), 8f, 6f);

                UIBuild.OnClick(_root, () => onPick(index));

                _rarityStrip = UIBuild.Solid("Rarity", panel.transform, UITheme.Accent);
                UIBuild.Anchor(_rarityStrip.rectTransform, new Vector2(0f, 0.96f), new Vector2(1f, 1f));

                _name = UIBuild.Text("Name", panel.transform, "", UITheme.FontBody, UITheme.TextPrimary,
                    TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.78f), new Vector2(1f, 0.95f), 8f, 0f);

                var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(panel.transform, false);
                _art = artObject.GetComponent<Image>();
                _art.preserveAspect = true;
                _art.raycastTarget = false;
                UIBuild.Anchor(_art.rectTransform, new Vector2(0f, 0.44f), new Vector2(1f, 0.77f), 14f, 0f);

                _description = UIBuild.Text("Desc", panel.transform, "", UITheme.FontCaption,
                    UITheme.TextSecondary, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_description.rectTransform, new Vector2(0f, 0.24f), new Vector2(1f, 0.42f), 8f, 0f);

                _price = UIBuild.Text("Price", panel.transform, "", UITheme.FontHeading, UITheme.Accent,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(_price.rectTransform, new Vector2(0f, 0.12f), new Vector2(1f, 0.23f), 8f, 0f);

                _state = UIBuild.Text("State", panel.transform, "", UITheme.FontMicro, UITheme.TextMuted,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(_state.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.11f), 8f, 0f);
            }

            public void Bind(RewardDef good, int price, string blockedReason, bool selling)
            {
                _root.SetActive(good != null);
                if (good == null) return;

                Color rarity = UITheme.Rarity(Mathf.Max(1, good.tier));
                _rarityStrip.color = rarity;
                _name.text = good.displayName;
                _description.text = good.description;

                Sprite art = LoadArt(good);
                _art.sprite = art;
                _art.enabled = art != null;

                bool actionable = blockedReason == null;
                _price.text = $"{price:N0} G";
                _price.color = actionable ? UITheme.Accent : UITheme.TextMuted;
                _state.text = selling ? "판매" : actionable ? "구매" : blockedReason;
                _state.color = actionable ? UITheme.Positive : UITheme.TextMuted;
            }

            /// <summary>소모품은 <c>artPath</c>를, 장비는 <c>icon</c>을 본다.</summary>
            private static Sprite LoadArt(RewardDef good)
            {
                if (good.item != null) return ItemTooltip.LoadArt(good.item);
                return string.IsNullOrWhiteSpace(good.artPath) ? null : Resources.Load<Sprite>(good.artPath);
            }
        }
    }
}
