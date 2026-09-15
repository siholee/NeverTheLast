using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>발키리 N — 창 던지기. 단일 `60 + STR×0.6`. 비접촉·물리·화살.</summary>
    public sealed class ValkyrieJavelin : BaseNormalCode
    {
        public ValkyrieJavelin(NormalCodeContext context) : base(context)
        {
            CodeName = "창 던지기";
            Power = 60;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Arrow };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
        };
    }
}
