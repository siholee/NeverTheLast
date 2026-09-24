using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>공허의 프리즘 N — 단일 적에게 100 + INT×0.6 위력의 비접촉 특수 피해.</summary>
    public sealed class VoidPrismNormal : BaseNormalCode
    {
        public VoidPrismNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "공허 광선";
            Power = 20;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };
    }
}
