using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    public class Sachi : BaseNormalCode
    {
        public Sachi(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            MaxStage = 1;
            Power = 40;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            // 사양 "INT의 40%" = 위력 40. 피해 = 위력 × INT × SkillPowerScale.
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(Power, BaseEnums.PrimaryStat.INT) * critMultiplier));
        }

        protected override List<int> GetDamageTags()
        {
            return new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.NonContactAttack,
                DamageTag.Special,
            };
        }
    }
}
