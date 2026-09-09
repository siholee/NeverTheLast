using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    /// <summary>
    /// 시의 일반행동. 빠르게 파고들어 베는 접촉 공격이다.
    ///
    /// 시는 '시의 종언'(ShiFinalVerse)이 일반행동 적중마다 스택을 쌓는 구조이므로,
    /// 위력을 낮게 잡아 **타수 자체가 자원**이 되도록 설계했다. 타수를 늘리는 것은 DEX다.
    /// </summary>
    public sealed class ShiVoidSlash : BaseNormalCode
    {
        public ShiVoidSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            CastingDelay = 0.35f;
            MaxStage = 3;
            StagePowers = new[] { 38, 46, 54 };
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override List<int> GetDamageTags()
        {
            return new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.ContactAttack,
                DamageTag.Physical,
                DamageTag.Slash,
            };
        }
    }
}
