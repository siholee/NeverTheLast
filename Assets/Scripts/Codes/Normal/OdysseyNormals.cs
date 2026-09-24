using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 히포캅투스 N(1913) — 단일 적에게 50 + STR×0.8.
    /// 공격력이 형편없는 대신 몸으로 버티는 것이 이 괴수의 몫이다.
    /// </summary>
    public sealed class HippocampusStrike : BaseNormalCode
    {
        public HippocampusStrike(NormalCodeContext context) : base(context)
        {
            CodeName = "들이받기";
            Power = 50;
            PowerStat = BaseEnums.PrimaryStat.STR;
            PowerStatCoefficient = 0.8f;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };
    }
}
