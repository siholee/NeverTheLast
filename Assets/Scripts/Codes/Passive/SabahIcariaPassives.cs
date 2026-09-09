using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    public static class SabahIcariaCodeIds
    {
        public const int SabahInnate = 203;
        public const int IcariaInnate = 226;
        public const int IcariaMentalStrength = 96;
        public const int IcariaPyroAffinity = 97;
        public const int SabahAssassin = 98;
    }

    internal static class SabahIcariaStatusIds
    {
        public const int SabahInnate = 6500;
        public const int IcariaInnate = 6501;
        public const int IcariaMentalStrength = 6502;
        public const int IcariaPyroAffinity = 6503;
        public const int SabahAssassin = 6504;
    }

    /// <summary>
    /// 사바흐 고유 패시브. 대상의 디버프 수만큼 피해가 증가하며,
    /// 아군이 적에게 해로운 상태를 실제로 적용·갱신할 때 아즈라엘 스택을 얻는다.
    /// </summary>
    public sealed class SabahDebuffHunter : UniquePassiveCode
    {
        private Action<Unit, Unit, Entities.Status.UnitStatus> _negativeStatusHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public SabahDebuffHunter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "빈틈 포착";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.SabahInnate, "sabah_debuff_hunter", CodeName,
                Caster, Caster, new SabahDebuffDamageEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "공격 대상의 디버프 1개당 가하는 피해가 2% 증가합니다."));

            if (_registered) return;
            _negativeStatusHandler = OnNegativeStatusGranted;
            _cleanupHandler = _ => StopCode();
            Unit.AnyNegativeStatusGranted += _negativeStatusHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnNegativeStatusGranted(Unit source, Unit target, Entities.Status.UnitStatus status)
        {
            if (Caster == null || !Caster.isActive || source == null || target == null) return;
            if (source.IsEnemy != Caster.IsEnemy || target.IsEnemy == Caster.IsEnemy) return;
            Caster.AddUltimateResource(1);
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyNegativeStatusGranted -= _negativeStatusHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _registered = false;
        }
    }

    internal sealed class SabahDebuffDamageEffect : BaseEffect
    {
        public SabahDebuffDamageEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            int debuffs = target.GetAllStatuses()
                .Count(status => status.Category == BaseEnums.StatusCategory.Negative);
            return 1f + debuffs * 0.02f;
        }
    }

    /// <summary>사바흐의 암살자 — 100% 초과 치명타 확률을 치명타 피해로 1:1 전환.</summary>
    public sealed class SabahAssassin : PassiveCode
    {
        public SabahAssassin(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "암살자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.SabahAssassin, "sabah_assassin", CodeName,
                Caster, Caster, new ExcessCritConversionEffect(1f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "100%를 초과한 치명타 확률을 같은 비율의 치명타 피해로 전환합니다."));
        }
    }

    /// <summary>이카리아 고유 패시브 — 공격 체력 비용 ×2, 실제 소모 비율만큼 피해 증가.</summary>
    public sealed class IcariaRecklessChallenge : UniquePassiveCode
    {
        public IcariaRecklessChallenge(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "무모한 도전";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.IcariaInnate, "icaria_reckless_challenge", CodeName,
                Caster, Caster, new IcariaRecklessChallengeEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "공격의 체력 소모량이 2배가 되고 실제 최대 체력 소모 비율만큼 해당 공격 피해가 증가합니다."));
        }
    }

    internal sealed class IcariaRecklessChallengeEffect : BaseEffect
    {
        public IcariaRecklessChallengeEffect() : base(0) { }

        public override float AttackSelfHpCostMultiplier(Unit unit) => unit == Target ? 2f : 1f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context != null ? 1f + UnityEngine.Mathf.Max(0f, context.SelfHpSpentRatio) : 1f;
    }

    /// <summary>상태이상 최종 적용 단계에서 10% 확률로 저항.</summary>
    public sealed class IcariaMentalStrength : PassiveCode
    {
        public IcariaMentalStrength(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "정신력";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.IcariaMentalStrength, "icaria_mental_strength", CodeName,
                Caster, Caster, new IcariaMentalStrengthEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "해로운 상태가 적용될 때 10% 확률로 저항합니다."));
        }
    }

    internal sealed class IcariaMentalStrengthEffect : BaseEffect
    {
        public IcariaMentalStrengthEffect() : base(0, 0.1f) { }
        public override float NegativeStatusResistanceChanceModifier(
            Unit unit, Entities.Status.UnitStatus status) => unit == Target ? 0.1f : 0f;
    }

    /// <summary>이카리아의 공격 준비 코드가 활성 여부를 확인하는 표식형 패시브.</summary>
    public sealed class IcariaPyroAffinity : PassiveCode
    {
        public IcariaPyroAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 불";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.IcariaPyroAffinity, "icaria_pyro_affinity", CodeName,
                Caster, Caster, new MarkerBuffEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "불 원소 보유 중 공격하면 최대 체력의 5%를 소모하고 피해가 20% 증가합니다."));
        }
    }
}
