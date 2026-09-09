using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 소환수 공용 규칙.
    ///
    /// 소환수는 <b>칸을 차지하지 않는다</b>는 요구 때문에 별도 <see cref="Unit"/>을 만들지 않는다.
    /// 대신 소환자에 붙는 논리 객체로 두고, 피해만 이 헬퍼를 통해 나간다.
    ///
    ///   · 소환자의 <c>가하는 피해 증가</c> 버프를 물려받지 않는다.
    ///     (<see cref="DamageTag.SummonAttack"/>을 보고 <c>Unit.CalculateFinalDamage</c>가 건너뛴다)
    ///   · 소환수 전용 배율만 받는다. 황금 사과는 장착자의 소환수에만 적용되고,
    ///     로키의 <c>소환사</c>(265)는 <b>필드 전체</b>에 적용된다.
    ///   · <c>n번째 일반행동마다</c> 계열 카운터는 <see cref="DamageTag.NormalAttack"/>을 보므로
    ///     소환수 공격이 자동으로 제외된다.
    /// </summary>
    public static class Summons
    {
        /// <summary>소환수 공격 하나를 해결한다. 실제로 들어간 피해 컨텍스트를 만들어 넘긴다.</summary>
        /// <param name="owner">소환자. 피해 계수의 기준이 된다.</param>
        /// <param name="target">대상.</param>
        /// <param name="power">스킬 위력.</param>
        /// <param name="stat">위력에 곱할 소환자의 스탯.</param>
        /// <param name="extraTags">접촉/물리 등 소환수별 추가 태그.</param>
        public static void Deal(
            Unit owner, Unit target, int power, BaseEnums.PrimaryStat stat, params int[] extraTags)
        {
            if (owner == null || !owner.isActive) return;
            if (target == null || !target.isActive || target.IsUntargetable) return;

            bool isCrit = Random.value <= owner.CritChanceCurr;
            float critMultiplier = isCrit ? owner.CritMultiplierCurr : 1f;

            int damage = Mathf.Max(1, Mathf.RoundToInt(
                owner.SkillDamage(power, stat) * critMultiplier * DamageMultiplier(owner)));

            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.SummonAttack,
                DamageTag.AdditionalAttack,
            };
            if (extraTags != null) tags.AddRange(extraTags);

            target.TakeDamage(new DamageContext(owner, damage, BaseEnums.CodeType.Passive, tags, isCrit));
        }

        /// <summary>
        /// 소환자 개인 효과와 필드 전체 효과 중 가장 높은 소환수 피해 배율.
        /// 같은 효과를 여럿이 들고 있어도 중첩되지 않는다.
        /// </summary>
        public static float DamageMultiplier(Unit owner)
        {
            if (owner == null) return 1f;

            float bonus = 0f;
            foreach (var effect in owner.ActiveStatuses.SelectMany(status => status.Effects))
            {
                if (effect.EffectObject == null) continue;
                bonus = Mathf.Max(bonus, effect.EffectObject.SummonDamageMultiplierModifier(owner) - 1f);
            }

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(owner))
            {
                if (ally == owner) continue;
                foreach (var effect in ally.ActiveStatuses.SelectMany(status => status.Effects))
                {
                    if (effect.EffectObject == null) continue;
                    bonus = Mathf.Max(bonus,
                        effect.EffectObject.AlliedSummonDamageMultiplierModifier(ally, owner) - 1f);
                }
            }
            return 1f + Mathf.Max(0f, bonus);
        }
    }
}
