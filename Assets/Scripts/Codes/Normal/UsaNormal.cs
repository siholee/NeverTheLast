using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 우사 N(180) — 분신을 1기 부른다. 피해를 주지 않는 일반행동이다.
    ///
    /// 상한(<see cref="UsaTaoistNature.MaxClones"/>)에 닿아 더 부를 수 없어도 행동은 소모된다.
    /// 자리 상황을 보고 다른 일을 하지는 않는다 — 분신을 늘리는 것이 이 캐릭터의 본업이다.
    /// </summary>
    public sealed class UsaNormalAttack : BaseNormalCode
    {
        public UsaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "분신 소환";
            Power = 0;
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            UsaTaoistNature.SummonClones(Caster, 1);

            // 피해가 없는 일반행동도 '일반행동 한 번'으로 세야 한다.
            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>분신 N(502) — 단일 적에게 160 + INT×0.5. 소환수 공격이다.</summary>
    public sealed class CloneNormalAttack : BaseNormalCode
    {
        public CloneNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 160;
            PowerStat = BaseEnums.PrimaryStat.INT;
            PowerStatCoefficient = 0.5f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT)
                * (critMultiplier > 1f ? Summons.CritMultiplier(Caster.SummonOwner, critMultiplier) : 1f)
                * Summons.DamageMultiplier(Caster.SummonOwner)));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.SummonAttack, DamageTag.Special, DamageTag.ContactAttack,
        };
    }
}
