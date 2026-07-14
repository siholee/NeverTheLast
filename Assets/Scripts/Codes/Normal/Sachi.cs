using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Helpers;
using UnityEngine;

namespace Codes.Normal
{
    public class Sachi : BaseNormalCode
    {
        public Sachi(NormalCodeContext context) : base(context)
        {
            CodeName = "사친";
            MaxStage = 1;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * 0.4f * critMultiplier));
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
