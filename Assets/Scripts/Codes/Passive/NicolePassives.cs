using System;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    public static class NicoleCodeIds
    {
        public const int Innate = 206;
    }

    internal static class NicoleStatusIds
    {
        public const int ElectricField = 6520;
        public const int Overcharge = 6521;
        public const int ArmorBreak = 6522;
    }

    /// <summary>
    /// 니콜 고유 P — 일렉트릭 필드.
    ///
    /// 두 가지를 한 상태로 묶는다.
    ///   · 번개 속성(고유 원소)이거나 전기를 두른 아군의 가하는 피해 +20% — <c>HasCombatElement</c>가 두 축을 함께 본다
    ///   · 아군이 <b>스스로 5회 행동</b>할 때마다 전기 원소를 부착
    ///
    /// 전기를 두른 아군의 집합이 전투 도중 계속 바뀌므로, 아그니의 오라처럼 시전 시점에
    /// 대상을 확정하지 않는다. 아군 전원에게 상태를 하나씩 붙여 두고 배율 쪽에서 매번
    /// 원소 보유를 다시 묻는다. 5회 카운터도 같은 상태가 들고 있어야 아군마다 따로 센다.
    /// </summary>
    public sealed class NicoleElectricField : UniquePassiveCode
    {
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public NicoleElectricField(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "일렉트릭 필드";
            IgnoresActivationChance = true;
        }

        private string Key => $"nicole_electric_field_{Caster.GetEntityId()}";

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NicoleStatusIds.ElectricField, Key, CodeName,
                    Caster, ally, new NicoleElectricFieldEffect(),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "번개 속성이거나 번개 원소를 두르면 가하는 피해 +20%. 5회 행동마다 번개 원소를 얻습니다."));
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)) ally.RemoveStatusByKey(Key);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _cleanupHandler = null;
            _registered = false;
        }
    }

    /// <summary>
    /// 일렉트릭 필드가 아군 한 명에게 붙이는 실체.
    ///
    /// 행동 수는 턴이 아니라 <b>행동</b>을 센다 — 일반행동·궁극기·추가행동을 모두 포함한다.
    /// 턴을 쓰지 않는 궁극기와 추가행동까지 세므로 빠른 아군일수록 전기를 자주 두른다.
    /// (지속피해 '풍화'가 쓰는 것과 같은 세 훅이다)
    /// </summary>
    internal sealed class NicoleElectricFieldEffect : BaseEffect
    {
        /// <summary>전기 원소를 다시 두르기까지 필요한 행동 수.</summary>
        private const int ActionsPerCharge = 5;
        private const float DamageMultiplier = 1.2f;

        private int _actions;
        private Action<EventContext> _handler;

        public NicoleElectricFieldEffect() : base(0, DamageMultiplier) { }

        public override void OnApply()
        {
            if (Target == null) return;

            _handler = _ => CountAction();
            Target.AddListener(BaseEnums.UnitEventType.OnNormalActivates, _handler);
            Target.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
            Target.AddListener(BaseEnums.UnitEventType.OnAdditionalActivates, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;

            Target.RemoveListener(BaseEnums.UnitEventType.OnNormalActivates, _handler);
            Target.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
            Target.RemoveListener(BaseEnums.UnitEventType.OnAdditionalActivates, _handler);
            _handler = null;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && Caster != null && Caster.isActive &&
               Target.HasCombatElement(BaseEnums.UnitElement.Electro)
                ? DamageMultiplier
                : 1f;

        private void CountAction()
        {
            if (Target == null || !Target.isActive || Caster == null || !Caster.isActive) return;

            if (++_actions < ActionsPerCharge) return;
            _actions = 0;

            // 반응을 막지 않는다. 이미 전기를 두른 아군에게 한 번 더 걸리면 축전(LUK 증가)이 되고,
            // 세이의 바위와 겹치면 접지로 서로 걷힌다. 아군에게 버프 반응을 만드는 몇 안 되는 경로다.
            Target.GrantCombatElement(
                BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
