using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive.Reward
{
    public sealed class FeintCode : RewardPassiveCode
    {
        public FeintCode(PassiveCodeContext context) : base(context, 633) { }
        protected override void OnCast() => AddStatus(Caster, new ToughnessEfficiencyEffect(.25f));
    }

    public sealed class EchoSongCode : RewardPassiveCode
    {
        public EchoSongCode(PassiveCodeContext context) : base(context, 731) { }
        protected override void OnCast() => Toughness.Broken += OnBreak;
        protected override void OnStop() => Toughness.Broken -= OnBreak;
        private void OnBreak(CombatDefeatContext context)
        {
            Unit target = context.Grantor;
            if (!HolderWorking || target == null || !target.isActive || target.HpCurr <= 0 || target.IsEnemy == Caster.IsEnemy) return;
            // 격파 보상 피해는 다시 강인도를 깎거나 지속피해 버프를 받지 않는다.
            target.TakeDamage(new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.SpecialAttack) * .8f)),
                BaseEnums.CodeType.Passive, new() { DamageTag.TrueDamage, DamageTag.BonusDamage }));
        }
    }

    public sealed class DefeatMomentumCode : RewardPassiveCode
    {
        private readonly BaseEnums.StatType _stat;
        private int _stacks;
        public DefeatMomentumCode(PassiveCodeContext context, int id) : base(context, id)
            => _stat = id == 717 ? BaseEnums.StatType.Attack : BaseEnums.StatType.Speed;
        protected override void OnCast() { _stacks = 0; Toughness.EnemyDefeated += OnDefeat; }
        protected override void OnStop() { Toughness.EnemyDefeated -= OnDefeat; _stacks = 0; }
        private void OnDefeat(CombatDefeatContext context)
        {
            if (!HolderWorking || context.Grantee != Caster || _stacks >= 4) return;
            _stacks++;
            AddStatus(Caster, new StatModifierEffect(_stat, _stacks * (_stat == BaseEnums.StatType.Attack ? .10f : .05f), percent: true));
            Caster.RefreshAttributes();
        }
    }

    public sealed class DivineOfferingCode : RewardPassiveCode
    {
        public DivineOfferingCode(PassiveCodeContext context) : base(context, 823) { }
        protected override void OnCast() => Toughness.EnemyDefeated += OnDefeat;
        protected override void OnStop() => Toughness.EnemyDefeated -= OnDefeat;
        private void OnDefeat(CombatDefeatContext context)
        {
            if (!HolderWorking || context.Grantor == null || context.Grantor.IsEnemy == Caster.IsEnemy || !context.Grantor.IsOnField) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * .04f));
            foreach (Unit ally in FieldAllies()) ally.Heal(amount, Caster);
        }
    }
}
