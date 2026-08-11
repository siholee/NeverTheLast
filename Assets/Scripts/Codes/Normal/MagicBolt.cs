using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    public class MagicBolt : BaseNormalCode
    {
        public MagicBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            MaxStage = 3;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            int stageBonus = CurrentStage switch
            {
                1 => 100,
                2 => 200,
                _ => 300,
            };

            return (int)((Caster.GetBaseInt() + stageBonus) * critMultiplier);
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

    /// <summary>케찰코아틀 일반공격: 단일 적에게 INT 기반 위력 60 특수 피해.</summary>
    public sealed class QuetzalcoatlBall : BaseNormalCode
    {
        public QuetzalcoatlBall(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Special };
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
