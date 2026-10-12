using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// T2 보상 코드 중 데이터만으로 되지 않는 것(Detail_17 §4.2) — 617 · 619~630.
    /// 조건이 붙어도 "어떤 공격 · 어떤 대상"의 분류일 뿐 전장 상태를 보지 않는 숫자 하나짜리 효과다.
    /// </summary>
    public static partial class RewardCodes
    {
        static partial void CreateT2(int codeId, PassiveCodeContext context, ref PassiveCode code)
        {
            code = codeId switch
            {
                633 => new FeintCode(context),
                617 => new PlagueCurseCode(context),
                619 => new SummonerAdeptCode(context),
                620 => new GodSlayerCode(context),
                621 => new ElementBrandCode(context),
                622 => new ManaBreathingCode(context),
                623 => new HealingTouchCode(context),
                624 => new GuardianWardCode(context),
                625 => new CelestialBodyCode(context),
                626 => new MidasHandCode(context),
                627 => new LearningPassionCode(context),
                628 => new HeroicTaleCode(context),
                629 => new StandardBearerCode(context),
                630 => new BookOfAccumulationCode(context),
                _ => code,
            };
        }
    }

    /// <summary>617 역병의 저주 — 자신이 부여한 지속피해 +15%. 상위 811.</summary>
    public sealed class PlagueCurseCode : RewardPassiveCode
    {
        public PlagueCurseCode(PassiveCodeContext context) : base(context, 617) { }

        protected override void OnCast()
        {
            // 즉발 반응과 구분한다. 자연 틱과 즉시 발동은 모두 원 생성자의 지속피해다.
            AddStatus(Caster, new ConditionalOutgoingDamageMultiplierEffect(
                damage => damage?.IsDamageOverTime == true, 1.15f));
        }
    }

    /// <summary>619 소환술사 — 필드에 있으면 아군 소환수 피해 +15%. 721 소환사와 같은 "summon_damage" 계열(높은 하나만).</summary>
    public sealed class SummonerAdeptCode : RewardPassiveCode, IRewardAuraSource
    {
        public const string Family = "summon_damage";

        public SummonerAdeptCode(PassiveCodeContext context) : base(context, 619) { }

        public string AuraFamily => Family;
        public float AuraValue => 0.15f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new SummonDamageAuraEffect(), key: RewardAura.KeyOf(Family));
        }

        /// <summary>
        /// 값은 질의마다 오라 최고값으로 다시 구한다 — 보유자가 쓰러지면 0이 되고 721이 함께 있으면 큰 쪽이 이긴다.
        /// 소환수 피해는 공격자를 소환자로 기록하므로 소환 태그를 먼저 본다.
        /// </summary>
        private sealed class SummonDamageAuraEffect : BaseEffect
        {
            public SummonDamageAuraEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target) return 1f;
                bool summonDamage = attacker.IsSummon || attacker.CountsAsSummon ||
                                    (context != null && context.HasTag(DamageTag.SummonAttack));
                return summonDamage ? 1f + RewardAura.Best(Target, Family) : 1f;
            }
        }
    }

    /// <summary>620 신살자 — 최대 체력이 자신의 150% 이상인 적에게 피해 +20%.</summary>
    public sealed class GodSlayerCode : RewardPassiveCode
    {
        public GodSlayerCode(PassiveCodeContext context) : base(context, 620) { }

        protected override void OnCast() => AddStatus(Caster, new BigTargetEffect());

        private sealed class BigTargetEffect : BaseEffect
        {
            public BigTargetEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && target != null && target.HpMax >= attacker.HpMax * 1.5f ? 1.20f : 1f;
        }
    }

    /// <summary>621 원소의 낙인 — 자신의 속성 원소를 보유(속성 · 부착 모두)한 적에게 피해 +12%.</summary>
    public sealed class ElementBrandCode : RewardPassiveCode
    {
        public ElementBrandCode(PassiveCodeContext context) : base(context, 621) { }

        protected override void OnCast() => AddStatus(Caster, new BrandedTargetEffect());

        private sealed class BrandedTargetEffect : BaseEffect
        {
            public BrandedTargetEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target || target == null) return 1f;
                // 속성이 없는 유닛(None)은 낙인을 찍을 원소가 없다. 판정은 고유 · 부착을 함께 보는 HasCombatElement.
                if (!Enum.TryParse(attacker.Element, true, out BaseEnums.UnitElement element) ||
                    element == BaseEnums.UnitElement.None) return 1f;
                return target.HasCombatElement(element) ? 1.12f : 1f;
            }
        }
    }

    /// <summary>622 마나 호흡법 — 마나 효율 +15%. 마나 자원이 아닌 유닛은 <c>RecoverMana</c>가 거르므로 따로 막지 않는다.</summary>
    public sealed class ManaBreathingCode : RewardPassiveCode
    {
        public ManaBreathingCode(PassiveCodeContext context) : base(context, 622) { }

        protected override void OnCast() => AddStatus(Caster, new ManaEfficiencyEffect(1.15f));
    }

    /// <summary>623 치유의 손길 — 부여하는 치유 +20%. 상위 810.</summary>
    public sealed class HealingTouchCode : RewardPassiveCode
    {
        public HealingTouchCode(PassiveCodeContext context) : base(context, 623) { }

        protected override void OnCast() => AddStatus(Caster, new OutgoingHealingEffect());

        private sealed class OutgoingHealingEffect : BaseEffect
        {
            public OutgoingHealingEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
                => source == Target ? 1.20f : 1f;
        }
    }

    /// <summary>624 수호의 결계 — 부여하는 보호막 +20%.</summary>
    public sealed class GuardianWardCode : RewardPassiveCode
    {
        public GuardianWardCode(PassiveCodeContext context) : base(context, 624) { }

        protected override void OnCast() => AddStatus(Caster, new OutgoingShieldEffect());

        private sealed class OutgoingShieldEffect : BaseEffect
        {
            public OutgoingShieldEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float OutgoingShieldMultiplierModifier(Unit source, Unit target)
                => source == Target ? 1.20f : 1f;
        }
    }

    /// <summary>625 천상의 신체 — 받는 치유 · 보호막 +20%.</summary>
    public sealed class CelestialBodyCode : RewardPassiveCode
    {
        public CelestialBodyCode(PassiveCodeContext context) : base(context, 625) { }

        protected override void OnCast() => AddStatus(Caster, new ReceivedSupportEffect());

        private sealed class ReceivedSupportEffect : BaseEffect
        {
            public ReceivedSupportEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float HealingReceivedMultiplierModifier(Unit unit) => unit == Target ? 1.20f : 1f;
            public override float ShieldReceivedMultiplierModifier(Unit unit) => unit == Target ? 1.20f : 1f;
        }
    }

    /// <summary>626 황금의 손 — 전투 골드 +15%. 파티에서 중첩되지 않는다(정산이 가장 큰 하나만 센다).</summary>
    public sealed class MidasHandCode : RewardPassiveCode
    {
        public MidasHandCode(PassiveCodeContext context) : base(context, 626) { }

        public override float PartyGoldAuraBonus() => 0.15f;

        // 전투 밖 정산이 일하는 코드라 라운드 안에서는 할 일이 없다.
        protected override void OnCast() { }
    }

    /// <summary>627 배움의 열정 — 자신이 얻는 EXP +20%.</summary>
    public sealed class LearningPassionCode : RewardPassiveCode
    {
        public LearningPassionCode(PassiveCodeContext context) : base(context, 627) { }

        public override float SelfExpBonus() => 0.20f;

        protected override void OnCast() { }
    }

    /// <summary>628 영웅담 — 아군 전원이 얻는 EXP +8%. 파티에서 중첩되지 않는다.</summary>
    public sealed class HeroicTaleCode : RewardPassiveCode
    {
        public HeroicTaleCode(PassiveCodeContext context) : base(context, 628) { }

        public override float PartyExpAuraBonus() => 0.08f;

        protected override void OnCast() { }
    }

    /// <summary>629 기수 — 대상 우선도 +1. 상위 726.</summary>
    public sealed class StandardBearerCode : RewardPassiveCode
    {
        public StandardBearerCode(PassiveCodeContext context) : base(context, 629) { }

        protected override void OnCast() => AddStatus(Caster, new TargetPriorityModifierEffect(1));
    }

    /// <summary>630 축적의 서 — 전투에서 승리하면 자신이 가진 성장 코드들의 영구 중첩 +1.</summary>
    public sealed class BookOfAccumulationCode : RewardPassiveCode
    {
        public BookOfAccumulationCode(PassiveCodeContext context) : base(context, 630) { }

        protected override void OnCast() { }

        public override void OnBattleSettled(bool victory, bool holderSurvived)
        {
            if (!victory || Caster == null) return;
            // 같은 키를 쓰는 코드가 둘이어도 한 번만 쌓는다. 획득량 배율(축적의 촉진 등)은 GainPermanentStacks가 곱한다.
            foreach (string key in Caster.ActivePassiveCodesResolved()
                         .Select(passive => passive.PermanentStackKey)
                         .Where(key => !string.IsNullOrEmpty(key))
                         .Distinct()
                         .ToList())
            {
                Caster.GainPermanentStacks(key, 1);
            }
        }
    }
}
