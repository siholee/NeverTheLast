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
using static BaseClasses.BaseEnums;

namespace Codes.Passive.Reward
{
    /// <summary>지딜·치유 연계 보상. 행동/보유자 일반 차례를 사용하며 파생 치유는 다시 증폭하지 않는다.</summary>
    public sealed class SecretSupportRewardCode : RewardPassiveCode
    {
        public static readonly int[] Ids = { 636, 637, 638, 639, 648, 649, 650, 651, 732, 733, 734, 735, 736, 737, 738, 824, 825, 910 };
        private static int _derivedHealing;
        private readonly TurnCooldown _shieldCooldown = new(1);
        private readonly HashSet<(Unit actor, int action, Unit target)> _echoUses = new();
        private readonly HashSet<(Unit target, string key)> _timed = new();
        private int _energyTurn, _energyUses;
        private bool _rescuing;
        internal int Id => RewardId;
        internal bool Working => HolderWorking && !Caster.IsDying && Caster.HpCurr > 0;
        public SecretSupportRewardCode(PassiveCodeContext context, int id) : base(context, id) { }
        public override int BaseShieldAmount => RewardId == 736 ? StatDamage(0, StatType.Defense, 1.5f) : 0;

        protected override void OnCast()
        {
            _shieldCooldown.Reset(); _echoUses.Clear(); _energyTurn = -1; _energyUses = 0;
            if (RewardId is 636 or 637 or 638 or 735) AddStatus(Caster, new HolderEffect(this), category: StatusCategory.Neutral);
            if (RewardId is 734 or 736 or 648 or 649 or 650) Unit.AnyHealed += Healed;
            if (RewardId is 639 or 737) Unit.AnyDamageDealt += Damaged;
            if (SecretRewardRules.IsProvider(RewardId)) SecretRewardRules.Register(this);
        }
        protected override void OnStop()
        {
            Unit.AnyHealed -= Healed; Unit.AnyDamageDealt -= Damaged;
            if (SecretRewardRules.IsProvider(RewardId)) SecretRewardRules.Unregister(this);
            foreach (var pair in _timed)
                if (pair.target != null && pair.target.GetAllStatuses().Any(s => s.Key == pair.key && s.Caster == Caster))
                    pair.target.RemoveStatusByKey(pair.key);
            _timed.Clear(); _echoUses.Clear(); _shieldCooldown.Reset();
        }
        private void Timed(Unit target, string key, BaseEffect effect, StatusCategory category)
        {
            var status = BuffStatus.Create(StatusId(), key, CodeName, Caster, target, effect, duration: 2,
                category: category, description: Entry.description);
            status.NormalActionDuration = true;
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            target.AddStatus(status); _timed.Add((target, key));
        }
        private void Healed(Unit target, Unit source, int effective, int overflow)
        {
            if (!Working || source != Caster || target == null || !target.IsOnField ||
                target.IsEnemy != Caster.IsEnemy || (long)effective + overflow <= 0) return;
            switch (RewardId)
            {
                case 734:
                    // 같은 버프는 갱신한다. 다른 보유자가 마지막으로 건 버프를 정지 시 지우지 않는다.
                    Timed(target, "reward_lantern", new StatModifierEffect(StatType.Attack, .15f, true), StatusCategory.Positive);
                    break;
                case 649:
                    Timed(Caster, "reward_victory_hymn", new StatModifierEffect(StatType.Speed, .10f, true), StatusCategory.Positive);
                    break;
                case 736:
                    if (_shieldCooldown.TryUse(Caster)) Caster.AddShield(Mathf.RoundToInt(Caster.StatValue(StatType.Defense) * 1.5f), Caster);
                    break;
                case 648:
                    if (_derivedHealing > 0) return;
                    var scheduler = GameManager.Instance?.ActionScheduler;
                    Unit actor = scheduler?.ExecutingUnit;
                    int action = actor != null ? scheduler.CurrentActionId : -Caster.TurnCount - 1;
                    if (!_echoUses.Add((actor ?? Caster, action, target))) return;
                    DerivedHeal(target, Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * .01f)));
                    break;
                case 650:
                    if (_derivedHealing > 0 || target == Caster || effective <= 0) return;
                    int amount = Mathf.RoundToInt(effective * .20f);
                    if (amount > 0) DerivedHeal(Caster, amount);
                    break;
            }
        }
        private void DerivedHeal(Unit target, int amount)
        {
            _derivedHealing++;
            try { target.Heal(amount, Caster); }
            finally { _derivedHealing--; }
        }
        private void Damaged(DamageResolvedContext hit)
        {
            if (!Working || hit?.DamageContext == null || hit.DamageDealt <= 0 || hit.DamageContext.IsSharedDamage) return;
            if (RewardId == 639 && hit.Target != null && hit.Target.IsEnemy != Caster.IsEnemy && hit.Attacker != null &&
                hit.Attacker.IsEnemy == Caster.IsEnemy && hit.DamageContext.IsDamageOverTime)
            {
                if (_energyTurn != Caster.TurnCount) { _energyTurn = Caster.TurnCount; _energyUses = 0; }
                if (_energyUses >= 5 || Caster.ManaCurr >= Caster.ManaMax) return;
                int before = Caster.ManaCurr;
                Caster.RecoverMana(2); // 고유 중첩형 자원은 공통 마나 회복을 받지 않는다.
                if (Caster.ManaCurr > before) _energyUses++;
            }
            else if (RewardId == 737 && hit.Target == Caster && hit.Attacker != null && hit.Attacker.IsOnField &&
                hit.Attacker.IsEnemy != Caster.IsEnemy && DirectAttack.IsDirect(hit.DamageContext))
                Timed(hit.Attacker, "reward_nerve", new StatModifierEffect(StatType.SpecialDefense, -.15f, true), StatusCategory.Negative);
        }
        private sealed class HolderEffect : BaseEffect
        {
            private readonly SecretSupportRewardCode _owner;
            public HolderEffect(SecretSupportRewardCode owner) : base(0) => _owner = owner;
            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => _owner.Working && attacker == Target && _owner.Id == 636 && context?.IsDamageOverTime == true ? .8f : 1;
            public override int OutgoingStatusDurationModifier(Unit caster, UnitStatus status)
                => _owner.Working && caster == Target && _owner.Id == 637 && SecretRewardRules.IsDot(status) ? 1 : 0;
            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => _owner.Working && unit == Target && _owner.Id == 735 && unit.HpCurr == unit.HpMax ? .8f : 1;
            public override bool TryPreventDeath(Unit unit, Unit attacker)
            {
                if (_owner.Id != 638 || !_owner.IsActive || _owner._rescuing || unit != _owner.Caster || !unit.IsOnField) return false;
                var instance = unit.ActiveRewardInstance(638);
                if (instance == null) return false;
                _owner._rescuing = true;
                try
                {
                    bool preserved = Codes.Passive.AstolfoTravelKit.TryPreserveBattleConsumable();
                    if (!preserved && !unit.RemoveRewardCodeInstance(instance.instanceId)) return false;
                    // 실제 사망·처치·축전 이벤트를 만들지 않는다. 부활은 치유 연계의 연료가 아니다.
                    return unit.RestoreHpAfterPreventedDeath(.30f);
                }
                finally { _owner._rescuing = false; }
            }
        }
    }

    /// <summary>적별 공유 중립 표식. 동일 오라/중첩 생성자는 최고 하나만 적용한다.</summary>
    public static class SecretRewardRules
    {
        public const int StackLimit = 50;
        public const string MarkKey = "reward_shared_secret";
        private static readonly HashSet<SecretSupportRewardCode> Providers = new();
        private static readonly HashSet<Unit> Marked = new();
        public static bool IsProvider(int id) => id is 732 or 733 or 738 or 824 or 825 or 910 or 651;
        public static bool IsDot(UnitStatus status) => status?.Effects?.Any(e => e.EffectObject?.IsDamageOverTime == true) == true;
        private static bool Has(int id, bool enemySide) => Providers.Any(p => p.Id == id && p.Working && p.Caster.IsEnemy == enemySide);
        public static int Stacks(Unit target) => target?.GetAllStatuses().Where(s => s.Key == MarkKey)
            .SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<SecretMark>().Select(e => e.Count).FirstOrDefault() ?? 0;
        internal static void Register(SecretSupportRewardCode code)
        {
            if (!Providers.Add(code)) return;
            if (Providers.Count == 1)
            {
                Unit.AnyNegativeStatusGranted += Applied;
                Unit.AnyDamageDealt += Damaged;
                Unit.AnyUnitDied += Died;
            }
            Refresh();
        }
        internal static void Unregister(SecretSupportRewardCode code)
        {
            Providers.Remove(code);
            if (Providers.Count == 0)
            {
                Unit.AnyNegativeStatusGranted -= Applied; Unit.AnyDamageDealt -= Damaged; Unit.AnyUnitDied -= Died;
                foreach (Unit target in Marked.ToList()) if (target != null) target.RemoveStatusByKey(MarkKey);
                Marked.Clear();
            }
            else Refresh();
        }
        private static void Refresh()
        { foreach (Unit target in Marked.ToList()) if (target != null && target.IsOnField) target.RefreshAttributes(); }
        private static void Died(Unit target, Unit attacker) { Marked.Remove(target); Refresh(); }
        private static void Applied(Unit source, Unit target, UnitStatus status)
        {
            if (source == null || target == null || source.IsEnemy == target.IsEnemy || !IsDot(status)) return;
            int amount = Has(824, source.IsEnemy) ? 3 : Has(732, source.IsEnemy) ? 1 : 0;
            if (amount > 0) Gain(target, amount);
        }
        private static void Damaged(DamageResolvedContext hit)
        {
            if (hit?.DamageContext == null || hit.Attacker == null || hit.Target == null || hit.DamageDealt <= 0 ||
                !hit.DamageContext.IsDamageOverTime || hit.DamageContext.IsSharedDamage || hit.Attacker.IsEnemy == hit.Target.IsEnemy) return;
            if (Has(825, hit.Attacker.IsEnemy)) Gain(hit.Target, 1);
        }
        private static void Gain(Unit target, int amount)
        {
            if (!target.IsOnField || target.HpCurr <= 0) return;
            int old = Stacks(target), count = Mathf.Min(StackLimit, old + amount);
            if (count == old) return;
            var status = BuffStatus.Create(98910, MarkKey, "비밀", null, target, new SecretMark(count),
                category: StatusCategory.Neutral, description: $"비밀 {count}/{StackLimit}. 적별 공유 표식. 전투 종료 시 소멸. 정화·강화 해제 불가.");
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            target.AddStatus(status); Marked.Add(target);
        }
        // 공통 피해 계산에 한 번만 곱한다. 추후 증원/교체된 적도 별도 상태 설치 없이 오라를 받는다.
        public static float DamageMultiplier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker == null || target == null || Providers.Count == 0) return 1;
            float result = 1;
            if (Codes.Passive.YamaDeathVerdict.IsShocked(attacker) && Has(651, !attacker.IsEnemy)) result *= .85f;
            if (Has(738, !attacker.IsEnemy) && attacker.GetAllStatuses().Any(s => s.CountsAsDebuff && IsDot(s))) result *= .80f;
            if (context?.IsDamageOverTime == true && attacker.IsEnemy != target.IsEnemy && Has(910, attacker.IsEnemy))
                result *= 1 + Stacks(target) * .02f;
            return result;
        }
        private sealed class SecretMark : BaseEffect
        {
            public readonly int Count;
            public SecretMark(int count) : base(0, count) => Count = count;
            public override float StatPercentModifier(Unit unit, StatType stat)
                => unit == Target && stat == StatType.Attack && Has(733, !unit.IsEnemy) ? -Count * .006f : 0;
        }
    }
}
