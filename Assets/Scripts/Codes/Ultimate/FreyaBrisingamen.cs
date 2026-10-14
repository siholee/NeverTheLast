using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>프레이아 자신의 유효 치유만 저축한다. 긴급 치유는 자원을 쓰고 다시 저축하지 않는다.</summary>
    public sealed class FreyaBrisingamen : UltimateCode
    {
        public const string Resource = "freya_brisingamen";
        public const float CapacityRatio = .75f, DamagePerHealthRatio = .40f, DecayRatio = .25f, EmergencyRatio = .18f;
        private bool _working, _emergencyHealing;
        private int _serial, _lastEmergencyAction = int.MinValue;
        private readonly HashSet<Unit> _pending = new();
        private readonly List<Unit> _holders = new();
        public string AuraKey => $"freya_brisingamen_{Caster?.GetEntityId()}";
        public int Energy => Caster?.GetCombatResource(Resource) ?? 0;
        public float DamageBonus => Working ? Mathf.Min(.30f, Energy / (float)Mathf.Max(1, Caster.HpMax) * DamagePerHealthRatio) : 0;
        private bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public override bool IsPersistent => true;
        public override bool IsAutoCast => false;
        public FreyaBrisingamen(UltimateCodeContext context) : base(context)
        {
            CodeName = "브리싱가멘의 기억";
            CombatResourceLabels.Register(Resource, "온기", unit => $"온기 {unit.GetCombatResource(Resource)}/{unit.GetCombatResourceMaximum(Resource)}");
        }
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            _lastEmergencyAction = int.MinValue;
            Resize(true);
            Unit.AnyHealed += Healed;
            Unit.AnyDamageDealt += Damaged;
            HealthCost.AnyPaid += HealthPaid;
            ActionScheduler.AnyActionStarted += Started;
            ApplyAura();
        }
        private void Resize(bool reset = false) => Caster.SetCombatResourceMaximum(Resource, Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * CapacityRatio)), reset);
        private void Healed(Unit target, Unit source, int actual, int overheal)
        {
            if (!Working || _emergencyHealing || source != Caster || target == null || target.IsEnemy != Caster.IsEnemy || !target.IsOnField || actual <= 0) return;
            Resize();
            Caster.AddCombatResource(Resource, actual);
        }
        private void Started(Unit actor, ActionScheduler.ActionKind kind, string label)
        {
            if (!Working) return;
            Resize();
            ApplyAura();
            if (actor == Caster && kind == ActionScheduler.ActionKind.Normal)
                Caster.AddCombatResource(Resource, -Mathf.CeilToInt(Energy * DecayRatio));
            foreach (Unit ally in Allies()) TryEmergency(ally);
        }
        private IEnumerable<Unit> Allies()
        {
            var grid = GridManager.Instance;
            return grid == null ? Enumerable.Empty<Unit>() : grid.heroList.Concat(grid.enemyList)
                .Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == Caster.IsEnemy);
        }
        private void ApplyAura()
        {
            foreach (Unit ally in Allies())
            {
                if (ally.HasStatusKey(AuraKey)) continue;
                ally.AddStatus(BuffStatus.Create(71821, AuraKey, CodeName, Caster, ally, new Warmth(this), 0, isBeneficial: true,
                    description: "프레이아의 저장 온기에 비례하여 가하는 피해 증가(최대30%). 프레이아가 필드에 있는 동안 유지."));
                if (!_holders.Contains(ally)) _holders.Add(ally);
            }
        }
        private bool Eligible(Unit target) => Working && target != null && target.isActive && target.IsOnField && !target.IsSummon &&
            target.IsEnemy == Caster.IsEnemy && target.HpCurr > 0 && target.HpCurr <= target.HpMax * .30f;
        private void Damaged(DamageResolvedContext context) { if (context?.DamageContext.HpDamage > 0) TryEmergency(context.Target); }
        private void HealthPaid(Unit unit, int amount) => TryEmergency(unit);
        private void TryEmergency(Unit target)
        {
            if (!Eligible(target) || _pending.Contains(target)) return;
            int cost = Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * EmergencyRatio));
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null || !Caster.TryConsumeCombatResource(Resource, cost)) return;
            _pending.Add(target);
            bool reserved = true;
            void Refund()
            {
                if (!reserved) return;
                reserved = false;
                _pending.Remove(target);
                if (Working) Caster.AddCombatResource(Resource, cost);
            }
            if (!scheduler.EnqueuePriorityAdditional(Caster, $"freya_emergency_{++_serial}", "온기를 나누는 손", () =>
            {
                if (!Eligible(target)) { Refund(); return; }
                reserved = false;
                _pending.Remove(target);
                _lastEmergencyAction = scheduler.CurrentActionId;
                _emergencyHealing = true;
                try { target.Heal(cost, Caster); }
                finally { _emergencyHealing = false; }
                AudioManager.PlayCue("freya.emergency.cast");
                Effects.Vfx.NordFx.FreyaEmergency(target);
            }, () => { if (Eligible(target) && !Caster.isControlled) return true; Refund(); return false; }, Refund)) Refund();
        }
        public bool IsEmergencyAction(Unit actor) => actor == Caster && GameManager.Instance?.ActionScheduler.CurrentActionId == _lastEmergencyAction;
        public override void StopCode()
        {
            _working = false;
            _lastEmergencyAction = int.MinValue;
            Unit.AnyHealed -= Healed;
            Unit.AnyDamageDealt -= Damaged;
            HealthCost.AnyPaid -= HealthPaid;
            ActionScheduler.AnyActionStarted -= Started;
            foreach (Unit holder in _holders) holder?.RemoveStatusByKey(AuraKey);
            _holders.Clear();
            _pending.Clear();
            if (Caster != null) Caster.AddCombatResource(Resource, -Energy);
        }
        private sealed class Warmth : BaseEffect
        {
            private readonly FreyaBrisingamen _source;
            public Warmth(FreyaBrisingamen source) : base(0, 0) => _source = source;
            public override bool IsBeneficial => true;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => attacker == Target ? 1f + _source.DamageBonus : 1f;
        }
    }
}
