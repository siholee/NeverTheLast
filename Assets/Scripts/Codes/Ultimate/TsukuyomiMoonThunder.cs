using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>모든 적에게 INT 80% 범위 피해를 입히고 번개 원소를 부여한다.</summary>
    public sealed class TsukuyomiMoonThunder : UltimateCode
    {
        /// <summary>사양 "INT의 80%"에 대응하는 고정 위력.</summary>
        private const int UltimatePower = 80;

        public TsukuyomiMoonThunder(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "월뢰만천";
            CastingDelay = 1f;
            CodeTags = new List<int> { DamageTag.Special };
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < CastingDelay)
            {
                if (!Caster.isActive || Caster.isControlled)
                {
                    StopCode();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            List<Unit> targets = Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && unit.HpCurr > 0).ToList();
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            // 사양 "INT의 80%" = 위력 80.
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(UltimatePower, BaseEnums.PrimaryStat.INT) * critMultiplier));
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack };

            foreach (Unit target in targets)
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target != null && target.isActive)
                {
                    target.GrantCombatElement(BaseEnums.UnitElement.Electro);
                }
            }
            StopCode();
        }

        public override void StopCode()
        {
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive && Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive);
        }
    }
}
