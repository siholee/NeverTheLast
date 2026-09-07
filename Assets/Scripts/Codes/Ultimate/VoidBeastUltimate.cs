using System.Collections.Generic;
using BaseClasses;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허의 멧돼지 U 광란의 일격 — 단일 적에게 120 + STR×1.5 위력의 접촉 물리 피해.
    /// 팔레트 변형만 적중 후 자기 원소를 부착한다. 기본형의 원소는 공허라 아무것도 붙이지 않는다.
    /// </summary>
    public sealed class VoidBeastUltimate : SimpleUltimate
    {
        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidBeastUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "광란의 일격", 4, 0.5f)
        {
            _attachedElement = attachedElement;
            Power = 120;
            PowerStatCoefficient = 1.5f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.ContactAttack,
            };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * crit));
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
