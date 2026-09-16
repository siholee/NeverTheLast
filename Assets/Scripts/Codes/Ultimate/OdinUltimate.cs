using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 오딘 U — 최후의 룬.
    ///
    /// 적 전체를 치고 <b>치유량 감소를 5턴으로 새로 매긴다.</b>
    /// 고유 P가 거는 3턴을 한 번에 덮어 파티 전체의 힐 잠금을 길게 늘이는 것이 요점이고,
    /// 그래서 이 보스전은 "회복으로 버틴다"가 통하지 않는다.
    /// </summary>
    public sealed class OdinFinalRune : SimpleUltimate
    {
        private const int UltimatePower = 100;
        private const int ReductionTurns = 5;
        private const float Reduction = 0.40f;

        public OdinFinalRune(UltimateCodeContext context) : base(context, "최후의 룬", 0.7f)
        {
            Power = UltimatePower;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override void Resolve()
        {
            List<Unit> targets = Enemies();
            if (targets.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));

            foreach (Unit target in targets)
            {
                if (target == null || !target.isActive) continue;

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));

                if (target.isActive)
                {
                    HealingReductionStatus.Apply(target, Caster, ReductionTurns, "최후의 룬", Reduction);
                }
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
