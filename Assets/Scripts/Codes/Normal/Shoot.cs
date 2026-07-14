using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Helpers;

namespace Codes.Normal
{
    public class Shoot : BaseNormalCode
    {
        public Shoot(NormalCodeContext context) : base(context)
        {
            CodeName = "사격";
            MaxStage = 3;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            int stageBonus = CurrentStage switch
            {
                1 => 100,
                2 => 200,
                _ => 300,
            };

            return (int)((Caster.GetBaseStr() + stageBonus) * critMultiplier);
        }

        protected override List<int> GetDamageTags()
        {
            return new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.NonContactAttack,
                DamageTag.Physical,
            };
        }
    }
}
