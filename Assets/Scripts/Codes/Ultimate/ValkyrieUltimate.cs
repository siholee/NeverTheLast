using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 발키리 U — 서리창.
    ///
    /// 단일 적에게 `150 + STR×0.8`을 꽂고 얼음을 남긴다.
    /// 그 얼음이 광전사·에인하르의 불을 기다리는 재료가 되므로,
    /// 친위대 편성에서는 발키리가 볼바 대신 융해의 앞단을 맡는다.
    /// </summary>
    public sealed class ValkyrieFrostLance : SimpleUltimate
    {
        private const int LancePower = 150;
        private const float LanceStrCoefficient = 0.8f;

        public ValkyrieFrostLance(UltimateCodeContext context)
            : base(context, "서리창", 0.5f)
        {
            Power = LancePower;
            PowerStatCoefficient = LanceStrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
        }

        protected override void Resolve()
        {
            Unit target = Combat.CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
            }, isCrit));

            if (!target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Enemies().Count > 0;
    }
}
