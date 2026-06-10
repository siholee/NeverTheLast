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
        public bool isRare;
        public int atkBonus;
        public int defBonus;
        public float critChanceBonus;
        public float codeAccelerationBonus;
        public int hpBonus;
        public int healAmount;
        public int rerollTicketBonus;
        public int randomTokenAmount;
        public bool fullHealParty;
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

        private static readonly List<RewardDef> FallbackRewardPool = new()
        {
            new RewardDef { id = "heal_small", displayName = "소회복", description = "아군 전체 HP를 500 회복", tier = 1, healAmount = 500 },
            new RewardDef { id = "atk_up", displayName = "공격력 강화", description = "선택 유닛 공격력 강화 +1", tier = 1, atkBonus = 1 },
            new RewardDef { id = "def_up", displayName = "방어력 강화", description = "선택 유닛 방어력 강화 +1", tier = 1, defBonus = 1 },
            new RewardDef { id = "crit_up", displayName = "치명 강화", description = "선택 유닛 치명타 강화 +1", tier = 2, critChanceBonus = 1f },
            new RewardDef { id = "hp_up", displayName = "체력 강화", description = "선택 유닛 체력 강화 +1", tier = 2, hpBonus = 1 },
            new RewardDef { id = "haste_up", displayName = "가속 강화", description = "선택 유닛 코드 가속 +5%", tier = 3, codeAccelerationBonus = 0.05f },
            new RewardDef { id = "tickets", displayName = "재정비권", description = "리롤 티켓 +3", tier = 3, rerollTicketBonus = 3 },
            new RewardDef { id = "tokens", displayName = "전리품", description = "무작위 토큰 8개 획득", tier = 3, randomTokenAmount = 8 },
            new RewardDef { id = "full_heal", displayName = "완전 회복", description = "아군 전체 HP 완전 회복", tier = 4, isRare = true, fullHealParty = true },
            new RewardDef { id = "big_atk", displayName = "큰 공격력", description = "선택 유닛 공격력 강화 +3", tier = 4, isRare = true, atkBonus = 3 },
        };

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

        public List<RewardDef> GenerateRewards(int count = 3, int rewardRound = 1, GameMode mode = GameMode.Training)
        {
            EnsureRewardData();
            var pool = new List<RewardDef>(_rewardDataList?.rewards ?? FallbackRewardPool);
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

            if (reward.fullHealParty)
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
