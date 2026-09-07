using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 수르트 U — 라그나로크. 단일 적에게 STR 위력 120을 가한 뒤
    /// 적 전체에게 STR 위력 80과 불 원소를 부여한다.
    ///
    /// 최초 단일 대상이 광역 단계까지 살아 있으면 같은 대상을 두 번 때린 것으로 계산한다 —
    /// 고유 패시브 '황혼'의 방어막도 그만큼 두 번 발동한다.
    /// </summary>
    public sealed class SurtrRagnarok : UltimateCode
    {
        private const int SingleTargetPower = 120;
        private const int AreaPower = 80;

        public SurtrRagnarok(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "라그나로크";
            Cooldown = 5;
            CastingDelay = 0.6f;
            MaxStage = 1;
            Power = SingleTargetPower;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash };
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
            if (!cast)
            {
                StopCode();
                yield break;
            }

            List<Unit> enemies = CombatTargets.AliveEnemies(Caster);
            if (enemies.Count == 0)
            {
                StopCode();
                yield break;
            }

            Unit primary = CombatTargets.PickByPriority(enemies);

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;

            // 단일 타격은 CurrentPower를, 뒤따르는 광역 타격은 그에 비례한 위력을 쓴다.
            // 단계·비례 성분이 붙어도 두 타격의 비율이 그대로 유지되게 하기 위해서다.
            int singlePower = CurrentPower;
            int areaPower = Mathf.Max(1, Mathf.RoundToInt(singlePower * ((float)AreaPower / SingleTargetPower)));

            Deal(primary, singlePower, DamageTag.SingleTarget, isCrit, crit);

            foreach (Unit enemy in enemies)
            {
                Deal(enemy, areaPower, DamageTag.AllTarget, isCrit, crit);
                if (enemy.isActive)
                    enemy.GrantCombatElement(
                        BaseEnums.UnitElement.Pyro,
                        Unit.CommonElementAuraDuration,
                        Caster);
            }

            StopCode();
        }

        private void Deal(Unit target, int power, int targetTag, bool isCrit, float crit)
        {
            if (target == null || !target.isActive || target.IsUntargetable) return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) * crit));
            target.TakeDamage(new DamageContext(
                Caster,
                damage,
                BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    targetTag,
                    DamageTag.UltAttack,
                    DamageTag.Physical,
                    DamageTag.ContactAttack,
                    DamageTag.Slash,
                },
                isCrit));
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && CombatTargets.AliveEnemies(Caster).Count > 0;
    }
}
