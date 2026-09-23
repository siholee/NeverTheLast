using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 세이메이 U — 태산부군제(43).
    /// 아군 전원에게 3턴 재생과, 다음 자기 행동이 끝날 때까지 공격으로 봉인부를 붙이는 부적을 준다.
    /// </summary>
    public sealed class SeimeiHeavenEarthReversal : SimpleUltimate
    {
        private const int RegenerationStatusId = 9820;
        private const int AttackSealStatusId = 9821;

        public SeimeiHeavenEarthReversal(UltimateCodeContext context)
            : base(context, "태산부군제", 0.5f) { }

        protected override void Resolve()
        {
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster).ToList())
            {
                string sourceId = Caster.GetEntityId().ToString();
                ally.AddStatus(BuffStatus.Create(
                    RegenerationStatusId, $"seimei_regeneration_{sourceId}", "태산부군제 — 재생",
                    Caster, ally, new PercentHealOverTimeEffect(12.5f),
                    duration: 3,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "3턴간 턴 시작 시 최대 체력의 1/8을 회복합니다."));

                string attackSealKey = $"seimei_attack_seal_{sourceId}";
                ally.AddStatus(BuffStatus.Create(
                    AttackSealStatusId, attackSealKey, "태산부군제 — 공격 봉인부",
                    Caster, ally, new SeimeiAttackSealEffect(attackSealKey),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "다음 자기 행동이 끝날 때까지 공격한 적에게 봉인부를 부착합니다."));
            }
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Caster.IsOnField;
    }

    /// <summary>
    /// 다단 공격은 한 행동·한 대상당 봉인부 한 장만 붙인다. 상태 지속시간은 숫자 턴이 아니라
    /// 보유자의 다음 행동 종료로 끊어, 적용 직후 턴 시작 시 사라지는 문제를 피한다.
    /// </summary>
    internal sealed class SeimeiAttackSealEffect : BaseEffect
    {
        private readonly string _statusKey;
        private readonly HashSet<EntityId> _markedTargets = new();
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _turnEndHandler;
        private Action<EventContext> _cleanupHandler;
        private int _actionId = int.MinValue;

        public SeimeiAttackSealEffect(string statusKey) : base(0) => _statusKey = statusKey;

        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            if (Target == null) return;
            _damageHandler = OnDamageDealt;
            _turnEndHandler = _ => Target?.RemoveStatusByKey(_statusKey);
            _cleanupHandler = _ => Target?.RemoveStatusByKey(_statusKey);
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnTurnEnd, _turnEndHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        public override void OnRemove()
        {
            if (Target == null) return;
            if (_damageHandler != null) Target.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            if (_turnEndHandler != null) Target.RemoveListener(BaseEnums.UnitEventType.OnTurnEnd, _turnEndHandler);
            if (_cleanupHandler != null)
            {
                Target.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Target.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (context == null || context.Attacker != Target || context.Target == null ||
                context.DamageDealt <= 0 || context.Target.IsEnemy == Target.IsEnemy)
                return;

            int currentAction = Managers.GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (currentAction != _actionId)
            {
                _actionId = currentAction;
                _markedTargets.Clear();
            }

            if (_markedTargets.Add(context.Target.GetEntityId()))
                SeimeiIds.ApplySeal(Caster, context.Target);
        }
    }
}
