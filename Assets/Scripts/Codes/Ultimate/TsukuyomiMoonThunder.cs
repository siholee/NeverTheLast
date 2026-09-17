using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 모든 적에게 INT 기반 위력 80의 피해를 입히고 월식(3턴 지속피해)과 달의 위상 디버프 세 가지를 남긴다.
    ///
    /// 예전에는 번개 원소를 부여했다. 츠쿠요미는 원소 반응에 끼지 않는 지속피해 서포터로
    /// 정리했으므로, 궁극기의 몫은 적 전체에 지속피해와 디버프를 한 번에 까는 것이다.
    /// 월식·위상 셋이 한 적에게 네 칸을 채워 달의 인력(241)을 거의 상한까지 올린다.
    /// </summary>
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
                TsukuyomiMoonlight.ApplyEclipse(Caster, target);
                TsukuyomiMoonlight.ApplyAllPhases(Caster, target);
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
