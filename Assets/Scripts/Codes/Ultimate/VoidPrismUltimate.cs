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
        public VoidPrismUltimate(UltimateCodeContext context)
            : base(context, "공허 포격", 0.5f)
        {
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

            // 부착 원소는 시전자 자신의 원소다. 원종 프리즘은 공허 속성이라 아무것도 붙이지 않는다.
            if (!target.isActive) return;
            if (!System.Enum.TryParse(Caster.Element, true, out BaseEnums.UnitElement own)) return;
            if (own is BaseEnums.UnitElement.None or BaseEnums.UnitElement.Void) return;

            target.GrantCombatElement(own, Unit.CommonElementAuraDuration, Caster);
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
