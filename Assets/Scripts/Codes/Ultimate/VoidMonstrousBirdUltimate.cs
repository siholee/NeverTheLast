using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허의 괴조 U — 전체 적에게 80 + STR×0.8 위력의 접촉 물리 피해.
    /// 팔레트 변형만 피해 후 지정된 원소를 부착한다.
    /// </summary>
    public sealed class VoidMonstrousBirdUltimate : SimpleUltimate
    {
        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidMonstrousBirdUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "공허 강습", 0.45f)
        {
            _attachedElement = attachedElement;
            Power = 80;
            PowerStatCoefficient = 0.8f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.ContactAttack,
            };
        }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * crit));

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>(CodeTags), isCrit));

                if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
                {
                    target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
