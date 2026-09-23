using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class AsclepiusStatusIds
    {
        public const int ManaBreathing = 5600;
        public const int CriticalTreatment = 5601;
        public const int DivineMedicine = 5602;
        public const int ChaliceOfLife = 5603;
    }

    /// <summary>
    /// 생명의 잔(224) — 체력이 부족한 아군이 행동할 때 반응 치유한다.
    /// 아스클레피아가 실제 치유를 발생시킨 행동마다 궁극기 자원 5%를 한 번만 얻는다.
    /// </summary>
    public sealed class AsclepiusChaliceOfLife : PersistentStatusPassive
    {
        public AsclepiusChaliceOfLife(PassiveCodeContext context)
            : base(context, AsclepiusStatusIds.ChaliceOfLife, "asclepius_chalice_of_life", "생명의 잔",
                "체력이 부족한 아군이 행동하면 20 + CON×0.6만큼 치유하고, 유효 치유 행동마다 궁극기 자원을 5% 회복합니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new ChaliceOfLifeEffect();
    }

    internal sealed class ChaliceOfLifeEffect : BaseEffect
    {
        private Action<Unit, ActionScheduler.ActionKind, string> _actionHandler;
        private int _lastResourceActionId = int.MinValue;

        public ChaliceOfLifeEffect() : base(0) { }
        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            _actionHandler = OnAnyActionStarted;
            ActionScheduler.AnyActionStarted += _actionHandler;
        }

        public override void OnRemove()
        {
            if (_actionHandler != null) ActionScheduler.AnyActionStarted -= _actionHandler;
        }

        private void OnAnyActionStarted(Unit actor, ActionScheduler.ActionKind kind, string label)
        {
            if (Target == null || !Target.isActive || !Target.IsOnField || actor == null ||
                !actor.isActive || actor.IsEnemy != Target.IsEnemy || actor.HpCurr >= actor.HpMax)
                return;

            int heal = Mathf.Max(1, Mathf.RoundToInt(20f + Target.GetBaseCon() * 0.6f));
            actor.ModifyHp(actor.HpCurr + heal, Target);
        }

        public override void OnEffectiveHealingGranted(
            Unit source, Unit target, int effectiveHealing, int hpBeforeHealing)
        {
            if (source != Target || effectiveHealing <= 0 || Target == null || !Target.isActive) return;

            int actionId = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastResourceActionId == actionId) return;
            _lastResourceActionId = actionId;

            int resource = Mathf.Max(1, Mathf.CeilToInt(Target.ManaMax * 0.05f));
            Target.AddUltimateResource(resource);
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
            Caster, Caster, new ManaEfficiencyEffect(1.2f),
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
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AsclepiusStatusIds.DivineMedicine, "asclepius_divine_medicine", CodeName,
            Caster, Caster, new DivineMedicineEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "부여하는 치유와 보호막이 50% 증가하고, 받은 대상의 부정 상태를 모두 해제합니다."));
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
