using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 수르트 N — 단일 적에게 STR 기반 위력 90의 접촉 물리 베기.
    ///
    /// 도발과 방어막을 두르던 대체행동은 사라졌다. 수르트는 전열 수호자가 아니라
    /// 체력을 태워 때리는 딜러이며, 황혼(280)이 이 한 방의 값을 매 행동 치른다.
    /// </summary>
    public sealed class SurtrScorchingSlash : BaseNormalCode
    {
        private const int NormalPower = 90;

        public SurtrScorchingSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            CastingDelay = 0.4f;
            MaxStage = 1;
            Power = NormalPower;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Slash };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget,
            DamageTag.NormalAttack,
            DamageTag.Physical,
            DamageTag.ContactAttack,
            DamageTag.Slash,
        };
    }
}
