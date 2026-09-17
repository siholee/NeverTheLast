using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Normal
{
    /// <summary>
    /// 피그말리온의 일반행동: a004-N피그말리온
    /// CON 기반 위력 60의 단일공격
    /// </summary>
    public class a004_NPygmalion : BaseNormalCode
    {
        public a004_NPygmalion(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 60;
        }

        /// <summary>
        /// 피그말리온은 CON을 근거로 위력 60의 피해를 계산한다.
        /// </summary>
        protected override int CalculateDamage(float critMultiplier)
        {
            // 피그말리온은 방어형 컨셉이므로 CON을 근거로 때린다.
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));
        }
        
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
