using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>보상 정의 (전투 후 제공할 옵션)</summary>
    [System.Serializable]
    public class RewardDef
    {
        public string id;
        public string displayName;
        public string description;
        public bool   isRare;

        // 적용할 스탯 보너스 (AddRunBonus로 전달)
        public int   atkBonus;
        public int   defBonus;
        public float critChanceBonus;
        public float speedBonus;
        public int   hpBonus;

        // 특수 효과 (추후 확장)
        public bool fullHealParty;
    }

    /// <summary>
    /// 로그라이트 보상 관리자.
    /// 전투 후 3개의 보상 중 하나를 선택하면 RunManager.AdvanceEncounter()를 호출한다.
    /// DontDestroyOnLoad 싱글톤.
    /// </summary>
    public class RewardManager : MonoBehaviour
    {
        public static RewardManager Instance { get; private set; }

        // ── 보상 풀 ───────────────────────────────────────────────────────────────
        private static readonly List<RewardDef> _rewardPool = new()
        {
            new RewardDef { id = "heal_small",    displayName = "소회복",     description = "아군 전체 HP +500 회복",    isRare = false, hpBonus = 0, fullHealParty = false },
            new RewardDef { id = "atk_up",        displayName = "공격력 증가", description = "선택한 유닛의 ATK 기반 +50", isRare = false, atkBonus = 50 },
            new RewardDef { id = "def_up",        displayName = "방어력 증가", description = "선택한 유닛의 DEF 기반 +30", isRare = false, defBonus = 30 },
            new RewardDef { id = "crit_up",       displayName = "치명타 증가", description = "선택한 유닛의 치명타율 +10%",  isRare = false, critChanceBonus = 0.10f },
            new RewardDef { id = "speed_up",      displayName = "속도 증가",  description = "선택한 유닛의 SPD 기반 +10", isRare = false, speedBonus = 10f },
            new RewardDef { id = "hp_up",         displayName = "체력 증가",  description = "선택한 유닛의 HP 기반 +500",  isRare = false, hpBonus = 500 },
            new RewardDef { id = "full_heal",     displayName = "완전 회복",  description = "아군 전체 HP 완전 회복",      isRare = true,  fullHealParty = true },
            new RewardDef { id = "big_atk",       displayName = "큰 공격력",  description = "선택한 유닛의 ATK 기반 +120", isRare = true,  atkBonus = 120 },
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

        /// <summary>보상 풀에서 count개를 무작위 추출 (rare 가중치 0.2)</summary>
        public List<RewardDef> GenerateRewards(int count = 3)
        {
            var pool = new List<RewardDef>(_rewardPool);
            var result = new List<RewardDef>();

            // 확률적 추출 (rare 항목 가중치 낮게)
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                List<RewardDef> candidates = new List<RewardDef>();
                foreach (var r in pool)
                {
                    // rare는 20% 확률 (5번 중 1번)
                    if (!r.isRare || Random.value < 0.2f)
                        candidates.Add(r);
                }
                if (candidates.Count == 0) candidates.AddRange(pool); // fallback

                var pick = candidates[Random.Range(0, candidates.Count)];
                result.Add(pick);
                pool.Remove(pick);
            }
            return result;
        }

        /// <summary>
        /// 보상 적용 후 다음 인카운터로 진행.
        /// target이 null이면 전체 파티 대상 보상.
        /// </summary>
        public void ApplyReward(RewardDef reward, Unit target = null)
        {
            if (reward == null) return;

            Debug.Log($"[RewardManager] 보상 적용: {reward.displayName}");

            if (reward.fullHealParty)
            {
                // 전체 파티 HP 완전 회복
                foreach (var hero in GridManager.Instance.heroList)
                {
                    if (hero.isActive)
                    {
                        hero.ModifyHp(hero.HpMax);
                    }
                }
            }
            else if (target != null)
            {
                // 선택한 유닛에게 스탯 보너스
                target.AddRunBonus(
                    atkBase:       reward.atkBonus,
                    defBase:       reward.defBonus,
                    critChanceBase: reward.critChanceBonus,
                    speedBase:     reward.speedBonus,
                    hpBase:        reward.hpBonus
                );
            }

            // UI 닫고 다음 인카운터로
            GameManager.Instance.uiManager.HideRewardPanel();
            RunManager.Instance.AdvanceEncounter();
        }
    }
}
