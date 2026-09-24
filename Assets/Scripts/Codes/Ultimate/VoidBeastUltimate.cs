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
        public VoidBeastUltimate(UltimateCodeContext context)
            : base(context, "광란의 일격", 0.5f)
        {
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

            // 부착 원소는 시전자 자신의 원소다. 공허 속성이면 아무것도 붙이지 않는다.
            if (!target.isActive) return;
            if (!System.Enum.TryParse(Caster.Element, true, out BaseEnums.UnitElement own)) return;
            if (own is BaseEnums.UnitElement.None or BaseEnums.UnitElement.Void) return;

            target.GrantCombatElement(own, Unit.CommonElementAuraDuration, Caster);
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
