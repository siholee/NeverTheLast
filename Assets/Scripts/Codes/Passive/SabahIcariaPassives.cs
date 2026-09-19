using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    public static class SabahIcariaCodeIds
    {
        public const int SabahInnate = 203;
        public const int IcariaInnate = 226;
        public const int IcariaMentalStrength = 96;
        public const int IcariaPyroAffinity = 97;
        public const int SabahAssassin = 98;
    }

    internal static class SabahIcariaStatusIds
    {
        public const int SabahInnate = 6500;
        public const int IcariaInnate = 6501;
        public const int IcariaMentalStrength = 6502;
        public const int IcariaPyroAffinity = 6503;
        public const int SabahAssassin = 6504;
    }

    /// <summary>
    /// 사바흐 고유 패시브. 대상의 디버프 수만큼 피해가 증가하며,
    /// 아군이 적에게 해로운 상태를 실제로 적용·갱신할 때 아즈라엘 스택을 얻는다.
    ///
    /// <b>지속피해도 디버프다</b>(<see cref="Entities.Status.UnitStatus.CountsAsDebuff"/>).
    /// 부여할 때 한 번, 그 지속피해가 적을 태울 때마다 또 한 번 스택이 오른다 —
    /// 화상을 까는 탱커와 디버퍼가 함께 서면 궁극기가 여러 경로로 차는 구조다.
    /// </summary>
    public sealed class SabahDebuffHunter : UniquePassiveCode
    {
        public const float DamagePerDebuff = 0.04f;

        /// <summary>밸런스 검증용 누적치. 실제로 자원이 오른 경우만 센다.</summary>
        public int DebuffResourceGained { get; private set; }
        public int SuperconductResourceGained { get; private set; }
        public int DamageOverTimeResourceGained { get; private set; }

        // 유닛이 패배 후 재초기화되어 패시브 인스턴스가 바뀌어도 한 시뮬레이션 전체를 셀 수 있다.
        public static int AuditDebuffResourceGained { get; private set; }
        public static int AuditSuperconductResourceGained { get; private set; }
        public static int AuditDamageOverTimeResourceGained { get; private set; }

        public static void ResetAuditCounters()
        {
            AuditDebuffResourceGained = 0;
            AuditSuperconductResourceGained = 0;
            AuditDamageOverTimeResourceGained = 0;
        }

        private Action<Unit, Unit, Entities.Status.UnitStatus> _negativeStatusHandler;
        private Action<DamageResolvedContext> _damageOverTimeHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public SabahDebuffHunter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "빈틈 포착";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.SabahInnate, "sabah_debuff_hunter", CodeName,
                Caster, Caster, new SabahDebuffDamageEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"공격 대상의 디버프(지속피해 포함) 1개당 가하는 피해가 {DamagePerDebuff:P0} 증가합니다."));

            if (_registered) return;
            _negativeStatusHandler = OnNegativeStatusGranted;
            _damageOverTimeHandler = OnDamageOverTimeTick;
            _cleanupHandler = _ => StopCode();
            Unit.AnyNegativeStatusGranted += _negativeStatusHandler;
            Unit.AnyDamageDealt += _damageOverTimeHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnNegativeStatusGranted(Unit source, Unit target, Entities.Status.UnitStatus status)
        {
            if (Caster == null || !Caster.isActive || source == null || target == null) return;
            if (source.IsEnemy != Caster.IsEnemy || target.IsEnemy == Caster.IsEnemy) return;
            int before = Caster.ManaCurr;
            Caster.AddUltimateResource(1);
            int gained = Caster.ManaCurr - before;
            if (gained <= 0) return;
            DebuffResourceGained += gained;
            AuditDebuffResourceGained += gained;
            if (status != null && status.StatusName == "초전도")
            {
                SuperconductResourceGained += gained;
                AuditSuperconductResourceGained += gained;
            }
        }

        /// <summary>아군이 건 지속피해가 적을 태운 순간. 지속피해는 태그가 비어 있고 CodeType이 Effect다.</summary>
        private void OnDamageOverTimeTick(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || context == null) return;
            if (context.DamageDealt <= 0 || context.DamageContext?.CodeType != BaseEnums.CodeType.Effect) return;
            Unit source = context.Attacker;
            Unit target = context.Target;
            if (source == null || target == null) return;
            if (source.IsEnemy != Caster.IsEnemy || target.IsEnemy == Caster.IsEnemy) return;
            if (!target.HasDamageOverTimeStatus()) return;
            int before = Caster.ManaCurr;
            Caster.AddUltimateResource(1);
            int gained = Caster.ManaCurr - before;
            if (gained > 0)
            {
                DamageOverTimeResourceGained += gained;
                AuditDamageOverTimeResourceGained += gained;
            }
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyNegativeStatusGranted -= _negativeStatusHandler;
            Unit.AnyDamageDealt -= _damageOverTimeHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _registered = false;
        }
    }

    internal sealed class SabahDebuffDamageEffect : BaseEffect
    {
        public SabahDebuffDamageEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            int debuffs = target.GetAllStatuses().Count(status => status.CountsAsDebuff);
            return 1f + debuffs * SabahDebuffHunter.DamagePerDebuff;
        }
    }

    /// <summary>
    /// 광신도(24) — 사바흐 Lv.4. 메인으로 훈련받을 때 어느 훈련이든 LUK 몫 +10%.
    /// 직감(84)의 LUK판이다. 피그말리온·니콜의 행운아 계열과 함께 LUK 육성 축을 만든다.
    /// </summary>
    public sealed class SabahFanatic : PassiveCode
    {
        public const float TrainingBonus = 0.10f;

        public SabahFanatic(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "광신도";
            IgnoresActivationChance = true;
        }

        public override float MainTrainingBonus(BaseEnums.PrimaryStat stat)
            => stat == BaseEnums.PrimaryStat.LUK ? TrainingBonus : 0f;
    }

    /// <summary>
    /// 영감(119) — 서포트 카드로 훈련에 앉았을 때 스킬 힌트 발생률 ×1.5.
    /// 니콜 Lv.20 · 피그말리온 Lv.22. 전투 효과는 없다.
    /// </summary>
    public sealed class Inspiration : PassiveCode
    {
        public const float HintRateBonus = 0.50f;

        public Inspiration(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "영감";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>사바흐의 암살자 — 100% 초과 치명타 확률을 치명타 피해로 1:1 전환.</summary>
    public sealed class SabahAssassin : PassiveCode
    {
        public SabahAssassin(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "암살자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.SabahAssassin, "sabah_assassin", CodeName,
                Caster, Caster, new ExcessCritConversionEffect(1f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "100%를 초과한 치명타 확률을 같은 비율의 치명타 피해로 전환합니다."));
        }
    }

    /// <summary>이카리아 고유 패시브 — 공격 체력 비용 ×2, 실제 소모 비율만큼 피해 증가.</summary>
    public sealed class IcariaRecklessChallenge : UniquePassiveCode
    {
        public IcariaRecklessChallenge(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "무모한 도전";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.IcariaInnate, "icaria_reckless_challenge", CodeName,
                Caster, Caster, new IcariaRecklessChallengeEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "공격의 체력 소모량이 2배가 되고 실제 최대 체력 소모 비율만큼 해당 공격 피해가 증가합니다."));
        }
    }

    internal sealed class IcariaRecklessChallengeEffect : BaseEffect
    {
        public IcariaRecklessChallengeEffect() : base(0) { }

        public override float AttackSelfHpCostMultiplier(Unit unit) => unit == Target ? 2f : 1f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context != null ? 1f + UnityEngine.Mathf.Max(0f, context.SelfHpSpentRatio) : 1f;
    }

    /// <summary>상태이상 최종 적용 단계에서 10% 확률로 저항.</summary>
    public sealed class IcariaMentalStrength : PassiveCode
    {
        public IcariaMentalStrength(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "정신력";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.IcariaMentalStrength, "icaria_mental_strength", CodeName,
                Caster, Caster, new IcariaMentalStrengthEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "해로운 상태가 적용될 때 10% 확률로 저항합니다."));
        }
    }

    internal sealed class IcariaMentalStrengthEffect : BaseEffect
    {
        public IcariaMentalStrengthEffect() : base(0, 0.1f) { }
        public override float NegativeStatusResistanceChanceModifier(
            Unit unit, Entities.Status.UnitStatus status) => unit == Target ? 0.1f : 0f;
    }

    /// <summary>이카리아의 공격 준비 코드가 활성 여부를 확인하는 표식형 패시브.</summary>
    public sealed class IcariaPyroAffinity : PassiveCode
    {
        public IcariaPyroAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 불";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                SabahIcariaStatusIds.IcariaPyroAffinity, "icaria_pyro_affinity", CodeName,
                Caster, Caster, new MarkerBuffEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "불 원소 보유 중 공격하면 최대 체력의 5%를 소모하고 피해가 20% 증가합니다."));
        }
    }
}
