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
            // 코드 단계(1~3)는 더 이상 오르지 않는다. 늘 1단계 값이던 38을 고정 위력으로 둔다.
            MaxStage = 1;
            Power = 38;
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
