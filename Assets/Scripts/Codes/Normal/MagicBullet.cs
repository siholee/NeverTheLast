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
    /// 마력탄 (주술사 클래스 기본 스킬 — Basic 행동 슬롯)<br/>
    /// 단일 적에게 INT*40 피해를 준다. 무조건 적중 (미스 판정 없음).
    /// </summary>
    public class MagicBullet : NormalCode
    {
        public MagicBullet(NormalCodeContext context) : base(context)
        {
            CodeType      = BaseEnums.CodeType.Normal;
            CodeName      = "마력탄";
            Caster        = context.Caster;
            CastingDelay  = 0.3f;
            ManaAmount    = 15;    // 기본 공격: 마나 소량 회복
            Element       = BaseEnums.ElementType.Physical;
            SkillCategory = BaseEnums.SkillCategory.Basic; // SP 생성 (Basic)
            SpCost        = 0;
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

            // INT*40 피해 (치명타 적용, 무조건 적중)
            bool isCrit        = Random.value <= Caster.CritChanceCurr;
            float critMult     = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage         = Mathf.RoundToInt(Caster.IntStat * 40f * critMult);

            var ctx = new DamageContext(
                Caster, damage,
                BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget },
                Element, isCrit);

            foreach (var target in TargetUnits)
            {
                // TODO: 마력탄 투사체 SFX
                yield return new WaitForSeconds(0.3f);
                if (target.isActive)
                {
                    target.TakeDamage(ctx);
                    Debug.Log($"[마력탄] {Caster.UnitName} → {target.UnitName}: {damage} 피해 (INT*40{(isCrit ? ", 치명" : "")})");
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
