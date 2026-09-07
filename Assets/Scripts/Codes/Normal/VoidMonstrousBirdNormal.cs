using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>공허의 괴조 N — 단일 적에게 100 + STR×0.6 위력의 접촉 물리 피해.</summary>
    public sealed class VoidMonstrousBirdNormal : BaseNormalCode
    {
        public VoidMonstrousBirdNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "괴조의 발톱";
            Power = 100;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.35f;
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
