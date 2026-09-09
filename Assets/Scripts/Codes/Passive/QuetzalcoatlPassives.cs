using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class QuetzalcoatlStatusIds
    {
        public const int Bounty = 5270;
        public const int Wisdom = 5271;
        public const int Lucky = 5272;
        public const int Scholar = 5273;
        public const int Meditation = 5274;
        public const int WingedSerpent = 5275;
    }

    /// <summary>필드의 풀 아군 수에 비례해 아군 전체가 가하는 피해를 강화한다.</summary>
    public sealed class QuetzalcoatlBounty : UniquePassiveCode
    {
        private static readonly float[] PerAllyBonuses = { 0.05f, 0.10f, 0.15f };
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public QuetzalcoatlBounty(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "풍요의 축복";
            MaxStage = 3;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            float perAlly = PerAllyBonuses[Mathf.Clamp(CurrentStage, 1, MaxStage) - 1];
            foreach (Unit ally in global::Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                ally.AddStatus(BuffStatus.Create(
                    QuetzalcoatlStatusIds.Bounty,
                    $"quetzalcoatl_bounty_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new QuetzalcoatlBountyEffect(perAlly),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"필드의 풀 아군 1명당 가하는 피해가 {perAlly:P0} 증가합니다."));
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            string key = $"quetzalcoatl_bounty_{Caster.GetEntityId()}";
            foreach (Unit ally in global::Target.GetAllAllies(Caster)) ally?.RemoveStatusByKey(key);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }
    }

    public sealed class ScholarshipPassive : PassiveCode
    {
        public ScholarshipPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "학식";
            IgnoresActivationChance = true;
        }

        // 전투 효과는 없다. TrainingManager가 서포트 카드의 활성 패시브를 확인해
        // INT 집중 훈련 효율 +10%를 적용한다.
    }

    public sealed class QuetzalcoatlLucky : PassiveCode
    {
        public QuetzalcoatlLucky(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "행운아";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            QuetzalcoatlStatusIds.Lucky, "quetzalcoatl_lucky", CodeName,
            Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.LUK, 4),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true, description: "LUK +4"));
    }

    public sealed class QuetzalcoatlOvercritScholar : PassiveCode
    {
        public QuetzalcoatlOvercritScholar(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "예리함";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            QuetzalcoatlStatusIds.Scholar, "quetzalcoatl_scholar", CodeName,
            Caster, Caster, new ExcessCritConversionEffect(1.5f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "100%를 초과한 치명타 확률의 1.5배를 치명타 피해로 전환합니다."));
    }

    public sealed class QuetzalcoatlMeditation : PassiveCode
    {
        public QuetzalcoatlMeditation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "명상";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            QuetzalcoatlStatusIds.Meditation, "quetzalcoatl_meditation", CodeName,
            Caster, Caster, new QuetzalcoatlMeditationEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "전투 중 1턴마다 INT가 1 증가합니다."));
    }

    /// <summary>Lv.90 날개 달린 뱀 — 10턴마다 아군 전체에게 3턴짜리 마나 재생을 다시 건다.</summary>
    public sealed class QuetzalcoatlWingedSerpent : PeriodicTurnPassive
    {
        private const int IntervalTurnCount = 10;
        private const int AuraTurns = 3;

        public QuetzalcoatlWingedSerpent(PassiveCodeContext context)
            : base(context, IntervalTurnCount, fireOnStart: true)
        {
            CodeName = "날개 달린 뱀";
            Transferable = false;
        }

        protected override void OnPeriodElapsed()
        {
            foreach (Unit ally in global::Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                ally.AddStatus(BuffStatus.Create(
                    QuetzalcoatlStatusIds.WingedSerpent,
                    $"quetzalcoatl_winged_serpent_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new QuetzalcoatlManaRegenEffect(),
                    duration: AuraTurns,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"{AuraTurns}턴 동안 턴마다 케찰코아틀 INT/10만큼 마나를 회복합니다."));
            }
        }
    }

    internal sealed class QuetzalcoatlBountyEffect : BaseEffect
    {
        private readonly float _perAlly;
        public QuetzalcoatlBountyEffect(float perAlly) : base(0, perAlly) => _perAlly = perAlly;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || Caster == null || !Caster.isActive) return 1f;
            int dendroAllies = global::Target.GetAllAllies(Caster)
                .Count(unit => unit != null && unit.isActive && unit.HasCombatElement(BaseEnums.UnitElement.Dendro));
            return 1f + dendroAllies * _perAlly;
        }
    }

    internal sealed class QuetzalcoatlMeditationEffect : BaseEffect
    {
        private int _stacks;

        public QuetzalcoatlMeditationEffect() : base(0) { }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.INT ? _stacks : 0;

        /// <summary>턴마다 INT +1.</summary>
        public override void OnOwnerTurn()
        {
            _stacks++;
            Target?.RefreshAttributes();
        }
    }

    internal sealed class QuetzalcoatlManaRegenEffect : BaseEffect
    {
        public QuetzalcoatlManaRegenEffect() : base(0) { }

        /// <summary>턴마다 INT/10의 2배를 회복한다. 턴 하나가 예전 2초에 해당한다.</summary>
        public override void OnOwnerTurn()
        {
            if (Caster == null || Target == null) return;
            Target.RecoverMana(Mathf.Max(0, Mathf.FloorToInt(Caster.GetBaseInt() / 10f)) * 2);
        }
    }
}
