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
    /// 스톤 샤워 (세이 Lv.25 스킬, SP-2)<br/>
    /// 적 전체에게 INT*60 바위 범위 피해.
    /// </summary>
    public class StoneShower : NormalCode
    {
        public StoneShower(NormalCodeContext context) : base(context)
        {
            CodeType           = BaseEnums.CodeType.Normal;
            CodeName           = "스톤 샤워";
            Caster             = context.Caster;
            CastingDelay       = 0.8f;
            ManaAmount         = 25;
            Element            = BaseEnums.ElementType.Geo;
            SkillCategory      = BaseEnums.SkillCategory.Skill;
            SpCost             = 2;
            TargetType         = BaseEnums.TargetType.Range; // 범위: 적 전체
            RangeTargetsEnemies = true;
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

            // TargetUnits = 적 전체 (BattleManager가 Range로 사전 결정)
            if (TargetUnits == null || TargetUnits.Count == 0)
            {
                StopCode();
                yield break;
            }

            bool isCrit    = Random.value <= Caster.CritChanceCurr;
            float critMult = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage     = Mathf.RoundToInt(Caster.IntStat * 60f * critMult);

            var ctx = new DamageContext(
                Caster, damage,
                BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.AllTarget },
                Element, isCrit);

            // TODO: 낙석 범위 SFX
            yield return new WaitForSeconds(0.3f);
            foreach (var target in TargetUnits)
            {
                if (target.isActive)
                {
                    target.TakeDamage(ctx);
                    Debug.Log($"[스톤 샤워] {target.UnitName}에게 {damage} 피해 (INT*60{(isCrit ? ", 치명" : "")})");
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
