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
using static BaseClasses.BaseEnums;

namespace Codes.Passive.Reward
{
    /// <summary>묶음 개시·자신의 특수행동·실제 원소 부착을 별개의 사건으로 다룬다.</summary>
    public sealed class SpecialElementRewardCode : RewardPassiveCode
    {
        public static readonly int[] Ids = { 513, 663, 664, 665, 666, 763, 764, 765, 766, 767, 768, 837, 838, 839, 917 };
        private readonly TurnCooldown _cooldown = new(2), _elementCooldown = new(1);
        private int _lastAction = -1;
        private bool Working => HolderWorking && !Caster.IsDying && Caster.HpCurr > 0;
        private static ActionScheduler Scheduler => GameManager.Instance?.ActionScheduler;
        public SpecialElementRewardCode(PassiveCodeContext context, int id) : base(context, id) { }
        protected override void OnCast()
        {
            _cooldown.Reset(); _elementCooldown.Reset(); _lastAction = -1;
            AddStatus(Caster, new HolderEffect(this), category: StatusCategory.Neutral);
            SpecialAction.Opened += Opened;
            SpecialAction.Finished += Finished;
            ActionScheduler.AnyActionStarted += Started;
            ActionScheduler.AnyActionFinished += Resolved;
            Unit.AnyCombatElementGranted += Granted;
            if (RewardId is 666 or 767 && Working && SpecialAction.CanOpen(Caster))
            {
                string key = "special_reward_opening_" + RewardId;
                if (Caster.HasStatusKey(key)) return;
                var used = BuffStatus.Create(StatusId(1), key, CodeName, null, Caster, new OpeningUsed(), category: StatusCategory.Neutral);
                used.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
                Caster.AddStatus(used);
                if (RewardId == 666 && Caster.UltimateResourceType == UltimateResourceType.Mana)
                    Caster.AddUltimateResource(Mathf.RoundToInt(Caster.ManaMax * .25f));
                if (RewardId == 767) foreach (Unit ally in FieldAllies()) ally.AddShield(Defense() * 2, Caster);
            }
        }
        protected override void OnStop()
        {
            SpecialAction.Opened -= Opened; SpecialAction.Finished -= Finished;
            ActionScheduler.AnyActionStarted -= Started; ActionScheduler.AnyActionFinished -= Resolved;
            Unit.AnyCombatElementGranted -= Granted;
        }
        private int Defense() => Mathf.Max(1, Mathf.RoundToInt(Caster.StatValue(StatType.Defense)));
        private void HealAll(float ratio)
        {
            foreach (Unit ally in FieldAllies().Where(u => !u.IsDying && u.HpCurr > 0))
                ally.Heal(Mathf.Max(1, Mathf.RoundToInt(ally.HpMax * ratio)), Caster);
        }
        private void Opened(SpecialAction.Batch batch)
        {
            if (!Working || batch.Opener != Caster) return;
            if (RewardId == 764)
            {
                HealAll(.12f);
                foreach (Unit ally in FieldAllies()) ally.RemoveAllNegativeStatuses(Caster);
            }
        }
        private void Finished(SpecialAction.Batch batch)
        {
            if (!Working || batch.Opener != Caster || RewardId != 763) return;
            foreach (Unit ally in batch.Members.Where(u => u != null && u.isActive && u.IsOnField && !u.IsDying && u.HpCurr > 0))
                ally.Heal(Mathf.Max(1, Mathf.RoundToInt(ally.HpMax * .08f)), Caster);
        }
        private void Started(Unit actor, ActionScheduler.ActionKind kind, string label)
        {
            if (!Working || actor != Caster || kind != ActionScheduler.ActionKind.Special ||
                _lastAction == Scheduler.CurrentActionId) return;
            _lastAction = Scheduler.CurrentActionId;
            if (RewardId is 663 or 765 && _cooldown.TryUse(Caster))
            {
                if (RewardId == 663) HealAll(.04f);
                else foreach (Unit ally in FieldAllies())
                {
                    ally.AddShield(Mathf.RoundToInt(Defense() * 1.2f), Caster);
                    AddStatus(ally, new NegativeStatusWardEffect(), "_resistance", duration: 2);
                }
            }
        }
        private void Resolved(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (Working && RewardId == 513 && actor == Caster && kind == ActionScheduler.ActionKind.Special)
                Caster.AddShield(Mathf.RoundToInt(Defense() * .8f), Caster);
        }
        private void Granted(Unit source, Unit target, UnitElement element)
        {
            if (!Working || source != Caster || target == null || target.IsEnemy == Caster.IsEnemy ||
                RewardId is not (665 or 768) || !_elementCooldown.TryUse(Caster)) return;
            if (RewardId == 665) Caster.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * .03f)), Caster);
            else if (Caster.UltimateResourceType == UltimateResourceType.Mana) Caster.AddUltimateResource(6);
        }
        private static bool Direct(DamageContext context) => context != null && !context.IsDamageOverTime &&
            !context.IsSharedDamage && !context.HasTag(DamageTag.BonusDamage) && !context.HasTag(DamageTag.ElementalReaction);
        public static bool IsSpecialHit(Unit attacker, DamageContext context) => Direct(context) &&
            (context.HasTag(DamageTag.SpecialAttack) || Scheduler?.ExecutingUnit == attacker && Scheduler.ActingKind == ActionScheduler.ActionKind.Special);
        private sealed class OpeningUsed : BaseEffect { public OpeningUsed() : base(0) { } }
        private sealed class HolderEffect : BaseEffect
        {
            private readonly SpecialElementRewardCode _owner;
            public HolderEffect(SpecialElementRewardCode owner) : base(0) => _owner = owner;
            private bool InBatch(Unit attacker) => SpecialAction.CurrentBatch?.Opener == attacker;
            private int Count => SpecialAction.CurrentBatch?.Members.Contains(Target) == true ? Mathf.Min(6, SpecialAction.CurrentBatch.Count) : 0;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (!_owner.Working || attacker != Target) return 1;
                if (_owner.RewardId == 766) return Direct(context) && context.AppliesElement ? 1.18f : 1;
                if (!IsSpecialHit(attacker, context)) return 1;
                return _owner.RewardId switch
                {
                    664 => 1.20f,
                    839 => 1.40f,
                    838 => attacker.Element == nameof(UnitElement.Electro) ? 1.35f : 1,
                    837 => 1 + Count * .08f,
                    917 => InBatch(attacker) ? 1 + Count * .08f : 1,
                    _ => 1
                };
            }
            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => _owner.Working && attacker == Target && _owner.RewardId == 917 && InBatch(attacker) && IsSpecialHit(attacker, context)
                    ? 1 - Count * .04f : 1;
        }
    }
}
