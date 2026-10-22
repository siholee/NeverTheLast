using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Codes.Passive.Reward
{
    /// <summary>실제 추가행동과 공격 태그를 구분하는 정밀·추공 보상.</summary>
    public sealed class FollowUpRewardCode : RewardPassiveCode
    {
        public static readonly int[] Ids = { 656, 657, 658, 659, 660, 754, 755, 756, 757, 832, 833, 913 };
        public const string PrecisionKey = "reward_precision";
        public const int PrecisionLimit = 20;
        private readonly HashSet<(int action, Unit target)> _sharpened = new();
        private readonly HashSet<int> _finished = new(), _reactions = new();
        private readonly Dictionary<Unit, int> _fatal = new();
        private readonly HashSet<Unit> _bullTargets = new();
        private bool Working => HolderWorking && !Caster.IsDying && Caster.HpCurr > 0;
        private static ActionScheduler Scheduler => GameManager.Instance?.ActionScheduler;
        private static bool OwnAdditional(Unit unit) => unit != null && Scheduler?.ExecutingUnit == unit && ActionScheduler.IsAdditional(Scheduler.ActingKind);
        public static int Precision(Unit unit) => unit?.GetAllStatuses().Where(s => s.Key == PrecisionKey)
            .SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<PrecisionMark>().Select(e => e.Count).FirstOrDefault() ?? 0;
        public static void GainPrecision(Unit unit, int amount)
        {
            if (unit == null || !unit.IsOnField || unit.IsDying || unit.HpCurr <= 0 || amount <= 0) return;
            int count = Mathf.Min(PrecisionLimit, Precision(unit) + amount);
            var mark = BuffStatus.Create(98913, PrecisionKey, "정밀", null, unit, new PrecisionMark(count),
                category: StatusCategory.Neutral, description: $"정밀 {count}/{PrecisionLimit}. 전투 종료 시 초기화. 정화·강화 해제 불가.");
            mark.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            unit.AddStatus(mark);
            unit.RefreshAttributes();
        }
        public FollowUpRewardCode(PassiveCodeContext context, int id) : base(context, id) { }
        protected override void OnCast()
        {
            _sharpened.Clear(); _finished.Clear(); _reactions.Clear(); _fatal.Clear();
            AddStatus(Caster, new HolderEffect(this), category: StatusCategory.Neutral);
            if (RewardId == 833 && Working && !Caster.HasStatusKey("reward_precision_prepared"))
            {
                // 재시전/중복 개전 알림으로 같은 전투의 준비 중첩을 복제하지 않는다.
                var used = BuffStatus.Create(98914, "reward_precision_prepared", "준비된 자", null, Caster,
                    new PrecisionMark(0), category: StatusCategory.Neutral);
                used.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
                Caster.AddStatus(used); GainPrecision(Caster, 6);
            }
            ActionScheduler.AnyActionFinished += Finished;
            ActionScheduler.AnyActionValueReduced += Advanced;
            Unit.AnyDamageDealt += Damaged;
            Unit.AnyUnitDied += Died;
        }
        protected override void OnStop()
        {
            ActionScheduler.AnyActionFinished -= Finished;
            ActionScheduler.AnyActionValueReduced -= Advanced;
            Unit.AnyDamageDealt -= Damaged; Unit.AnyUnitDied -= Died;
            foreach (Unit target in _bullTargets)
                if (target != null && target.GetAllStatuses().Any(s => s.Key == "reward_bullfighter" && s.Caster == Caster))
                    target.RemoveStatusByKey("reward_bullfighter");
            _bullTargets.Clear();
            _sharpened.Clear(); _finished.Clear(); _reactions.Clear(); _fatal.Clear();
        }
        private static bool AdditionalHit(DamageContext context) => context != null && context.HasTag(DamageTag.AdditionalAttack) &&
            !context.HasTag(DamageTag.BonusDamage) && !context.HasTag(DamageTag.ElementalReaction) && !context.IsDamageOverTime && !context.IsSharedDamage;
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (!Working || actor != Caster || !ActionScheduler.IsAdditional(kind) || !_finished.Add(Scheduler.CurrentActionId)) return;
            if (RewardId == 755) GainPrecision(Caster, 1);
            if (RewardId == 757) Caster.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * .03f)), Caster);
        }
        private void Advanced(Unit target, Unit actor)
        {
            if (RewardId != 657 || !Working || actor != Caster || target == null || target.IsEnemy == Caster.IsEnemy || !target.IsOnField) return;
            var status = BuffStatus.Create(StatusId(), "reward_bullfighter", CodeName, Caster, target,
                new BullfighterEffect(), category: StatusCategory.Negative, description: Entry.description);
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            target.AddStatus(status); _bullTargets.Add(target);
        }
        private void Damaged(DamageResolvedContext hit)
        {
            if (!Working || hit?.Target == null || hit.Attacker != Caster || hit.Target.IsEnemy == Caster.IsEnemy ||
                hit.DamageDealt <= 0 || !AdditionalHit(hit.DamageContext)) return;
            int action = Scheduler?.CurrentActionId ?? -1;
            if (RewardId == 832 && hit.Target.HpCurr <= 0) _fatal[hit.Target] = action;
            if (RewardId == 756 && hit.Target.isActive && hit.Target.HpCurr > 0 && _sharpened.Add((action, hit.Target)))
                hit.Target.TakeDamage(new DamageContext(Caster, 20 + Caster.Level * 2, CodeType.Passive,
                    new List<int> { DamageTag.TrueDamage, DamageTag.BonusDamage, DamageTag.SingleTarget }));
        }
        private void Died(Unit dead, Unit attacker)
        {
            if (RewardId == 832 && Working && attacker == Caster && dead != null && _fatal.TryGetValue(dead, out int action) && _reactions.Add(action))
                Scheduler?.AdvanceAction(Caster, 1f);
            if (dead != null) _fatal.Remove(dead);
        }
        private sealed class PrecisionMark : BaseEffect
        {
            public int Count { get; }
            public PrecisionMark(int count) : base(0, count) => Count = count;
        }
        private sealed class HolderEffect : BaseEffect
        {
            private readonly FollowUpRewardCode _owner;
            public HolderEffect(FollowUpRewardCode owner) : base(0) => _owner = owner;
            public override float StatPercentModifier(Unit unit, StatType stat) => _owner.Working && unit == Target &&
                _owner.RewardId == 658 && stat == StatType.Attack ? Precision(unit) * .02f : 0;
            public override float ActionAttackBonus(Unit unit) => _owner.Working && unit == Target && _owner.RewardId == 913 && OwnAdditional(unit) ? Precision(unit) * .03f : 0;
            public override float ActionManaRecoveryMultiplier(Unit unit) => _owner.Working && unit == Target && _owner.RewardId == 660 &&
                unit.ReceivingDamageContext == null && OwnAdditional(unit) ? 1.30f : 1;
            public override float TaggedCritDamageBonus(Unit attacker, IReadOnlyList<int> tags)
            {
                if (!_owner.Working || attacker != Target || tags == null || !tags.Contains(DamageTag.AdditionalAttack) || tags.Contains(DamageTag.BonusDamage)) return 0;
                if (_owner.RewardId == 913) return Precision(attacker) * .05f;
                return _owner.RewardId == 656 && tags.Contains(DamageTag.SingleTarget) && !tags.Contains(DamageTag.MultiTarget) ? .40f : 0;
            }
            public override float ReceivingDamageModifier(Unit unit, DamageContext context) => _owner.Working && unit == Target &&
                _owner.RewardId == 754 && unit.ShieldCurr > 0 ? .75f : 1;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => _owner.Working && attacker == Target &&
                _owner.RewardId == 659 && AdditionalHit(context) && context.HasTag(DamageTag.CounterAttack) ? 1.20f : 1;
        }
        /// <summary>다음 실제 공격 한 묶음에서 소비한다. 다단 전체는 같은 행동 ID의 치피 감소를 받는다.</summary>
        private sealed class BullfighterEffect : BaseEffect
        {
            private int _usedAction = -1;
            public BullfighterEffect() : base(0) { Category = EffectCategory.Negative; }
            public override float CritDamageBonusAgainst(Unit attacker, Unit target, CodeType codeType) => attacker == Target ? -.40f : 0;
            public override void OnApply() { Unit.AnyDamageDealt += Damaged; ActionScheduler.AnyActionFinished += Finished; }
            public override void OnRemove() { Unit.AnyDamageDealt -= Damaged; ActionScheduler.AnyActionFinished -= Finished; }
            private void Damaged(DamageResolvedContext hit)
            {
                if (hit?.Attacker != Target || hit.DamageDealt <= 0 || hit.DamageContext == null || hit.DamageContext.IsDamageOverTime ||
                    hit.DamageContext.IsSharedDamage || hit.DamageContext.HasTag(DamageTag.BonusDamage) || hit.DamageContext.HasTag(DamageTag.ElementalReaction)) return;
                if (Scheduler?.ExecutingUnit == Target) _usedAction = Scheduler.CurrentActionId;
                else Target.RemoveStatusByKey("reward_bullfighter");
            }
            private void Finished(Unit actor, ActionScheduler.ActionKind kind)
            {
                if (actor == Target && _usedAction == Scheduler.CurrentActionId) Target.RemoveStatusByKey("reward_bullfighter");
            }
        }
    }
}
