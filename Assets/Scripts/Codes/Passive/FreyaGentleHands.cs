using BaseClasses;
using Codes.Base;
using Codes.Ultimate;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public sealed class FreyaGentleHands : UniquePassiveCode
    {
        public const float HealRatio = .02f;
        private bool _working;
        public FreyaGentleHands(PassiveCodeContext context) : base(context) => CodeName = "다시 일어날 수 있도록";
        public override void CastCode()
        {
            if (_working) return;
            _working = true;
            ActionScheduler.AnyActionFinished += Finished;
        }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (!_working || Caster == null || !Caster.isActive || !Caster.IsOnField || actor == null || !actor.isActive ||
                !actor.IsOnField || actor.IsEnemy != Caster.IsEnemy || actor.IsSummon ||
                !(ActionScheduler.IsAdditional(kind) || kind == ActionScheduler.ActionKind.Special || kind == ActionScheduler.ActionKind.Ultimate)) return;
            if (Caster.ActiveUltimateCode is FreyaBrisingamen reservoir && reservoir.IsEmergencyAction(actor)) return;
            actor.Heal(Mathf.RoundToInt(Caster.HpMax * HealRatio), Caster);
        }
        public override void StopCode() { _working = false; ActionScheduler.AnyActionFinished -= Finished; }
    }
}
