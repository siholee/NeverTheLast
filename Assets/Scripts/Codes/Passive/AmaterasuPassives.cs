using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    internal static class AmaterasuStatusIds
    {
        public const int SunRhythm = 5700;
        public const int Concealment = 5701;
        public const int Fireworks = 5702;
        public const int Breakthrough = 5703;
    }

    /// <summary>기본공격마다 6초간 DEX +4, 최대 6중첩.</summary>
    public sealed class AmaterasuSunRhythm : UniquePassiveCode
    {
        private Action<EventContext> _hitHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public AmaterasuSunRhythm(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "태양의 박자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _hitHandler = _ => AddStack();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void AddStack()
        {
            if (Caster.GetAllStatuses(AmaterasuStatusIds.SunRhythm).Count >= 6) return;
            Caster.AddStatus(BuffStatus.Create(
                AmaterasuStatusIds.SunRhythm, "amaterasu_sun_rhythm", CodeName,
                Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, 4),
                duration: 3,   // 6초 → 3턴
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "DEX +4 (최대 6중첩)."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey("amaterasu_sun_rhythm");
            _registered = false;
        }
    }

    public sealed class AmaterasuConcealment : PassiveCode
    {
        public AmaterasuConcealment(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "투명";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AmaterasuStatusIds.Concealment, "amaterasu_concealment", CodeName,
            Caster, Caster, new LowHealthPriorityEffect(0.35f, -1),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "체력이 35% 이하일 때 대상 지정 우선도가 1 감소합니다."));
    }

    public sealed class AmaterasuFireworks : PassiveCode
    {
        private Action<EventContext> _hitHandler;
        private Action<EventContext> _cleanupHandler;
        private int _attacks;
        private bool _registered;

        public AmaterasuFireworks(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "폭죽놀이";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _attacks = 0;
            _hitHandler = OnNormalHit;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnNormalHit(EventContext context)
        {
            if (++_attacks % 4 != 0) return;
            List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                .FindAll(unit => unit != null && unit.isActive && !unit.IsUntargetable);
            for (int i = 0; i < 4 && enemies.Count > 0; i++)
            {
                Unit target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
                bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(20, BaseEnums.PrimaryStat.DEX) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Passive,
                    new List<int> { DamageTag.SingleTarget, DamageTag.Physical, DamageTag.NonContactAttack },
                    isCrit));
            }
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
            _attacks = 0;
        }
    }

    public sealed class AmaterasuBreakthrough : PassiveCode
    {
        private readonly HashSet<Unit> _contributedTargets = new();
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public AmaterasuBreakthrough(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "파죽지세";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _damageHandler = context =>
            {
                if (context?.Target != null && context.DamageDealt > 0) _contributedTargets.Add(context.Target);
            };
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Unit.AnyUnitDied += OnAnyUnitDied;
            _registered = true;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (dead == null || !_contributedTargets.Remove(dead) || Caster == null || !Caster.isActive) return;
            Caster.AddStatus(BuffStatus.Create(
                AmaterasuStatusIds.Breakthrough, "amaterasu_breakthrough", CodeName,
                // 주스탯이 DEX가 아닌 보유자(로키 등)도 온전히 쓸 수 있도록 주스탯으로 일반화했다.
                Caster, Caster, new PrimaryStatBonusBuffEffect(Caster.MainPrimaryStat, 12),
                duration: 2,   // 4초 → 2턴
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "DEX +12."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Unit.AnyUnitDied -= OnAnyUnitDied;
            _contributedTargets.Clear();
            _registered = false;
        }
    }

    internal sealed class LowHealthPriorityEffect : BaseEffect
    {
        private readonly float _threshold;
        private readonly int _modifier;
        public LowHealthPriorityEffect(float threshold, int modifier) : base(0, modifier)
        {
            _threshold = threshold;
            _modifier = modifier;
        }

        public override int TargetPriorityAdditiveModifier(Unit unit)
            => unit == Target && unit.HpMax > 0 && unit.HpCurr <= unit.HpMax * _threshold ? _modifier : 0;
    }
}
