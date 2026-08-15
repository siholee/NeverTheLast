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
    /// 세이의 일반 공격. TFT 카이사(이카시아의 비)를 따른다.
    ///
    ///   · <b>첫 발</b>은 평소 일반공격 규칙으로 고른 <b>지정 대상</b>에게 나간다.
    ///   · <b>나머지 5발</b>은 살아 있는 적 중 <b>무작위</b>로 흩어진다.
    ///
    /// 6발, 총 위력 80의 특수 피해를 입힌다. 세이는 INT 주력이라 INT를 근거로 피해를 낸다.
    /// 모든 발사체는 <b>비접촉 특수</b> 공격이므로 직선으로 날아간다.
    /// </summary>
    public sealed class SeiRadiantBolt : BaseNormalCode
    {
        private const int BasePower = 80;
        private const int BaseMissileCount = 6;

        public SeiRadiantBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = BasePower;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
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
        /// 첫 발이 나갈 지정 대상만 정한다. 나머지 발사체의 대상은 발사 시점에 무작위로 뽑는다.
        /// </summary>
        protected override List<Unit> SelectTarget() => base.SelectTarget();

        protected override IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            Unit primary = targets.FirstOrDefault(
                target => target != null && target.isActive && !target.IsUntargetable);
            if (primary == null) yield break;

            int missileCount = BaseMissileCount;
            int damagePerMissile = context.Damage / missileCount;
            int damageRemainder = context.Damage % missileCount;
            var plannedTargets = new List<Unit>(missileCount);

            for (int i = 0; i < missileCount; i++)
            {
                // 첫 발은 지정 대상, 나머지는 무작위 — TFT 카이사와 같은 분배다.
                Unit target = i == 0 ? primary : RandomEnemy() ?? primary;
                plannedTargets.Add(target);

                GameManager.Instance.sfxManager.FireElementalProjectile(
                    Caster,
                    target,
                    delay,
                    ProjectilePathType.Linear,
                    ProjectileFlight.DataFor(ProjectilePathType.Linear),
                    _prefab);
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

        /// <summary>살아 있는 적 중 하나를 무작위로 고른다. 없으면 null.</summary>
        private Unit RandomEnemy()
        {
            List<Unit> candidates = GetAvailableEnemies()
                .Where(enemy => enemy != null && enemy.isActive && !enemy.IsUntargetable)
                .ToList();
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
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
