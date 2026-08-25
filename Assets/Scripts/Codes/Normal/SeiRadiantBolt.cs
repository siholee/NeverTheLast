using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 세이의 일반 공격. 단일 적에게 INT 위력 50의 비접촉 특수 피해를 입힌다.
    /// GEO 전용 금색 발사체를 사용해 카이사 기본 공격처럼 짧고 선명하게 직선 비행한다.
    /// </summary>
    public sealed class SeiRadiantBolt : BaseNormalCode
    {
        private const int BasePower = 50;

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

        protected override IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            const float flightDuration = 0.32f;
            foreach (Unit target in targets)
            {
                if (target == null || !target.isActive || target.IsUntargetable) continue;

                var boltPrefab = ElementalProjectiles.PrefabFor(BaseEnums.UnitElement.Anemo)
                    ?? ElementalProjectiles.PrefabFor(BaseEnums.UnitElement.Geo)
                    ?? _prefab;
                GameManager.Instance?.sfxManager?.FireTintedProjectile(
                    boltPrefab,
                    BaseEnums.UnitElement.Geo,
                    Caster,
                    target,
                    flightDuration,
                    ProjectilePathType.Linear,
                    ProjectileFlight.DataFor(ProjectilePathType.Linear));

                yield return new WaitForSeconds(flightDuration);
                if (target == null || !target.isActive || target.IsUntargetable) continue;
                target.TakeDamage(context);
                Caster.Invoke(
                    BaseEnums.UnitEventType.OnNormalAttackHit,
                    new EventContext(Caster, target, context));
            }
        }

    }
}
