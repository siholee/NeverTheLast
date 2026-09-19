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
    /// 수르트 U — 라그나로크. 단일 적에게 고정 위력 180 + STR×0.8을 가한 뒤
    /// 적 전체에게 고정 위력 100 + STR×0.6과 불 원소를 부여한다.
    ///
    /// 최초 단일 대상이 광역 단계까지 살아 있으면 같은 대상을 두 번 때린다.
    ///
    /// 체력 연소(<see cref="Passive.SurtrBurn"/>)는 <b>행동에 한 번</b>이라 때린 횟수와 무관하다.
    /// 적이 몇이든 값이 같고, 고유 패시브 '황혼'의 보호막은 맞힌 적 수만큼 붙으므로
    /// 라그나로크는 연소를 가장 싸게 쓰고 가장 크게 두르는 행동이다.
    /// </summary>
    public sealed class SurtrRagnarok : UltimateCode
    {
        private const int SingleTargetFlatPower = 180;
        private const float SingleTargetStrCoefficient = 0.8f;
        private const int AreaFlatPower = 100;
        private const float AreaStrCoefficient = 0.6f;

        private float _burnMultiplier = 1f;

        public SurtrRagnarok(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "라그나로크";
            CastingDelay = 0.6f;
            MaxStage = 1;
            Power = SingleTargetFlatPower;
            PowerStatCoefficient = SingleTargetStrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
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
            _burnMultiplier = Passive.SurtrBurn.Pay(Caster);

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;

            // 두 타격 모두 고정 몫으로 초반 저점을 보장하되, 후속 광역의 STR 계수는 조금 낮춘다.
            int singlePower = CurrentPower;
            int areaPower = Mathf.Max(1, AreaFlatPower + Mathf.RoundToInt(
                Caster.GetBaseStr() * AreaStrCoefficient));

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
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) * crit * _burnMultiplier));
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
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && CombatTargets.AliveEnemies(Caster).Count > 0;
    }
}
