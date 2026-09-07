using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>범용 거인 N — 단일 대상에게 STR 기반 접촉·물리 피해.</summary>
    public sealed class GenericGiantNormalAttack : BaseNormalCode
    {
        public GenericGiantNormalAttack(NormalCodeContext context, int power) : base(context)
        {
            CodeName = "일반공격";
            Power = power;
            CodeTags = new List<int> { DamageTag.ContactAttack, DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical,
        };
    }
}
