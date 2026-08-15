using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Normal
{
    /// <summary>
    /// 세이의 일반 공격. 카이사 Q처럼 여러 유도탄을 주변 적에게 균등 분배하며,
    /// 고립된 적에게는 모든 유도탄이 집중된다.
    ///
    /// 6발, 총 위력 80의 특수 피해를 입힌다.
    ///
    /// 세이는 INT 주력이므로 두 형태 모두 INT를 근거로 피해를 낸다.
    /// </summary>
    public sealed class SeiRadiantBolt : BaseNormalCode
    {
        private const int BasePower = 80;
        private const int BaseMissileCount = 6;

        public SeiRadiantBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = BasePower;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(BasePower, BaseEnums.PrimaryStat.INT) * critMultiplier));
        }

        protected override List<int> GetDamageTags()
        {
            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.NonContactAttack,
                DamageTag.Special,
            };
            return tags;
        }

        /// <summary>
        /// 우선 대상은 기존 일반공격 규칙으로 정하고, 나머지 적도 유도탄 분배 후보로 함께 반환한다.
        /// </summary>
        protected override List<Unit> SelectTarget()
        {
            List<Unit> primaryTargets = base.SelectTarget();
            if (primaryTargets.Count == 0) return primaryTargets;

            Unit primary = primaryTargets[0];
            return new[] { primary }
                .Concat(GetAvailableEnemies()
                    .Where(enemy => enemy != primary)
                    .OrderByDescending(enemy => enemy.Priority)
                    .ThenBy(enemy => enemy.HpCurr))
                .ToList();
        }

        protected override IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            List<Unit> availableTargets = targets
                .Where(target => target != null && target.isActive && !target.IsUntargetable)
                .ToList();
            if (availableTargets.Count == 0)
            {
                yield break;
            }

            int missileCount = BaseMissileCount;
            int damagePerMissile = context.Damage / missileCount;
            int damageRemainder = context.Damage % missileCount;
            var plannedTargets = new List<Unit>(missileCount);

            for (int i = 0; i < missileCount; i++)
            {
                Unit target = availableTargets[i % availableTargets.Count];
                plannedTargets.Add(target);
                // 세이의 유도탄은 특수 피해라 직선형이다.
                GameManager.Instance.sfxManager.FireSingleProjectile(
                    _prefab,
                    Caster,
                    target,
                    delay,
                    ProjectilePathType.Linear,
                    ProjectileFlight.DataFor(ProjectilePathType.Linear));
            }

            yield return new WaitForSeconds(delay);

            for (int i = 0; i < missileCount; i++)
            {
                Unit target = plannedTargets[i];
                if (target == null || !target.isActive || target.IsUntargetable)
                {
                    target = GetAvailableEnemies().FirstOrDefault();
                }
                if (target == null) continue;

                int missileDamage = Mathf.Max(1, damagePerMissile + (i < damageRemainder ? 1 : 0));
                DamageContext missileContext = new(
                    Caster,
                    missileDamage,
                    BaseEnums.CodeType.Normal,
                    BuildMissileTags(),
                    context.IsCrit);
                target.TakeDamage(missileContext);
                Caster.Invoke(
                    BaseEnums.UnitEventType.OnNormalAttackHit,
                    new EventContext(Caster, target, missileContext));
            }
        }

        private List<int> BuildMissileTags()
        {
            List<int> tags = new()
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.NonContactAttack,
                DamageTag.Special,
            };
            return tags;
        }
    }
}
