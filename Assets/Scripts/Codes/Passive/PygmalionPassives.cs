using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    internal static class PygmalionStatusIds
    {
        public const int WeaklingContempt = 5800;
        public const int SlowAndSteady = 5801;
        public const int SharpThorns = 5802;
        public const int LoveGodBlessing = 5803;
        public const int RoseThorns = 5804;
        public const int Burn = 5805;
    }

    public static class PygmalionCombat
    {
        public const string BurnStatusKey = "pygmalion_burn";

        public static void ApplyBurn(Unit caster, Unit target)
        {
            if (caster == null || target == null || !target.isActive) return;
            target.AddStatus(BuffStatus.Create(
                PygmalionStatusIds.Burn, BurnStatusKey, "화상",
                caster, target, new PygmalionBurnEffect(),
                duration: 3f,
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "3초간 매초 40 + 시전자 CON의 5%만큼 피해를 입습니다. 중첩되어도 지속피해 종류는 1개로 계산합니다."));
        }

        public static bool IsContactDamage(DamageContext context)
            => context?.DamageTags?.Contains(DamageTag.ContactAttack) == true;
    }

    public sealed class PygmalionWeaklingContempt : PassiveCode
    {
        public PygmalionWeaklingContempt(PassiveCodeContext context) : base(context)
        {
            CodeName = "약자 멸시";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            PygmalionStatusIds.WeaklingContempt, "pygmalion_weakling_contempt", CodeName,
            Caster, Caster, new WeaklingContemptEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "공격자의 지속피해 종류 하나당 받는 피해가 1% 감소합니다(최대 20%)."));
    }

    public sealed class PygmalionSlowAndSteady : PassiveCode
    {
        public PygmalionSlowAndSteady(PassiveCodeContext context) : base(context)
        {
            CodeName = "천천히 꾸준히";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            PygmalionStatusIds.SlowAndSteady, "pygmalion_slow_and_steady", CodeName,
            Caster, Caster, new PygmalionDotApplicationEffect(1.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "자신이 부여하는 지속피해가 25% 증가합니다."));
    }

    public sealed class PygmalionSharpThorns : PassiveCode
    {
        public PygmalionSharpThorns(PassiveCodeContext context) : base(context)
        {
            CodeName = "날카로운 가시";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            PygmalionStatusIds.SharpThorns, "pygmalion_sharp_thorns", CodeName,
            Caster, Caster, new SharpThornsEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "접촉 피해를 받으면 공격자에게 3초간 치유량 50% 감소를 부여합니다."));
    }

    public sealed class PygmalionLoveGodBlessing : PassiveCode
    {
        public PygmalionLoveGodBlessing(PassiveCodeContext context) : base(context)
        {
            CodeName = "사랑신의 가호";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            PygmalionStatusIds.LoveGodBlessing, "pygmalion_love_god_blessing", CodeName,
            Caster, Caster, new ContactDamageReductionEffect(0.75f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "접촉 분류 기술에게 받는 피해가 25% 감소합니다."));
    }

    /// <summary>궁극기 장미의 가시: 피해 감소·도발·접촉 반격 화상.</summary>
    internal sealed class RoseThornsEffect : BaseEffect
    {
        private const float BurnInternalCooldown = 1f;
        private Action<EventContext> _damageHandler;
        private readonly Dictionary<Unit, float> _nextBurnTimeByAttacker = new();

        public RoseThornsEffect() : base(0) { }

        public override void OnApply()
        {
            _damageHandler = OnTakingDamage;
            Target?.AddListener(BaseEnums.UnitEventType.OnTakingDamage, _damageHandler);
        }

        private void OnTakingDamage(EventContext context)
        {
            if (!PygmalionCombat.IsContactDamage(context?.DmgCtx)) return;
            Unit attacker = context.DmgCtx.Attacker;
            if (attacker == null) return;
            if (_nextBurnTimeByAttacker.TryGetValue(attacker, out float nextTime) && Time.time < nextTime) return;

            _nextBurnTimeByAttacker[attacker] = Time.time + BurnInternalCooldown;
            PygmalionCombat.ApplyBurn(Target, attacker);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnTakingDamage, _damageHandler);
            _nextBurnTimeByAttacker.Clear();
        }

        public override float ReceivingDamageModifier(Unit unit) => unit == Target ? 0.5f : 1f;
        public override int TargetPriorityAdditiveModifier(Unit unit) => unit == Target ? 1 : 0;
    }

    internal sealed class WeaklingContemptEffect : BaseEffect
    {
        public WeaklingContemptEffect() : base(0) { }

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.Attacker == null) return 1f;
            int dotCount = Mathf.Min(20, context.Attacker.GetDamageOverTimeStatusCount());
            return 1f - dotCount * 0.01f;
        }
    }

    internal sealed class PygmalionDotApplicationEffect : BaseEffect
    {
        private readonly float _multiplier;
        public PygmalionDotApplicationEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float DamageOverTimeApplicationMultiplier(Unit unit) => unit == Target ? _multiplier : 1f;
    }

    internal sealed class SharpThornsEffect : BaseEffect
    {
        private Action<EventContext> _damageHandler;

        public SharpThornsEffect() : base(0) { }

        public override void OnApply()
        {
            _damageHandler = OnTakingDamage;
            Target?.AddListener(BaseEnums.UnitEventType.OnTakingDamage, _damageHandler);
        }

        private void OnTakingDamage(EventContext context)
        {
            if (!PygmalionCombat.IsContactDamage(context?.DmgCtx)) return;
            HealingReductionStatus.Apply(context.DmgCtx.Attacker, Target, 3f, "날카로운 가시");
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnTakingDamage, _damageHandler);
        }
    }

    internal sealed class ContactDamageReductionEffect : BaseEffect
    {
        private readonly float _multiplier;
        public ContactDamageReductionEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target && PygmalionCombat.IsContactDamage(context) ? _multiplier : 1f;
    }

    internal sealed class PygmalionBurnEffect : BaseEffect
    {
        private float _elapsed;
        public override bool IsDamageOverTime => true;

        public PygmalionBurnEffect() : base(0)
        {
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnApply() => _elapsed = 0f;

        public override void OnUpdate(float deltaTime)
        {
            float previous = _elapsed;
            _elapsed += deltaTime;
            int ticks = Mathf.FloorToInt(_elapsed) - Mathf.FloorToInt(previous);
            for (int i = 0; i < ticks; i++) DealTick();
        }

        public override int EstimateDamagePerSecond() => CalculateDamage();

        private int CalculateDamage()
        {
            if (Caster == null) return 0;
            float multiplier = Caster.GetDamageOverTimeApplicationMultiplier();
            return Mathf.Max(0, Mathf.RoundToInt((40f + Caster.GetBaseCon() * 0.05f) * multiplier));
        }

        private void DealTick()
        {
            if (Caster == null || Target == null || !Target.isActive || Target.HpCurr <= 0) return;
            Target.TakeDamage(new DamageContext(
                Caster, CalculateDamage(), BaseEnums.CodeType.Effect,
                new List<int> { DamageTag.SingleTarget, DamageTag.NonContactAttack }));
        }
    }
}
