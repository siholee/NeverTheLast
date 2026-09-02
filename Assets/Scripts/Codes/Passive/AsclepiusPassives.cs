using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    internal static class AsclepiusStatusIds
    {
        public const int ManaBreathing = 5600;
        public const int CriticalTreatment = 5601;
        public const int DivineMedicine = 5602;
        public const int NashorsTooth = 5603;
    }

    /// <summary>내셔의 이빨: 궁극기 사용 후 5초간 DEX +40.</summary>
    public sealed class AsclepiusNashorsTooth : UniquePassiveCode
    {
        private System.Action<EventContext> _castHandler;
        private System.Action<EventContext> _cleanupHandler;
        private bool _registered;

        public AsclepiusNashorsTooth(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "생명의 잔";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _castHandler = _ => ApplyDexBuff();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _castHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void ApplyDexBuff()
        {
            Caster?.AddStatus(BuffStatus.Create(
                AsclepiusStatusIds.NashorsTooth, "asclepius_nashors_tooth", CodeName,
                Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, 40),
                duration: 2,   // 5초 → 2턴
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "궁극기 사용 후 2턴간 DEX가 40 증가합니다."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _castHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey("asclepius_nashors_tooth");
            _registered = false;
        }
    }

    public sealed class AsclepiusManaBreathing : PassiveCode
    {
        public AsclepiusManaBreathing(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "마나 호흡법";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AsclepiusStatusIds.ManaBreathing, "asclepius_mana_breathing", CodeName,
            Caster, Caster, new ManaBreathingEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "INT가 제공하는 마나 회복 효율 기여분이 20% 증가합니다."));
    }

    public sealed class AsclepiusCriticalTreatment : PassiveCode
    {
        public AsclepiusCriticalTreatment(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "치명적인 치유";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AsclepiusStatusIds.CriticalTreatment, "asclepius_critical_treatment", CodeName,
            Caster, Caster, new CriticalTreatmentEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "치유와 보호막이 치명타 확률로 치명타 피해 배율만큼 증가합니다."));
    }

    public sealed class AsclepiusDivineMedicine : PassiveCode
    {
        public const int CodeId = 32;

        public AsclepiusDivineMedicine(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "의신의 가호";
            IgnoresActivationChance = true;
            // 의술(70)의 강화 등급.
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AsclepiusStatusIds.DivineMedicine, "asclepius_divine_medicine", CodeName,
            Caster, Caster, new DivineMedicineEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "부여하는 치유와 보호막이 50% 증가하고, 받은 대상의 부정 상태를 모두 해제합니다."));
    }

    internal sealed class ManaBreathingEffect : BaseEffect
    {
        public ManaBreathingEffect() : base(0, 1.2f) { }
        public override float ManaRecoveryIntContributionMultiplierModifier(Unit unit)
            => unit == Target ? 1.2f : 1f;
    }

    internal sealed class CriticalTreatmentEffect : BaseEffect
    {
        public CriticalTreatmentEffect() : base(0) { }
        public override bool EnablesHealingShieldCritical(Unit source) => source == Target;
    }

    internal sealed class DivineMedicineEffect : BaseEffect
    {
        public DivineMedicineEffect() : base(0, 1.5f) { }
        public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            => source == Target ? 1.5f : 1f;
        public override float OutgoingShieldMultiplierModifier(Unit source, Unit target)
            => source == Target ? 1.5f : 1f;
        public override void OnHealingOrShieldGranted(Unit source, Unit target)
        {
            if (source == Target) target?.RemoveAllNegativeStatuses();
        }
    }
}
