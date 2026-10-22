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
    /// <summary>소환·발상 보상. 발상은 정화되지 않는 전투 중립 표식이며 런 저장 자원이 아니다.</summary>
    public sealed class SummonIdeaRewardCode : RewardPassiveCode, IRewardAuraSource
    {
        public static readonly int[] Ids = { 652, 653, 654, 655, 748, 749, 750, 751, 752, 753, 826, 827, 828, 829, 830, 831, 911, 912 };
        public const string IdeaKey = "reward_idea";
        private readonly TurnCooldown _cleanse = new(2);
        private readonly HashSet<string> _species = new();
        private readonly HashSet<DamageContext> _scorched = new();
        private readonly HashSet<Unit> _observed = new();
        private readonly Dictionary<Unit, DamageContext> _fatal = new();
        private int Id => RewardId;
        private bool Working => HolderWorking && !Caster.IsDying && Caster.HpCurr > 0;
        public string AuraFamily => "summon_protection";
        public float AuraValue => Id == 655 && Working ? .30f : 0;
        public SummonIdeaRewardCode(PassiveCodeContext context, int id) : base(context, id) { }
        public static int Ideas(Unit unit) => unit?.CombatEffects().OfType<IdeaMark>().Select(e => e.Count).FirstOrDefault() ?? 0;
        public static void GainIdea(Unit unit, int amount)
        {
            if (unit == null || !unit.IsOnField || unit.IsDying || unit.HpCurr <= 0 || amount <= 0) return;
            int count = (int)System.Math.Min(int.MaxValue, (long)Ideas(unit) + amount);
            var status = BuffStatus.Create(98911, IdeaKey, "발상", null, unit, new IdeaMark(count),
                category: StatusCategory.Neutral, description: $"발상 {count}. 전투 종료 시 소멸. 정화·강화 해제 불가.");
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            unit.AddStatus(status);
        }
        private static bool Live(Unit unit) => unit != null && unit.isActive && unit.IsOnField && !unit.IsDying && unit.HpCurr > 0;
        private int OwnCount => Summons.Of(Caster).Count(Live);
        private List<Unit> AlliesWithSummons() => SummonSupportCombat.Allies(Caster, true).Where(Live).ToList();
        private int FieldCount => AlliesWithSummons().Count(u => u.IsSummon || u.CountsAsSummon);
        private bool AloneFront => Caster.currentCell != null &&
            Caster.currentCell.xPos == GridManager.Instance.GetFrontColumn(Caster.IsEnemy) &&
            FieldAllies().Count(u => Live(u) && u.currentCell != null && u.currentCell.xPos == Caster.currentCell.xPos) == 1;

        protected override void OnCast()
        {
            _cleanse.Reset(); _species.Clear(); _scorched.Clear(); _fatal.Clear(); _observed.Clear();
            AddStatus(Caster, new HolderEffect(this), category: StatusCategory.Neutral);
            Summons.AnySummonCreated += Created;
            Unit.AnyUnitDied += Died;
            Unit.AnyUltimateCast += Ultimate;
            Unit.AnyHealed += Healed;
            Unit.AnyDamageDealt += Damaged;
            Unit.AnyNegativeStatusGranted += Debuffed;
            ActionScheduler.AnyActionFinished += Finished;
            // 고유 특성이 먼저 만드는 개전 소환도 같은 전투의 생성 1회로 센다.
            foreach (Unit summon in AlliesWithSummons().Where(u => u.IsSummon).ToList()) Observe(summon.SummonOwner, summon, true);
            BindProtection();
        }
        protected override void OnStop()
        {
            Summons.AnySummonCreated -= Created; Unit.AnyUnitDied -= Died;
            Unit.AnyUltimateCast -= Ultimate; Unit.AnyHealed -= Healed; Unit.AnyDamageDealt -= Damaged;
            Unit.AnyNegativeStatusGranted -= Debuffed; ActionScheduler.AnyActionFinished -= Finished;
            _fatal.Clear(); _species.Clear(); _observed.Clear(); _scorched.Clear(); _cleanse.Reset();
        }
        private void Created(Unit owner, Unit summon, SummonSource source, object tag)
        {
            if (!Working || owner == null || owner.IsEnemy != Caster.IsEnemy) return;
            Observe(owner, summon); BindProtection(); RefreshSummons(); Caster.RefreshAttributes();
        }
        private void Observe(Unit owner, Unit summon, bool initial = false)
        {
            if (!Working || !Live(summon) || !_observed.Add(summon)) return;
            if (Id == 751) GainIdea(Caster, 1);
            if (owner != Caster) return;
            if (Id == 830 && _species.Add(summon.SummonKind ?? summon.ID.ToString()))
                foreach (Unit ally in FieldAllies()) GainIdea(ally, 2);
            if (Id == 831 && !initial && AlliesWithSummons().Any(u => u.isControlled || u.GetAllStatuses().Any(s => s.CountsAsDebuff)) && _cleanse.TryUse(Caster))
                foreach (Unit ally in AlliesWithSummons()) ally.RemoveAllNegativeStatuses(Caster);
        }
        private void BindProtection()
        {
            if (Id != 655) return;
            // 아직 자기 개전 특성으로 소환수 판정을 얻지 않은 아군도 설치한다. 실제 보정은 질의 때 가린다.
            foreach (Unit unit in AlliesWithSummons())
                AddStatus(unit, new ProtectionEffect(), key: RewardAura.KeyOf(AuraFamily), category: StatusCategory.Neutral);
        }
        private void RefreshSummons()
        {
            if (Id != 655) return;
            foreach (Unit unit in AlliesWithSummons()) unit.RefreshAttributes();
        }
        private void Died(Unit dead, Unit attacker)
        {
            if (Working && dead != null)
            {
                if (Id == 829 && dead.IsSummon && dead.SummonOwner == Caster) GainIdea(Caster, 1);
                if (Id == 911 && dead.IsEnemy != Caster.IsEnemy && !dead.IsSummon && attacker == Caster &&
                    _fatal.TryGetValue(dead, out var hit) && hit.HasTag(DamageTag.SummonAttack)) GainIdea(Caster, 3);
            }
            _fatal.Remove(dead);
            RefreshSummons();
            if (Working) Caster.RefreshAttributes();
        }
        private void Damaged(DamageResolvedContext hit)
        {
            if (Id == 911 && Working && hit?.Target != null && hit.Attacker == Caster && hit.Target.HpCurr <= 0 &&
                !hit.DamageContext.IsSharedDamage && hit.DamageContext.HasTag(DamageTag.SummonAttack)) _fatal[hit.Target] = hit.DamageContext;
        }
        private void Ultimate(Unit actor)
        {
            if (Id != 827 || !Working || actor != Caster) return;
            int count = OwnCount;
            foreach (Unit ally in FieldAllies()) GainIdea(ally, count);
        }
        private void Healed(Unit target, Unit source, int effective, int overflow)
        {
            if (Id != 653 || !Working || source != Caster || !Live(target) || target.IsEnemy != Caster.IsEnemy || overflow <= 0) return;
            target.AddShield(Mathf.Max(1, Mathf.RoundToInt(overflow * .30f)), Caster);
        }
        private void Debuffed(Unit source, Unit target, Entities.Status.UnitStatus status)
        {
            if (Id != 750 || !Working || source != Caster || target == null || target.IsEnemy == Caster.IsEnemy) return;
            var hit = Caster.ReceivingDamageContext;
            if (hit == null || hit.Attacker != target || hit.ResolvedDamage <= 0 || !DirectAttack.IsDirect(hit) || hit.IsSharedDamage || !_scorched.Add(hit)) return;
            target.TakeDamage(new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(Caster.StatValue(StatType.Defense) * .50f)),
                CodeType.Passive, new List<int> { DamageTag.TrueDamage, DamageTag.BonusDamage, DamageTag.PassiveAttack }));
        }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            _scorched.Clear(); _fatal.Clear();
            if (Working && Id == 748) Caster.RefreshAttributes();
        }
        private sealed class IdeaMark : BaseEffect
        {
            public int Count { get; }
            public IdeaMark(int count) : base(0, count) => Count = count;
        }
        private sealed class ProtectionEffect : BaseEffect
        {
            public ProtectionEffect() : base(0) { }
            public override float StatPercentModifier(Unit unit, StatType stat) => stat == StatType.Defense && (unit.IsSummon || unit.CountsAsSummon)
                ? RewardAura.Best(unit, "summon_protection") : 0;
            // 실제 소환수의 상속 방어는 고정값 효과로 설치된다(서기관 등). 그 몫도 동일하게 보정한다.
            public override float StatFlatModifier(Unit unit, StatType stat) => stat == StatType.Defense && unit.IsSummon
                ? RewardAura.Best(unit, "summon_protection") * unit.CombatEffects().Where(e => e is not ProtectionEffect).Sum(e => e.StatFlatModifier(unit, stat)) : 0;
        }
        private sealed class HolderEffect : BaseEffect
        {
            private readonly SummonIdeaRewardCode _owner;
            public HolderEffect(SummonIdeaRewardCode owner) : base(0) => _owner = owner;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (!_owner.Working || attacker != Target) return 1;
                return _owner.Id switch
                {
                    912 => 1 + Mathf.Min(.60f, _owner.OwnCount * .15f),
                    828 => 1 + Mathf.Min(.60f, Ideas(attacker) * .03f),
                    654 => SummonSupportCombat.IsSummonDamage(context) ? 1.35f : .80f,
                    _ => 1
                };
            }
            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            {
                if (!_owner.Working || unit != Target) return 1;
                return _owner.Id switch
                {
                    826 => 1 - Mathf.Min(.30f, _owner.FieldCount * .04f),
                    748 => _owner.AloneFront ? .85f : 1,
                    752 => 1 - Mathf.Min(.25f, Ideas(unit) * .015f),
                    _ => 1
                };
            }
            public override float StatPercentModifier(Unit unit, StatType stat) => _owner.Working && unit == Target &&
                _owner.Id == 748 && stat == StatType.Defense && _owner.AloneFront ? .30f : 0;
            public override float OutgoingShieldMultiplierModifier(Unit source, Unit target) => _owner.Working && source == Target &&
                _owner.Id == 749 && _owner.OwnCount > 0 ? 1.30f : 1;
            public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            {
                if (!_owner.Working || source != Target) return 1;
                return _owner.Id switch
                {
                    652 => _owner.OwnCount == 1 ? 1.25f : 1,
                    753 => 1 + Mathf.Min(.40f, Ideas(source) * .025f),
                    _ => 1
                };
            }
        }
    }
}
