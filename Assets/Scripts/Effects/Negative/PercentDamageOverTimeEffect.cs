using System.Collections.Generic;
using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 퍼센트 기반 지속 피해(DOT) 효과
    /// 최대 체력의 일정 비율만큼 지속 피해 (예: 최대체력의 2%)
    /// </summary>
    public class PercentDamageOverTimeEffect : BaseEffect
    {
        public override bool IsDamageOverTime => true;
                
        public PercentDamageOverTimeEffect(int effectId, float coefficient = 2f) : base(effectId, coefficient)
        {
            EffectName = "퍼센트 지속 피해";
            EffectDescription = $"최대 체력의 {coefficient}%에 해당하는 지속 피해";
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnApply()
        {
            Debug.Log($"[Percent DOT] {Target.UnitName}에게 퍼센트 지속 피해 효과 적용 (계수: {Coefficient}%)");
        }

        /// <summary>보유자의 턴마다 최대 체력의 계수%만큼 깎는다. 예전 0.1초 간격에서 턴당 1회로 정리했다.</summary>
        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive || Target.HpCurr <= 0) return;

            Target.TakeDamage(new DamageContext(
                Caster,
                CalculateTickDamage(),
                BaseEnums.CodeType.Effect,
                new List<int>(),
                false,
                10000000));
        }

        public override int EstimateDamagePerTurn() => CalculateTickDamage();

        private int CalculateTickDamage()
        {
            float multiplier = Caster != null ? Caster.GetDamageOverTimeApplicationMultiplier() : 1f;
            return Mathf.Max(0, Mathf.RoundToInt((Target?.HpMax ?? 0) * Coefficient / 100f * multiplier));
        }
        
        public override void OnRemove()
        {
            Debug.Log($"[Percent DOT] {Target.UnitName}에게서 퍼센트 지속 피해 효과 제거");
        }
    }
}
