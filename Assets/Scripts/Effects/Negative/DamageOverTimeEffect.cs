using System.Collections.Generic;
using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 지속 피해(DOT) 효과
    /// 시전자의 공격력에 비례한 지속 피해 (예: 공격력의 20%)
    /// </summary>
    public class DamageOverTimeEffect : BaseEffect
    {
        public override bool IsDamageOverTime => true;
                
        public DamageOverTimeEffect(int effectId, float coefficient = 100f) : base(effectId, coefficient)
        {
            EffectName = "지속 피해";
            EffectDescription = $"시전자 공격력의 {coefficient}%에 해당하는 지속 피해";
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnApply()
        {
            Debug.Log($"[DOT] {Target.UnitName}에게 지속 피해 효과 적용 (계수: {Coefficient}%)");
        }

        /// <summary>
        /// 보유자의 턴마다 한 번 터진다.
        ///
        /// 예전에는 0.1초 간격으로 초당 10회 터졌다. 벽시계 기준이라 프레임·연출 길이에 따라
        /// 총 피해가 흔들렸고, 초당 위력 50 스킬 10회라는 사실상 조정되지 않은 수치였다.
        /// 턴제로 옮기면서 <b>턴당 1회</b>로 정리했다.
        /// </summary>
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
            return Mathf.Max(0, Mathf.RoundToInt((Caster?.SkillDamage(50) ?? 0) * Coefficient / 100f * multiplier));
        }
        
        public override void OnRemove()
        {
            Debug.Log($"[DOT] {Target.UnitName}에게서 지속 피해 효과 제거");
        }
    }
}
