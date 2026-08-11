using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    public sealed class AsclepiusNormalAttack : BaseNormalCode
    {
        public AsclepiusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(Power, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };
    }

    public sealed class AmaterasuNormalAttack : BaseNormalCode
    {
        public AmaterasuNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 70;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(Power, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Physical, DamageTag.NonContactAttack,
        };
    }
}
