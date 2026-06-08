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
    /// 바위 미사일 (세이 Lv.15 스킬, SP-1)<br/>
    /// 8회 INT*20 바운스 피해. 매 히트마다 무작위 단일 적 자동 선택 (플레이어 선택 불가).
    /// </summary>
    public class RockMissile : NormalCode
    {
        public const int BounceCount = 8;

        public RockMissile(NormalCodeContext context) : base(context)
        {
            CodeType      = BaseEnums.CodeType.Normal;
            CodeName      = "바위 미사일";
            Caster        = context.Caster;
            CastingDelay  = 0.3f;
            ManaAmount    = 20;
            Element       = BaseEnums.ElementType.Geo;
            SkillCategory = BaseEnums.SkillCategory.Skill;
            SpCost        = 1;
            TargetType    = BaseEnums.TargetType.Bounce; // 바운스: 내부에서 자동 랜덤 선택
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}이 {CodeName} 시전 ({BounceCount}회 바운스)");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (CastingDelay > 0f)
                yield return new WaitForSeconds(CastingDelay);

            int baseDmg = Mathf.RoundToInt(Caster.IntStat * 20f);

            for (int i = 0; i < BounceCount; i++)
            {
                // 매 히트마다 무작위 단일 대상 (바운스 = 자동 랜덤, 플레이어 개입 불가)
                var randomTargets = GridManager.Instance.GetAutoSingleTarget(Caster);
                if (randomTargets == null || randomTargets.Count == 0) break;

                Unit target = randomTargets[0];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMult = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.RoundToInt(baseDmg * critMult);

                var ctx = new DamageContext(
                    Caster, damage,
                    BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.SingleTarget },
                    Element, isCrit);

                // TODO: 바위 파편 투사체 SFX
                yield return new WaitForSeconds(0.2f);
                if (target.isActive)
                {
                    target.TakeDamage(ctx);
                    Debug.Log($"[바위 미사일] {i+1}/{BounceCount} → {target.UnitName}: {damage} 피해 (INT*20{(isCrit ? ", 치명" : "")})");
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
