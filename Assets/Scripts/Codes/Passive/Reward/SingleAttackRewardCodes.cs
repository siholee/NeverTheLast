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
using static BaseClasses.BaseEnums;

namespace Codes.Passive.Reward
{
    /// <summary>단일 일반공격·간파·분배 피해와 소모형 방호 보상. 실제 피해 문맥과 코드 인스턴스를 사용한다.</summary>
    public sealed class SingleAttackRewardCode : RewardPassiveCode, IRewardAuraSource
    {
        public static readonly int[] Ids = { 661, 662, 758, 759, 760, 761, 762, 834, 835, 836, 914, 915, 916 };
        private static readonly HashSet<SingleAttackRewardCode> Auras = new();
        private readonly HashSet<(int action, Unit target)> _lastWill = new();
        private readonly Dictionary<Unit, DamageContext> _fatal = new();
        private int _singleAction = -1, _spentAction = -1, _spentMana, _haste, _executionKills;
        private bool _refunded, _consuming;
        private bool Working => HolderWorking && !Caster.IsDying && Caster.HpCurr > 0;
        private static ActionScheduler Scheduler => GameManager.Instance?.ActionScheduler;
        public string AuraFamily => RewardId == 661 ? "idol_guard" : "shield_sanctuary";
        public float AuraValue => Working ? RewardId == 661 ? .10f : RewardId == 758 ? .15f : 0f : 0f;
        public override int BaseShieldAmount => RewardId == 760 ? StatDamage(0, StatType.Attack, 4f) : 0;
        public SingleAttackRewardCode(PassiveCodeContext context, int id) : base(context, id) { }

        public static bool SingleAttack(DamageContext context) => context != null && context.HasTag(DamageTag.SingleTarget) &&
            !context.HasTag(DamageTag.MultiTarget) && !context.HasTag(DamageTag.AllTarget) &&
            !context.IsDamageOverTime && !context.IsSharedDamage && context.CodeType != BaseEnums.CodeType.Effect &&
            !context.HasTag(DamageTag.BonusDamage) && !context.HasTag(DamageTag.ElementalReaction);
        public static bool SingleNormal(DamageContext context) => SingleAttack(context) && context.HasTag(DamageTag.NormalAttack);

