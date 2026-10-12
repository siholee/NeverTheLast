using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    [System.Serializable]
    public class RewardDef
    {
        public string id;
        public string displayName;
        public string description;
        public int tier = 1;
        public List<int> themeIds;
        public bool isRare;
        public int healAmount;
        /// <summary>대상 한 명의 최대 체력에 대한 회복 비율(0~1).</summary>
        public float healPercent;
        /// <summary>대상 한 명을 완전히 회복한다.</summary>
        public bool fullHealTarget;
        /// <summary>쓰러진 대상 한 명을 최대 체력 대비 비율(0~1)만큼 되살린다.</summary>
        public float revivePercent;
        public int rerollTicketBonus;
        public int randomTokenAmount;

        /// <summary>
        /// 올려 주는 레벨 수. 0이면 사탕이 아니다.
        /// <see cref="levelGrantParty"/>가 false면 한 명을 지정하고, true면 파티 전원에게 준다.
        /// </summary>
        public int levelGrant;

        /// <summary>사탕을 파티 전원에게 먹인다. 대상 선택이 없어진다.</summary>
        public bool levelGrantParty;

        /// <summary>
        /// 상점 기본가. 0이면 상점에 올리지 않는다.
        /// 실제 가격은 <see cref="RewardManager.ShopPrice"/>가 스테이지를 곱해 정한다.
        /// </summary>
        public int goldCost;
        public bool fullHealParty;
        /// <summary>쓰러진 모든 아군을 최대 체력으로 되살린다.</summary>
        public bool fullReviveParty;
        /// <summary>Resources 기준 보상 일러스트 경로.</summary>
        public string artPath;
        public int itemId;

        /// <summary>보상 코드 ID(패시브 500~999). 0이면 코드 보상이 아니다. 받을 아군을 고른다(Detail_17 §2.1).</summary>
        public int codeId;
        [System.NonSerialized] public Unit saleOwner;
        public string saleInstanceId;

        /// <summary>경험의 서 — 다음 레벨 필요 EXP 대비 비율(0.5 = 50%).</summary>
        public float expPercentOfNext;
        /// <summary>경험의 서를 파티 전원(쓰러진 아군 포함)에게 쓴다(상권).</summary>
        public bool expParty;
        /// <summary>휴식의 향로 — 짧은 휴식 횟수를 이만큼 늘린다(상한 GameManager.MaxShortRestChargesWithIncense).</summary>
        public int shortRestChargeGrant;

        /// <summary>레벨을 올려 주는 보상인가.</summary>
        public bool IsLevelGrant => levelGrant > 0;

        public bool IsCodeReward => codeId > 0;
        public bool IsExpGrant => expPercentOfNext > 0f;

        public bool IsConsumable => itemId <= 0 || item?.IsFood == true;

        public bool RequiresHealingTargetSelection => healPercent > 0f || fullHealTarget;
        public bool RequiresReviveTargetSelection => revivePercent > 0f;

        /// <summary>한 명을 지정하는 사탕. 전체 사탕은 고를 것이 없다.</summary>
        public bool RequiresLevelTargetSelection => (IsLevelGrant && !levelGrantParty) || (IsExpGrant && !expParty);

        public bool RequiresCodeTargetSelection => IsCodeReward;

        public bool RequiresTargetSelection =>
            RequiresHealingTargetSelection || RequiresReviveTargetSelection || RequiresLevelTargetSelection ||
            RequiresCodeTargetSelection;
        public bool IsHealingReward => RequiresHealingTargetSelection || fullHealParty || healAmount > 0;
        public bool IsRevivalReward => RequiresReviveTargetSelection || fullReviveParty;

        /// <summary>공용 인벤토리 아이템 보상의 원본 데이터.</summary>
        public ItemData item;
    }

    public class RewardManager : MonoBehaviour
    {
        /// <summary>보상 3택의 기본 장수.</summary>
        public const int BaseRewardCount = 3;

        /// <summary>
        /// 이만큼 스테이지를 넘길 때마다 보상 카드가 한 장 는다.
        /// 포켓로그는 200웨이브 동안 50웨이브마다 황금 몬스터볼로 선택지를 +1(최대 +3) 한다.
        /// 우리 런은 100스테이지이므로 같은 비율로 25스테이지마다 +1이다.
        /// </summary>
        public const int RewardCountStageStep = 25;

        /// <summary>보상 카드 최대 장수. 포켓로그와 같이 기본 3 + 3.</summary>
        public const int MaxRewardCount = 6;

        /// <summary>
        /// 이 스테이지를 이긴 뒤 보여 줄 보상 장수. 1~24: 3 · 25~49: 4 · 50~74: 5 · 75~: 6.
        /// 무한 모드도 6에서 멈춘다.
        /// </summary>
        public static int RewardCountForStage(int stage)
            => Mathf.Clamp(BaseRewardCount + Mathf.Max(0, stage) / RewardCountStageStep, BaseRewardCount, MaxRewardCount);


        public static RewardManager Instance { get; private set; }

        public static void DestroyInstance()
        {
            if (Instance == null) return;

            var instance = Instance;
            Instance = null;
            Destroy(instance.gameObject);
        }

        private RewardDataList _rewardDataList;
        private ItemDataList _itemDataList;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 전투 보상 3택.
        ///
        /// <b>후보는 이번 전투에서 처치한 적의 드랍 + 공용 드랍 + 소모품을 한 풀로 합친 것이다.</b>
        /// 그 풀에서 라운드 티어 확률로 티어를 굴려 뽑으므로, 적 드랍이 안 나올 수도 소모품이 안 나올 수도 있다.
        /// 소모품 장수에는 상한이 없다 — 드랍과 소모품은 같은 풀의 동등한 후보다.
        /// 식량은 적별 드랍표와 공용 드랍표 양쪽에서 공급되며 공용 인벤토리에 바로 쌓인다.
        /// </summary>
        /// <summary>보상 풀의 세 분류(Detail_17 §5.1).</summary>
        public enum RewardCategory { Code, Consumable, Drop }

        /// <summary>
        /// 전투 보상 카드. 카드마다 ① 라운드 티어를 굴리고 ② 분류(코드 · 소모품 · 적 드랍)를 굴려 ③ 그 분류 · 티어에서 뽑는다.
        /// 코드는 태그 가중치(파티 태그와 겹침 ×2 · 범용 ×1 · 그 밖 ×0.5)와 상위 가중치를 받는다.
        /// 후보가 없으면 같은 티어의 다른 분류 → 가까운 티어 순으로 물러나고, 선택지 중 최소 1장은 코드다.
        /// </summary>
        public List<RewardDef> GenerateRewards(int count, int rewardRound, IEnumerable<int> killedEnemyIds)
        {
            EnsureRewardData();
            _itemDataList ??= GameManager.Instance?.itemDataList ?? GameManager.Instance?.dataManager?.FetchItemDataList();

            var pools = new Dictionary<RewardCategory, List<RewardDef>>
            {
                [RewardCategory.Code] = BuildCodePool(),
                [RewardCategory.Consumable] = BuildConsumablePool(),
                [RewardCategory.Drop] = BuildDropPool(killedEnemyIds),
            };
            var result = new List<RewardDef>();

            for (int i = 0; i < count && pools.Values.Any(pool => pool.Count > 0); i++)
            {
                int tier = RollTier(rewardRound);
                RewardCategory category = RollCategory();
                RewardDef reward = PickFrom(pools, category, tier);
                if (reward == null) continue;
                result.Add(reward);
                RemoveEverywhere(pools, reward);
            }

            // 코드 보장 — 코드가 하나도 없으면 코드가 아닌 카드 한 장을 같은 티어 근처의 코드로 바꾼다.
            if (result.Count > 0 && result.All(reward => !reward.IsCodeReward) && pools[RewardCategory.Code].Count > 0)
            {
                int replaceIndex = UnityEngine.Random.Range(0, result.Count);
                RewardDef code = PickWithinCategory(pools[RewardCategory.Code], result[replaceIndex].tier, nearest: true);
                if (code != null) result[replaceIndex] = code;
            }

            return result;
        }

        /// <summary>같은 선택을 다시 굴린다(운명의 주사위 · 리롤 티켓). 처치 기록은 그대로 쓴다.</summary>
        public List<RewardDef> RerollRewards(int count, int rewardRound)
            => GenerateRewards(count, rewardRound, GameManager.Instance?.BattleKilledEnemyIds);

        private RewardCategory RollCategory()
        {
            Dictionary<string, float> weights = _rewardDataList?.rewardCategoryWeights;
            float code = Weight(weights, "code", 55f);
            float consumable = Weight(weights, "consumable", 30f);
            float drop = Weight(weights, "drop", 15f);
            float roll = UnityEngine.Random.value * Mathf.Max(0.0001f, code + consumable + drop);
            if (roll < code) return RewardCategory.Code;
            return roll < code + consumable ? RewardCategory.Consumable : RewardCategory.Drop;
        }

        private static float Weight(Dictionary<string, float> table, string key, float fallback)
            => table != null && table.TryGetValue(key, out float value) ? Mathf.Max(0f, value) : fallback;

        /// <summary>정한 분류 · 티어 → 같은 티어 다른 분류 → 가까운 티어(전 분류) 순으로 물러난다.</summary>
        private RewardDef PickFrom(Dictionary<RewardCategory, List<RewardDef>> pools, RewardCategory category, int tier)
        {
            RewardDef exact = PickWithinCategory(pools[category], tier, nearest: false);
            if (exact != null) return exact;

            foreach (RewardCategory other in pools.Keys.Where(key => key != category).OrderBy(_ => UnityEngine.Random.value))
            {
                RewardDef sameTier = PickWithinCategory(pools[other], tier, nearest: false);
                if (sameTier != null) return sameTier;
            }

            List<RewardDef> all = pools.Values.SelectMany(pool => pool).ToList();
            return PickWithinCategory(all, tier, nearest: true);
        }

        private RewardDef PickWithinCategory(List<RewardDef> pool, int tier, bool nearest)
        {
            if (pool == null || pool.Count == 0) return null;
            List<RewardDef> candidates = pool.Where(reward => reward.tier == tier).ToList();
            if (candidates.Count == 0 && nearest)
            {
                int closest = pool.Min(reward => Mathf.Abs(reward.tier - tier));
                candidates = pool.Where(reward => Mathf.Abs(reward.tier - tier) == closest).ToList();
            }
            if (candidates.Count == 0) return null;
            return WeightedPick(candidates);
        }

        private RewardDef WeightedPick(List<RewardDef> candidates)
        {
            float total = candidates.Sum(CodeWeight);
            float roll = UnityEngine.Random.value * total;
            foreach (RewardDef candidate in candidates)
            {
                roll -= CodeWeight(candidate);
                if (roll <= 0f) return candidate;
            }
            return candidates[candidates.Count - 1];
        }

        /// <summary>코드 후보의 태그 · 상위 가중치. 코드가 아니면 1이다.</summary>
        private float CodeWeight(RewardDef reward)
        {
            if (reward == null || !reward.IsCodeReward) return 1f;
            Dictionary<string, float> weights = _rewardDataList?.codeTagWeights;
            IReadOnlyList<string> tags = Codes.Base.RewardCodeCatalog.TagsOf(reward.codeId);
            HashSet<string> partyTags = PartyArchetypeTags();

            float weight;
            if (tags.Any(tag => partyTags.Contains(tag))) weight = Weight(weights, "match", 2f);
            else if (tags.Any(tag => string.Equals(tag, "Universal", System.StringComparison.OrdinalIgnoreCase)))
                weight = Weight(weights, "universal", 1f);
            else weight = Weight(weights, "other", 0.5f);

            bool partyHoldsLower = Codes.Base.RewardCodeCatalog.All()
                .Where(entry => Codes.Base.RewardCodeCatalog.IsUpperOf(reward.codeId, entry.id))
                .Any(entry => PartyHeroes().Any(hero => hero.RewardCodeIds.Contains(entry.id)));
            if (partyHoldsLower) weight *= Mathf.Max(0f, _rewardDataList?.upperCodeWeight ?? 1.5f);
            return Mathf.Max(0.0001f, weight);
        }

        private static void RemoveEverywhere(Dictionary<RewardCategory, List<RewardDef>> pools, RewardDef reward)
        {
            foreach (List<RewardDef> pool in pools.Values) pool.Remove(reward);
        }

        /// <summary>파티(쓰러진 아군 포함, 소환수 제외). 코드 후보 판정 · 가중치가 쓴다.</summary>
        private static List<Unit> PartyHeroes()
            => Combat.AttackForms.PartyUnits().ToList();

        private static HashSet<string> PartyArchetypeTags()
        {
            var tags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Unit hero in PartyHeroes())
                foreach (string tag in hero.ArchetypeTags) tags.Add(tag);
            return tags;
        }

        /// <summary>
        /// 지금 파티에 낼 수 있는 보상 코드. 미구현 · 파티 전원이 이미 가진 것(또는 그 상위) · 형태/성장 조건 미달은 뺀다.
        /// </summary>
        public List<RewardDef> BuildCodePool()
        {
            List<Unit> party = PartyHeroes();
            if (party.Count == 0) return new List<RewardDef>();
            bool hasGrowth = party.Any(hero => hero.HasArchetypeTag("Growth"));
            bool hasTreasury = party.Any(hero => hero.HasArchetypeTag("Treasury"));
            bool hasToughness = party.Any(hero => hero.ActivePassiveCodesResolved().Any(code => code is Codes.Passive.OrpheusCheatedDeath));

            return Codes.Base.RewardCodeCatalog.All()
                .Where(entry => Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.id))
                .Where(entry => entry.requiresAttackForm <= 0 || Combat.AttackForms.PartyHas(entry.requiresAttackForm))
                .Where(entry => !entry.requiresGrowth || hasGrowth)
                .Where(entry => !entry.requiresTreasury || hasTreasury)
                .Where(entry => !entry.requiresToughness || hasToughness)
                .Where(entry => party.Any(hero => hero.CanReceiveRewardCode(entry.id, out _, out _, out _)))
                .Select(BuildCodeReward)
                .ToList();
        }

        public static RewardDef BuildCodeReward(Codes.Base.CodeCatalog.Entry entry)
        {
            return new RewardDef
            {
                id = $"code_{entry.id}",
                codeId = entry.id,
                displayName = entry.verbalName,
                description = entry.description,
                tier = Mathf.Clamp(entry.tier, 1, 5),
                goldCost = entry.shopFixed ? entry.shopPrice : 0,
            };
        }

        /// <summary>소모품 분류 — 즉시 보상(90_rewards) + 보상 풀에 오르는 비약 · 코드 도구(40_items).</summary>
        private List<RewardDef> BuildConsumablePool()
        {
            var pool = new List<RewardDef>();
            if (_rewardDataList?.rewards != null)
            {
                pool.AddRange(_rewardDataList.rewards.Where(reward =>
                    reward != null &&
                    (!reward.IsHealingReward || HasInjuredHero()) &&
                    (!reward.IsRevivalReward || HasFallenHero()) &&
                    // 써서 달라지는 것만 — 짧은 휴식이 상한이면 휴식의 향로는 고를 수 없다.
                    (reward.shortRestChargeGrant <= 0 ||
                     (GameManager.Instance?.ShortRestCharges ?? 0) < GameManager.MaxShortRestChargesWithIncense)));
            }
            pool.AddRange((_itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && item.rewardPool && !item.eventOnly)
                .Select(BuildItemReward));
            return pool;
        }

        /// <summary>적 드랍 분류 — 이번 전투 처치한 적의 드랍표 + 공용 드랍(식량 · 귀중품).</summary>
        private List<RewardDef> BuildDropPool(IEnumerable<int> killedEnemyIds)
        {
            var dropIds = new HashSet<int>(_rewardDataList?.commonDropItemIds ?? new List<int>());
            foreach (int itemId in EnemyDropIds(killedEnemyIds)) dropIds.Add(itemId);

            return (_itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && dropIds.Contains(item.id) && !item.eventOnly)
                .Select(BuildItemReward)
                .ToList();
        }

        private static RewardDef BuildItemReward(ItemData item)
        {
            return new RewardDef
            {
                id = $"item_{item.id}",
                itemId = item.id,
                displayName = item.name,
                description = BuildItemDescription(item),
                tier = Mathf.Clamp(item.rarity, 1, 5),
                item = item,
            };
        }

        /// <summary>처치한 적들의 드랍표를 합친다. 알 수 없는 적 ID는 건너뛴다.</summary>
        private static IEnumerable<int> EnemyDropIds(IEnumerable<int> killedEnemyIds)
        {
            if (killedEnemyIds == null) yield break;
            List<EnemyData> enemies = GameManager.Instance?.dataManager?.FetchEnemyDataList()?.enemies;
            if (enemies == null) yield break;

            foreach (int enemyId in killedEnemyIds.Distinct())
            {
                EnemyData data = enemies.FirstOrDefault(enemy => enemy.id == enemyId);
                if (data?.drops == null) continue;
                foreach (int itemId in data.drops) yield return itemId;
            }
        }

        // ── 상점 ─────────────────────────────────────────────────────

        /// <summary>
        /// 상점 소모품 매대. <c>goldCost</c>를 가진 보상만 올라간다.
        /// 순서는 yaml에 적힌 순서를 그대로 따른다(회복약 → 부활약, 티어 오름차순).
        /// </summary>
        public List<RewardDef> BuildShopGoods()
        {
            EnsureRewardData();
            return _rewardDataList?.rewards?
                .Where(reward => reward != null && reward.goldCost > 0)
                .ToList() ?? new List<RewardDef>();
        }

        /// <summary>
        /// 판매가. <b>기본가 × 현재 스테이지</b>다.
        ///
        /// 골드 수입도 스테이지에 비례하므로(처치 25×스테이지, 승리 50+25×스테이지),
        /// 이렇게 곱해 두면 <b>구매력이 런 내내 일정</b>하다. 고정가로 두면 후반에
        /// 모든 물건이 공짜나 다름없어진다.
        /// </summary>
        public static int ShopPrice(RewardDef reward, int stage)
        {
            if (reward == null) return 0;
            int basePrice = reward.goldCost;
            if (basePrice <= 0 && reward.item != null) basePrice = reward.item.shopPrice;
            if (basePrice <= 0 && reward.IsCodeReward)
                basePrice = Codes.Base.RewardCodeCatalog.Find(reward.codeId)?.shopPrice ?? 0;
            if (basePrice <= 0) basePrice = TierBasePrice(reward.tier);
            return (int)Math.Min(int.MaxValue, (long)basePrice * Mathf.Max(1, stage));
        }

        /// <summary>
        /// 사탕을 먹인다. 지정 사탕은 그 한 명에게, 전체 사탕은 파티 전원에게.
        ///
        /// 쓰러진 아군도 레벨을 받는다 — 사탕이 부활약을 겸하지는 않지만, 한 판 진 것 때문에
        /// 성장에서만 뒤처지면 그 격차를 되돌릴 방법이 없기 때문이다.
        /// </summary>
        private static void ApplyLevelGrant(RewardDef reward, Unit target)
        {
            if (reward.levelGrantParty)
            {
                List<Unit> party = LevelGrantCandidates();
                foreach (Unit hero in party) hero.GrantLevels(reward.levelGrant);
                Debug.Log($"[보상] {reward.displayName} — 아군 {party.Count}명이 레벨 +{reward.levelGrant}");
                return;
            }

            target?.GrantLevels(reward.levelGrant);
        }

        /// <summary>사탕을 먹을 수 있는 아군. 전투 불능도 포함한다.</summary>
        public static List<Unit> LevelGrantCandidates()
        {
            return GridManager.Instance?.heroList?
                .Where(IsValidLevelTarget)
                .ToList() ?? new List<Unit>();
        }

        public static bool IsValidLevelTarget(Unit unit)
            => unit != null && !unit.IsEnemy &&
               (unit.ID > 0 || unit.LastActiveId > 0);

        /// <summary>
        /// 아이템 보상. 귀중품은 입는 물건이 아니라 <b>주머니</b>로 간다.
        /// 값은 지금 스테이지로 확정해 함께 넣는다.
        /// </summary>
        private bool ApplyItemReward(RewardDef reward, Unit target)
        {
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            if (inventory == null) return false;

            ItemData data = reward.item ?? FindItem(reward.itemId);
            if (data != null && data.IsValuable)
            {
                inventory.AddValuable(data.id, ValuablePrice(data, CurrentShopStage));
                return true;
            }

            return inventory.AddItem(reward.itemId);
        }

        private ItemData FindItem(int itemId)
        {
            _itemDataList ??= GameManager.Instance?.itemDataList
                              ?? GameManager.Instance?.dataManager?.FetchItemDataList();
            return _itemDataList?.items?.FirstOrDefault(item => item != null && item.id == itemId);
        }

        private static int CurrentShopStage => Mathf.Max(1, GameManager.Instance?.RoundManager?.Stage ?? 1);

        /// <summary>귀중품을 팔아 받는 골드. 상점 가격과 같은 축(기본가 × 스테이지)이다.</summary>
        public static int ValuablePrice(ItemData item, int stage)
            => item == null || item.sellPrice <= 0 ? 0 : item.sellPrice * Mathf.Max(1, stage);

        /// <summary>상점 공용 아이템 매대. 현재는 긴 휴식용 식량이 오른다.</summary>
        public List<RewardDef> BuildShopItems()
        {
            _itemDataList ??= GameManager.Instance?.itemDataList
                              ?? GameManager.Instance?.dataManager?.FetchItemDataList();
            return (_itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && item.shopPrice > 0 && !item.eventOnly)
                .Select(item => new RewardDef
                {
                    id = $"item_{item.id}",
                    itemId = item.id,
                    displayName = item.name,
                    description = BuildItemDescription(item),
                    tier = Mathf.Clamp(item.rarity, 1, 5),
                    goldCost = item.shopPrice,
                    item = item,
                })
                .ToList();
        }

        private static bool HasInjuredHero()
        {
            return GridManager.Instance?.heroList?.Any(hero =>
                hero != null && hero.isActive && !hero.IsEnemy && hero.HpCurr < hero.HpMax) == true;
        }

        private static bool HasFallenHero()
        {
            return GridManager.Instance?.heroList?.Any(IsValidReviveTarget) == true;
        }

        private static string BuildItemDescription(ItemData item)
        {
            if (item.IsValuable) return $"귀중품 | 판매가 {ValuablePrice(item, CurrentShopStage):N0}G";
            if (item.IsFood) return $"공용 식량 +{item.foodValue}";
            string effect=UI.Core.ItemTooltip.EffectLine(item);
            return string.IsNullOrEmpty(effect)?UI.Core.ItemTooltip.CategoryName(item.category):effect;
        }

        private void EnsureRewardData()
        {
            if (_rewardDataList != null) return;
            _rewardDataList = GameManager.Instance?.dataManager?.FetchRewardDataList();
        }

        private int RollTier(int rewardRound)
        {
            EnsureRewardData();
            List<RewardTierOddsData> oddsTable = _rewardDataList?.rewardTierOdds;
            if (oddsTable == null || oddsTable.Count == 0)
            {
                return UnityEngine.Random.value < 0.25f ? 2 : 1;
            }

            // 확률표는 10라운드까지만 있다. 그 뒤로는 10라운드 행을 계속 쓴다.
            int cappedRound = Mathf.Min(rewardRound, 10);
            RewardTierOddsData odds = oddsTable
                .Where(row => row.round <= cappedRound)
                .OrderByDescending(row => row.round)
                .FirstOrDefault() ?? oddsTable[0];

            if (odds.tierWeights == null || odds.tierWeights.Count == 0)
            {
                return 1;
            }

            int totalWeight = odds.tierWeights.Sum();
            if (totalWeight <= 0)
            {
                return 1;
            }

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int current = 0;
            for (int i = 0; i < odds.tierWeights.Count; i++)
            {
                current += odds.tierWeights[i];
                if (roll < current)
                {
                    return i + 1;
                }
            }

            return 1;
        }

        private List<RewardDef> _selection;
        private readonly HashSet<RewardDef> _claimed = new();
        private bool _royalSelection, _applying;
        public bool FreeRewardClaimed { get; private set; }
        public int PaidRewardCount { get; private set; }
        public bool RoyalSelection => _selection != null && _royalSelection;
        public bool CanRerollSelection => _selection != null && !FreeRewardClaimed && !_applying;
        public static int TierBasePrice(int tier) => new[] { 75, 150, 300, 600, 1200 }[Mathf.Clamp(tier, 1, 5) - 1];
        public void BeginRewardSelection(List<RewardDef> rewards)
        {
            if (ReferenceEquals(_selection, rewards)) return;
            // 받은 뒤에는 다른 목록으로 갈아 끼워 무료 선택을 복제할 수 없다.
            if (_selection != null && FreeRewardClaimed) return;
            _selection = rewards; _claimed.Clear(); FreeRewardClaimed = false; PaidRewardCount = 0;
            _royalSelection = Combat.PartyTreasury.RoyalPurchaseEntitled;
        }
        public bool IsClaimed(RewardDef reward) => reward != null && _claimed.Contains(reward);
        public int SelectionPrice(RewardDef reward) => FreeRewardClaimed ? ShopPrice(reward, CurrentShopStage) : 0;
        public bool CanClaimReward(RewardDef reward)
            => !_applying && _selection != null && _selection.Contains(reward) && !IsClaimed(reward) &&
               (!FreeRewardClaimed || (_royalSelection && PaidRewardCount < 2)) &&
               (!FreeRewardClaimed || GameManager.Instance?.inventoryManager?.Gold >= SelectionPrice(reward));
        public bool TryClaimReward(RewardDef reward, Unit target = null, int overwriteCodeId = 0)
        {
            if (GameManager.Instance?.gameState != BaseEnums.GameState.RewardSelection || !CanClaimReward(reward)) return false;
            _applying = true;
            int price = SelectionPrice(reward); bool paid = FreeRewardClaimed;
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            try
            {
                if (paid && (inventory == null || !inventory.TrySpendGold(price))) return false;
                if (!ApplyRewardEffect(reward, target, overwriteCodeId))
                { if (paid) inventory.AddGold(price); return false; }
                _claimed.Add(reward); FreeRewardClaimed = true; if (paid) PaidRewardCount++;
                AudioManager.PlayCue("ui.reward.select"); return true;
            }
            finally { _applying = false; }
        }
        public bool FinishRewardSelection()
        {
            if (_applying || _selection == null || !FreeRewardClaimed || GameManager.Instance?.gameState != BaseEnums.GameState.RewardSelection) return false;
            _selection = null; _claimed.Clear(); _royalSelection = false;
            Combat.PartyTreasury.Clear();
            GameManager.Instance?.uiManager?.HideRewardPanel();
            RunManager.Instance?.AdvanceToNextStage(); return true;
        }
        public void ResetRewardSelection()
        { _selection = null; _claimed.Clear(); FreeRewardClaimed = false; PaidRewardCount = 0; _royalSelection = false; }

        /// <summary>무료 선택 또는 남은 카드 구매. 구매권이 있으면 명시적으로 마칠 때까지 목록을 유지한다.</summary>
        public void ApplyReward(RewardDef reward, Unit target = null, int overwriteCodeId = 0)
        {
            if (!TryClaimReward(reward, target, overwriteCodeId)) return;
            if (!RoyalSelection) FinishRewardSelection();
        }

        /// <summary>
        /// 보상 효과만 적용한다. 상점 구매도 같은 통로를 쓴다 — 상점에서 산 회복약과
        /// 보상으로 받은 회복약이 다르게 동작하면 안 되기 때문이다.
        /// 대상이 맞지 않으면 아무것도 하지 않고 false를 돌려준다.
        /// </summary>
        public bool ApplyRewardEffect(RewardDef reward, Unit target = null, int overwriteCodeId = 0)
        {
            if (reward == null) return false;

            // 보상 코드 — 칸 · 상위 대체 규칙은 Unit이 지킨다. 칸이 찼는데 덮어쓸 코드를 안 골랐으면 실패한다.
            if (reward.IsCodeReward)
            {
                if (target == null || target.IsEnemy || target.IsSummon)
                {
                    Debug.LogWarning($"[보상] {reward.displayName}은(는) 받을 아군이 필요합니다.");
                    return false;
                }
                return target.TryGrantRewardCode(reward.codeId, overwriteCodeId);
            }

            if (reward.IsExpGrant)
            {
                List<Unit> recipients = reward.expParty ? LevelGrantCandidates() : new List<Unit> { target };
                foreach (Unit hero in recipients.Where(IsValidLevelTarget))
                    hero.AddExp(Mathf.Max(1, Mathf.RoundToInt(hero.ExpToNextLevel * reward.expPercentOfNext)));
                return recipients.Any(IsValidLevelTarget);
            }

            if (reward.shortRestChargeGrant > 0)
            {
                return GameManager.Instance != null && GameManager.Instance.TryGrantShortRestCharges(reward.shortRestChargeGrant);
            }

            if (reward.RequiresHealingTargetSelection && !IsValidHealingTarget(target))
            {
                Debug.LogWarning($"[보상] {reward.displayName}은(는) 회복할 아군 대상이 필요합니다.");
                return false;
            }
            if (reward.RequiresReviveTargetSelection && !IsValidReviveTarget(target))
            {
                Debug.LogWarning($"[보상] {reward.displayName}은(는) 되살릴 아군 대상이 필요합니다.");
                return false;
            }
            if (reward.RequiresLevelTargetSelection && !IsValidLevelTarget(target))
            {
                Debug.LogWarning($"[보상] {reward.displayName}은(는) 레벨을 올릴 아군 대상이 필요합니다.");
                return false;
            }

            if (reward.IsLevelGrant)
            {
                ApplyLevelGrant(reward, target);
            }
            else if (reward.itemId > 0)
            {
                // 아이템을 실제로 인벤토리에 넣지 못했으면 false를 돌려준다. 상점이 이 값을 보고 값을 물린다.
                if (!ApplyItemReward(reward, target)) return false;
            }
            else if (reward.fullHealParty)
            {
                foreach (var hero in GridManager.Instance.heroList)
                {
                    if (hero != null && hero.isActive && !hero.IsEnemy)
                    {
                        hero.RestoreHpOutsideCombat(hero.HpMax);
                    }
                }
            }
            else if (reward.fullReviveParty)
            {
                foreach (var hero in GridManager.Instance.heroList)
                {
                    if (IsValidReviveTarget(hero)) hero.ReviveAfterBattle(1f);
                }
            }
            else if (reward.revivePercent > 0f)
            {
                target.ReviveAfterBattle(reward.revivePercent);
            }
            else if (reward.fullHealTarget)
            {
                target.RestoreHpOutsideCombat(target.HpMax);
            }
            else if (reward.healPercent > 0f)
            {
                int amount = CalculateHealingAmount(reward, target);
                target.RestoreHpOutsideCombat(amount);
            }
            else if (reward.healAmount > 0)
            {
                foreach (var hero in GridManager.Instance.heroList)
                {
                    if (hero != null && hero.isActive && !hero.IsEnemy)
                    {
                        hero.ModifyHp(hero.HpCurr + reward.healAmount);
                    }
                }
            }
            else if (reward.randomTokenAmount > 0)
            {
                GrantRandomTokens(reward.randomTokenAmount);
            }
            else if (reward.rerollTicketBonus > 0)
            {
                GameManager.Instance.inventoryManager.rerollTicketCount += reward.rerollTicketBonus;
                GameManager.Instance.inventoryManager.RefreshPanel();
            }
            return true;
        }

        public static bool IsValidHealingTarget(Unit target)
        {
            return target != null && target.isActive && !target.IsEnemy && target.HpCurr > 0;
        }

        public static bool IsValidReviveTarget(Unit target)
        {
            return target != null && !target.isActive && !target.IsEnemy &&
                   target.currentCell != null && target.LastActiveId > 0;
        }

        /// <summary>회복 보상 카드와 테스트가 함께 쓰는 기준 회복량. 실제 적용 시 회복량 보정은 Unit이 처리한다.</summary>
        public static int CalculateHealingAmount(RewardDef reward, Unit target)
        {
            if (reward == null || target == null) return 0;
            if (reward.fullHealTarget) return Mathf.Max(0, target.HpMax - target.HpCurr);
            return Mathf.Max(0, Mathf.CeilToInt(target.HpMax * Mathf.Clamp01(reward.healPercent)));
        }

        public static int CalculateReviveHp(RewardDef reward, Unit target)
        {
            if (reward == null || target == null || target.HpMax <= 0) return 0;
            return Mathf.Clamp(Mathf.CeilToInt(target.HpMax * Mathf.Clamp01(reward.revivePercent)), 1, target.HpMax);
        }

        private static void GrantRandomTokens(int amount)
        {
            var inventory = GameManager.Instance.inventoryManager;
            if (inventory == null) return;

            for (int i = 0; i < amount; i++)
            {
                int randomTokenId = UnityEngine.Random.Range(1, 9);
                inventory.AddToken(randomTokenId, 1);
            }
        }
    }
}
