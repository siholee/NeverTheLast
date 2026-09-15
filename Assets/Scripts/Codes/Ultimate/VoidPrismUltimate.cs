using System.Collections.Generic;
using BaseClasses;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허의 프리즘 U — 단일 적에게 150 + INT×1.2 위력의 비접촉 특수 피해.
    /// 팔레트 변형만 적중 후 생성자에 지정된 원소를 부착한다.
    /// </summary>
    public sealed class VoidPrismUltimate : SimpleUltimate
    {
        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidPrismUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "프리즘 파동", 0.5f)
        {
            _attachedElement = attachedElement;
            Power = 150;
            PowerStatCoefficient = 1.2f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CodeTags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * crit));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>(CodeTags), isCrit));

            if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
            {
                target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
