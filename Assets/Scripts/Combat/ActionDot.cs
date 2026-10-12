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
    public enum ActionDotKind { Shock, Burn, Poison, Curse }

    /// <summary>지속피해의 자연 발동과 대상 일반행동 2회 시계를 분리한다. 즉시 발동은 시계를 건드리지 않는다.</summary>
    public static class ActionDot
    {
        private static readonly HashSet<Unit> NormalActors = new();
        public static bool IsNormalActionRunning(Unit actor) => NormalActors.Contains(actor);
        public static void ResetClock() => NormalActors.Clear();
        public static void OnActionFinished(Unit actor, ActionScheduler.ActionKind kind)
        {
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
            float ratio = kind switch { ActionDotKind.Shock => .60f, ActionDotKind.Burn => .20f, ActionDotKind.Curse => .50f, _ => .015f };
            var status = BuffStatus.Create(kind switch { ActionDotKind.Shock => 5910, ActionDotKind.Burn => 5911, ActionDotKind.Poison => 5915, _ => 5916 }, Key(kind), Name(kind), source, target,
                new ActionDotEffect(kind, ratio), duration: duration,
                stackPolicy: BaseEnums.StatusStackPolicy.RefreshDuration, category: BaseEnums.StatusCategory.Negative,
                description: $"대상의 일반행동 {duration}회 동안 유지됩니다. 같은 지속피해 재부여는 기간을 갱신합니다.");
            status.NormalActionDuration = true;
            status.SkipEffectHitRoll = inheritedHit;
            return target.TryAddStatus(status);
        }

        private static string Name(ActionDotKind kind) => kind switch
        { ActionDotKind.Shock => "감전", ActionDotKind.Burn => "화상", ActionDotKind.Poison => "맹독", _ => "저주" };

        public static void OnActionStarted(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (actor == null || !actor.isActive) return;
            bool normal = kind == ActionScheduler.ActionKind.Normal;
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
                if (status.SkipCurrentNormalCompletion) { status.SkipCurrentNormalCompletion = false; continue; }
                status.ElapsedTurns++;
                if (status.RemainingTurns == 0) actor.RemoveStatusByKey(status.Key);
            }
            NormalActors.Remove(actor);
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
            BaseEnums.CodeType.Effect, new List<int>()) { IsDamageOverTime = true, StatusVfxKey = ActionDot.Key(Kind) };
    }
}
