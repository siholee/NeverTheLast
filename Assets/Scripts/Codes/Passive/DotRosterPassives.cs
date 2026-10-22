using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public abstract class DotRosterPassive : UniquePassiveCode
    {
        private bool _working;
        protected DotRosterPassive(PassiveCodeContext context) : base(context) { }
        public bool Working => _working && Caster != null && Caster.IsOnField && Caster.HpCurr > 0;
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, End);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, End);
            StartObserving();
        }
        public override void StopCode()
        {
            if (!_working) return;
            _working = false;
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnDeath, End);
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, End);
            StopObserving();
        }
        private void End(EventContext _) => StopCode();
        protected abstract void StartObserving();
        protected abstract void StopObserving();
        protected IEnumerable<Unit> Enemies() => CombatTargets.AliveEnemies(Caster);
    }

    public sealed class NephthysPoisonGarden : DotRosterPassive
    {
        public const float RuptureRatio = 2.50f;
        private readonly HashSet<Unit> _marked = new();
        public string MarkKey => "nephthys_eye_" + Caster.GetEntityId();
        public NephthysPoisonGarden(PassiveCodeContext context) : base(context) => CodeName = "죽음의 개화";
        protected override void StartObserving() => ActionDot.NaturalTickResolved += OnNaturalTick;
        protected override void StopObserving()
        {
            ActionDot.NaturalTickResolved -= OnNaturalTick;
            foreach (Unit target in _marked) if (target != null) target.RemoveStatusByKey(MarkKey);
            _marked.Clear();
        }
        public long Eyes(Unit target) => target?.GetAllStatuses().Where(s => s.Key == MarkKey)
            .SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<EyeEffect>().Select(e => e.Stacks).FirstOrDefault() ?? 0;
        public bool ApplyPoisonAndEye(Unit target)
        {
            if (!Working || !ActionDot.Apply(Caster, target, ActionDotKind.Poison)) return false;
            long count = Eyes(target) + 1;
            var status = BuffStatus.Create(71640, MarkKey, "독안", Caster, target, new EyeEffect(count),
                category: BaseEnums.StatusCategory.Neutral,
                description: $"독안 {count}중첩. 맹독 자연 발동에 전부 소비, 중첩당 특공250% 파열. 정화·강화 해제 불가.");
            target.AddStatus(status); _marked.Add(target); return true;
        }
        private void OnNaturalTick(ActionDot.NaturalDotTickContext tick)
        {
            if (!Working || tick.Effect.Kind != ActionDotKind.Poison || tick.Damage.Attacker == null ||
                tick.Damage.Attacker.IsEnemy != Caster.IsEnemy || tick.Target == null || !tick.Target.IsOnField) return;
            long stacks = Eyes(tick.Target);
            if (stacks <= 0) return;
            tick.Target.RemoveStatusByKey(MarkKey);
            int raw = (int)Math.Min(int.MaxValue, Math.Round((double)Caster.StatValue(BaseEnums.StatType.SpecialAttack) * RuptureRatio * stacks));
            var damage = new DamageContext(Caster, Mathf.Max(1, raw), BaseEnums.CodeType.Effect, new List<int>())
            {
                IsDamageOverTime = true, DotKind = ActionDotKind.Poison, DotTrigger = DotTriggerKind.Derived,
                DotRootId = tick.Damage.DotRootId, StatusVfxKey = ActionDot.Key(ActionDotKind.Poison)
            };
            Effects.Vfx.VfxComposer.PlayAffectedSlot("nephthys.passive", 0, Caster, tick.Target);
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, CodeName);
            tick.Target.TakeDamage(damage);
        }
        public sealed class EyeEffect : BaseEffect
        {
            public long Stacks { get; }
            public EyeEffect(long stacks) : base(0) { Stacks = stacks; Category = BaseEnums.EffectCategory.Neutral; }
        }
    }

    public sealed class AtalantePoisonHunt : DotRosterPassive
    {
        public const int PursuitLimit = 4;
        private int _remaining, _epoch, _serial;
        public int Remaining => _remaining;
        public AtalantePoisonHunt(PassiveCodeContext context) : base(context) => CodeName = "사냥독";
        protected override void StartObserving()
        {
            RefreshPursuits();
            ActionDot.NaturalTickResolved += OnNaturalTick;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
        }
        protected override void StopObserving()
        {
            ActionDot.NaturalTickResolved -= OnNaturalTick;
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
            _remaining = 0; _epoch++;
        }
        private void OnNormal(EventContext _) => RefreshPursuits();
        public void RefreshPursuits() { _remaining = PursuitLimit; _epoch++; }
        public static float DotMultiplier(DamageContext context)
        {
            if (context?.Attacker == null || context.DotKind is not (ActionDotKind.Poison or ActionDotKind.Bleed) || GridManager.Instance == null) return 1f;
            float bonus = GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList)
                .Where(u => u != null && u.IsOnField && u.IsEnemy == context.Attacker.IsEnemy)
                .SelectMany(u => u.ActivePassiveCodes.OfType<AtalantePoisonHunt>()).Where(p => p.Working)
                .Select(p => .35f + .20f * p.Caster.StatValue(BaseEnums.StatType.SpecialAttack) /
                    (p.Caster.StatValue(BaseEnums.StatType.SpecialAttack) + UnitStats.ArmorConstant(p.Caster.Level))).DefaultIfEmpty(0).Max();
            return 1f + bonus;
        }
        private void OnNaturalTick(ActionDot.NaturalDotTickContext tick)
        {
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (!Working || tick.Effect.Kind is not (ActionDotKind.Poison or ActionDotKind.Bleed) || tick.Damage.Attacker == null ||
                tick.Damage.Attacker.IsEnemy != Caster.IsEnemy || tick.Target == null || !tick.Target.IsOnField ||
                _remaining <= 0 || scheduler == null || Caster.ActiveNormalCode is not Codes.Normal.AtalanteHuntingArrow normal) return;
            int epoch = _epoch; _remaining--;
            void Refund() { if (Working && epoch == _epoch) _remaining = Mathf.Min(PursuitLimit, _remaining + 1); }
            bool accepted = scheduler.EnqueueAdditional(Caster, "atalante_hunt_" + ++_serial, "상처를 쫓는 화살",
                () => normal.CastPursuit(tick.Target), () => Working && !Caster.isControlled && tick.Target != null && tick.Target.IsOnField, Refund);
            if (!accepted) Refund();
        }
    }

    public sealed class NeithCatalyticWeave : DotRosterPassive
    {
        private readonly HashSet<Unit> _holders = new();
        private bool _syncing;
        public string MarkKey => "neith_catalysis_" + Caster.GetEntityId();
        public NeithCatalyticWeave(PassiveCodeContext context) : base(context) => CodeName = "촉진의 직조";
        public float DefenseCut => .22f + .10f * Caster.StatValue(BaseEnums.StatType.SpecialAttack) /
            (Caster.StatValue(BaseEnums.StatType.SpecialAttack) + UnitStats.ArmorConstant(Caster.Level));
        protected override void StartObserving()
        {
            Unit.AnyStatusChanged += Changed;
            foreach (Unit enemy in Enemies()) Sync(enemy);
        }
        protected override void StopObserving()
        {
            Unit.AnyStatusChanged -= Changed;
            foreach (Unit enemy in _holders) if (enemy != null) enemy.RemoveStatusByKey(MarkKey);
            _holders.Clear();
        }
        private void Changed(Unit owner, UnitStatus status, bool added)
        {
            if (!Working || _syncing || owner == null) return;
            if (owner == Caster) { foreach (Unit enemy in Enemies()) Sync(enemy); }
            else if (owner.IsEnemy != Caster.IsEnemy && (status.Key == ActionDot.Key(ActionDotKind.Shock) || status.Key == MarkKey)) Sync(owner);
        }
        public void Sync(Unit target)
        {
            if (_syncing || target == null) return;
            _syncing = true;
            try
            {
                bool wanted = Working && target.IsOnField && YamaDeathVerdict.IsShocked(target);
                if (!wanted) { target.RemoveStatusByKey(MarkKey); return; }
                if (!target.HasStatusKey(MarkKey))
                {
                    var status = BuffStatus.Create(71660, MarkKey, "촉진", Caster, target, new CatalysisEffect(this),
                        category: BaseEnums.StatusCategory.Negative, description: "감전 유지 중 특수방어력22~32% 감소.");
                    status.SkipEffectHitRoll = status.SkipStatusResistanceRoll = true;
                    target.AddStatus(status); _holders.Add(target);
                    Effects.Vfx.VfxComposer.PlayAffectedSlot("neith.passive", 0, Caster, target);
                    AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, CodeName);
                }
                else target.RefreshAttributes();
            }
            finally { _syncing = false; }
        }
        private sealed class CatalysisEffect : BaseEffect
        {
            private readonly NeithCatalyticWeave _owner;
            public CatalysisEffect(NeithCatalyticWeave owner) : base(0) { _owner = owner; Category = BaseEnums.EffectCategory.Negative; }
            public override float StatPercentModifier(Unit unit, BaseEnums.StatType stat)
                => unit == Target && stat == BaseEnums.StatType.SpecialDefense && _owner.Working && YamaDeathVerdict.IsShocked(unit) ? -_owner.DefenseCut : 0;
        }
    }

    public sealed class EreshkigalUnderworldFeast : DotRosterPassive
    {
        public const float HealRatio = .020f;
        public const int HealLimit = 3;
        private sealed class HealBudget
        {
            public readonly HashSet<long> Roots = new();
            public int Count;
        }
        private readonly Dictionary<(Unit actor, int action), HealBudget> _budgets = new();
        private int _outsideAction = -1, _count;
        public int HealsThisAction => _count;
        public EreshkigalUnderworldFeast(PassiveCodeContext context) : base(context) => CodeName = "명계의 양식";
        protected override void StartObserving()
        {
            _outsideAction = -1; _count = 0; _budgets.Clear();
            Unit.AnyDamageDealt += Damaged; ActionScheduler.AnyActionFinished += Finished;
        }
        protected override void StopObserving()
        {
            Unit.AnyDamageDealt -= Damaged; ActionScheduler.AnyActionFinished -= Finished;
            _budgets.Clear(); _count = 0; _outsideAction = -1;
        }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
            => _budgets.Remove((actor, GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1));
        private void Damaged(DamageResolvedContext hit)
        {
            var damage = hit?.DamageContext;
            if (!Working || damage == null || !damage.IsDamageOverTime || damage.IsSharedDamage || hit.DamageDealt <= 0 ||
                hit.Attacker == null || hit.Target == null || hit.Attacker.IsEnemy != Caster.IsEnemy || hit.Target.IsEnemy == Caster.IsEnemy) return;
            var scheduler = GameManager.Instance?.ActionScheduler;
            Unit actor = scheduler?.ExecutingUnit; int action = scheduler?.CurrentActionId ?? -1;
            if (actor == null && action != _outsideAction)
            { _budgets.Remove((null, _outsideAction)); _outsideAction = action; }
            // 선제 개입 뒤 부모 행동으로 돌아와도 그 행동의 소비량을 보존한다.
            var key = (actor, action);
            if (!_budgets.TryGetValue(key, out var budget)) _budgets[key] = budget = new HealBudget();
            _count = budget.Count;
            long root = damage.DotRootId > 0 ? damage.DotRootId : ActionDot.NextRootId();
            if (budget.Count >= HealLimit || !budget.Roots.Add(root)) return;
            _count = ++budget.Count;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * HealRatio)), Caster);
                Effects.Vfx.VfxComposer.PlayAffectedSlot("ereshkigal.passive", 0, Caster, ally);
            }
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Resolve, CodeName);
        }
    }
}
