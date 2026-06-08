using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 팀 공유 SP 풀 (HSR 스타일). 최대 5 SP.
    /// Basic 공격 → +1 SP 생성. Skill 공격 → SpCost 소모.
    /// 궁극기(Ultimate)는 SP와 무관하며 별도 마나 시스템 사용.
    /// </summary>
    public class SPManager
    {
        public const int Max             = 5;
        public const int BasicAttackGain = 1;

        public int Current { get; private set; } = 0;

        /// <summary>SP 변화 시 발생. 파라미터: 현재 SP 값</summary>
        public event System.Action<int> OnSPChanged;

        public bool CanAfford(int cost) => Current >= cost;

        public void Spend(int cost)
        {
            if (cost <= 0) return;
            Current = Mathf.Max(0, Current - cost);
            OnSPChanged?.Invoke(Current);
            Debug.Log($"[SP] -{cost} → {Current}/{Max}");
        }

        public void Generate(int amount)
        {
            if (amount <= 0) return;
            Current = Mathf.Min(Max, Current + amount);
            OnSPChanged?.Invoke(Current);
            Debug.Log($"[SP] +{amount} → {Current}/{Max}");
        }

        /// <summary>인카운터 시작 시 SP 리셋 (0으로 초기화)</summary>
        public void ResetForEncounter()
        {
            Current = 0;
            OnSPChanged?.Invoke(Current);
            Debug.Log("[SP] 인카운터 리셋 → 0");
        }
    }
}
