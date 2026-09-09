using BaseClasses;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Effects.Buffs
{
    /// <summary>
    /// 조건 없이 값 하나만 곱하거나 더하는 효과들의 공용 구현.
    ///
    /// 같은 한 줄짜리 계산이 진영·캐릭터별 파일마다 다른 이름으로 흩어져 있었다.
    /// 이름만 다르고 하는 일이 같으면 <b>수정할 때 한 군데를 빠뜨리기 쉬우므로</b>
    /// 계산이 조건 없는 것들만 여기로 모은다.
    /// 조건이 붙는 효과(전열일 때만, 특정 원소일 때만 등)는 각자의 파일에 그대로 둔다.
    /// </summary>
    public class ReceivingDamageMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;
        private readonly bool _beneficial;

        /// <param name="multiplier">받는 피해에 곱할 값. 1 미만이면 경감, 초과면 취약.</param>
        public ReceivingDamageMultiplierEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
            _beneficial = multiplier < 1f;
            if (!_beneficial) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _beneficial;

        public override float ReceivingDamageModifier(Unit unit) => unit == Target ? _multiplier : 1f;
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

    /// <summary>스탯 배율. <paramref name="stat"/>이 null이면 올스탯에 걸린다.</summary>
    public class PrimaryStatMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;
        private readonly BaseEnums.PrimaryStat? _stat;

        public PrimaryStatMultiplierEffect(float multiplier, BaseEnums.PrimaryStat? stat = null)
            : base(0, multiplier)
        {
            _multiplier = multiplier;
            _stat = stat;
            if (multiplier < 1f) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _multiplier > 1f;

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && (_stat == null || _stat == stat) ? _multiplier : 1f;
    }

    /// <summary>치명타 피해 배수에 더하는 가산치.</summary>
    public class CritMultiplierBonusEffect : BaseEffect
    {
        private readonly float _amount;

        public CritMultiplierBonusEffect(float amount) : base(0, amount) => _amount = amount;

        public override bool IsBeneficial => true;

        public override float CritMultiplierAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
    }

    /// <summary>자신이 <b>거는</b> 지속피해의 위력 배율.</summary>
    public class DamageOverTimeApplicationEffect : BaseEffect
    {
        private readonly float _multiplier;

        public DamageOverTimeApplicationEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => _multiplier > 1f;

        public override float DamageOverTimeApplicationMultiplier(Unit unit) => unit == Target ? _multiplier : 1f;
    }

    /// <summary>
    /// 방어구 파괴 — <b>자신의</b> 방어력에 곱하는 배율이라 공격자가 아니라 피격자에게 붙는다.
    /// 이름이 제각각(파쇄·분쇄·촉매)이어도 계산은 같으므로 한 클래스로 둔다.
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

    /// <summary>공격으로 자신을 회복할 때(흡혈 계열)만 적용되는 회복량 배율.</summary>
    public class AttackTriggeredHealingEffect : BaseEffect
    {
        private readonly float _multiplier;

        public AttackTriggeredHealingEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => _multiplier > 1f;

        public override float HealingReceivedMultiplierModifier(Unit unit, Unit source, bool attackTriggered)
            => unit == Target && source == unit && attackTriggered ? _multiplier : 1f;
    }

    /// <summary>100%를 초과한 치명타 확률을 치명타 피해로 바꾸는 전환 배율.</summary>
    public class ExcessCritConversionEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ExcessCritConversionEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => true;

        public override float ExcessCritChanceConversionMultiplier(Unit unit) => unit == Target ? _multiplier : 0f;
    }

    /// <summary>INT가 마나 회복에 기여하는 몫의 배율. 1 미만이면 위압 계열 디버프가 된다.</summary>
    public class ManaEfficiencyEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ManaEfficiencyEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
            if (multiplier < 1f) Category = BaseEnums.EffectCategory.Negative;
        }

        public override bool IsBeneficial => _multiplier > 1f;

        public override float ManaRecoveryIntContributionMultiplierModifier(Unit unit)
            => unit == Target ? _multiplier : 1f;
    }
}
