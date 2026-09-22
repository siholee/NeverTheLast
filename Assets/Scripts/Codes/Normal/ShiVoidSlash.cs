using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

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
        private const int FixedDamage = 200;
        private const float DexDamageCoefficient = 4.9f;

        public ShiVoidSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            CastingDelay = 0.35f;
            // 저레벨의 지나치게 낮은 저점을 고정 피해로 보완하고, DEX 성장분은 직접 계수로 반영한다.
            MaxStage = 1;
            Power = FixedDamage;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            int dex = Caster.GetBaseDex();
            return Mathf.Max(1, Mathf.RoundToInt(
                (FixedDamage + dex * DexDamageCoefficient) * critMultiplier));
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
