using System.Linq;
using BaseClasses;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Codes.Passive.Reward
{
    /// <summary>중립 표식·중첩 수가 아닌 현재 해로운 상태의 개수를 읽는다.</summary>
    public sealed class DebuffRewardCode : RewardPassiveCode
    {
        public static readonly int[] Ids = { 667, 668, 769, 770, 771, 840, 841, 918 };
        private readonly TurnCooldown _shieldCooldown = new(1);
        private bool Working => HolderWorking && !Caster.IsDying && Caster.HpCurr > 0;
        public DebuffRewardCode(PassiveCodeContext context, int id) : base(context, id) { }
        public override int BaseShieldAmount => Caster != null && (RewardId is 769 or 841)
            ? StatDamage(0, StatType.Defense, RewardId == 841 ? .8f : 1f) : 0;
        public static int Debuffs(Unit unit) => unit?.GetAllStatuses().Count(s => s.CountsAsDebuff) ?? 0;
        private static bool Direct(DamageContext context) => context != null && !context.IsDamageOverTime &&
            !context.IsSharedDamage && context.CodeType != CodeType.Effect &&
            !context.HasTag(DamageTag.BonusDamage) && !context.HasTag(DamageTag.ElementalReaction);

        protected override void OnCast()
        {
            _shieldCooldown.Reset();
            AddStatus(Caster, new HolderEffect(this), category: StatusCategory.Neutral);
            Unit.AnyNegativeStatusGranted += Granted;
        }
        protected override void OnStop() => Unit.AnyNegativeStatusGranted -= Granted;
        private void Granted(Unit source, Unit target, UnitStatus status)
        {
            if (!Working || source != Caster || target == null || target.IsEnemy == Caster.IsEnemy ||
                !target.isActive || target.IsDying || !target.IsOnField || status?.CountsAsDebuff != true) return;
            if (RewardId == 771)
                AddStatus(Caster, new StatModifierEffect(StatType.SpecialAttack, .20f, percent: true), "_insight", duration: 2);
            if (RewardId is not (769 or 841) || !_shieldCooldown.TryUse(Caster)) return;
            int amount = BaseShieldAmount;
            if (RewardId == 769) Caster.AddShield(amount, Caster);
            else ShieldCapability.GrantParty(Caster, amount);
        }
        private sealed class HolderEffect : BaseEffect
        {
            private readonly DebuffRewardCode _owner;
            public HolderEffect(DebuffRewardCode owner) : base(0) => _owner = owner;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (!_owner.Working || attacker != Target || !Direct(context)) return 1;
                int count = Mathf.Min(6, Debuffs(target));
                if (count == 0) return 1;
                return _owner.RewardId switch
                {
                    667 => 1.18f,
                    840 => 1.18f + .04f * count,
                    918 => 1.18f + .07f * count,
                    _ => 1
                };
            }
            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (!_owner.Working || attacker != Target || !Direct(context)) return 1;
                int count = Mathf.Min(6, Debuffs(target));
                return _owner.RewardId switch { 840 => 1 - .02f * count, 918 => 1 - .04f * count, _ => 1 };
            }
            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => _owner.Working && unit == Target && _owner.RewardId == 770 && context != null && !context.IsSharedDamage
                    ? 1 - .04f * Mathf.Min(5, Debuffs(context.Attacker)) : 1;
            public override int OutgoingStatusDurationModifier(Unit caster, UnitStatus status)
                => _owner.Working && caster == Target && _owner.RewardId == 668 && status?.CountsAsDebuff == true &&
                    status.Duration > 0 && !status.IsControl && status.Owner != null && status.Owner.IsEnemy != caster.IsEnemy &&
                    status.Effects.All(e => e.EffectObject?.IsDamageOverTime != true) ? 1 : 0;
        }
    }
}
