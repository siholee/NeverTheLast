using System.Linq;
using BaseClasses;
using Combat;
using Effects.Base;
using Entities;
using Managers;

namespace Codes.Passive.Reward
{
    /// <summary>재보 코드 9종. 개인 효과와 공용 잔고를 분리하고 환급은 실제 행동 종료에만 처리한다.</summary>
    public sealed class TreasuryRewardCode : RewardPassiveCode
    {
        private int _normalCount;
        public TreasuryRewardCode(PassiveCodeContext context, int id) : base(context, id) { }
        public static bool Supports(int id) => id is 631 or 632 or 728 or 729 or 730 or 819 or 820 or 908 or 909;
        private bool Working => HolderWorking && PartyTreasury.CanUse(Caster);
        protected override void OnCast()
        {
            _normalCount = 0;
            if (RewardId == 728 && PartyTreasury.ClaimOpeningGrant(Caster, RewardId)) PartyTreasury.Charge(Caster, 2);
            if (RewardId == 730) Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
            if (RewardId == 908) PartyTreasury.SpentActionFinished += OnSpentFinished;
            if (RewardId == 819)
            {
                AddStatus(Caster, new InsuranceEffect(this));
                PartyTreasury.BalanceChanged += RefreshInsurance; RefreshInsurance();
            }
        }
        protected override void OnStop()
        {
            if (RewardId == 730) Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
            if (RewardId == 908) PartyTreasury.SpentActionFinished -= OnSpentFinished;
            if (RewardId == 819) PartyTreasury.BalanceChanged -= RefreshInsurance;
        }
        private void OnNormal(EventContext context)
        {
            if (!Working || ++_normalCount < 3) return;
            _normalCount = 0; PartyTreasury.Charge(Caster, 1);
        }
        private void OnSpentFinished(Unit actor, int spent, ActionScheduler.ActionKind kind)
        {
            if (Working && actor == Caster && spent > 0) PartyTreasury.Charge(Caster, 1);
        }
        private void RefreshInsurance() { if (Caster != null) Caster.RefreshAttributes(); }
        private static bool Has(Unit actor, int id) => actor != null && actor.ActivePassiveCodesResolved()
            .OfType<TreasuryRewardCode>().Any(code => code.RewardId == id && code.Working);
        public static float CritDamageBonus(Unit actor) => Has(actor, 632) ? .40f : 0;
        public static float SupportBonus(Unit actor, bool spent) => spent && Has(actor, 729) ? .30f : 0;
        public static float DamageBonus(DamageContext hit)
        {
            if (hit?.IsTreasuryAttack != true) return 0;
            float bonus = Has(hit.Attacker, 631) ? .10f : 0;
            if (hit.IsTreasurySpendAttack && hit.CodeType == BaseEnums.CodeType.Ultimate && hit.TreasuryShotIndex >= 6 && Has(hit.Attacker, 909)) bonus += .80f;
            return bonus;
        }
        public static float DefenseMultiplier(DamageContext hit)
            => hit?.IsTreasurySpendAttack == true && Has(hit.Attacker, 820) &&
                DamageMitigation.TryGetDefenseStat(hit, out var stat) && stat == BaseEnums.StatType.Defense ? .70f : 1;
        private sealed class InsuranceEffect : BaseEffect
        {
            private readonly TreasuryRewardCode _code;
            public InsuranceEffect(TreasuryRewardCode code) : base(0) => _code = code;
            public override bool IsBeneficial => true;
            public override float StatPercentModifier(Unit unit, BaseEnums.StatType stat)
                => unit == Target && _code.Working && PartyTreasury.Available >= 4 &&
                    stat is BaseEnums.StatType.Defense or BaseEnums.StatType.SpecialDefense ? .12f : 0;
        }
    }
}
