using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>범용 씨앗 N — DEX 기반 위력 20의 화살을 같은 대상에게 3발 발사한다.</summary>
    public sealed class GenericSeedVolley : BaseNormalCode
    {
        private const int VolleyPower = 20;
        private const int BaseHitCount = 3;

        public GenericSeedVolley(NormalCodeContext context) : base(context)
        {
            CodeName = "연속 사격";
            Power = VolleyPower;
            CastingDelay = 0.35f;
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
        };

        protected override IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            int hitCount = BaseHitCount + (Caster.HasStatus(GenericSeedStatusIds.Overdrive) ? 1 : 0);
            for (int hit = 0; hit < hitCount; hit++)
            {
                if (targets.Count == 0 || targets[0] == null || !targets[0].isActive) yield break;
                var hitContext = new DamageContext(
                    Caster, context.Damage, BaseEnums.CodeType.Normal,
                    new List<int>(context.DamageTags), context.IsCrit,
                    context.Penetration, context.DurabilityPenetration);
                yield return base.FireProjectile(targets, delay / hitCount, hitContext);
            }
        }
    }
}
