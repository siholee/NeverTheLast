using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class MarieCodeIds
    {
        public const int Innate = 360;
        public const int NobleBloodline = 99;
        public const int Officer = 100;
        public const int ArcDeTriomphe = 101;
    }

    internal static class MarieStatusIds
    {
        public const int CriticalCommand = 6510;
        public const int NobleBloodline = 6511;
        public const int ArcDeTriomphe = 6512;
    }

    /// <summary>필드에 있는 동안 마리의 LUK%만큼 모든 아군의 치명타 피해를 높인다.</summary>
    public sealed class MarieCriticalCommand : UniquePassiveCode
    {
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public MarieCriticalCommand(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "혁명의 장교";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            string key = $"marie_critical_command_{Caster.GetEntityId()}";
            foreach (Unit ally in Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    MarieStatusIds.CriticalCommand, key, CodeName,
                    Caster, ally, new MarieCriticalCommandEffect(),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "마리의 LUK%만큼 치명타 피해가 증가합니다."));
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            string key = $"marie_critical_command_{Caster.GetEntityId()}";
            foreach (Unit ally in Allies(Caster)) ally.RemoveStatusByKey(key);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _cleanupHandler = null;
            _registered = false;
        }

        internal static Unit[] Allies(Unit caster)
        {
            if (caster == null) return Array.Empty<Unit>();
            return global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive)
                .Concat(caster.isActive ? new[] { caster } : Array.Empty<Unit>())
                .Distinct()
                .ToArray();
        }
    }

    internal sealed class MarieCriticalCommandEffect : BaseEffect
    {
        public MarieCriticalCommandEffect() : base(0) { }
        public override float CritMultiplierAdditiveModifier(Unit unit)
            => unit == Target && Caster != null && Caster.isActive
                ? Mathf.Max(0, Caster.GetBaseLuk()) * 0.01f
                : 0f;
    }

    public sealed class MarieNobleBloodline : PassiveCode
    {
        public MarieNobleBloodline(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "고귀한 혈통";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                MarieStatusIds.NobleBloodline, "marie_noble_bloodline", CodeName,
                Caster, Caster, new PrimaryStatMultiplierEffect(1.05f, BaseEnums.PrimaryStat.LUK),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "LUK가 5% 증가합니다."));

            // 고유 오라가 먼저 적용되므로, 상승한 LUK를 아군의 치명타 피해 캐시에 다시 반영한다.
            foreach (Unit ally in MarieCriticalCommand.Allies(Caster)) ally.RefreshAttributes();
        }
    }

    /// <summary>DEX 집중 훈련에 배치되면 효율 +10%. TrainingManager가 읽는다.</summary>
    public sealed class MarieOfficer : PassiveCode
    {
        public MarieOfficer(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "장교";
            IgnoresActivationChance = true;
        }
    }

    public sealed class MarieArcDeTriomphe : PassiveCode
    {
        public MarieArcDeTriomphe(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "개선문";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            int battleStartCodes = Caster.ActivePassiveCodes.Count(code => code != null) +
                                   Caster.ActiveItemPassiveCodes.Count(code => code != null);
            if (battleStartCodes < 3) return;

            foreach (Unit ally in MarieCriticalCommand.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    MarieStatusIds.ArcDeTriomphe,
                    $"marie_arc_de_triomphe_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new CritChanceBuffEffect(1f),
                    duration: 4,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "치명타 확률 +100% (4턴)."));
            }
        }
    }
}
