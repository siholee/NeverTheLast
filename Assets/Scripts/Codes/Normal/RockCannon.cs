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
    /// 암석포 (세이 Lv.1 스킬 — Normal 행동 슬롯, SP-1)<br/>
    /// 단일 적에게 CON*60 바위 피해 + 바위 원소 부여.<br/>
    /// 이후 다른 모든 적에게 CON*40 바위 스플래시 피해.<br/>
    /// P1 패시브 활성 시: CON → GetEffectiveCon() = CON+INT 사용.
    /// </summary>
    public class RockCannon : NormalCode
    {
        public RockCannon(NormalCodeContext context) : base(context)
        {
            CodeType      = BaseEnums.CodeType.Normal;
            CodeName      = "암석포";
            Caster        = context.Caster;
            CastingDelay  = 0.5f;
            ManaAmount    = 20;
            Element       = BaseEnums.ElementType.Geo;
            SkillCategory = BaseEnums.SkillCategory.Skill;
            SpCost        = 1;
            TargetType    = BaseEnums.TargetType.Single; // 메인 타겟: 단일
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

            // P1 패시브 적용: GetEffectiveCon() = CON + INT (P1 활성 시)
            int effectiveCon = Caster.GetEffectiveCon();
            bool p1Active    = Caster.PermanentPassiveFlags.Contains("SeiP1");

            // ── 메인 타겟: CON*60 바위 피해 ──────────────────────────────────
            bool isCrit    = Random.value <= Caster.CritChanceCurr;
            float critMult = isCrit ? Caster.CritMultiplierCurr : 1f;
            int mainDmg    = Mathf.RoundToInt(effectiveCon * 60f * critMult);

            var mainCtx = new DamageContext(
                Caster, mainDmg,
                BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget },
                Element, isCrit);

            Unit mainTarget = TargetUnits[0];
            // TODO: 암석포 투사체 SFX
            yield return new WaitForSeconds(0.4f);
            if (mainTarget.isActive)
            {
                mainTarget.TakeDamage(mainCtx);
                Debug.Log($"[암석포] 메인: {mainTarget.UnitName}에게 {mainDmg} 피해 " +
                          $"(effCON={effectiveCon}×60, P1:{p1Active}{(isCrit ? ", 치명" : "")})");
            }

            // ── 스플래시: 다른 모든 적에게 CON*40 바위 피해 ─────────────────
            var allEnemies = GridManager.Instance.ResolveRangeTarget(Caster, true);
            allEnemies.Remove(mainTarget);
            if (allEnemies.Count > 0)
            {
                yield return new WaitForSeconds(0.25f);
                int splashDmg = Mathf.RoundToInt(effectiveCon * 40f * critMult);
                var splashCtx = new DamageContext(
                    Caster, splashDmg,
                    BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.AllTarget },
                    Element, isCrit);

                foreach (var enemy in allEnemies)
                {
                    if (enemy.isActive)
                    {
                        enemy.TakeDamage(splashCtx);
                        Debug.Log($"[암석포] 스플래시: {enemy.UnitName}에게 {splashDmg} 피해");
                    }
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