        protected override void OnCast()
        {
            _lastWill.Clear(); _fatal.Clear(); _singleAction = _spentAction = -1;
            _spentMana = _haste = _executionKills = 0; _refunded = _consuming = false;
            AddStatus(Caster, new HolderEffect(this), category: StatusCategory.Neutral);
            if (RewardId is 661 or 758) Auras.Add(this);
            if (RewardId is 759 or 762 or 835 or 914 or 915 or 916) Unit.AnyDamageDealt += Damaged;
            if (RewardId is 762 or 916) Unit.AnyUnitDied += Died;
            if (RewardId == 761) Unit.AnyNegativeStatusGranted += Debuffed;
            if (RewardId == 916) Unit.AnyUltimateManaSpent += Spent;
            if (RewardId is 834 or 915) Caster.AddListener<EventContext>(UnitEventType.OnNormalActionResolved, NormalResolved);
            if (RewardId == 760) Caster.AddListener<EventContext>(UnitEventType.OnAfterDamageTaken, AfterDamage);
        }
        protected override void OnStop()
        {
            Auras.Remove(this);
            Unit.AnyDamageDealt -= Damaged; Unit.AnyUnitDied -= Died;
            Unit.AnyNegativeStatusGranted -= Debuffed; Unit.AnyUltimateManaSpent -= Spent;
            Caster?.RemoveListener<EventContext>(UnitEventType.OnNormalActionResolved, NormalResolved);
            Caster?.RemoveListener<EventContext>(UnitEventType.OnAfterDamageTaken, AfterDamage);
            _lastWill.Clear(); _fatal.Clear(); _spentMana = 0;
        }
        private void NormalResolved(EventContext context)
        {
            if (!Working) return;
            if (RewardId == 834) { _haste++; Caster.RefreshAttributes(); }
            if (RewardId == 915 && Scheduler?.ExecutingUnit == Caster && _singleAction == Scheduler.CurrentActionId)
                Caster.RecoverMana(Unit.ManaPerNormalAction / 2);
        }
        private void Damaged(DamageResolvedContext hit)
        {
            if (!Working || hit?.Target == null || hit.Attacker != Caster || hit.Target.IsEnemy == Caster.IsEnemy ||
                hit.DamageContext == null || hit.DamageContext.IsSharedDamage || hit.DamageDealt <= 0) return;
            Unit target = hit.Target; var context = hit.DamageContext;
            if (target.HpCurr <= 0) _fatal[target] = context;
            if (SingleNormal(context))
            {
                if (Scheduler?.ExecutingUnit == Caster) _singleAction = Scheduler.CurrentActionId;
                if (RewardId == 835 && target.HpCurr > 0)
                    Debuff(target, "reward_birth_sword", "사산 검", new HealingReduction(), "받는 치유50% 감소.");
                if (RewardId == 914 && target.HpCurr > 0 && _lastWill.Add((Scheduler?.CurrentActionId ?? -1, target)))
                    Bonus(Caster, target, Mathf.RoundToInt(Mathf.Min(target.HpMax * .02f, Caster.AtkCurr * 3f)));
            }
            if (RewardId == 759 && SingleAttack(context) && target.HpCurr > 0) EnemyInsight.Gain(Caster, target);
        }
        private void Spent(Unit actor, int amount)
        {
            if (!Working || RewardId != 916 || actor != Caster) return;
            _spentMana = amount; _spentAction = Scheduler?.CurrentActionId ?? -1; _refunded = false;
        }
        private void Died(Unit target, Unit attacker)
        {
            if (Working && attacker == Caster && target != null && target.IsEnemy != Caster.IsEnemy && !target.IsSummon &&
                _fatal.TryGetValue(target, out DamageContext hit))
            {
                if (RewardId == 762 && hit.IsExecution && _executionKills < 5)
                { _executionKills++; GameManager.Instance?.AddBattleGoldBonus(.10f); }
                if (RewardId == 916 && !_refunded && _spentMana > 0 && _spentAction == Scheduler?.CurrentActionId &&
                    hit.HasTag(DamageTag.UltAttack) && !hit.IsDamageOverTime)
                { _refunded = true; Caster.AddUltimateResource(Mathf.RoundToInt(_spentMana * .30f)); }
            }
            if (target != null) _fatal.Remove(target);
        }
        private bool Consume()
        {
            var instance = Caster.ActiveRewardInstance(RewardId);
            return instance != null && (Codes.Passive.AstolfoTravelKit.TryPreserveBattleConsumable() ||
                                       Caster.RemoveRewardCodeInstance(instance.instanceId));
        }
        private void AfterDamage(EventContext context)
        {
            if (RewardId != 760 || !Working || _consuming || context?.DmgCtx == null || context.DmgCtx.ResolvedDamage <= 0 || Caster.HpCurr > Caster.HpMax * .30f) return;
            _consuming = true;
            try
            {
                int amount = BaseShieldAmount;
                if (Consume()) Caster.AddShield(amount, Caster);
            }
            finally { _consuming = false; }
        }
        private void Debuffed(Unit source, Unit target, UnitStatus status)
        {
            if (RewardId != 761 || !Working || _consuming || target != Caster || status?.IsControl != true) return;
            _consuming = true;
            try { if (Consume()) Caster.CleanseDebuffs(true, Caster); }
            finally { _consuming = false; }
        }
        internal static void Bonus(Unit source, Unit target, int amount)
        {
            if (amount <= 0 || target == null || !target.isActive || target.HpCurr <= 0) return;
            target.TakeDamage(new DamageContext(source, amount, BaseEnums.CodeType.Passive,
                new List<int> { DamageTag.SingleTarget, DamageTag.PassiveAttack, DamageTag.TrueDamage, DamageTag.BonusDamage }));
        }
        internal static void Debuff(Unit target, string key, string name, BaseEffect effect, string description)
        {
            Unit source = effect.Caster;
            var status = BuffStatus.Create(98932, key, name, source, target, effect, duration: 2,
                category: StatusCategory.Negative, description: description);
            status.NormalActionDuration = true;
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            target.AddStatus(status);
        }
        private void Debuff(Unit target, string key, string name, HealingReduction effect, string description)
        { effect.Caster = Caster; Debuff(target, key, name, (BaseEffect)effect, description); }
        private sealed class HealingReduction : BaseEffect
        {
            public HealingReduction() : base(0) { }
            public override float HealingReceivedMultiplierModifier(Unit unit) => unit == Target ? .5f : 1f;
        }
        private sealed class HolderEffect : BaseEffect
        {
            private readonly SingleAttackRewardCode _owner;
            public HolderEffect(SingleAttackRewardCode owner) : base(0) => _owner = owner;
            public override float StatPercentModifier(Unit unit, StatType stat) => _owner.Working && unit == Target &&
                _owner.RewardId == 834 && stat == StatType.Speed ? _owner._haste * .05f : 0f;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (!_owner.Working || attacker != Target || context == null) return 1;
                if (_owner.RewardId == 836 && target != null && target.ShieldCurr > 0) return 1.30f;
                return _owner.RewardId == 662 && context.IsDistributed ? 1.25f : 1f;
            }
        }
        // 타격 시 조회하여 개전 뒤 증원된 아군과 칸 없는 소환수도 같은 오라를 받는다.
        public static float AuraMultiplier(Unit attacker, Unit target)
            => Auras.Count == 0 ? 1f : (attacker != null && attacker.ShieldCurr > 0 ? 1f + RewardAura.Best(attacker, "shield_sanctuary") : 1f) *
               (target != null ? 1f - RewardAura.Best(target, "idol_guard") : 1f);
    }

    /// <summary>아군 정밀과 별개인 적별 공유 중립 표식. 시전자가 사라져도 남으며 전투 종료 시 정리한다.</summary>
    public static class EnemyInsight
    {
        public const string MarkKey = "reward_enemy_insight", VulnerableKey = "reward_insight_exposed";
        public static int Stacks(Unit target) => target?.GetAllStatuses().Where(s => s.Key == MarkKey)
            .SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<Mark>().Select(e => e.Count).FirstOrDefault() ?? 0;
        public static void Gain(Unit source, Unit target)
        {
            if (source == null || target == null || !target.isActive || !target.IsOnField || target.HpCurr <= 0 || source.IsEnemy == target.IsEnemy) return;
            int count = Stacks(target) + 1;
            target.RemoveStatusByKey(MarkKey);
            if (count >= 3)
            {
                var exposed = new Exposed { Caster = source };
                SingleAttackRewardCode.Debuff(target, VulnerableKey, "간파: 노출", exposed, "단일 공격으로 받는 피해15% 증가.");
                SingleAttackRewardCode.Bonus(source, target, Mathf.RoundToInt(source.AtkCurr * 1.2f));
                return;
            }
            var status = BuffStatus.Create(98931, MarkKey, "간파", null, target, new Mark(count),
                category: StatusCategory.Neutral, description: $"간파 {count}/3. 정화·강화 해제 불가. 3중첩에서 소모되어 고정 피해와 단일 공격 취약을 남긴다.");
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            target.AddStatus(status);
        }
        private sealed class Mark : BaseEffect
        {
            public readonly int Count;
            public Mark(int count) : base(0) => Count = count;
            public override void OnApply() => Target.AddListener<EventContext>(UnitEventType.OnRoundEnd, End);
            public override void OnRemove() => Target?.RemoveListener<EventContext>(UnitEventType.OnRoundEnd, End);
            private void End(EventContext _) => Target.RemoveStatusByKey(MarkKey);
        }
        private sealed class Exposed : BaseEffect
        {
            public Exposed() : base(0) { }
            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => unit == Target && SingleAttackRewardCode.SingleAttack(context) ? 1.15f : 1f;
        }
    }
}

