using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    /// <summary>
    /// 시의 일반 공격. 빠르게 파고들어 베는 접촉 공격이다.
    ///
    /// 시는 '시의 종언'(ShiFinalVerse)이 일반 공격 적중마다 스택을 쌓는 구조이므로,
    /// 위력을 낮게 잡고 쿨다운을 짧게 두어 **타수 자체가 자원**이 되도록 설계했다.
    /// </summary>
    public sealed class ShiVoidSlash : BaseNormalCode
    {
        public ShiVoidSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Cooldown = 1.6f;      // 기본 2초보다 짧다 — 스택을 빨리 쌓기 위한 설계
            CastingDelay = 0.35f;
            ManaAmount = 12;
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
