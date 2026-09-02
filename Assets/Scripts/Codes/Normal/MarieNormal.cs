using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>마리 N — 단일 적에게 LUK×0.5의 비접촉 물리 사격.</summary>
    public sealed class MariePistolShot : BaseNormalCode
    {
        public MariePistolShot(NormalCodeContext context) : base(context)
        {
            CodeName = "장교의 사격";
            Power = 50;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.LUK) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
        };
    }
}
