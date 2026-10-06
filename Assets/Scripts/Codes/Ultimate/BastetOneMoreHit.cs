using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Entities;
using Managers;

namespace Codes.Ultimate
{
    /// <summary>아군 반격이 실제 피해를 주면 같은 대상을 냥냥펀치로 한 번 더 친다.</summary>
    public sealed class BastetOneMoreHit : UltimateCode
    {
        public override int AttackForm => DamageTag.Strike;

        private readonly HashSet<int> _handledActionIds = new();
        private bool _subscribed;

        public override bool IsAutoCast => false;
        public override bool IsPersistent => true;

        public BastetOneMoreHit(UltimateCodeContext context) : base(context)
            => CodeName = "미운 놈 한 대 더 때리기";

        public override void CastCode()
        {
            if (_subscribed) return;
            Unit.AnyDamageDealt += OnAnyDamageDealt;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (_subscribed) Unit.AnyDamageDealt -= OnAnyDamageDealt;
            _subscribed = false;
            _handledActionIds.Clear();
            if (Caster != null) Caster.isCasting = false;
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (!CanTrigger(context)) return;
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;
            int actionId = scheduler.CurrentActionId;
            if (!_handledActionIds.Add(actionId)) return;

            Unit target = context.Target;
            scheduler.EnqueuePriorityAdditional(Caster, $"bastet_counter_{actionId}", CodeName,
                () => (Caster.ActiveNormalCode as BastetCatPunch)?.CastAdditional(target),
                () => CanFollowUp(target));
        }

        private bool CanTrigger(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField || context?.Attacker == null ||
                context.Target == null || context.DamageContext == null || context.DamageDealt <= 0) return false;
            if (context.Attacker == Caster || context.Attacker.IsEnemy != Caster.IsEnemy) return false;
            if (!context.DamageContext.HasTag(DamageTag.CounterAttack) ||
                context.DamageContext.HasTag(DamageTag.BonusDamage)) return false;
            // 피해 이벤트는 Die보다 먼저 오므로 이 시점의 0 HP도 즉시 걸러 남은 예약을 만들지 않는다.
            return context.Target.HpCurr > 0 && context.Target.IsEnemy != Caster.IsEnemy;
        }

        private bool CanFollowUp(Unit target)
            => Caster != null && Caster.isActive && Caster.IsOnField && !Caster.isControlled &&
               target != null && target.isActive && target.IsOnField && !target.IsUntargetable &&
               target.IsEnemy != Caster.IsEnemy;
    }
}
