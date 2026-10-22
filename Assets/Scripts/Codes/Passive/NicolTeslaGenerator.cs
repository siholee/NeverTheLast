using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>번개 부착/반응으로 강도가 아닌 일회성 사용 횟수를 쌓는다.</summary>
    public sealed class NicolTeslaGenerator : UniquePassiveCode
    {
        public const string GuardKey = "nicol_tesla_guard", PowerKey = "nicol_tesla_power";
        public const float GuardMultiplier = .85f, PowerMultiplier = 1.20f;
        private readonly HashSet<Unit> _holders = new();
        private bool _working;
        public NicolTeslaGenerator(PassiveCodeContext context) : base(context) => CodeName = "테슬라 발전기";
        private bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            Unit.AnyCombatElementGranted += Attached;
            Unit.AnyElementalReactionResolved += Reacted;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, Died);
        }
        public override void StopCode()
        {
            _working = false;
            Unit.AnyCombatElementGranted -= Attached;
            Unit.AnyElementalReactionResolved -= Reacted;
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnDeath, Died);
            foreach (Unit unit in _holders.ToList()) if (unit != null)
                foreach (var status in unit.GetAllStatuses().Where(s => s.Caster == Caster && (s.Key == GuardKey || s.Key == PowerKey)).ToList()) unit.RemoveStatus(status);
            _holders.Clear();
        }
        private void Died(EventContext _) => StopCode();
        private bool Hostile(Unit source, Unit target) => Working && source != null && source.IsEnemy == Caster.IsEnemy &&
            target != null && target.IsEnemy != Caster.IsEnemy;
        private void Attached(Unit source, Unit target, BaseEnums.UnitElement element)
        { if (element == BaseEnums.UnitElement.Electro && Hostile(source, target)) Grant(false); }
        private void Reacted(ElementalReactionContext context)
        { if (context.Contains(BaseEnums.UnitElement.Electro) && Hostile(context.Source, context.Target)) Grant(true); }
        private void Grant(bool power)
        {
            var grid = Managers.GridManager.Instance;
            if (grid == null) return;
            foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Concat(grid.summonList)
                .Where(u => u != null && u.isActive && u.IsOnField && u.IsEnemy == Caster.IsEnemy).Distinct().ToList())
            {
                string key = power ? PowerKey : GuardKey;
                var effect = ally.GetAllStatuses().Where(s => s.Key == key && s.Caster == Caster)
                    .SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<ChargeEffect>().FirstOrDefault();
                if (effect != null) effect.Gain();
                else ally.AddStatus(BuffStatus.Create(power ? 71682 : 71681, key, power ? "테슬라: 증폭" : "테슬라: 보호", Caster, ally,
                    new ChargeEffect(power), duration: 0, isBeneficial: true,
                    description: power ? "가하는 공격 피해20% 증가1회. 중첩은 사용 횟수로 누적하며 실제 타격마다1개 소비." : "받는 공격 피해15% 감소1회. 중첩은 사용 횟수로 누적하며 실제 피격마다1개 소비."));
                _holders.Add(ally);
            }
        }
        public sealed class ChargeEffect : BaseEffect
        {
            private readonly bool _power;
            private bool _active;
            public int Charges { get; private set; } = 1;
            public ChargeEffect(bool power) : base(0) => _power = power;
            public override bool IsBeneficial => true;
            public void Gain() { if (Charges < int.MaxValue) Charges++; }
            private static bool Attack(DamageContext c) => c != null && c.CodeType != BaseEnums.CodeType.Effect &&
                !c.IsSharedDamage && !c.IsDamageOverTime && !c.HasTag(DamageTag.ElementalReaction) && !c.HasTag(DamageTag.BonusDamage);
            public override void OnApply() { _active = true; Unit.AnyDamageDealt += Resolved; }
            public override void OnRemove() { _active = false; Unit.AnyDamageDealt -= Resolved; }
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => _power && Charges > 0 && attacker == Target && target != null && target.IsEnemy != Target.IsEnemy && Attack(context) ? PowerMultiplier : 1f;
            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => !_power && Charges > 0 && unit == Target && context?.Attacker != null && context.Attacker.IsEnemy != Target.IsEnemy && Attack(context) ? GuardMultiplier : 1f;
            private void Resolved(DamageResolvedContext hit)
            {
                if (!_active || Charges <= 0 || hit == null || hit.DamageDealt <= 0 || !Attack(hit.DamageContext) ||
                    hit.Attacker == null || hit.Target == null || hit.Attacker.IsEnemy == hit.Target.IsEnemy ||
                    (_power ? hit.Attacker != Target : hit.Target != Target)) return;
                Charges--;
                if (Charges == 0) Target.RemoveStatusByKey(_power ? PowerKey : GuardKey);
            }
        }
    }
}
