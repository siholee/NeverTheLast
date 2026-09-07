using System.Collections.Generic;
using BaseClasses;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>범용 거인 U — 고정 피해 + STR 기반 위력의 단일 접촉 타격.</summary>
    public sealed class GenericGiantHammerfall : SimpleUltimate
    {
        private readonly int _flatDamage;

        public GenericGiantHammerfall(UltimateCodeContext context, int flatDamage, int statPower)
            : base(context, "거인의 철퇴", 4, 0.55f)
        {
            _flatDamage = flatDamage;
            Power = statPower;
            CodeTags = new List<int> { DamageTag.ContactAttack, DamageTag.Physical };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int baseDamage = _flatDamage + Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR);
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * (isCrit ? Caster.CritMultiplierCurr : 1f)));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack,
                    DamageTag.ContactAttack, DamageTag.Physical,
                },
                isCrit));
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
