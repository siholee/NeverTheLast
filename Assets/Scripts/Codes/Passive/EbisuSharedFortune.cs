using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>실제 피격 대상은 남겨 반격을 유지하고, 경감 뒤 피해 일부를 자신의 체력으로 감당한다.</summary>
    public sealed class EbisuSharedFortune : UniquePassiveCode
    {
        public const float ShareRatio = .65f;
        public const int MaximumStacks = 12;
        public const float DamagePerStack = .025f;
        public const string BuffKey = "ebisu_gratitude";
        private readonly HashSet<(Unit target, int action)> _handled = new();
        private readonly HashSet<Unit> _holders = new();
        private bool _working;
        public EbisuSharedFortune(PassiveCodeContext context) : base(context) => CodeName = "복은 함께 나누는 거야";
        private bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField && Caster.HpCurr > 0;
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true; _handled.Clear(); _holders.Clear();
            Unit.AnyDamageDealt += OnDamaged;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath);
        }
        public override void StopCode()
        {
            _working = false;
            Unit.AnyDamageDealt -= OnDamaged;
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath);
            foreach (Unit holder in _holders) if (holder != null) holder.RemoveStatusByKey(BuffKey);
            _holders.Clear(); _handled.Clear();
        }
        private void OnDeath(EventContext _) => StopCode();

        public static int ShareDamage(Unit target, DamageContext original, int damage)
        {
            if (target == null || original == null || damage <= 1 || original.IsSharedDamage ||
                original.Attacker == null || original.Attacker.IsEnemy == target.IsEnemy || GridManager.Instance == null ||
                GameManager.Instance?.gameState != BaseEnums.GameState.RoundInProgress) return damage;
            var guards = target.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            var guard = guards.Where(unit => unit != null && unit != target && unit.isActive && unit.IsOnField && !unit.IsUntargetable)
                .SelectMany(unit => unit.ActivePassiveCodes.OfType<EbisuSharedFortune>())
                .Where(code => code.Working).OrderByDescending(code => code.Caster.HpCurr).FirstOrDefault();
            if (guard == null) return damage;
            int share = Mathf.FloorToInt(damage * ShareRatio);
            if (share <= 0) return damage;
            var transfer = new DamageContext(original.Attacker, share, BaseEnums.CodeType.Effect,
                new List<int> { DamageTag.SharedDamage }) { IsSharedDamage = true };
            guard.Caster.TakeDamage(transfer);
            // 죽음·무적·취소로 실제 감당하지 못한 양은 원래 대상에게 남긴다. 피해를 삭제하지 않는다.
            int absorbed = Mathf.Min(damage, transfer.ResolvedDamage);
            original.SharedDamage += absorbed;
            return damage - absorbed;
        }

        private void OnDamaged(DamageResolvedContext hit)
        {
            var action = GameManager.Instance?.ActionScheduler;
            if (!Working || hit?.Target == null || !hit.Target.isActive || hit.Target.HpCurr <= 0 || !hit.Target.IsOnField ||
                hit.Target.IsEnemy != Caster.IsEnemy || hit.DamageDealt <= 0 || hit.DamageContext == null ||
                hit.DamageContext.IsSharedDamage || !DirectAttack.IsDirect(hit.DamageContext) ||
                hit.Attacker.IsEnemy == Caster.IsEnemy || action?.ExecutingUnit == null ||
                !_handled.Add((hit.Target, action.CurrentActionId))) return;
            GrantStack(hit.Target);
        }
        private void GrantStack(Unit target)
        {
            int previous = target.GetAllStatuses().Where(status => status.Key == BuffKey)
                .SelectMany(status => status.Effects).Select(effect => effect.EffectObject).OfType<GratitudeEffect>()
                .Select(effect => effect.Stacks).DefaultIfEmpty(0).Max();
            int stacks = Mathf.Min(MaximumStacks, previous + 1);
            var status = BuffStatus.Create(71631, BuffKey, "돌아오는 복", Caster, target,
                new GratitudeEffect(stacks), duration: 3, isBeneficial: true,
                description: $"피격 중첩 {stacks}/{MaximumStacks}: 가하는 피해 +{stacks * DamagePerStack * 100f:0.#}%. 자신의 일반행동3회 완료까지.");
            status.NormalActionDuration = true;
            if (target.TryAddStatus(status)) _holders.Add(target);
        }
        public sealed class GratitudeEffect : OutgoingDamageMultiplierEffect
        {
            public int Stacks { get; }
            public GratitudeEffect(int stacks) : base(1f + stacks * DamagePerStack) => Stacks = stacks;
        }
    }
}
