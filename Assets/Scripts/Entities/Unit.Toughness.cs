using System.Collections.Generic;
using BaseClasses;
using Managers;

namespace Entities
{
    public partial class Unit
    {
        public int ToughnessCurr { get; private set; }
        public int ToughnessMax { get; private set; }
        public Unit ToughnessSource { get; private set; }
        public bool HasToughness => ToughnessCurr > 0;
        private readonly HashSet<(int action, int attacker)> _toughnessActions = new();
        private readonly HashSet<DamageContext> _toughnessProbes = new();

        public bool GrantToughness(int amount, Unit source)
        {
            if (amount <= 0 || !IsEnemy || IsSummon || !isActive || ToughnessMax > 0) return false;
            ToughnessMax = ToughnessCurr = amount;
            ToughnessSource = source;
            RefreshView();
            return true;
        }

        public void ClearToughness()
        {
            bool had = ToughnessMax > 0;
            ToughnessMax = ToughnessCurr = 0;
            ToughnessSource = null;
            _toughnessActions.Clear();
            _toughnessProbes.Clear();
            if (had) RefreshView();
        }

        private void RecoverToughnessOnNormalTurn()
        {
            Combat.Toughness.Ensure(this);
            if (ToughnessMax > 0 && ToughnessCurr == 0) ToughnessCurr = ToughnessMax;
            _toughnessActions.Clear();
            _toughnessProbes.Clear();
            RefreshView();
        }

        private void ReduceToughness(DamageContext context)
        {
            Combat.Toughness.Ensure(this);
            // 실사망 타격에는 가상 처치를 겹쳐 주지 않는다. 막힌 타격·반응·지속·추가 고정 피해는 삭감하지 않는다.
            if (HpCurr <= 0 || ToughnessCurr <= 0 || context.ResolvedDamage <= 0 || !Combat.Toughness.IsAttack(context) ||
                context.Attacker.IsEnemy == IsEnemy) return;
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler?.ExecutingUnit != null)
            {
                // 다단 타격을 한 행동의 삭감량으로 센다. 지휘 사격은 공격자마다 각각 한 공격이다.
                if (!_toughnessActions.Add((scheduler.CurrentActionId, context.Attacker.GetInstanceID()))) return;
            }
            else if (!_toughnessProbes.Add(context)) return;
            ToughnessCurr = UnityEngine.Mathf.Max(0, ToughnessCurr - Combat.Toughness.Estimate(context.Attacker, Combat.Toughness.BaseReduction(context)));
            context.ToughnessBroken = ToughnessCurr == 0;
            RefreshView();
        }
    }
}
