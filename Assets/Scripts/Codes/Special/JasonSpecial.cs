using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Special
{
    /// <summary>이아손 SP — 궁극기에 반응해 광역 피해를 주고 아군 궁극기 피해를 영구 중첩한다.</summary>
    public sealed class JasonArgonautSupport : SpecialCode
    {
        public JasonArgonautSupport(SpecialCodeContext context) : base(context)
        {
            CodeName = "아르고의 지원";
            CastingDelay = 0.35f;
            Power = 30;
            PowerStatCoefficient = 0.4f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CodeTags = new List<int>
            {
                DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.AdditionalAttack,
            };
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast) { StopCode(); yield break; }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * crit));
            foreach (Unit enemy in Combat.CombatTargets.AliveEnemies(Caster).ToList())
            {
                enemy.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Special, new List<int>
                {
                    DamageTag.AllTarget, DamageTag.Physical, DamageTag.NonContactAttack,
                    DamageTag.AdditionalAttack,
                }, isCrit));
            }

            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    5360, "jason_argonaut_support_stack", CodeName, Caster, ally,
                    new JasonUltimateDamageStackEffect(0.01f),
                    stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                    isBeneficial: true,
                    description: "궁극기 피해 +1%. 중첩됩니다."));
            }

            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            if (CurrSkillCoroutine != null)
            {
                Caster.StopCoroutine(CurrSkillCoroutine);
                CurrSkillCoroutine = null;
            }
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Combat.CombatTargets.AliveEnemies(Caster).Count > 0;
    }

    internal sealed class JasonUltimateDamageStackEffect : BaseEffect
    {
        private readonly float _bonus;
        public JasonUltimateDamageStackEffect(float bonus) : base(0, bonus) => _bonus = bonus;
        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || context == null) return 1f;
            bool ultimate = context.CodeType == BaseEnums.CodeType.Ultimate ||
                            context.DamageTags?.Contains(DamageTag.UltAttack) == true;
            return ultimate ? 1f + _bonus : 1f;
        }
    }
}
