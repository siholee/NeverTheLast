using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Combat
{
    public enum ActionDotKind { Shock, Burn, Poison, Curse, Bleed }

    /// <summary>지속피해의 자연 발동과 대상 일반행동 2회 시계를 분리한다. 즉시 발동은 시계를 건드리지 않는다.</summary>
    public static class ActionDot
    {
        private static readonly HashSet<Unit> NormalActors = new();
        private static readonly HashSet<(Unit actor, int action)> AttackCompletions = new();
        private static readonly HashSet<(Unit actor, int action)> BleedNormalCompletions = new();
        private static bool _observingAttacks;
        public static bool IsNormalActionRunning(Unit actor) => NormalActors.Contains(actor);
        public static void ResetClock() { NormalActors.Clear(); AttackCompletions.Clear(); BleedNormalCompletions.Clear(); }
        private static void ObserveAttack(DamageResolvedContext context)
        {
            var scheduler = GameManager.Instance?.ActionScheduler;
            Unit actor = scheduler?.ExecutingUnit;
            if (actor == null || context == null || !DirectAttack.IsDirect(context.DamageContext) ||
                context.DamageDealt <= 0 || (context.Attacker != actor && context.Attacker != actor.SummonOwner)) return;
            AttackCompletions.Add((actor, scheduler.CurrentActionId));
        }
        public static void OnActionFinished(Unit actor, ActionScheduler.ActionKind kind)
        {
            var key = (actor, GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1);
            if (actor != null && AttackCompletions.Remove(key) && actor.isActive)
                foreach (var status in actor.GetAllStatuses())
                foreach (var effect in status.Effects.Select(x=>x.EffectObject).OfType<ActionDotEffect>())
                    if (actor.isActive && actor.GetAllStatuses().Contains(status) && effect.Kind == ActionDotKind.Bleed)
                        actor.TakeDamage(effect.CreateDamageOverTimeTick());
            // 일반 완료 알림은 코루틴 안에서 먼저 난다. 마지막 출혈 틱 뒤에만 기간을 소모한다.
            if (actor != null && BleedNormalCompletions.Remove(key))
                foreach (var status in actor.GetAllStatuses().Where(IsBleed)) AdvanceDuration(actor,status);
            if (kind != ActionScheduler.ActionKind.Normal) return;
            NormalActors.Remove(actor);
            if (actor != null) foreach (var status in actor.GetAllStatuses()) status.SkipCurrentNormalCompletion = false;
        }
        public static void RememberSource(Unit source)
        {
            if (GridManager.Instance == null) return;
            foreach (Unit target in GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList).Concat(GridManager.Instance.summonList))
                if (target != null) foreach (var effect in target.GetAllStatuses().SelectMany(x => x.Effects).Select(x => x.EffectObject).OfType<ActionDotEffect>())
                    if (effect.Caster == source) effect.RememberPower();
        }
        public static string Key(ActionDotKind kind) => kind == ActionDotKind.Shock ? "reaction_shock" : kind.ToString().ToLowerInvariant();

        public static bool Apply(Unit source, Unit target, ActionDotKind kind, int duration = 2, bool inheritedHit = false)
        {
            if (source == null || target == null || !target.isActive || duration < 1) return false;
            float ratio = kind switch { ActionDotKind.Shock => .60f, ActionDotKind.Burn => .20f, ActionDotKind.Curse => .50f, ActionDotKind.Bleed => .30f, _ => .015f };
            var status = BuffStatus.Create(kind switch { ActionDotKind.Shock => 5910, ActionDotKind.Burn => 5911, ActionDotKind.Poison => 5915, ActionDotKind.Bleed => 5917, _ => 5916 }, Key(kind), Name(kind), source, target,
                new ActionDotEffect(kind, ratio), duration: duration,
                stackPolicy: BaseEnums.StatusStackPolicy.RefreshDuration, category: BaseEnums.StatusCategory.Negative,
                description: $"대상의 일반행동 {duration}회 동안 유지됩니다. 같은 지속피해 재부여는 기간을 갱신합니다.");
            status.NormalActionDuration = true;
            status.SkipEffectHitRoll = inheritedHit;
            return target.TryAddStatus(status);
        }

        private static string Name(ActionDotKind kind) => kind switch
        { ActionDotKind.Shock => "감전", ActionDotKind.Burn => "화상", ActionDotKind.Poison => "맹독", ActionDotKind.Bleed => "출혈", _ => "저주" };

        public static void OnActionStarted(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (actor == null || !actor.isActive) return;
            if (!_observingAttacks) { _observingAttacks = true; Unit.AnyDamageDealt += ObserveAttack; }
            bool normal = kind == ActionScheduler.ActionKind.Normal;
            if (normal) actor.RecoverToughnessOnNormalAction();
            if (normal && GameManager.Instance?.ActionScheduler?.ExecutingUnit == actor) NormalActors.Add(actor);
            // 복사본을 순회한다. 틱으로 사망·정화가 일어나면 남은 효과를 다시 확인한다.
            foreach (UnitStatus status in actor.GetAllStatuses())
            foreach (ActionDotEffect effect in status.Effects.Select(x => x.EffectObject).OfType<ActionDotEffect>())
            {
                if (!actor.isActive || !actor.GetAllStatuses().Contains(status)) continue;
                if (effect.Kind == ActionDotKind.Burn || normal && effect.Kind is ActionDotKind.Shock or ActionDotKind.Poison)
                    actor.TakeDamage(effect.CreateDamageOverTimeTick());
            }
            if (!normal || !actor.isActive || GridManager.Instance == null) return;
            foreach (Unit target in GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList).Concat(GridManager.Instance.summonList).Distinct().ToArray())
            {
                if (target == null || !target.isActive || !target.IsOnField) continue;
                foreach (UnitStatus status in target.GetAllStatuses())
                foreach (ActionDotEffect effect in status.Effects.Select(x => x.EffectObject).OfType<ActionDotEffect>())
                    if (target.isActive && actor.isActive && effect.Kind == ActionDotKind.Curse && effect.Caster == actor)
                        target.TakeDamage(effect.CreateDamageOverTimeTick());
            }
        }

        public static void OnNormalResolved(Unit actor)
        {
            foreach (UnitStatus status in actor.GetAllStatuses())
            {
                if (!status.NormalActionDuration) continue;
                if (IsBleed(status) && IsNormalActionRunning(actor))
                {
                    BleedNormalCompletions.Add((actor, GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1));
                    continue;
                }
                AdvanceDuration(actor,status);
            }
            // 일반 실제 종료 때까지 보관하여 출혈의 마지막 틱과 재부여 시계를 일치시킨다.
            if (!BleedNormalCompletions.Any(x=>x.actor==actor)) NormalActors.Remove(actor);
        }
        private static bool IsBleed(UnitStatus status) => status.Effects.Any(x=>x.EffectObject is ActionDotEffect e && e.Kind==ActionDotKind.Bleed);
        private static void AdvanceDuration(Unit actor,UnitStatus status)
        {
            if (status.SkipCurrentNormalCompletion) { status.SkipCurrentNormalCompletion = false; return; }
            status.ElapsedTurns++;
            if (status.RemainingTurns == 0) actor.RemoveStatusByKey(status.Key);
        }
    }

    public sealed class ActionDotEffect : BaseEffect
    {
        public readonly ActionDotKind Kind;
        private readonly float _ratio;
        private float _lastPower;
        public override bool IsDamageOverTime => true;
        public ActionDotEffect(ActionDotKind kind, float ratio) : base(0, ratio)
        { Kind = kind; _ratio = ratio; Category = BaseEnums.EffectCategory.Negative; }
        public override void OnApply() => RememberPower();
        internal void RememberPower()
        { if (Caster != null && Caster.isActive) _lastPower = Caster.StatValue(BaseEnums.StatType.SpecialAttack); }
        public override int EstimateDamagePerTurn()
        {
            RememberPower();
            float raw = Kind == ActionDotKind.Poison ? (Target?.HpMax ?? 0) * _ratio : _lastPower * _ratio;
            if (Kind is ActionDotKind.Shock or ActionDotKind.Burn) raw *= Effects.Negative.ElementalReaction.HarmfulPotency(Caster);
            return Mathf.Max(1, Mathf.RoundToInt(raw));
        }
        public override DamageContext CreateDamageOverTimeTick() => new DamageContext(Caster, EstimateDamagePerTurn(),
            BaseEnums.CodeType.Effect, Kind is ActionDotKind.Shock or ActionDotKind.Burn
                ? new List<int> { DamageTag.ElementalReaction } : new List<int>())
            { IsDamageOverTime = true, StatusVfxKey = ActionDot.Key(Kind) };
    }
}
