using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// T4 보상 코드 — 빌드 핵심 · 파티 오라 · 상위(Detail_17 §4.4). 801~818.
    /// 수치 · 조건은 기획서 문구가 원본이다. 클래스마다 한 코드.
    /// </summary>
    public static partial class RewardCodes
    {
        static partial void CreateT4(int codeId, PassiveCodeContext context, ref PassiveCode code)
        {
            code = codeId switch
            {
                822 => new DefeatMomentumCode(context, 822),
                823 => new DivineOfferingCode(context),
                801 => new LastStandRecoveryCode(context),
                802 => new AllDayLongCode(context),
                803 => new PreparationCode(context),
                804 => new GuideCode(context),
                805 => new HeavenlyKillStarCode(context),
                806 => new AssassinCode(context),
                807 => new WoundOpenerCode(context),
                808 => new SpellWardCode(context),
                809 => new CycleCode(context),
                810 => new DivineFavorCode(context),
                811 => new DeathPactCode(context),
                812 => new ElementalistCode(context),
                813 => new EclipseSettlementCode(context),
                814 => new LeadershipCode(context),
                815 => new TacticianCode(context),
                816 => new HolyEnchantCode(context),
                817 => new PurificationBathCode(context),
                818 => new LayeredFateCode(context),
                _ => code,
            };
        }
    }

    /// <summary>
    /// 801 기사회생 — 체력이 25% 미만이 되면 3턴에 걸쳐 최대 체력 70% 회복. 전투당 1회(재기의 바람의 상위, 상위는 901).
    /// 한 번에 채우지 않고 보유자의 턴마다 나눠 준다 — 총량이 70%가 되도록 턴당 70/3 %.
    /// </summary>
    public sealed class LastStandRecoveryCode : RewardPassiveCode
    {
        private const float Threshold = 0.25f;
        private const int Turns = 3;
        private const float TotalPercent = 70f;
        private bool _used;

        public LastStandRecoveryCode(PassiveCodeContext context) : base(context, 801) { }

        protected override void OnCast()
        {
            _used = false;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamage);
        }

        protected override void OnStop()
            => Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamage);

        private void OnAfterDamage(EventContext context)
        {
            // 쓰러진 뒤에는 회복할 수 없다. 임계 아래로 떨어진 그 피해에서 한 번만 발동한다.
            if (_used || !HolderWorking || Caster.HpCurr <= 0) return;
            if (Caster.HpCurr >= Caster.HpMax * Threshold) return;

            _used = true;
            AddStatus(Caster, new PercentHealOverTimeEffect(TotalPercent / Turns), "_regen", Turns);
        }
    }

    /// <summary>
    /// 802 하루종일도 할 수 있어 — 가한 피해의 15%만큼 회복. 초과 치유가 발생하면 자신의 턴당 1회 방어력 60% 보호막. 투사의 상위.
    /// 넘친 양은 <see cref="Unit.Heal"/>이 알리는 AnyHealed에서 동기적으로 받는다 — 치유 배율을 탄 뒤의 값이라야 정확하기 때문이다.
    /// </summary>
    public sealed class AllDayLongCode : RewardPassiveCode
    {
        private const float HealRatio = 0.15f;
        private readonly TurnCooldown _shieldCooldown = new(1);
        private bool _capturing;
        private int _overflow;

        public AllDayLongCode(PassiveCodeContext context) : base(context, 802) { }

        protected override void OnCast()
        {
            _capturing = false;
            _overflow = 0;
            _shieldCooldown.Reset();
            Unit.AnyDamageDealt += OnDamageDealt;
            Unit.AnyHealed += OnHealed;
        }

        protected override void OnStop()
        {
            Unit.AnyDamageDealt -= OnDamageDealt;
            Unit.AnyHealed -= OnHealed;
        }

        private void OnHealed(Unit target, Unit source, int effective, int overflow)
        {
            if (_capturing && target == Caster) _overflow += overflow;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (context == null || context.Attacker != Caster || !IsActive || !Caster.isActive || Caster.HpCurr <= 0) return;
            int amount = Mathf.RoundToInt(context.DamageDealt * HealRatio);
            if (amount <= 0) return;

            _overflow = 0;
            _capturing = true;
            try { Caster.Heal(amount, Caster); }
            finally { _capturing = false; }
            if (_overflow <= 0 || !_shieldCooldown.TryUse(Caster)) return;
            Caster.AddShield(Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Defense) * 0.60f), Caster);
        }
    }

    /// <summary>803 사전준비 — 전투 시작 시 마나가 최대치의 50%로 찬다. <c>Passive</c> · <c>Stack</c> 자원에는 무효.</summary>
    public sealed class PreparationCode : RewardPassiveCode
    {
        public PreparationCode(PassiveCodeContext context) : base(context, 803) { }

        protected override void OnCast()
        {
            if (Caster.UltimateResourceType != BaseEnums.UltimateResourceType.Mana) return;
            // 이미 절반 이상이면 그대로 둔다 — 깎지 않는다.
            int need = Caster.ManaMax / 2 - Caster.ManaCurr;
            if (need > 0) Caster.AddUltimateResource(need);
        }
    }

    /// <summary>
    /// 804 인도자 — 궁극기를 쓴 뒤 필드의 다른 아군 전원에게 마나 +10. 자신의 다음 턴 전까지 1회.
    /// 자기 턴이 시작될 때 다시 장전한다.
    /// </summary>
    public sealed class GuideCode : RewardPassiveCode
    {
        private const int ManaGift = 10;
        private bool _armed;

        public GuideCode(PassiveCodeContext context) : base(context, 804) { }

        protected override void OnCast()
        {
            _armed = true;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnUltimateActivates, OnUltimate);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnTurnStart, OnTurnStart);
        }

        protected override void OnStop()
        {
            if (Caster == null) return;
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnUltimateActivates, OnUltimate);
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnTurnStart, OnTurnStart);
        }

        private void OnTurnStart(EventContext context) => _armed = true;

        private void OnUltimate(EventContext context)
        {
            if (!_armed || !IsActive) return;
            _armed = false;
            foreach (Unit ally in FieldAllies())
            {
                if (ally != Caster) ally.RecoverMana(ManaGift);
            }
        }
    }

    /// <summary>
    /// 805 천살성 — 공격 후 최대 체력 8% 미만인 일반 등급 적을 즉시 처형(902 당연한 운명의 하위).
    /// 지속피해는 공격이 아니므로 보지 않는다. 처형은 보호막까지 뚫는 고정 피해다.
    /// </summary>
    public sealed class HeavenlyKillStarCode : RewardPassiveCode
    {
        private const float Threshold = 0.08f;
        private bool _executing;

        public HeavenlyKillStarCode(PassiveCodeContext context) : base(context, 805) { }

        protected override void OnCast()
        {
            _executing = false;
            Unit.AnyDamageDealt += OnDamageDealt;
        }

        protected override void OnStop() => Unit.AnyDamageDealt -= OnDamageDealt;

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (_executing || context == null || context.Attacker != Caster || !IsActive || !Caster.isActive) return;
            if (context.DamageContext != null && context.DamageContext.CodeType == BaseEnums.CodeType.Effect) return;

            Unit target = context.Target;
            // 이 이벤트는 사망 판정 전에 온다 — 방금 그 공격으로 쓰러진 적(HpCurr 0 이하)은 이미 죽은 것이라 건너뛴다.
            if (target == null || !target.isActive || target.HpCurr <= 0 || target.IsEnemy == Caster.IsEnemy) return;
            if (target.IsSummon || target.UnitTier != "normal") return;
            if (target.HpCurr >= target.HpMax * Threshold) return;

            _executing = true;
            // 처형으로 죽은 대상은 즉시 비활성화되므로 같은 행동의 다음 타격이 두 번 처형하지 않는다.
            target.TakeDamage(new DamageContext(Caster, target.HpCurr + target.ShieldCurr, BaseEnums.CodeType.Passive,
                new List<int> { DamageTag.SingleTarget, DamageTag.PassiveAttack, DamageTag.TrueDamage, DamageTag.ShieldPenetration }));
            _executing = false;
        }
    }

    /// <summary>806 암살자 — 100%를 넘는 치명타 확률을 1:1로 치명타 피해로 바꾼다.</summary>
    public sealed class AssassinCode : RewardPassiveCode
    {
        public AssassinCode(PassiveCodeContext context) : base(context, 806) { }

        protected override void OnCast() => AddStatus(Caster, new CritOverflowEffect());

        private sealed class CritOverflowEffect : BaseEffect
        {
            public CritOverflowEffect() : base(0) { }
            public override bool IsBeneficial => true;
            public override float CritOverflowToDamageRatio(Unit attacker) => attacker == Target ? 1f : 0f;
        }
    }

    /// <summary>807 상처 벌리기 — 지속피해 중인 적을 공격하면 방어 30% 무시.</summary>
    public sealed class WoundOpenerCode : RewardPassiveCode
    {
        private const float DefenseKept = 0.7f;

        public WoundOpenerCode(PassiveCodeContext context) : base(context, 807) { }

        protected override void OnCast() => AddStatus(Caster, new WoundEffect());

        private sealed class WoundEffect : BaseEffect
        {
            public WoundEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && target != null && target.HasDamageOverTimeStatus() ? DefenseKept : 1f;
        }
    }

    /// <summary>
    /// 808 불침의 결계 — 4턴마다 피해 1회를 완전히 막는 결계. 결계가 남아 있으면 주기를 세지 않는다.
    /// 전투 시작에 하나 두르고, 깨진 뒤 자기 턴을 4번 세면 다시 두른다.
    /// 막는 방법: 피해 처리 직전에 컨텍스트를 취소하고 결계를 걷는다. 광역 공격이 컨텍스트를 대상마다 재사용하므로
    /// 피해 후 이벤트에서 취소를 되돌려 다른 대상이 같이 막히지 않게 한다.
    /// </summary>
    public sealed class SpellWardCode : RewardPassiveCode
    {
        private const int RenewTurns = 4;
        private int _counter;
        private DamageContext _blocked;

        public SpellWardCode(PassiveCodeContext context) : base(context, 808) { }

        private string WardKey => StatusKey("_ward");
        private bool HasWard => Caster != null && Caster.HasStatusKey(WardKey);

        protected override void OnCast()
        {
            _counter = 0;
            _blocked = null;
            RaiseWard();
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnTurnStart, OnTurnStart);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnBeforeDamageTaken, OnBeforeDamage);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamage);
        }

        protected override void OnStop()
        {
            if (Caster == null) return;
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnTurnStart, OnTurnStart);
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnBeforeDamageTaken, OnBeforeDamage);
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamage);
            if (_blocked != null) _blocked.IsCancelled = false;
            _blocked = null;
        }

        private void RaiseWard() => AddStatus(Caster, new MarkerBuffEffect(), "_ward");

        private void OnTurnStart(EventContext context)
        {
            if (HasWard)
            {
                _counter = 0;
                return;
            }
            if (++_counter < RenewTurns) return;
            _counter = 0;
            RaiseWard();
        }

        private void OnBeforeDamage(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            if (damage == null || damage.IsCancelled || damage.Damage <= 0 || !HasWard) return;
            damage.IsCancelled = true;
            _blocked = damage;
            Caster.RemoveStatusByKey(WardKey);
        }

        private void OnAfterDamage(EventContext context)
        {
            if (_blocked == null || !ReferenceEquals(context?.DmgCtx, _blocked)) return;
            _blocked.IsCancelled = false;
            _blocked = null;
        }
    }

    /// <summary>
    /// 809 순환 — 같은 진영 아군이 쓰러지면 무작위 생존 아군 하나가 전투 끝까지 피해 +25%, 받는 피해 -25%. 중첩된다.
    /// 소환수가 쓰러진 것은 세지 않는다.
    /// </summary>
    public sealed class CycleCode : RewardPassiveCode
    {
        private const float OutgoingBonus = 1.25f;
        private const float ReceivingCut = 0.75f;

        public CycleCode(PassiveCodeContext context) : base(context, 809) { }

        protected override void OnCast() => Unit.AnyUnitDied += OnUnitDied;

        protected override void OnStop() => Unit.AnyUnitDied -= OnUnitDied;

        private void OnUnitDied(Unit dead, Unit attacker)
        {
            if (!HolderWorking || dead == null || dead == Caster || dead.IsSummon || dead.IsEnemy != Caster.IsEnemy) return;

            // 사망 이벤트는 칸이 비워지기 전에 오므로 쓰러진 본인을 후보에서 뺀다.
            List<Unit> pool = FieldAllies().Where(ally => ally != dead && ally.HpCurr > 0).ToList();
            if (pool.Count == 0) return;
            Unit chosen = pool[Random.Range(0, pool.Count)];

            // 같은 키의 Stack 상태는 겹쳐서 쌓인다. 정리는 키 하나로 한꺼번에 된다.
            AddStatus(chosen, new OutgoingDamageMultiplierEffect(OutgoingBonus), "_cycle",
                stackPolicy: BaseEnums.StatusStackPolicy.Stack);
            AddStatus(chosen, new ReceivingDamageMultiplierEffect(ReceivingCut), "_cycle",
                stackPolicy: BaseEnums.StatusStackPolicy.Stack);
        }
    }

    /// <summary>
    /// 810 의신의 가호 — 부여하는 치유 · 보호막 +40%, 받은 대상의 해로운 상태 1개 제거(치유의 손길의 상위).
    /// 가장 먼저 걸린 해로운 상태(지속피해 포함) 하나를 걷는다.
    /// </summary>
    public sealed class DivineFavorCode : RewardPassiveCode
    {
        private const float SupportBonus = 1.40f;

        public DivineFavorCode(PassiveCodeContext context) : base(context, 810) { }

        protected override void OnCast()
        {
            AddStatus(Caster, new DivineSupportEffect());
            Unit.AnyHealed += OnHealed;
            Unit.AnyShieldGranted += OnShieldGranted;
        }

        protected override void OnStop()
        {
            Unit.AnyHealed -= OnHealed;
            Unit.AnyShieldGranted -= OnShieldGranted;
        }

        private void OnHealed(Unit target, Unit source, int effective, int overflow)
        {
            if (effective > 0) CleanseOne(target, source);
        }

        private void OnShieldGranted(Unit target, Unit source, int amount)
        {
            if (amount > 0) CleanseOne(target, source);
        }

        private void CleanseOne(Unit target, Unit source)
        {
            if (!IsActive || source != Caster || target == null || target.IsEnemy != Caster.IsEnemy) return;
            UnitStatus harmful = target.GetAllStatuses().FirstOrDefault(status => status != null && status.CountsAsDebuff);
            if (harmful != null) target.RemoveStatus(harmful);
        }

        private sealed class DivineSupportEffect : BaseEffect
        {
            public DivineSupportEffect() : base(0) { }
            public override bool IsBeneficial => true;
            public override float OutgoingHealingMultiplierModifier(Unit source, Unit target) => source == Target ? SupportBonus : 1f;
            public override float OutgoingShieldMultiplierModifier(Unit source, Unit target) => source == Target ? SupportBonus : 1f;
        }
    }

    /// <summary>
    /// 811 죽음의 계약 — 자신이 부여하는 지속피해 +35%, 지속 +1턴(역병의 저주의 상위).
    /// 지속 가산은 상태가 붙기 전에 물으므로, 아직 효과 객체가 없는 ID 효과(1001 · 1002)도 지속피해로 읽는다.
    /// </summary>
    public sealed class DeathPactCode : RewardPassiveCode
    {
        private const float DotBonus = 1.35f;

        public DeathPactCode(PassiveCodeContext context) : base(context, 811) { }

        protected override void OnCast() => AddStatus(Caster, new PactEffect());

        private static bool IsDamageOverTimeStatus(UnitStatus status)
            => status?.Effects != null && status.Effects.Exists(instance =>
                instance.EffectObject?.IsDamageOverTime == true || instance.EffectId == 1001 || instance.EffectId == 1002);

        private sealed class PactEffect : BaseEffect
        {
            public PactEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && context?.IsDamageOverTime == true ? DotBonus : 1f;

            public override int OutgoingStatusDurationModifier(Unit caster, UnitStatus status)
                => caster == Target && IsDamageOverTimeStatus(status) ? 1 : 0;
        }
    }

    /// <summary>
    /// 812 원소술사 — 필드에 있으면 아군 전원의 원소 반응 피해 +25%(원소 촉매의 상위). 여러 명이 들면 높은 하나만.
    /// 같은 계열 "reaction"은 903 대해의 판결과 공유한다.
    /// </summary>
    public sealed class ElementalistCode : RewardPassiveCode, IRewardAuraSource
    {
        public const string Family = "reaction";

        public ElementalistCode(PassiveCodeContext context) : base(context, 812) { }

        public string AuraFamily => Family;
        public float AuraValue => 0.25f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new ReactionAuraEffect(), key: RewardAura.KeyOf(Family));
        }

        private sealed class ReactionAuraEffect : BaseEffect
        {
            public ReactionAuraEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target || context == null || !context.HasTag(DamageTag.ElementalReaction)) return 1f;
                return 1f + RewardAura.Best(Target, Family);
            }
        }
    }

    /// <summary>
    /// 813 월식 정산 — 지속피해 중인 적이 처치되면 남은 지속피해의 75%를 살아 있는 다른 적들에게 균등하게 나눠 준다.
    /// "주변"은 같은 진영 필드의 살아 있는 적 전원으로 읽는다. 나눠 주는 피해는 지속피해(Effect) 종류다.
    /// </summary>
    public sealed class EclipseSettlementCode : RewardPassiveCode
    {
        private const float Share = 0.75f;
        // 기한 없는 지속피해가 무한대로 정산되지 않게 센 턴 수의 상한.
        private const int OpenEndedTurnCap = 5;

        public EclipseSettlementCode(PassiveCodeContext context) : base(context, 813) { }

        protected override void OnCast() => Unit.AnyUnitDied += OnUnitDied;

        protected override void OnStop() => Unit.AnyUnitDied -= OnUnitDied;

        private void OnUnitDied(Unit dead, Unit attacker)
        {
            if (!HolderWorking || dead == null || dead.IsEnemy == Caster.IsEnemy || dead.IsSummon) return;

            // 사망 이벤트는 상태가 정리되기 전에 오므로 지금 읽는다.
            float remaining = 0f;
            foreach (UnitStatus status in dead.GetAllStatuses())
            {
                if (status?.Effects == null) continue;
                int turns = Mathf.Min(status.RemainingTurns, OpenEndedTurnCap);
                foreach (UnitStatus.EffectInstance instance in status.Effects)
                {
                    BaseEffect effect = instance.EffectObject;
                    if (effect != null && effect.IsDamageOverTime) remaining += effect.EstimateDamagePerTurn() * turns;
                }
            }
            if (remaining <= 0f) return;

            List<Unit> survivors = FieldEnemies().Where(enemy => enemy != dead && enemy.HpCurr > 0).ToList();
            if (survivors.Count == 0) return;

            int each = Mathf.Max(1, Mathf.RoundToInt(remaining * Share / survivors.Count));
            foreach (Unit enemy in survivors)
            {
                if (!enemy.isActive || enemy.HpCurr <= 0) continue;
                enemy.TakeDamage(new DamageContext(Caster, each, BaseEnums.CodeType.Effect, new List<int>()));
            }
        }
    }

    /// <summary>814 리더십 — 필드에 있으면 아군 전원 피해 +10%. 여러 명이 들면 높은 하나만.</summary>
    public sealed class LeadershipCode : RewardPassiveCode, IRewardAuraSource
    {
        public const string Family = "leadership";

        public LeadershipCode(PassiveCodeContext context) : base(context, 814) { }

        public string AuraFamily => Family;
        public float AuraValue => 0.10f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new LeadershipAuraEffect(), key: RewardAura.KeyOf(Family));
        }

        private sealed class LeadershipAuraEffect : BaseEffect
        {
            public LeadershipAuraEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target ? 1f + RewardAura.Best(Target, Family) : 1f;
        }
    }

    /// <summary>815 전술가 — 필드에 있으면 아군 전원 방어 무시 10%. 여러 명이 들면 높은 하나만.</summary>
    public sealed class TacticianCode : RewardPassiveCode, IRewardAuraSource
    {
        public const string Family = "tactician";

        public TacticianCode(PassiveCodeContext context) : base(context, 815) { }

        public string AuraFamily => Family;
        public float AuraValue => 0.10f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new TacticianAuraEffect(), key: RewardAura.KeyOf(Family));
        }

        private sealed class TacticianAuraEffect : BaseEffect
        {
            public TacticianAuraEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target ? Mathf.Max(0f, 1f - RewardAura.Best(Target, Family)) : 1f;
        }
    }

    /// <summary>
    /// 816 홀리 인챈트 — 필드에 있으면 아군 전원 치명타 확률 +10%p. 여러 명이 들면 높은 하나만.
    /// 스탯 캐시는 상태가 바뀔 때만 갱신되므로, 보유자가 쓰러진 뒤에도 값이 남지 않게 아군이 자기 턴마다 캐시를 새로 계산한다.
    /// </summary>
    public sealed class HolyEnchantCode : RewardPassiveCode, IRewardAuraSource
    {
        public const string Family = "holy";

        public HolyEnchantCode(PassiveCodeContext context) : base(context, 816) { }

        public string AuraFamily => Family;
        public float AuraValue => 0.10f;

        protected override void OnCast()
        {
            Unit.AnyUnitDied += OnHolderDied;
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new HolyAuraEffect(), key: RewardAura.KeyOf(Family));
        }

        protected override void OnStop() => Unit.AnyUnitDied -= OnHolderDied;

        private void OnHolderDied(Unit dead, Unit attacker)
        {
            if (dead != Caster) return;
            Unit.AnyUnitDied -= OnHolderDied;
            // 사망 알림은 비활성화보다 먼저다. 죽는 중인 오라는 제외하고 다른 아군의 캐시를 즉시 갱신한다.
            foreach (Unit ally in FieldAllies())
                if (!ally.IsDying) ally.RefreshAttributes();
        }

        private sealed class HolyAuraEffect : BaseEffect
        {
            public HolyAuraEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat)
                => unit == Target && stat == BaseEnums.StatType.CritRate ? RewardAura.Best(Target, Family) : 0f;

            public override void OnOwnerTurn()
            {
                if (Target != null && Target.isActive) Target.RefreshAttributes();
            }
        }
    }

    /// <summary>
    /// 817 목욕재계 — 전투가 끝나면 살아 있는 아군 전원이 최대 체력 15% 회복.
    /// 보유자가 살아 있어야 하고(문구의 "살아 있는 아군"), 승패와 상관없이 정산한다. 벤치는 이 전투에 없었으므로 제외.
    /// </summary>
    public sealed class PurificationBathCode : RewardPassiveCode
    {
        private const float HealRatio = 0.15f;

        public PurificationBathCode(PassiveCodeContext context) : base(context, 817) { }

        protected override void OnCast() { }

        public override void OnBattleSettled(bool victory, bool holderSurvived)
        {
            GridManager grid = GridManager.Instance;
            if (!holderSurvived || grid == null || Caster == null) return;

            foreach (Unit hero in grid.heroList.ToList())
            {
                if (hero == null || hero.IsEnemy || !hero.isActive || hero.currentCell == null ||
                    grid.IsBenchCell(hero.currentCell)) continue;
                hero.RestoreHpOutsideCombat(Mathf.RoundToInt(hero.HpMax * HealRatio));
            }
        }
    }

    /// <summary>818 겹겹이 쌓인 운명 — 자신이 얻는 영구 중첩 2배(축적의 촉진의 상위).</summary>
    public sealed class LayeredFateCode : RewardPassiveCode
    {
        public LayeredFateCode(PassiveCodeContext context) : base(context, 818) { }

        protected override void OnCast() { }

        public override float PermanentStackGainMultiplier(string key) => 2f;
    }
}
