using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 메테오 (세이 Lv.75 궁극기)<br/>
    /// 적 전체에게 INT*120 바위 광역 피해를 입힌다.<br/>
    /// 궁극기 에너지가 가득 찼을 때 사용 가능 (ManaCurr >= ManaMax).
    /// </summary>
    public class Meteor : UltimateCode
    {
        public Meteor(UltimateCodeContext context) : base(context)
        {
            CodeType     = BaseEnums.CodeType.Ultimate;
            CodeName     = "메테오";
            Caster       = context.Caster;
            CastingDelay = 2.0f;
            Element      = BaseEnums.ElementType.Geo;
            TargetType   = BaseEnums.TargetType.AoE; // 광역: 적 전체
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"[메테오] {Caster.UnitName} 시전 — 적 전체 INT*120 바위 광역 피해");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 캐스팅 연출 (긴 선딜)
            if (CastingDelay > 0f)
                yield return new WaitForSeconds(CastingDelay);

            // TargetUnits = 적 전체 (BattleManager.ResolveTargetsCoroutine이 AoE로 사전 설정)
            if (TargetUnits == null || TargetUnits.Count == 0)
            {
                StopCode();
                yield break;
            }

            bool isCrit    = Random.value <= Caster.CritChanceCurr;
            float critMult = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage     = Mathf.RoundToInt(Caster.IntStat * 120f * critMult);

            var ctx = new DamageContext(
                Caster, damage,
                BaseEnums.CodeType.Ultimate,
                new List<int> { DamageTag.AllTarget },
                Element, isCrit);

            // TODO: 대형 운석 낙하 연출 SFX
            yield return new WaitForSeconds(0.5f);

            foreach (var target in TargetUnits)
            {
                if (target.isActive)
                {
                    target.TakeDamage(ctx);
                    Debug.Log($"[메테오] {target.UnitName}에게 {damage} 피해 (INT*120{(isCrit ? ", 치명" : "")})");
                }
            }

            StopCode();
        }

        public override void StopCode() => Caster.isCasting = false;

        public override bool HasValidTarget()
            => GridManager.Instance.TargetAllEnemies(Caster).Count > 0;
    }
}
