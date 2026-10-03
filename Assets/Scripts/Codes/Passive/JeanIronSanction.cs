using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Combat;
using Entities;

namespace Codes.Passive
{
    /// <summary>잔 — 자신이 직접 공격의 최종 대상이면 원래 공격 전에 신성한 일격으로 받아친다.</summary>
    public sealed class JeanIronSanction : UniquePassiveCode, IPreAttackInterceptor
    {
        public const string ResourceId = "jean_counter_remaining";
        private bool _subscribed;

        public string InterruptLabel => CodeName;

        public JeanIronSanction(PassiveCodeContext context) : base(context)
            => CodeName = "철권제재";

        public override void CastCode()
        {
            if (Caster == null) return;
            int maximum = Caster.CounterUsesBase;
            Caster.SetCombatResourceMaximum(ResourceId, maximum, true);
            Caster.AddCombatResource(ResourceId, maximum);
            if (_subscribed) return;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = false;
        }

        public bool CanIntercept(AttackTargetRoutingContext context, Unit finalTarget)
        {
            if (Caster == null || context?.Attacker == null || finalTarget != Caster) return false;
            return Caster.isActive && Caster.IsOnField && !Caster.isControlled &&
                   context.Attacker.isActive && context.Attacker.IsOnField && !context.Attacker.IsUntargetable &&
                   context.Attacker.IsEnemy != Caster.IsEnemy &&
                   Caster.GetCombatResource(ResourceId) > 0 &&
                   Caster.ActiveNormalCode is JeanHolyStrike;
        }

        public IEnumerator Intercept(AttackTargetRoutingContext context, Unit finalTarget)
        {
            if (!CanIntercept(context, finalTarget) || !Caster.TryConsumeCombatResource(ResourceId, 1)) yield break;
            if (Caster.ActiveNormalCode is JeanHolyStrike strike)
                yield return strike.CastCounter(context.Attacker);
        }

        private void OnNormalResolved(EventContext context)
        {
            int maximum = Caster?.GetCombatResourceMaximum(ResourceId) ?? 0;
            if (maximum <= 0) return;
            Caster.AddCombatResource(ResourceId, maximum - Caster.GetCombatResource(ResourceId));
        }
    }
}
