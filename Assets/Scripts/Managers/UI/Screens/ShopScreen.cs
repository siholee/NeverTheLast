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

        /// <summary>목록 한 줄의 높이(기준 해상도 px). 960×540에서도 두 줄 글자가 들어가는 높이다.</summary>
        private const float RowHeight = 112f;

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

        protected override string CanvasName => "ShopCanvas";
        protected override int SortingOrder => 72;
        protected override string Title => "상점";
        protected override string Caption => "SHOP";
        protected override Vector2 AnchorMin => new(0.08f, 0.14f);
        protected override Vector2 AnchorMax => new(0.92f, 0.86f);
        protected override bool CloseOnBackdrop => true;

        private readonly Dictionary<Tab, Button> _tabButtons = new();
        private readonly Dictionary<Tab, Image> _tabMarks = new();

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

            BuildStatusBar();
            BuildTabs();

            _guide = UIBuild.Text("TabGuide", _goodsRoot, "", UITheme.FontReadSmall, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineLeft, wrap: true);
            UIBuild.Anchor(_guide.rectTransform, new Vector2(0f, 0.755f), new Vector2(1f, 0.825f), 8f, 0f);

            _content = UIBuild.Container("Content", _goodsRoot);
            UIBuild.Anchor(_content, new Vector2(0f, 0.125f), new Vector2(1f, 0.75f));

            _message = UIBuild.Text("Message", _goodsRoot, "", UITheme.FontReadSmall, UITheme.Accent,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_message.rectTransform, new Vector2(0f, 0.07f), new Vector2(1f, 0.122f));

            _sellAll = UIBuild.Button("SellAll", _goodsRoot, "전부 팔기", OnSellAll, false, UITheme.FontReadSmall);
            UIBuild.Anchor(_sellAll.GetComponent<RectTransform>(), new Vector2(0.02f, 0f), new Vector2(0.26f, 0.065f));

            Button close = UIBuild.Button("Close", _goodsRoot, "닫기", Hide, primary: true, fontSize: UITheme.FontReadSmall);
            UIBuild.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.40f, 0f), new Vector2(0.60f, 0.065f));

            _targetPicker = new UnitTargetPicker(Body, OnTargetPicked, ShowGoods, "매대로 돌아가기");
            // 코드는 받을 아군 → (칸이 차면) 버릴 코드 순으로 판을 번갈아 연다. 보상 화면과 같은 흐름이다.
            _codeFlow = new RewardCodeFlow(Body, ShowGoods, SetTitle, "매대로 돌아가기");
        }

        /// <summary>왼쪽 기둥 — 행상인. 그림이 아직 없으면 이름표와 대사만 남는다.</summary>
        private void BuildMerchant()
        {
            _merchant = UIBuild.Container("Merchant", Body);
            UIBuild.Anchor(_merchant, Vector2.zero, new Vector2(0.18f, 1f));

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
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.35f));

            _merchantLine = UIBuild.Text("Line", _merchant, "", UITheme.FontReadSmall, UITheme.TextSecondary,
                TextAlignmentOptions.Top, wrap: true);
            UIBuild.Anchor(_merchantLine.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.27f), 8f, 0f);
        }

        /// <summary>맨 위 줄 — 보유 골드 · 시세 스테이지 · 남은 구매. 사기 전에 셋 다 눈에 들어와야 한다.</summary>
        private void BuildStatusBar()
        {
            Image bar = UIBuild.Panel("Status", _goodsRoot, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 8,
                UITheme.Outline, 1);
            UIBuild.Anchor(bar.rectTransform, new Vector2(0f, 0.915f), Vector2.one);

            _wallet = UIBuild.Text("Wallet", bar.transform, "", UITheme.FontReadHeading, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_wallet.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), 10f, 0f);
            _stageInfo = UIBuild.Text("Stage", bar.transform, "", UITheme.FontReadSmall, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_stageInfo.rectTransform, new Vector2(0.30f, 0f), new Vector2(0.68f, 1f), 10f, 0f);
            _purchases = UIBuild.Text("Purchases", bar.transform, "", UITheme.FontReadHeading, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_purchases.rectTransform, new Vector2(0.68f, 0f), new Vector2(1f, 1f), 10f, 0f);
        }

        private void BuildTabs()
        {
            float width = 1f / TabOrder.Length;
            for (int i = 0; i < TabOrder.Length; i++)
            {
                Tab tab = TabOrder[i].Tab;
                Button button = UIBuild.Button($"Tab{tab}", _goodsRoot, TabOrder[i].Label, () => SwitchTo(tab),
                    false, UITheme.FontReadHeading);
                UIBuild.Anchor(button.GetComponent<RectTransform>(),
                    new Vector2(i * width, 0.84f), new Vector2((i + 1) * width, 0.905f), 6f, 0f);
                _tabButtons[tab] = button;

                Image mark = UIBuild.Solid($"Tab{tab}Mark", _goodsRoot, UITheme.Accent);
                UIBuild.Anchor(mark.rectTransform,
                    new Vector2(i * width, 0.828f), new Vector2((i + 1) * width, 0.836f), 6f, 0f);
                _tabMarks[tab] = mark;
            }
        }

        public override void Show()
        {
            EnsureBuilt();
            _consumables = GameManager.Instance?.rewardManager?.BuildShopGoods() ?? new List<RewardDef>();
            EnsureSupplyStock();
            _codes = BuildCodeStock();
            _message.text = "";
            _tab = Tab.Code;
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

            return goods;
        }

        private void Refresh()
        {
            int stage = CurrentStage;
            int gold = Inventory?.Gold ?? 0;
            int left = GameManager.Instance?.ShopPurchasesLeft ?? 0;

            _wallet.text = $"◆ {gold:N0} G";
            _stageInfo.text = _tab == Tab.Sell
                ? $"주머니 {Inventory?.Valuables?.Count ?? 0}점 · 전부 팔면 {Inventory?.ValuableTotalGold ?? 0:N0} G"
                : $"{stage}스테이지 시세 · 가격 = 기본가 × {stage}";
            _purchases.text = $"남은 구매 {left} / {GameManager.MaxShopPurchases}";
            _purchases.color = left > 0 ? UITheme.TextPrimary : UITheme.Danger;
            _guide.text = TabGuide(_tab);

            foreach (KeyValuePair<Tab, Image> pair in _tabMarks) pair.Value.gameObject.SetActive(pair.Key == _tab);
            foreach (KeyValuePair<Tab, Button> pair in _tabButtons)
            {
                TextMeshProUGUI label = pair.Value.GetComponentInChildren<TextMeshProUGUI>();
                if (label == null) continue;
                label.color = pair.Key == _tab ? UITheme.AccentDim : UITheme.TextSecondary;
                label.fontStyle = pair.Key == _tab ? FontStyles.Bold : FontStyles.Normal;
            }

            _sellAll.gameObject.SetActive(_tab == Tab.Sell);
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
            Tab.Code => "생존 · 제어 저항처럼 운에 맡기면 안 되는 코드만 고정으로 판다. 화력 코드는 전투 보상에서 얻는다. " +
                        "받을 아군을 정한 뒤 값을 치른다. 활성 7칸이 차면 보관함에 들어간다.",
            Tab.Supply => "공용 보급에 들어가는 물건이다. 식량은 긴 휴식(40)에, 정화의 성수는 공용 보급에서 써 다음 전투 1회에 걸린다.",
            Tab.Sell => "주운 귀중품을 골드로 바꾼다. 판매는 구매 횟수를 쓰지 않는다.",
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
                int count = Inventory?.Valuables?.Count ?? 0;
                return count == 0
                    ? "파실 물건은 없네요… 그래도 들러주신 건 좋았어요."
                    : $"{count}점이네요! 감정은 정확해요. 저, 이런 건 잘하거든요…";
            }

            if (_tab == Tab.Code)
            {
                return _codes.Count == 0
                    ? "코드는 아직 안 들여왔어요. 곧 구해 올게요…!"
                    : "싸게 구할 수 있는 건 목숨 지키는 것뿐이에요. 화력은… 운에 맡기세요.";
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

        // ── 코드 매대 ────────────────────────────────────────────────

        /// <summary>
        /// 상점 코드 세 장을 나란히 크게. 효과 전문 · 태그 · 상위 코드 · 가격 · 아군별로 받을 수 있는지(칸 · 덮어쓰기 · 보유)를
        /// 카드 한 장에 다 적는다. 카드를 누르면 받을 아군 선택으로 넘어간다.
        /// </summary>
        private void BuildCodeShelf(int stage)
        {
            if (_codes.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _content, "진열된 코드가 없습니다.",
                    UITheme.FontReadBody, UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Stretch(empty.rectTransform);
                return;
            }

            List<Unit> party = RewardCodeFlow.Party();
            int columns = Mathf.Max(3, _codes.Count);
            float width = 1f / columns;
            for (int i = 0; i < _codes.Count; i++)
            {
                RewardDef good = _codes[i];
                int index = i;
                string blocked = BuyBlockReason(good);
                Color rarity = UITheme.Rarity(Mathf.Max(1, good.tier));

                Image card = UIBuild.Panel($"CodeGood_{i}", _content, UITheme.SurfaceRaised, UIShapes.Corner.Diagonal, 12,
                    blocked == null ? rarity : UITheme.Outline, blocked == null ? 2 : 1);
                UIBuild.Anchor(card.rectTransform, new Vector2(i * width, 0f), new Vector2((i + 1) * width, 1f), 8f, 0f);
                UIBuild.OnClick(card.gameObject, () => OnPick(index));

                Image strip = UIBuild.Solid("Rarity", card.transform, rarity);
                strip.raycastTarget = false;
                UIBuild.Anchor(strip.rectTransform, new Vector2(0f, 0.985f), Vector2.one, 6f, 0f);

                TextMeshProUGUI name = UIBuild.Text("Name", card.transform, good.displayName, UITheme.FontReadTitle,
                    UITheme.TextPrimary);
                name.fontStyle = FontStyles.Bold;
                name.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.895f), new Vector2(0.78f, 0.98f), 16f, 0f);

                TextMeshProUGUI tier = UIBuild.Text("Tier", card.transform, $"T{good.tier}", UITheme.FontReadHeading,
                    rarity, TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(tier.rectTransform, new Vector2(0.78f, 0.895f), new Vector2(1f, 0.98f), 16f, 0f);

                string tagText = TagText(good.codeId);
                TextMeshProUGUI tags = UIBuild.Text("Tags", card.transform, tagText.Length > 0 ? $"#{tagText.Replace(" · ", "  #")}" : "",
                    UITheme.FontReadSmall, UITheme.Accent);
                tags.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(tags.rectTransform, new Vector2(0f, 0.835f), new Vector2(1f, 0.895f), 16f, 0f);

                TextMeshProUGUI effect = UIBuild.Text("Effect", card.transform, CodeCatalog.Summary(good.description),
                    UITheme.FontReadBody, UITheme.TextPrimary, TextAlignmentOptions.TopLeft, wrap: true);
                effect.richText = false;
                UIBuild.Anchor(effect.rectTransform, new Vector2(0f, 0.585f), new Vector2(1f, 0.83f), 16f, 0f);

                int upper = RewardCodeCatalog.UpperOf(good.codeId);
                TextMeshProUGUI upperLabel = UIBuild.Text("Upper", card.transform,
                    upper > 0 ? $"▲ 상위: {RewardCodeFlow.NameOf(upper)} (T{RewardCodeCatalog.TierOf(upper)})" : "상위 코드 없음",
                    UITheme.FontReadSmall, UITheme.TextMuted);
                upperLabel.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(upperLabel.rectTransform, new Vector2(0f, 0.53f), new Vector2(1f, 0.585f), 16f, 0f);

                int receivable = party.Count(unit => unit.CanReceiveRewardCode(good.codeId, out _, out _, out _));
                TextMeshProUGUI receiversHead = UIBuild.Text("ReceiversHead", card.transform,
                    $"받을 수 있는 아군 {receivable} / {party.Count}", UITheme.FontReadSmall,
                    receivable > 0 ? UITheme.Positive : UITheme.Danger);
                UIBuild.Anchor(receiversHead.rectTransform, new Vector2(0f, 0.465f), new Vector2(1f, 0.525f), 16f, 0f);

                TextMeshProUGUI receivers = UIBuild.Text("Receivers", card.transform, ReceiverLines(good.codeId, party),
                    UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, wrap: true);
                receivers.richText = true;
                UIBuild.Anchor(receivers.rectTransform, new Vector2(0f, 0.19f), new Vector2(1f, 0.46f), 16f, 0f);

                TextMeshProUGUI price = UIBuild.Text("Price", card.transform, $"{ShopPriceOf(good, stage):N0} G",
                    UITheme.FontReadTitle, blocked == null ? UITheme.AccentDim : UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Anchor(price.rectTransform, new Vector2(0f, 0.105f), new Vector2(1f, 0.185f), 16f, 0f);

                StateBand(card.transform, blocked == null ? "구매 — 받을 아군 고르기" : blocked, blocked == null,
                    new Vector2(0f, 0.015f), new Vector2(1f, 0.1f));

                TooltipTrigger.Attach(card.gameObject, () => good.displayName, () => CodeTooltipLines(good), rarity);
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
                    selling ? "팔 귀중품이 없습니다. 적이 떨어뜨린 귀중품이 주머니에 모입니다." : "진열된 물건이 없습니다.",
                    UITheme.FontReadBody, UITheme.TextMuted, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Stretch(empty.rectTransform, 24f, 0f);
                return;
            }

            RectTransform list = UIBuild.ScrollArea("GoodsScroll", _content, out ScrollRect scroll);
            UIBuild.Stretch((RectTransform)scroll.transform);
            for (int i = 0; i < goods.Count; i++)
            {
                RewardDef good = goods[i];
                int index = i;
                string blocked = selling ? null : BuyBlockReason(good);
                int price = selling ? good.goldCost : ShopPriceOf(good, stage);
                Color rarity = UITheme.Rarity(Mathf.Max(1, good.tier));

                Image row = UIBuild.Panel($"Good_{i}", list, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10,
                    UITheme.Outline, 1);
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.sizeDelta = new Vector2(-16f, RowHeight - 8f);
                row.rectTransform.anchoredPosition = new Vector2(-4f, -i * RowHeight);
                UIBuild.OnClick(row.gameObject, () => OnPick(index));

                Image strip = UIBuild.Solid("Rarity", row.transform, rarity);
                strip.raycastTarget = false;
                UIBuild.Anchor(strip.rectTransform, new Vector2(0f, 0.1f), new Vector2(0f, 0.9f));
                strip.rectTransform.offsetMax = new Vector2(5f, 0f);

                var artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(row.transform, false);
                Image art = artObject.GetComponent<Image>();
                art.preserveAspect = true;
                art.raycastTarget = false;
                Sprite sprite = LoadArt(good);
                art.sprite = sprite;
                art.enabled = sprite != null;
                art.color = blocked == null ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                UIBuild.Pin(art.rectTransform, new Vector2(0f, 0.5f), new Vector2(84f, 84f), new Vector2(16f, 0f));

                TextMeshProUGUI name = UIBuild.Text("Name", row.transform, good.displayName, UITheme.FontReadBody,
                    UITheme.TextPrimary);
                name.fontStyle = FontStyles.Bold;
                name.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0.52f), new Vector2(0.60f, 0.95f));
                name.rectTransform.offsetMin = new Vector2(112f, 0f);

                TextMeshProUGUI desc = UIBuild.Text("Desc", row.transform, $"T{good.tier} · {DescriptionOf(good)}",
                    UITheme.FontReadSmall, UITheme.TextSecondary);
                desc.richText = false;
                desc.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(desc.rectTransform, new Vector2(0f, 0.06f), new Vector2(0.60f, 0.50f));
                desc.rectTransform.offsetMin = new Vector2(112f, 0f);

                TextMeshProUGUI priceLabel = UIBuild.Text("Price", row.transform, $"{price:N0} G", UITheme.FontReadHeading,
                    blocked == null ? UITheme.AccentDim : UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(priceLabel.rectTransform, new Vector2(0.60f, 0f), new Vector2(0.77f, 1f), 8f, 0f);

                StateBand(row.transform, selling ? "판매" : blocked ?? "구매", blocked == null,
                    new Vector2(0.785f, 0.18f), new Vector2(0.99f, 0.82f));

                if (good.item != null) ItemTooltip.Attach(row.gameObject, good.item);
            }

            list.sizeDelta = new Vector2(0f, goods.Count * RowHeight + 8f);
        }

        /// <summary>목록 줄의 설명. 공용 보급 물건은 효과 문구를, 나머지는 데이터 설명을 쓴다.</summary>
        private static string DescriptionOf(RewardDef good)
        {
            if (good.item != null && !good.item.IsValuable)
            {
                string effect = ItemTooltip.EffectLine(good.item);
                if (effect.Length > 0) return effect;
            }
            return good.description ?? "";
        }

        /// <summary>구매 상태 · 막힌 이유 띠. 이유가 가격보다 먼저 눈에 들어와야 헛클릭이 준다.</summary>
        private static void StateBand(Transform parent, string text, bool actionable, Vector2 min, Vector2 max)
        {
            Image band = UIBuild.Panel("StateBand", parent,
                actionable ? new Color(UITheme.Positive.r, UITheme.Positive.g, UITheme.Positive.b, 0.10f)
                    : new Color(1f, 0.804f, 0.239f, 0.16f),
                UIShapes.Corner.Diagonal, 6, actionable ? UITheme.Positive : UITheme.Outline, 1);
            band.raycastTarget = false;
            UIBuild.Anchor(band.rectTransform, min, max, 10f, 0f);
            TextMeshProUGUI state = UIBuild.Text("State", band.transform, text, UITheme.FontReadSmall,
                actionable ? UITheme.Positive : UITheme.Danger, TextAlignmentOptions.Center, wrap: true);
            state.fontStyle = FontStyles.Bold;
            UIBuild.Stretch(state.rectTransform, 6f, 1f);
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
