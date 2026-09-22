using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    internal static class HugoStatusIds
    {
        public const int Ensemble = 6490;
        public const int Poet = 6491;
        public const int GreatWriter = 6492;
        public const int PenseeDefense = 6493;
    }

    /// <summary>위고 P — 군상극. 기본 +1%, 아군 소환마다 전투 중 +1%가 누적된다.</summary>
    public sealed class HugoEnsemble : UniquePassiveCode
    {
        private int _summonStacks;
        private bool _registered;
        private Action<Unit, Unit> _spawned;
        private Action<EventContext> _cleanup;

        public HugoEnsemble(PassiveCodeContext context) : base(context)
        {
            CodeName = "군상극";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _summonStacks = 0;
            Caster.AddStatus(BuffStatus.Create(
                HugoStatusIds.Ensemble, "hugo_ensemble", CodeName,
                Caster, Caster, new HugoEnsembleEffect(() => _summonStacks),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "아군 소환수 피해 +1%. 아군이 소환할 때마다 +1%가 누적됩니다."));

            _spawned = OnSummonSpawned;
            _cleanup = _ => StopCode();
            Unit.AnySummonSpawned += _spawned;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            _registered = true;
        }

        private void OnSummonSpawned(Unit owner, Unit summon)
        {
            if (Caster == null || !Caster.isActive || owner == null || summon == null ||
                owner.IsEnemy != Caster.IsEnemy) return;
            _summonStacks++;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Unit.AnySummonSpawned -= _spawned;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            Caster.RemoveStatusByKey("hugo_ensemble");
            _registered = false;
        }
    }

    internal sealed class HugoEnsembleEffect : BaseEffect
    {
        private readonly Func<int> _stacks;
        public HugoEnsembleEffect(Func<int> stacks) : base(0) => _stacks = stacks;
        public override float AlliedSummonDamageBonusAdditive(Unit unit, Unit summonOwner)
            => unit == Target && unit.isActive && unit.IsOnField
                ? 0.01f * (1 + Math.Max(0, _stacks?.Invoke() ?? 0))
                : 0f;
    }

    public class HugoPoet : PassiveCode
    {
        protected readonly float Bonus;
        protected virtual int StatusId => HugoStatusIds.Poet;
        protected virtual string StatusKey => "hugo_poet";

        public HugoPoet(PassiveCodeContext context, float bonus = 0.20f) : base(context)
        {
            CodeName = "시인";
            Bonus = bonus;
            SupersededByCodeId = 145;
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            StatusId, StatusKey, CodeName, Caster, Caster,
            new HugoSummonCritEffect(Bonus),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: $"자신의 소환수 공격 치명타 피해 +{Bonus * 100f:0}%."));
    }

    public sealed class HugoGreatWriter : HugoPoet
    {
        protected override int StatusId => HugoStatusIds.GreatWriter;
        protected override string StatusKey => "hugo_great_writer";

        public HugoGreatWriter(PassiveCodeContext context) : base(context, 0.40f)
        {
            CodeName = "대문호";
            SupersededByCodeId = 0;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }
    }

    internal sealed class HugoSummonCritEffect : BaseEffect
    {
        private readonly float _bonus;
        public HugoSummonCritEffect(float bonus) : base(0, bonus) => _bonus = bonus;
        public override float SummonCritMultiplierAdditiveModifier(Unit unit)
            => unit == Target ? _bonus : 0f;
    }
}
