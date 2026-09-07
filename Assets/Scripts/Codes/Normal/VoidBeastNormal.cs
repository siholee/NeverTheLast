using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>공허의 멧돼지 N — 단일 적에게 80 + STR×0.6 위력의 접촉 물리 피해.</summary>
    public sealed class VoidBeastNormal : BaseNormalCode
    {
        public VoidBeastNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "멧돼지의 엄니";
            Power = 80;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };
    }
}
