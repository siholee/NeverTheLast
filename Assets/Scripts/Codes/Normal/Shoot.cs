using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    public class Shoot : BaseNormalCode
    {
        public Shoot(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            MaxStage = 3;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            StagePowers ??= new[] { 45, 55, 65 };
            return RollDamage(critMultiplier);
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
