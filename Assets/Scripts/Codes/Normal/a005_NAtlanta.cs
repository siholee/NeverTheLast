using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    /// <summary>
    /// 아탈란테의 일반공격: a005-N아탈란테
    /// 단일 적에게 DEX 기반 위력 60의 물리·비접촉 피해를 입힌다.
    /// </summary>
    public class a005_NAtlanta : BaseNormalCode
    {
        public a005_NAtlanta(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 60;
        }

        protected override List<int> GetDamageTags()
        {
            List<int> tags = new List<int>
            {
                BaseClasses.DamageTag.SingleTarget,
                BaseClasses.DamageTag.NormalAttack,
                BaseClasses.DamageTag.NonContactAttack,
                BaseClasses.DamageTag.Physical,
            };
            return tags;
        }
    }
}
