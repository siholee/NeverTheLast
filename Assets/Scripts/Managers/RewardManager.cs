using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Managers
{
    [System.Serializable]
    public class RewardDef
    {
        public string id;
        public string displayName;
        public string description;
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

        private static readonly List<RewardDef> RewardPool = new()
        {
            new RewardDef { id = "heal_small", displayName = "소회복", description = "아군 전체 HP를 500 회복", healAmount = 500 },
            new RewardDef { id = "atk_up", displayName = "공격력 강화", description = "선택 유닛 공격력 강화 +1", atkBonus = 1 },
            new RewardDef { id = "def_up", displayName = "방어력 강화", description = "선택 유닛 방어력 강화 +1", defBonus = 1 },
            new RewardDef { id = "crit_up", displayName = "치명 강화", description = "선택 유닛 치명타 강화 +1", critChanceBonus = 1f },
            new RewardDef { id = "hp_up", displayName = "체력 강화", description = "선택 유닛 체력 강화 +1", hpBonus = 1 },
            new RewardDef { id = "haste_up", displayName = "가속 강화", description = "선택 유닛 코드 가속 +5%", codeAccelerationBonus = 0.05f },
            new RewardDef { id = "tickets", displayName = "재정비권", description = "리롤 티켓 +3", rerollTicketBonus = 3 },
            new RewardDef { id = "tokens", displayName = "전리품", description = "무작위 토큰 8개 획득", randomTokenAmount = 8 },
            new RewardDef { id = "full_heal", displayName = "완전 회복", description = "아군 전체 HP 완전 회복", isRare = true, fullHealParty = true },
            new RewardDef { id = "big_atk", displayName = "큰 공격력", description = "선택 유닛 공격력 강화 +3", isRare = true, atkBonus = 3 },
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

        public List<RewardDef> GenerateRewards(int count = 3)
        {
            var pool = new List<RewardDef>(RewardPool);
            var result = new List<RewardDef>();

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var candidates = pool.FindAll(reward => !reward.isRare || Random.value < 0.25f);
                if (candidates.Count == 0)
                {
                    candidates.AddRange(pool);
                }

                var reward = candidates[Random.Range(0, candidates.Count)];
                result.Add(reward);
                pool.Remove(reward);
            }

            return result;
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
