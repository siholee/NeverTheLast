using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 화염살 — 평범한 나무지팡이 무기 스킬<br/>
    /// 단일 적에게 INT*40 화염 피해를 입히고 화염 원소를 부여한다.
    /// </summary>
    public class FireArrow : NormalCode
    {
        public FireArrow(NormalCodeContext context) : base(context)
        {
            CodeType      = BaseEnums.CodeType.Normal;
            CodeName      = "화염살";
            Caster        = context.Caster;
            CastingDelay  = 0.4f;
            ManaAmount    = 15;
            Element       = BaseEnums.ElementType.Pyro;
            SkillCategory = BaseEnums.SkillCategory.Skill;
            SpCost        = 1;
            TargetType    = BaseEnums.TargetType.Single;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (CastingDelay > 0f)
                yield return new WaitForSeconds(CastingDelay);

            if (TargetUnits == null || TargetUnits.Count == 0)
            {
                StopCode();
                yield break;
            }

            bool isCrit    = Random.value <= Caster.CritChanceCurr;
            float critMult = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage     = Mathf.RoundToInt(Caster.IntStat * 40f * critMult);

            var ctx = new DamageContext(
                Caster, damage,
                BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget },
                Element, isCrit);

            foreach (var target in TargetUnits)
            {
                // TODO: 화염살 투사체 SFX
                yield return new WaitForSeconds(0.4f);
                if (target.isActive)
                {
                    target.TakeDamage(ctx);
                    Debug.Log($"[화염살] {Caster.UnitName} → {target.UnitName}: {damage} 화염 피해 (INT*40{(isCrit ? ", 치명" : "")})");
                }
            }

            Caster.RecoverMana(ManaAmount);
            StopCode();
        }

        public override void StopCode() => Caster.isCasting = false;

        public override bool HasValidTarget()
            => GridManager.Instance.TargetNearestEnemy(Caster).Count > 0;
    }
}
