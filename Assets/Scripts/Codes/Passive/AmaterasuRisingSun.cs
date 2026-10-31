using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>동료의 실제 추가공격을 행동당 한 번 일광으로 저장한다. 마나와 별개의 전투 자원이다.</summary>
    public sealed class AmaterasuRisingSun : UniquePassiveCode
    {
        public const string ResourceKey = "amaterasu_sunlight";
        public const int MaxStacks = 12;
        public const int StacksPerAction = 2;
        private readonly HashSet<int> _handled = new();
        private bool _working;
        private Action<EventContext> _death;
        public int Stacks => Caster?.GetCombatResource(ResourceKey) ?? 0;
        public AmaterasuRisingSun(PassiveCodeContext context) : base(context)
        {
            CodeName = "해가 높아질수록";
            CombatResourceLabels.Register(ResourceKey, "일광");
        }
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            _handled.Clear();
            Caster.SetCombatResourceMaximum(ResourceKey, MaxStacks, resetCurrent: true);
            Unit.AnyDamageDealt += OnDamage;
            ActionScheduler.AnyActionFinished += OnFinished;
            _death = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _death);
        }
        private void OnDamage(DamageResolvedContext context)
        {
            if (!_working || Caster == null || !Caster.isActive || !Caster.IsOnField ||
                context == null || context.DamageDealt <= 0 || context.Attacker == null ||
                context.Attacker == Caster || context.Attacker.IsEnemy != Caster.IsEnemy ||
                context.Target == null || context.Target.IsEnemy == Caster.IsEnemy ||
                !DirectAttack.IsDirect(context.DamageContext)) return;
            var scheduler = GameManager.Instance?.ActionScheduler;
            // 피해 태그만 붙인 호출과 Special은 추가행동이 아니다. 소환 공격은 실제 행동자도 확인한다.
            Unit actor = scheduler?.ExecutingUnit;
            if (actor == null || actor == Caster || actor.IsEnemy != Caster.IsEnemy ||
                !ActionScheduler.IsAdditional(scheduler.ActingKind) ||
                !context.DamageContext.HasTag(DamageTag.AdditionalAttack) ||
                !_handled.Add(scheduler.CurrentActionId)) return;
            Caster.AddCombatResource(ResourceKey, StacksPerAction);
        }
        private void OnFinished(Unit actor, ActionScheduler.ActionKind kind)
        {
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler != null) _handled.Remove(scheduler.CurrentActionId);
        }
        public override void StopCode()
        {
            _working = false;
            Unit.AnyDamageDealt -= OnDamage;
            ActionScheduler.AnyActionFinished -= OnFinished;
            if (_death != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _death);
            _death = null;
            _handled.Clear();
            Caster?.AddCombatResource(ResourceKey, -Stacks);
        }
    }
}
