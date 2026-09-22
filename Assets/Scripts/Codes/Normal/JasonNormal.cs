using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    public sealed class JasonNormalAttack : BaseNormalCode
    {
        public JasonNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 10;
            PowerStatCoefficient = 0.8f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };
    }
}
