using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 수르트 N — 단일 적에게 고정 위력 110 + STR×0.8의 접촉 물리 베기.
    ///
    /// 행동을 열 때 체력을 태운다(<see cref="Passive.SurtrBurn"/>) — 최대 체력 15%로 피해 +40%,
    /// 태운 뒤 30% 아래가 되면 태우지 않는다. 예전에는 고유 패시브 '황혼'이 하던 일이다.
    /// </summary>
    public sealed class SurtrScorchingSlash : BaseNormalCode
    {
        private const int NormalFlatPower = 110;
        private const float NormalStrCoefficient = 0.8f;

        public SurtrScorchingSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            CastingDelay = 0.4f;
            MaxStage = 1;
            Power = NormalFlatPower;
            PowerStatCoefficient = NormalStrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Slash };
        }

        // CalculateDamage는 행동 하나에 한 번 불린다. 연소 값도 여기서 한 번 치른다.
        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier *
                Passive.SurtrBurn.Pay(Caster)));

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
