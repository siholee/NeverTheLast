using System;
using BaseClasses;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Effects.Buffs
{
    /// <summary>
    /// 조건 없이 값 하나만 곱하거나 더하는 효과들의 공용 구현.
    ///
    /// 같은 한 줄짜리 계산이 파일마다 다른 이름으로 흩어지면 <b>수정할 때 한 군데를 빠뜨리기 쉽다.</b>
    /// 조건이 붙는 효과(전열일 때만, 특정 원소일 때만 등)는 각자의 파일에 둔다.
    /// </summary>
    public class ReceivingDamageMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;

        /// <param name="multiplier">받는 피해에 곱할 값. 1 미만이면 경감, 초과면 취약.</param>
        public ReceivingDamageMultiplierEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
            if (multiplier > 1f) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _multiplier < 1f;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context) => unit == Target ? _multiplier : 1f;
    }

    /// <summary>조건 없이 <b>자신이 주는</b> 모든 피해에 곱하는 배율.</summary>
    public class OutgoingDamageMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;

        public OutgoingDamageMultiplierEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
            if (multiplier < 1f) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _multiplier > 1f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _multiplier : 1f;
    }

    /// <summary>피해 태그 등 호출 시점의 문맥을 만족할 때만 자신이 주는 피해를 곱한다.</summary>
    public class ConditionalOutgoingDamageMultiplierEffect : BaseEffect
    {
        private readonly Func<DamageContext, bool> _condition;
        private readonly float _multiplier;

        public ConditionalOutgoingDamageMultiplierEffect(Func<DamageContext, bool> condition, float multiplier)
            : base(0, multiplier)
        {
            _condition = condition;
            _multiplier = multiplier;
        }

        public override bool IsBeneficial => _multiplier > 1f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && (_condition?.Invoke(context) ?? false) ? _multiplier : 1f;
    }

    /// <summary>조건 성립 여부에 따라 서로 다른 받는 피해 배율을 적용한다.</summary>
    public class ConditionalReceivingDamageMultiplierEffect : BaseEffect
    {
        private readonly Func<DamageContext, bool> _condition;
        private readonly float _whenTrue;
        private readonly float _whenFalse;

        public ConditionalReceivingDamageMultiplierEffect(Func<DamageContext, bool> condition,
            float whenTrue, float whenFalse = 1f) : base(0, whenTrue)
        {
            _condition = condition;
            _whenTrue = whenTrue;
            _whenFalse = whenFalse;
        }

        public override bool IsBeneficial => _whenTrue < 1f || _whenFalse < 1f;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target ? ((_condition?.Invoke(context) ?? false) ? _whenTrue : _whenFalse) : 1f;
    }

    /// <summary>상태 수명 동안 보유자의 대상 지정 우선도에 고정값을 더한다.</summary>
    public class TargetPriorityModifierEffect : BaseEffect
    {
        private readonly int _additive;

        public TargetPriorityModifierEffect(int additive) : base(0, additive)
            => _additive = additive;

        public override bool IsBeneficial => _additive > 0;
        public override int TargetPriorityAdditiveModifier(Unit unit) => unit == Target ? _additive : 0;
    }

    /// <summary>
    /// 스탯 하나를 고정값 · 퍼센트로 올리거나 내린다. 값이 음수면 디버프로 분류된다.
    /// 값을 식으로 주면(<see cref="StatModifierEffect(BaseEnums.StatType, Func{float}, bool)"/>)
    /// 질의할 때마다 다시 계산한다 — "시전자 방어력의 15%"처럼 남의 스탯을 따라가는 버프가 쓴다.
    /// </summary>
    public class StatModifierEffect : BaseEffect
    {
        private readonly BaseEnums.StatType _stat;
        private readonly Func<float> _amount;
        private readonly bool _percent;

        public StatModifierEffect(BaseEnums.StatType stat, float amount, bool percent = false)
            : this(stat, () => amount, percent) { }

        public StatModifierEffect(BaseEnums.StatType stat, Func<float> amount, bool percent = false)
            : base(0, amount())
        {
            _stat = stat;
            _amount = amount;
            _percent = percent;
            if (amount() < 0f) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _amount() > 0f;

        public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat)
            => !_percent && unit == Target && stat == _stat ? _amount() : 0f;

        public override float StatPercentModifier(Unit unit, BaseEnums.StatType stat)
            => _percent && unit == Target && stat == _stat ? _amount() : 0f;
    }

    /// <summary>
    /// 방어 파괴 — <b>자신의</b> 방어 스탯에 곱하는 배율이라 공격자가 아니라 피격자에게 붙는다.
    /// 방어력 · 특수방어력 경감 모두에 걸린다.
    /// </summary>
    public class ArmorShredEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ArmorShredEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = Mathf.Clamp01(multiplier);
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? _multiplier : 1f;
    }

    /// <summary>마나 회복 배율. 1 미만이면 위압 계열 디버프가 된다.</summary>
    public class ManaEfficiencyEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ManaEfficiencyEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
            if (multiplier < 1f) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _multiplier > 1f;

        public override float ManaRecoveryMultiplierModifier(Unit unit) => unit == Target ? _multiplier : 1f;
    }
}
