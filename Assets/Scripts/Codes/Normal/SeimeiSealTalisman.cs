using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 세이메이 N — 금박 봉인부(43). 단일 적에게 CON 위력 45의 특수·비접촉 피해를
    /// 주고 봉인부 1중첩을 붙인다. 3중첩 전환은 고유 패시브가 공통 처리한다.
    /// </summary>
    public sealed class SeimeiSealTalisman : BaseNormalCode
    {
        public SeimeiSealTalisman(NormalCodeContext context) : base(context)
        {
            CodeName = "금박 봉인부";
            Power = 45;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive && target.HpCurr > 0)
                SeimeiIds.ApplySeal(Caster, target);
        }
    }
}
