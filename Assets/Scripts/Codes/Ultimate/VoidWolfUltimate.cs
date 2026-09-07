using System;
using System.Collections.Generic;
using BaseClasses;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허의 늑대 U 피의 포식 — 단일 적에게 120 + STR×1.1 위력의 접촉 물리 피해를 주고
    /// 실제로 입힌 피해의 50%를 회복한다. 팔레트 변형만 적중 후 자기 원소를 부착한다.
    /// </summary>
    public sealed class VoidWolfUltimate : SimpleUltimate
    {
        private const float LifestealRatio = 0.5f;

        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidWolfUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "피의 포식", 4, 0.45f)
        {
            _attachedElement = attachedElement;
            Power = 120;
            PowerStatCoefficient = 1.1f;
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

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * crit));

            // 흡혈은 OnDamageDealt 처리 중에 일어나야 '공격으로 자신을 회복'으로 인정된다.
            // 인라인으로 회복하면 흡혈귀·어둠의 군주가 붙지 않으므로 잠깐 리스너를 건다.
            Action<DamageResolvedContext> lifesteal = resolved =>
            {
                if (resolved?.Attacker != Caster || resolved.Target != target) return;
                if (resolved.DamageDealt <= 0 || !Caster.isActive) return;

                Caster.ModifyHp(
                    Caster.HpCurr + Mathf.Max(1, Mathf.RoundToInt(resolved.DamageDealt * LifestealRatio)),
                    Caster);
            };

            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, lifesteal);
            try
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>(CodeTags), isCrit));
            }
            finally
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, lifesteal);
            }

            if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
            {
                target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
