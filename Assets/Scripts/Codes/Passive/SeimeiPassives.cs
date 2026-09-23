using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>세이메이(43)의 봉인·치유 코드와 상태 ID.</summary>
    public static class SeimeiIds
    {
        public const int UnitId = 43;

        public const int TripleSeal = 243;
        public const int SealFormation = 146;
        public const int EmergencyTreatment = 147;
        public const int EmergencyRoom = 148;
        public const int ExposedGap = 116;

        public const int InnateStatus = 9810;
        public const int SealStatus = 9811;
        public const int SealFormationStatus = 9812;
        public const int EmergencyTreatmentStatus = 9813;
        public const int EmergencyRoomStatus = 9814;
        public const int ExposedGapStatus = 9815;

        public const string SealKey = "seimei_seal_talisman";
        public const int SealTriggerStacks = 3;

        /// <summary>봉인부를 한 장 붙이고 3중첩이면 전부 거둔 뒤 속박으로 바꾼다.</summary>
        public static bool ApplySeal(Unit seimei, Unit target)
        {
            if (seimei == null || target == null || !target.isActive || target.IsEnemy == seimei.IsEnemy)
                return false;

            int stacks = target.GetAllStatuses().Count(status => status.Key == SealKey);
            target.AddStatus(BuffStatus.Create(
                SealStatus, SealKey, "봉인부", seimei, target,
                new SealMarkerEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                category: BaseEnums.StatusCategory.Negative,
                description: $"3중첩이 되면 속박됩니다. ({Mathf.Min(SealTriggerStacks, stacks + 1)}/{SealTriggerStacks})"));

            stacks = target.GetAllStatuses().Count(status => status.Key == SealKey);
            if (stacks < SealTriggerStacks) return true;

            target.RemoveStatusByKey(SealKey);
            if (ControlStatuses.ApplyBind(target, seimei)) OnControlLanded(seimei, target);
            return true;
        }

        /// <summary>새 행동불능을 건 뒤 드러난 틈(116)을 적용한다.</summary>
        public static void OnControlLanded(Unit seimei, Unit target)
        {
            if (seimei == null || target == null || !target.isActive) return;
            if (!seimei.HasLearnedPassiveCode(ExposedGap)) return;

            target.AddStatus(BuffStatus.Create(
                ExposedGapStatus, "seimei_exposed_gap", "드러난 틈", seimei, target,
                new ReceivingDamageMultiplierEffect(1.1f),
                duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "받는 피해가 10% 증가합니다."));
        }
    }

    internal sealed class SealMarkerEffect : BaseEffect
    {
        public SealMarkerEffect() : base(0) => Category = BaseEnums.EffectCategory.Negative;
    }

    /// <summary>세이메이 P — 봉인부 3장을 속박으로 바꾸는 고유 패시브.</summary>
    public sealed class SeimeiTripleSeal : PersistentStatusPassive
    {
        public SeimeiTripleSeal(PassiveCodeContext context)
            : base(context, SeimeiIds.InnateStatus, "seimei_triple_seal", "삼중봉인",
                "봉인부가 한 대상에게 3중첩되면 모두 소비하고 다음 턴 전까지 속박합니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new SealMarkerEffect();
    }

    /// <summary>봉인진(146) — 행동불능인 적에게 아군이 가하는 피해 +10%.</summary>
    public sealed class SeimeiSealFormation : PersistentStatusPassive
    {
        public SeimeiSealFormation(PassiveCodeContext context)
            : base(context, SeimeiIds.SealFormationStatus, "seimei_seal_formation", "봉인진",
                "필드에 있는 동안 모든 아군이 행동불능인 적에게 가하는 피해가 10% 증가합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new SealFormationEffect();
    }

    internal sealed class SealFormationEffect : BaseEffect
    {
        public SealFormationEffect() : base(0, 0.1f) { }
        public override bool CountsAsReagentBuff => false;
        public override bool IsBeneficial => true;

        public override float AlliedConditionalDamageBonusAdditive(Unit owner, Unit attacker, Unit target)
            => owner == Target && owner.isActive && owner.IsOnField &&
               attacker != null && attacker.IsEnemy == owner.IsEnemy &&
               target != null && target.isControlled ? 0.1f : 0f;
    }

    /// <summary>응급치료(147) — 유효 치유마다 세이메이 CON만큼 보호막을 덧씌운다.</summary>
    public sealed class SeimeiEmergencyTreatment : PersistentStatusPassive
    {
        public SeimeiEmergencyTreatment(PassiveCodeContext context)
            : base(context, SeimeiIds.EmergencyTreatmentStatus, "seimei_emergency_treatment", "응급치료",
                "아군을 실제로 치유하면 자신의 CON만큼 보호막을 추가로 부여합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new EmergencyTreatmentEffect();
    }

    internal sealed class EmergencyTreatmentEffect : BaseEffect
    {
        public EmergencyTreatmentEffect() : base(0) { }
        public override bool IsBeneficial => true;

        public override void OnEffectiveHealingGranted(
            Unit source, Unit target, int effectiveHealing, int hpBeforeHealing)
        {
            if (source != Target || target == null || effectiveHealing <= 0 || !target.isActive) return;

            float multiplier = source.HasLearnedPassiveCode(SeimeiIds.EmergencyRoom) &&
                               hpBeforeHealing <= Mathf.FloorToInt(target.HpMax * 0.3f)
                ? 1.5f
                : 1f;
            int shield = Mathf.Max(1, Mathf.RoundToInt(source.GetBaseCon() * multiplier));
            target.AddShield(shield, source);
        }
    }

    /// <summary>응급실(148) — 체력 30% 이하 대상에 대한 치유량 +50%.</summary>
    public sealed class SeimeiEmergencyRoom : PersistentStatusPassive
    {
        public SeimeiEmergencyRoom(PassiveCodeContext context)
            : base(context, SeimeiIds.EmergencyRoomStatus, "seimei_emergency_room", "응급실",
                "체력이 30% 이하인 대상을 치유할 때 치유량과 응급치료 보호막이 50% 증가합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new EmergencyRoomEffect();
    }

    internal sealed class EmergencyRoomEffect : BaseEffect
    {
        public EmergencyRoomEffect() : base(0, 1.5f) { }
        public override bool IsBeneficial => true;

        public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            => source == Target && target != null && target.HpCurr <= Mathf.FloorToInt(target.HpMax * 0.3f)
                ? 1.5f
                : 1f;
    }

    /// <summary>드러난 틈(116) — 새 행동불능 대상이 받는 피해 +10%.</summary>
    public sealed class SeimeiExposedGap : PassiveCode
    {
        public SeimeiExposedGap(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "드러난 틈";
            IgnoresActivationChance = true;
        }

        public override void CastCode() { }
    }
}
