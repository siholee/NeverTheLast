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
        public int rerollTicketBonus;
        public int randomTokenAmount;
        public bool fullHealParty;
        public int itemId;
    }

    public class RewardManager : MonoBehaviour
    {
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

        public List<RewardDef> GenerateRewards(int count = 3, int rewardRound = 1, GameMode mode = GameMode.Training, int themeId = 0)
        {
            EnsureRewardData();
            _itemDataList ??= GameManager.Instance?.itemDataList ?? GameManager.Instance?.dataManager?.FetchItemDataList();
            var pool = (_itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && !item.eventOnly)
                .Select(item => new RewardDef
                {
                    id = $"item_{item.id}",
                    itemId = item.id,
                    displayName = item.name,
                    description = BuildItemDescription(item),
                    tier = Mathf.Clamp(item.rarity, 1, 5),
                })
                .ToList();
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

        private static string BuildItemDescription(ItemData item)
        {
            string stats = item.statBonuses == null || item.statBonuses.Count == 0
                ? ""
                : string.Join(", ", item.statBonuses.Select(bonus => $"{bonus.stat} +{bonus.amount}"));
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

        public void ApplyReward(RewardDef reward, Unit target = null)
        {
            if (reward == null) return;

            if (reward.itemId > 0)
            {
                GameManager.Instance.inventoryManager?.AddItem(reward.itemId);
            }
            else if (reward.fullHealParty)
            {
                foreach (var hero in GridManager.Instance.heroList)
                {
                    if (hero != null && hero.isActive && !hero.IsEnemy)
                    {
                        hero.ModifyHp(hero.HpMax);
                    }
                }
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

            GameManager.Instance.uiManager?.HideRewardPanel();
            RunManager.Instance?.AdvanceAfterReward();
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
