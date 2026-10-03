using BaseClasses;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 피해 한 번을 <b>어느 방어 스탯이 경감하는가</b>, 그리고 효과가 <b>걸리는가</b>를 한 곳에서 정한다.
    ///
    /// 2026-09 개편 규칙(Detail_02 §3):
    ///   · 고정 피해(TrueDamage)        — 경감 없음
    ///   · 지속피해 · 원소 반응 피해    — 특수방어력
    ///   · 그 밖의 모든 피해            — 방어력
    /// 공격력이 계수든 방어력 · 최대 체력이 계수든 경감은 같은 규칙을 탄다.
    ///
    /// 경감식은 <see cref="UnitStats.MitigationMultiplier"/>, 기준값은 공격자 레벨의
    /// <see cref="UnitStats.ArmorConstant"/>다.
    /// </summary>
    public static class DamageMitigation
    {
        /// <summary>이 피해를 경감할 스탯. 고정 피해면 false.</summary>
        public static bool TryGetDefenseStat(DamageContext context, out BaseEnums.StatType stat)
        {
            stat = BaseEnums.StatType.Defense;
            if (context == null || context.HasTag(DamageTag.TrueDamage)) return false;

            if (context.CodeType == BaseEnums.CodeType.Effect || context.HasTag(DamageTag.ElementalReaction) ||
                context.HasTag(DamageTag.SpecialDamage))
                stat = BaseEnums.StatType.SpecialDefense;
            return true;
        }

        /// <summary>
        /// 해로운 효과의 명중 확률. <b>특수공격력 대 특수방어력</b>의 비율로 정한다.
        ///
        ///   명중 = 기본 확률 × 2 × 특공 / (특공 + 특방)
        ///
        /// 두 스탯이 같으면 기본 확률 그대로다. 비율식이라 레벨이 끝없이 올라도 저울이 기울지 않는다
        /// (옛 CON 차이식은 레벨이 오를수록 격차가 벌어져 상한 · 하한에 붙어 버렸다).
        /// </summary>
        public static float EffectHitChance(Unit source, Unit target, float baseChance = 1f)
        {
            float attack = source != null ? Mathf.Max(1f, source.StatValue(BaseEnums.StatType.SpecialAttack)) : 1f;
            float resist = target != null ? Mathf.Max(1f, target.StatValue(BaseEnums.StatType.SpecialDefense)) : 1f;
            return Mathf.Clamp01(baseChance * 2f * attack / (attack + resist));
        }

        public static bool RollEffectHit(Unit source, Unit target, float baseChance = 1f)
            => target != null && target.isActive && Random.value <= EffectHitChance(source, target, baseChance);

        /// <summary>
        /// 특수공격력이 원소 반응을 키우는 배율. 같은 레벨 표준치(기준값)에서 1.5배,
        /// 특수공격력이 기준값의 두 배면 약 1.67배로 완만하게 수렴한다(상한 2배).
        /// </summary>
        public static float ReactionAmplifier(Unit source)
        {
            if (source == null) return 1f;
            float attack = Mathf.Max(0f, source.StatValue(BaseEnums.StatType.SpecialAttack));
            float constant = UnitStats.ArmorConstant(source.Level);
            return 1f + attack / (attack + constant);
        }
    }
}
