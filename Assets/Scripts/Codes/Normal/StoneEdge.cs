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
    /// 스톤 에지 (세이 Lv.40 스킬, SP-2)<br/>
    /// 지정 단일 적에게 CON*30 바위 피해를 3회 입히고 바위 원소를 부여한다.<br/>
    /// 플레이어가 대상을 직접 지정 가능 (Standard Single 타겟팅).<br/>
    /// P1 패시브 활성 시: CON → GetEffectiveCon() = CON+INT 사용.
    /// </summary>
    public class StoneEdge : NormalCode
    {
        public StoneEdge(NormalCodeContext context) : base(context)
        {
            CodeType      = BaseEnums.CodeType.Normal;
            CodeName      = "스톤 에지";
            Caster        = context.Caster;
            CastingDelay  = 0.5f;
            ManaAmount    = 30;
            Element       = BaseEnums.ElementType.Geo;
            SkillCategory = BaseEnums.SkillCategory.Skill;
            SpCost        = 2;
            TargetType    = BaseEnums.TargetType.Single; // 플레이어 지정 가능
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

            int effectiveCon = Caster.GetEffectiveCon(); // P1: CON+INT
            bool p1Active    = Caster.PermanentPassiveFlags.Contains("SeiP1");
            Unit target      = TargetUnits[0];

            for (int i = 0; i < 3; i++)
            {
                if (!target.isActive) break;

                bool isCrit    = Random.value <= Caster.CritChanceCurr;
                float critMult = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage     = Mathf.RoundToInt(effectiveCon * 30f * critMult);

                var ctx = new DamageContext(
                    Caster, damage,
                    BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.SingleTarget },
                    Element, isCrit);

                // TODO: 날카로운 암석 투사체 SFX
                yield return new WaitForSeconds(0.35f);
                if (target.isActive)
                {
                    target.TakeDamage(ctx);
                    Debug.Log($"[스톤 에지] {i+1}/3 → {target.UnitName}: {damage} 피해 " +
                              $"(effCON={effectiveCon}×30, P1:{p1Active}{(isCrit ? ", 치명" : "")})");
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
