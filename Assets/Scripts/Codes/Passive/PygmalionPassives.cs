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
        public const int RoseThorns = 5804;
    }

    public static class PygmalionCombat
    {
        public static void ApplyBurn(Unit caster, Unit target)
        {
            if (caster == null || target == null || !target.isActive) return;
            ElementalReaction.TryApplyBurn(caster, target);
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
            Caster, Caster, new DamageOverTimeApplicationEffect(1.25f),
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
            description: "접촉 피해를 받으면 공격자에게 2턴간 치유량 50% 감소를 부여합니다."));
    }

    /// <summary>
    /// 사랑 신의 가호(54) — 행운아(16)의 금색 상위. 서포트 카드로 어느 훈련에 앉든 그 훈련의 LUK 몫 +20%.
    /// 예전의 접촉 피해 −25%를 대체했다. 피그말리온이 초기 서포터가 되면서 LUK 훈련 요원 몫을 맡는다.
    /// </summary>
    public sealed class PygmalionLoveGodBlessing : PassiveCode
    {
        public const float TrainingBonus = 0.20f;

        public PygmalionLoveGodBlessing(PassiveCodeContext context) : base(context)
        {
            CodeName = "사랑 신의 가호";
            IgnoresActivationChance = true;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override float SupportTrainingBonus(BaseEnums.PrimaryStat stat)
            => stat == BaseEnums.PrimaryStat.LUK ? TrainingBonus : 0f;
    }

    /// <summary>궁극기 장미의 가시: 피해 감소·도발·접촉 반격 화상.</summary>
    internal sealed class RoseThornsEffect : BaseEffect
    {
        private const int BurnCooldownTurns = 1;
        private Action<EventContext> _damageHandler;
        private readonly Combat.TargetTurnCooldown _burnCooldown = new(BurnCooldownTurns);

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
            if (!_burnCooldown.TryUse(Target, attacker)) return;

            PygmalionCombat.ApplyBurn(Target, attacker);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnTakingDamage, _damageHandler);
            _burnCooldown.Clear();
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
            HealingReductionStatus.Apply(context.DmgCtx.Attacker, Target, 2, "날카로운 가시");
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnTakingDamage, _damageHandler);
        }
    }

}
