using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 오딘 N — 궁니르.
    ///
    /// 느리고 한 번이 크다. DEX 12의 보스라 좀처럼 오지 않는 대신 한 방이 무겁다.
    /// <b>회피되지 않는다</b> — 빗나가지 않는 창이라는 뜻이며, 같은 규칙을
    /// 전리품인 궁니르(4507)가 플레이어에게 그대로 넘겨 준다.
    /// </summary>
    public sealed class OdinGungnir : BaseNormalCode
    {
        private const int NormalPower = 160;

        public OdinGungnir(NormalCodeContext context) : base(context)
        {
            CodeName = "궁니르";
            CastingDelay = 0.5f;
            MaxStage = 1;
            Power = NormalPower;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Pierce };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce,
        };
    }
}
