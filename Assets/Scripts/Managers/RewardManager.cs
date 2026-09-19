using System.Collections.Generic;
using System.Linq;
using BaseClasses;
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
        public int atkBonus;
        public int defBonus;
        public int intBonus;
        public float critChanceBonus;
        public float codeAccelerationBonus;
        public int hpBonus;
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

        /// <summary>강화제가 올리는 5스탯. 비어 있으면 강화제가 아니다.</summary>
        public string tonicStat;
        /// <summary>강화제의 합연산 보정(T1~T3).</summary>
        public int tonicFlat;
        /// <summary>강화제의 곱연산 보정(T4~T5). 1 이하면 곱연산이 아니다.</summary>
        public float tonicMultiplier;

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

        public bool IsTonic => !string.IsNullOrWhiteSpace(tonicStat);

        /// <summary>레벨을 올려 주는 보상인가.</summary>
        public bool IsLevelGrant => levelGrant > 0;

        /// <summary>장비가 아닌 보상은 전부 소모품이다. 한 번의 3택에 한 장까지만 섞인다.</summary>
        public bool IsConsumable => itemId <= 0;

        /// <summary>강화제가 올리는 스탯. 데이터 오타는 여기서 걸린다.</summary>
        public bool TryGetTonicStat(out BaseEnums.PrimaryStat stat)
        {
            stat = default;
            return IsTonic && System.Enum.TryParse(tonicStat, true, out stat);
        }

        public bool RequiresHealingTargetSelection => healPercent > 0f || fullHealTarget;
        public bool RequiresReviveTargetSelection => revivePercent > 0f;

        /// <summary>한 명을 지정하는 사탕. 전체 사탕은 고를 것이 없다.</summary>
        public bool RequiresLevelTargetSelection => IsLevelGrant && !levelGrantParty;

        public bool RequiresTargetSelection =>
            RequiresHealingTargetSelection || RequiresReviveTargetSelection || RequiresLevelTargetSelection;
        public bool IsHealingReward => RequiresHealingTargetSelection || fullHealParty || healAmount > 0;
        public bool IsRevivalReward => RequiresReviveTargetSelection || fullReviveParty;

        /// <summary>
        /// 장비 보상의 원본 데이터. 보상 화면이 부위 · 중량 · 스탯을 <b>구조로</b> 보여 주기 위해 들고 다닌다.
        /// 예전에는 이걸 문자열 한 줄로 뭉쳐 description에 넣었는데, 그러면 세 장을 비교할 수 없었다.
        /// </summary>
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
        /// 적 전용 장비(<c>enemyOnly</c>, 초반의 무뎌진 무기)는 드랍표에 잘못 적혀 있어도 여기서 빠진다.
        /// 테마 보상 풀(<c>themeIds</c>)은 폐지했다 — 테마 장비는 그 테마 적이 떨군다.
        /// </summary>
        public List<RewardDef> GenerateRewards(int count, int rewardRound, GameMode mode, IEnumerable<int> killedEnemyIds)
        {
            EnsureRewardData();
            _itemDataList ??= GameManager.Instance?.itemDataList ?? GameManager.Instance?.dataManager?.FetchItemDataList();

            var dropIds = new HashSet<int>(_rewardDataList?.commonDropItemIds ?? new List<int>());
            foreach (int itemId in EnemyDropIds(killedEnemyIds)) dropIds.Add(itemId);

            var pool = (_itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && dropIds.Contains(item.id) &&
                               !item.eventOnly && !item.enemyOnly && IsAvailableForRoster(item))
                .Select(item => new RewardDef
                {
                    id = $"item_{item.id}",
                    itemId = item.id,
                    displayName = item.name,
                    description = BuildItemDescription(item),
                    tier = Mathf.Clamp(item.rarity, 1, 5),
                    item = item,
                })
                .ToList();

            if (_rewardDataList?.rewards != null)
            {
                pool.AddRange(_rewardDataList.rewards.Where(reward =>
                    reward != null &&
                    (!reward.IsHealingReward || HasInjuredHero()) &&
                    (!reward.IsRevivalReward || HasFallenHero())));
            }
            var result = new List<RewardDef>();

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int tier = RollTier(rewardRound, mode);
                var candidates = pool.FindAll(reward => reward.tier == tier);
                if (candidates.Count == 0)
                {
                    candidates.AddRange(pool.OrderBy(reward => Mathf.Abs(reward.tier - tier)).Take(3));
                }

                var reward = candidates[Random.Range(0, candidates.Count)];
                result.Add(reward);
                pool.Remove(reward);
            }

            return result;
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
        /// 상점 매대. <c>goldCost</c>를 가진 보상만 올라간다.
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
            if (reward == null || reward.goldCost <= 0) return 0;
            return reward.goldCost * Mathf.Max(1, stage);
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
            => unit != null && !unit.IsEnemy && unit.ID > 0;

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

            return inventory.AddItem(reward.itemId, target);
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

        /// <summary>
        /// 상점 장비 매대. <c>shopPrice</c>를 가진 장비만 올라간다.
        ///
        /// 캐릭터 전용 장비는 올리지 않는다 — 상점은 누구의 덱이든 같은 물건을 파는 자리이고,
        /// 적 드랍 장비는 <b>그 적을 이겨서</b> 얻는 것이라야 뜻이 산다(가격이 없으니 애초에 오르지 않는다).
        /// </summary>
        public List<RewardDef> BuildShopEquipment()
        {
            _itemDataList ??= GameManager.Instance?.itemDataList
                              ?? GameManager.Instance?.dataManager?.FetchItemDataList();
            return (_itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && item.shopPrice > 0 && !item.eventOnly && !item.enemyOnly &&
                               (item.requiredUnitIds == null || item.requiredUnitIds.Count == 0))
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

        /// <summary>
        /// 캐릭터 전용 장비(<c>requiredUnitIds</c>)는 주인 중 하나가 현재 파티·대기석·선발 덱에 있을 때만 후보가 된다.
        /// 칸자르·잠비야(사바흐)와 시구르드·브륀힐드·일본 세 사람의 장비가 같은 규칙을 쓴다.
        /// </summary>
        private static bool IsAvailableForRoster(ItemData item)
        {
            if (item?.requiredUnitIds == null || item.requiredUnitIds.Count == 0) return item != null;

            bool inPartyOrBench = GridManager.Instance?.heroList?.Any(unit =>
                unit != null && !unit.IsEnemy && item.requiredUnitIds.Contains(unit.ID)) == true;
            if (inPartyOrBench) return true;

            return CharacterSelectionManager.Instance?.Lineup?.Any(entry =>
                entry != null && item.requiredUnitIds.Contains(entry.UnitId)) == true;
        }

        private static string BuildItemDescription(ItemData item)
        {
            // 귀중품은 부위도 중량도 없다. 얼마에 팔리는지가 전부다.
            // 계산식만 적어 두면 값을 가늠할 수 없다는 QA가 있었다. 지금 스테이지 값으로 확정해 보여 준다.
            if (item.IsValuable) return $"귀중품 | 판매가 {ValuablePrice(item, CurrentShopStage):N0}G";

            string stats = item.statBonuses == null || item.statBonuses.Count == 0
                ? ""
                : string.Join(", ", item.statBonuses.Select(BaseClasses.EquipmentStatKeys.Describe));
            string suffix = string.IsNullOrWhiteSpace(stats) ? "" : $" | {stats}";
            return $"{item.category} | 중량 {Mathf.Max(0, item.weight)}{suffix}";
        }

        private void EnsureRewardData()
        {
            if (_rewardDataList != null) return;
            _rewardDataList = GameManager.Instance?.dataManager?.FetchRewardDataList();
        }

        private int RollTier(int rewardRound, GameMode mode)
        {
            EnsureRewardData();
            List<RewardTierOddsData> oddsTable = _rewardDataList?.rewardTierOdds;
            if (oddsTable == null || oddsTable.Count == 0)
            {
                return Random.value < 0.25f ? 2 : 1;
            }

            int cappedRound = mode == GameMode.Infinite
                ? Mathf.Min(rewardRound, 10)
                : rewardRound;
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

            int roll = Random.Range(0, totalWeight);
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

        /// <summary>보상 카드를 고른 뒤의 흐름. 효과를 적용하고 다음 스테이지로 넘어간다.</summary>
        public void ApplyReward(RewardDef reward, Unit target = null)
        {
            if (!ApplyRewardEffect(reward, target)) return;

            GameManager.Instance.uiManager?.HideRewardPanel();
            RunManager.Instance?.AdvanceToNextStage();
        }

        /// <summary>
        /// 보상 효과만 적용한다. 상점 구매도 같은 통로를 쓴다 — 상점에서 산 회복약과
        /// 보상으로 받은 회복약이 다르게 동작하면 안 되기 때문이다.
        /// 대상이 맞지 않으면 아무것도 하지 않고 false를 돌려준다.
        /// </summary>
        public bool ApplyRewardEffect(RewardDef reward, Unit target = null)
        {
            if (reward == null) return false;

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
            if (reward.IsTonic && !reward.TryGetTonicStat(out _))
            {
                Debug.LogWarning($"[보상] {reward.displayName}의 tonicStat '{reward.tonicStat}'을 5스탯으로 해석하지 못했습니다.");
                return false;
            }

            if (reward.IsTonic)
            {
                ApplyTonic(reward);
            }
            else if (reward.IsLevelGrant)
            {
                ApplyLevelGrant(reward, target);
            }
            else if (reward.itemId > 0)
            {
                // 장비를 실제로 건네지 못했으면 false를 돌려준다. 상점이 이 값을 보고 값을 물린다.
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
            else
            {
                Unit rewardTarget = target ?? GridManager.Instance.heroList.Find(hero => hero != null && hero.isActive && !hero.IsEnemy);
                rewardTarget?.AddRunBonus(
                    hpUpgrade: reward.hpBonus,
                    atkUpgrade: reward.atkBonus,
                    defUpgrade: reward.defBonus,
                    intUpgrade: reward.intBonus,
                    critChanceUpgrade: Mathf.RoundToInt(reward.critChanceBonus),
                    codeAccelerationBonus: reward.codeAccelerationBonus
                );
            }

            return true;
        }

        /// <summary>
        /// 강화제를 건다. 파티 전체에 걸리는 런 범위 보정이라 유닛이 아니라 런이 들고 있다
        /// (<see cref="Core.PartyTonicState"/>). 여기서는 스탯 캐시만 다시 돌린다.
        /// </summary>
        private static void ApplyTonic(RewardDef reward)
        {
            if (!reward.TryGetTonicStat(out PrimaryStat stat)) return;

            RunManager.Instance?.PartyTonics.Apply(stat, reward.tonicFlat, reward.tonicMultiplier, reward.displayName);
            GridManager.Instance?.RefreshAllyAttributes();
            Debug.Log($"[강화제] {reward.displayName} 적용 — {stat} " +
                      (reward.tonicMultiplier > 1f ? $"×{reward.tonicMultiplier:0.00}" : $"+{reward.tonicFlat}") +
                      $" ({Core.PartyTonicState.BattleDuration}전투)");
        }

        public static bool IsValidHealingTarget(Unit target)
        {
            return target != null && target.isActive && !target.IsEnemy && target.HpCurr > 0;
        }

        public static bool IsValidReviveTarget(Unit target)
        {
            return target != null && !target.isActive && !target.IsEnemy && !target.IsSummon &&
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
                int randomTokenId = Random.Range(1, 9);
                inventory.AddToken(randomTokenId, 1);
            }
        }
    }
}
