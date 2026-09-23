using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Normal
{
    /// <summary>
    /// 피그말리온의 일반행동: a004-N피그말리온
    /// 고정 위력 75 + CON×0.5의 CON 기반 단일공격. 초기 서포터 승격과 함께 저점을 올렸다.
    /// </summary>
    public class a004_NPygmalion : BaseNormalCode
    {
        private const int NormalFlatPower = 75;
        private const float NormalConCoefficient = 0.5f;

        public a004_NPygmalion(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = NormalFlatPower;
            PowerStatCoefficient = NormalConCoefficient;
            PowerStat = BaseEnums.PrimaryStat.CON;
        }

        /// <summary>
        /// 피그말리온은 CON을 근거로 피해를 계산한다.
        /// </summary>
        protected override int CalculateDamage(float critMultiplier)
        {
            // 피그말리온은 방어형 컨셉이므로 CON을 근거로 때린다.
            float summonCrit = critMultiplier > 1f
                ? Summons.CritMultiplier(Caster, critMultiplier)
                : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * summonCrit *
                Summons.DamageMultiplier(Caster)));
        }

        protected override List<int> GetDamageTags()
            => new()
            {
                DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Physical,
                DamageTag.ContactAttack, DamageTag.SummonAttack,
            };
        
        protected override IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            foreach (var target in targets)
            {
                ProjectilePathType path = ProjectileFlight.PathFor(context);
                var token = new ProjectileImpactToken();
                GameManager.Instance.sfxManager.FireElementalProjectile(
                    Caster, target, delay, path, ProjectileFlight.DataFor(path), _prefab,
                    token.MarkImpact);
                yield return ProjectileFlight.WaitForImpact(token, delay);
                target.TakeDamage(context);
            }
        }
    }
}
