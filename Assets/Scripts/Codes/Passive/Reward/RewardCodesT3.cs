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
    /// T3 보상 코드(701~727) — 조건 · 트리거 · 중첩 · 위치 · 전투당 1회 생존기(Detail_17 §4.3).
    /// T2는 숫자 하나로 끝나지만 T3는 전장 상태(위치 · 체력 임계 · 이벤트)를 본다.
    /// </summary>
    public static partial class RewardCodes
    {
        static partial void CreateT3(int codeId, PassiveCodeContext context, ref PassiveCode code)
        {
            code = codeId switch
            {
                731 => new EchoSongCode(context),
                701 => new RebirthWindCode(context),
                702 => new MentalityCode(context),
                703 => new SelfHealingCode(context),
                704 => new FighterCode(context),
                705 => new SentinelCode(context),
                706 => new LastStandCode(context),
                707 => new SniperCode(context),
                708 => new IronWallCode(context),
                709 => new BulwarkCode(context),
                710 => new SpellAccelerationCode(context),
                711 => new HuntersBlessingCode(context),
                712 => new HackerCode(context),
                713 => new GeoAffinityCode(context),
                714 => new FirstAidCode(context),
                715 => new FadingFlowerCode(context),
                716 => new AmbushCode(context),
                717 => new DefeatMomentumCode(context, 717),
                718 => new FireworksCode(context),
                719 => new OverhealCode(context),
                720 => new SealingCircleCode(context),
                721 => new SummonerCode(context),
                722 => new GlassCannonCode(context),
                723 => new ThunderStrikeCode(context),
                724 => new ChainStrikeCode(context),
                725 => new RetributionCode(context),
                726 => new BannerEmblemCode(context),
                727 => new MultiStackCode(context),
                _ => code,
            };
        }
    }

    /// <summary>T3 코드가 함께 쓰는 위치 · 공격 판정. 전열/후열은 열 번호로만 가른다(GridManager가 유일한 출처).</summary>
    internal static class RewardT3Util
    {
        public static bool InColumn(Unit unit, bool front)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || unit == null || unit.currentCell == null) return false;
            int column = front ? grid.GetFrontColumn(unit.IsEnemy) : grid.GetRearColumn(unit.IsEnemy);
            return unit.currentCell.xPos == column;
        }

        /// <summary>
        /// 그 열에 선 살아 있는 아군 수. 소환수는 파티원이 아니라 세지 않는다.
        /// 사망 이벤트는 칸이 비워지기 전에 오므로 체력 0 이하도 걸러야 쓰러지는 순간에 바로 맞는다.
        /// </summary>
        public static int CountAlliesInColumn(Unit holder, bool front, bool excludeHolder)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || holder == null) return 0;
            List<Unit> pool = holder.IsEnemy ? grid.enemyList : grid.heroList;
            int count = 0;
            foreach (Unit unit in pool)
            {
                if (unit == null || !unit.isActive || unit.HpCurr <= 0 || unit.IsSummon || !unit.IsOnField) continue;
                if (excludeHolder && unit == holder) continue;
                if (InColumn(unit, front)) count++;
            }
            return count;
        }

        /// <summary>지속피해 · 원소 반응은 "공격"이 아니다 — 위치/조건형 피해 증가가 붙지 않게 가른다.</summary>
        public static bool IsDirectAttack(DamageContext context)
            => context != null && context.CodeType != BaseEnums.CodeType.Effect &&
               !context.HasTag(DamageTag.ElementalReaction);
    }

    /// <summary>
    /// 위치에 따라 스탯이 바뀌는 코드의 공통 뼈대. 스탯은 캐시되므로 같은 편이 쓰러져 열의 사정이 바뀌면
    /// 보유자의 스탯을 다시 모아야 한다.
    /// </summary>
    public abstract class RewardPositionCode : RewardPassiveCode
    {
        protected RewardPositionCode(PassiveCodeContext context, int rewardId) : base(context, rewardId) { }

        protected sealed override void OnCast()
        {
            Unit.AnyUnitDied += OnAnyUnitDied;
            ApplyEffects();
        }

        protected override void OnStop() => Unit.AnyUnitDied -= OnAnyUnitDied;

        protected abstract void ApplyEffects();

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Caster == null || dead == null || dead.IsEnemy != Caster.IsEnemy) return;
            Caster.RefreshAttributes();
        }
    }

    // ── 701 ~ 704 생존 ───────────────────────────────────────────

    /// <summary>701 재기의 바람 — 체력 40% 이하가 되면 2턴 재생(턴마다 25%) + 받는 피해 -20%. 전투당 1회.</summary>
    public sealed class RebirthWindCode : RewardPassiveCode
    {
        private const float TriggerRatio = 0.40f;
        private bool _used;
        private bool _subscribed;

        public RebirthWindCode(PassiveCodeContext context) : base(context, 701) { }

        protected override void OnCast()
        {
            _used = false;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = false;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            // 쓰러진 뒤에는 회복해도 의미가 없다 — 사망 판정 이후 같은 이벤트가 와도 무시한다.
            if (_used || !HolderWorking || Caster.HpCurr <= 0) return;
            if (Caster.HpCurr > Caster.HpMax * TriggerRatio) return;

            _used = true;
            // 상태 지속 2턴 = 보유자의 다음 두 턴 시작에 한 번씩 → 25% × 2 = 50%.
            AddStatus(Caster, new PercentHealOverTimeEffect(25f), "_regen", duration: 2);
            AddStatus(Caster, new ReceivingDamageMultiplierEffect(0.8f), "_guard", duration: 2);
        }
    }

    /// <summary>702 정신력 — 해로운 상태 25% 저항, 제어 지속 -1턴(최소 1턴은 엔진이 보장).</summary>
    public sealed class MentalityCode : RewardPassiveCode
    {
        public MentalityCode(PassiveCodeContext context) : base(context, 702) { }

        protected override void OnCast()
            => AddStatus(Caster, new MentalityEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class MentalityEffect : BaseEffect
        {
            public MentalityEffect() : base(0) { }

            public override float NegativeStatusResistanceChanceModifier(Unit unit, UnitStatus status)
                => unit == Target ? 0.25f : 0f;

            public override int ControlDurationModifier(Unit unit) => unit == Target ? -1 : 0;
        }
    }

    /// <summary>703 자가치유 — 자기 턴 시작마다 최대 체력 4%, 체력 50% 이하면 8%.</summary>
    public sealed class SelfHealingCode : RewardPassiveCode
    {
        private bool _subscribed;

        public SelfHealingCode(PassiveCodeContext context) : base(context, 703) { }

        protected override void OnCast()
        {
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnTurnStart, OnTurnStart);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnTurnStart, OnTurnStart);
            _subscribed = false;
        }

        private void OnTurnStart(EventContext context)
        {
            if (!HolderWorking || Caster.HpCurr <= 0) return;
            // 회복량은 턴 시작 시점의 체력으로 가른다 — 막 위기에 빠진 사람이 두 배를 받는 것이 의도다.
            float ratio = Caster.HpCurr * 2 <= Caster.HpMax ? 0.08f : 0.04f;
            Caster.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * ratio)), Caster);
        }
    }

    /// <summary>704 투사 — 가한 피해의 8%를 회복, 체력 50% 이하면 16%.</summary>
    public sealed class FighterCode : RewardPassiveCode
    {
        public FighterCode(PassiveCodeContext context) : base(context, 704) { }

        protected override void OnCast() => Unit.AnyDamageDealt += OnAnyDamageDealt;

        protected override void OnStop() => Unit.AnyDamageDealt -= OnAnyDamageDealt;

        private void OnAnyDamageDealt(DamageResolvedContext resolved)
        {
            if (resolved == null || resolved.Attacker != Caster || !HolderWorking || Caster.HpCurr <= 0) return;
            float ratio = Caster.HpCurr * 2 <= Caster.HpMax ? 0.16f : 0.08f;
            int heal = Mathf.RoundToInt(resolved.DamageDealt * ratio);
            if (heal > 0) Caster.Heal(heal, Caster);
        }
    }

    // ── 705 ~ 708 위치 · 견고 ────────────────────────────────────

    /// <summary>705 파수꾼 — 전열일 때 필드의 후열 아군 1명당 공격력 · 방어력 +5%.</summary>
    public sealed class SentinelCode : RewardPositionCode
    {
        public SentinelCode(PassiveCodeContext context) : base(context, 705) { }

        protected override void ApplyEffects()
        {
            float Bonus() => RewardT3Util.InColumn(Caster, front: true)
                ? RewardT3Util.CountAlliesInColumn(Caster, front: false, excludeHolder: true) * 0.05f
                : 0f;
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Attack, Bonus, percent: true), "_atk",
                category: BaseEnums.StatusCategory.Neutral);
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Defense, Bonus, percent: true), "_def",
                category: BaseEnums.StatusCategory.Neutral);
        }
    }

    /// <summary>706 배수의 진 — 전열에 자신 외 아군이 없으면 방어력 +40%, 공격력 +15%.</summary>
    public sealed class LastStandCode : RewardPositionCode
    {
        public LastStandCode(PassiveCodeContext context) : base(context, 706) { }

        protected override void ApplyEffects()
        {
            // 전열 탱커용 코드다 — 보유자 자신이 전열에 서 있고 그 전열에 다른 아군이 없을 때만 켜진다.
            bool Alone() => RewardT3Util.InColumn(Caster, front: true) &&
                            RewardT3Util.CountAlliesInColumn(Caster, front: true, excludeHolder: true) == 0;
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Defense, () => Alone() ? 0.40f : 0f, percent: true),
                "_def", category: BaseEnums.StatusCategory.Neutral);
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Attack, () => Alone() ? 0.15f : 0f, percent: true),
                "_atk", category: BaseEnums.StatusCategory.Neutral);
        }
    }

    /// <summary>707 저격수 — 후열에서 적 후열을 공격하면 피해 +25%, 방어 10% 무시.</summary>
    public sealed class SniperCode : RewardPassiveCode
    {
        public SniperCode(PassiveCodeContext context) : base(context, 707) { }

        protected override void OnCast()
            => AddStatus(Caster, new SniperEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class SniperEffect : BaseEffect
        {
            public SniperEffect() : base(0) { }

            private bool Applies(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && target != null && target.IsEnemy != attacker.IsEnemy &&
                   RewardT3Util.IsDirectAttack(context) &&
                   RewardT3Util.InColumn(attacker, front: false) && RewardT3Util.InColumn(target, front: false);

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => Applies(attacker, target, context) ? 1.25f : 1f;

            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => Applies(attacker, target, context) ? 0.9f : 1f;
        }
    }

    /// <summary>708 철벽 — 일반행동마다 방어력 +4%(최대 10중첩), 최대 중첩이면 받는 피해 -10%.</summary>
    public sealed class IronWallCode : RewardPassiveCode
    {
        private const int MaxStacks = 10;
        private int _stacks;
        private bool _subscribed;

        public IronWallCode(PassiveCodeContext context) : base(context, 708) { }

        protected override void OnCast()
        {
            _stacks = 0;
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Defense, () => 0.04f * _stacks, percent: true),
                "_def", category: BaseEnums.StatusCategory.Neutral);
            AddStatus(Caster, new ConditionalReceivingDamageMultiplierEffect(_ => _stacks >= MaxStacks, 0.9f),
                "_dr", category: BaseEnums.StatusCategory.Neutral);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = false;
        }

        private void OnNormalResolved(EventContext context)
        {
            if (!HolderWorking || _stacks >= MaxStacks) return;
            _stacks++;
            // 스탯은 캐시라 중첩이 늘었음을 직접 알려야 한다.
            Caster.RefreshAttributes();
        }
    }

    // ── 709 ~ 711 반응 · 가속 · 정밀 ─────────────────────────────

    /// <summary>709 보루 — 이로운 효과(보호막 포함)를 받으면 2턴간 공격력 +15%, 속도 +6(갱신).</summary>
    public sealed class BulwarkCode : RewardPassiveCode
    {
        private bool _subscribed;

        public BulwarkCode(PassiveCodeContext context) : base(context, 709) { }

        protected override void OnCast()
        {
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnBeneficialEffectReceived, OnBeneficialReceived);
            Unit.AnyShieldGranted += OnAnyShieldGranted;
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnBeneficialEffectReceived, OnBeneficialReceived);
            Unit.AnyShieldGranted -= OnAnyShieldGranted;
            _subscribed = false;
        }

        private void OnBeneficialReceived(EventContext context)
        {
            if (!HolderWorking) return;
            // 보상 코드가 라운드 시작에 거는 상시 상태(오라 · 자기 강화)와 이 코드 자신의 버프가 다시 이 훅을 부르면
            // 시작하자마자 발동하고 스스로 되먹임된다. 방금 붙은 상태(목록의 마지막)가 보상 코드 것이면 무시한다.
            List<UnitStatus> statuses = Caster.GetAllStatuses();
            if (statuses.Count > 0 && statuses[statuses.Count - 1].Key?.StartsWith("reward_") == true) return;
            Trigger();
        }

        private void OnAnyShieldGranted(Unit target, Unit source, int amount)
        {
            if (target != Caster || amount <= 0 || !HolderWorking) return;
            Trigger();
        }

        private void Trigger()
        {
            // 같은 키로 Replace — 다시 받으면 지속이 처음부터 갱신된다.
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Attack, 0.15f, percent: true), "_atk", duration: 2);
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Speed, 6f), "_spd", duration: 2);
        }
    }

    /// <summary>710 주문 가속 — 궁극기 사용 후 2턴간 속도 +15, 그동안 일반행동 마나 획득 2배.</summary>
    public sealed class SpellAccelerationCode : RewardPassiveCode
    {
        private bool _subscribed;

        public SpellAccelerationCode(PassiveCodeContext context) : base(context, 710) { }

        protected override void OnCast()
        {
            ActionScheduler.AnyActionFinished += OnActionFinished;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            ActionScheduler.AnyActionFinished -= OnActionFinished;
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = false;
        }

        private void OnActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (unit != Caster || kind != ActionScheduler.ActionKind.Ultimate || !HolderWorking) return;
            // 속도 상태가 곧 "마나 2배 구간"의 표식이다 — 만료 · 갱신이 한 경로로 정리된다.
            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Speed, 15f), "_spd", duration: 2);
        }

        private void OnNormalResolved(EventContext context)
        {
            if (!HolderWorking || !Caster.HasStatusKey(StatusKey("_spd"))) return;
            // 기본 획득량을 한 번 더 — 마나 효율은 RecoverMana가 적용한다.
            Caster.RecoverMana(Unit.ManaPerNormalAction);
        }
    }

    /// <summary>711 사냥신의 가호 — 체력이 가득하지 않은 적 +15%, 지속피해 중인 적 +15%(조건 하나당 가산).</summary>
    public sealed class HuntersBlessingCode : RewardPassiveCode
    {
        public HuntersBlessingCode(PassiveCodeContext context) : base(context, 711) { }

        protected override void OnCast()
            => AddStatus(Caster, new HuntersBlessingEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class HuntersBlessingEffect : BaseEffect
        {
            public HuntersBlessingEffect() : base(0) { }

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target || target == null || target.IsEnemy == attacker.IsEnemy) return 1f;
                int conditions = (target.HpCurr < target.HpMax ? 1 : 0) + (target.HasDamageOverTimeStatus() ? 1 : 0);
                // 곱이 아니라 합 — 두 조건이 모두 맞아도 1.30이다.
                return 1f + 0.15f * conditions;
            }
        }
    }

    // ── 712 ~ 714 부여 · 원소 · 치유 ─────────────────────────────

    /// <summary>712 원소 침식 — 적에게 원소를 부착하면 대상의 방어력 · 특수방어력 -20%, 2턴.</summary>
    public sealed class HackerCode : RewardPassiveCode
    {
        public HackerCode(PassiveCodeContext context) : base(context, 712) { }

        protected override void OnCast() => Unit.AnyCombatElementGranted += OnElementGranted;

        protected override void OnStop() => Unit.AnyCombatElementGranted -= OnElementGranted;

        private void OnElementGranted(Unit source, Unit target, BaseEnums.UnitElement element)
        {
            if (source != Caster || !HolderWorking || target == null || !target.isActive || target.IsEnemy == Caster.IsEnemy)
                return;
            // 성공한 원소 부착에서 파생한다. 대상마다 키 하나, 이미 통과한 효과 명중은 다시 굴리지 않는다.
            AddStatus(target, new ArmorShredEffect(0.8f), "_shred", duration: 2,
                category: BaseEnums.StatusCategory.Negative, skipEffectHitRoll: true);
        }
    }

    /// <summary>713 원소 친화 - 바위 — 바위를 보유한 동안 접촉 피해 -25%, 그 공격자에게 바위 부착.</summary>
    public sealed class GeoAffinityCode : RewardPassiveCode
    {
        private bool _subscribed;

        public GeoAffinityCode(PassiveCodeContext context) : base(context, 713) { }

        protected override void OnCast()
        {
            AddStatus(Caster, new GeoGuardEffect(), category: BaseEnums.StatusCategory.Neutral);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = false;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            Unit attacker = damage?.Attacker;
            if (attacker == null || !HolderWorking || attacker.IsEnemy == Caster.IsEnemy || !attacker.isActive) return;
            if (!damage.HasTag(DamageTag.ContactAttack) || !Caster.HasCombatElement(BaseEnums.UnitElement.Geo)) return;
            attacker.GrantCombatElement(BaseEnums.UnitElement.Geo, source: Caster);
        }

        private sealed class GeoGuardEffect : BaseEffect
        {
            public GeoGuardEffect() : base(0) { }

            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => unit == Target && context != null && context.HasTag(DamageTag.ContactAttack) &&
                   unit.HasCombatElement(BaseEnums.UnitElement.Geo)
                    ? 0.75f
                    : 1f;
        }
    }

    /// <summary>714 응급치료 — 실제 치유 시 대상마다 자신의 턴당 1회, 방어력 35% 보호막을 더한다.</summary>
    public sealed class FirstAidCode : RewardPassiveCode
    {
        private readonly TargetTurnCooldown _cooldown = new(1);
        public FirstAidCode(PassiveCodeContext context) : base(context, 714) { }
        protected override void OnCast() { _cooldown.Clear(); Unit.AnyHealed += OnAnyHealed; }
        protected override void OnStop() { Unit.AnyHealed -= OnAnyHealed; _cooldown.Clear(); }
        private void OnAnyHealed(Unit target, Unit source, int effective, int overflow)
        {
            if (source != Caster || effective <= 0 || !HolderWorking || target == null || !target.isActive || !_cooldown.TryUse(Caster, target)) return;
            target.AddShield(Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Defense) * 0.35f), Caster);
        }
    }

    // ── 715 ~ 716 치명 ───────────────────────────────────────────

    /// <summary>715 화무십일홍 — 체력 15% 미만인 적에게 확정 치명타, 치명타 피해에 치명타 확률의 2배를 더한다.</summary>
    public sealed class FadingFlowerCode : RewardPassiveCode
    {
        public FadingFlowerCode(PassiveCodeContext context) : base(context, 715) { }

        protected override void OnCast()
            => AddStatus(Caster, new FadingFlowerEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class FadingFlowerEffect : BaseEffect
        {
            public FadingFlowerEffect() : base(0) { }

            private bool Applies(Unit attacker, Unit target)
                => attacker == Target && target != null && target.IsEnemy != attacker.IsEnemy &&
                   target.HpMax > 0 && target.HpCurr * 100 < target.HpMax * 15;

            public override bool ForcesCriticalAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType)
                => Applies(attacker, target);

            public override float CritDamageBonusAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType)
                => Applies(attacker, target) ? attacker.CritChanceCurr * 2f : 0f;
        }
    }

    /// <summary>716 기습 — 체력이 가득한 적을 공격하면 확정 치명타.</summary>
    public sealed class AmbushCode : RewardPassiveCode
    {
        public AmbushCode(PassiveCodeContext context) : base(context, 716) { }

        protected override void OnCast()
            => AddStatus(Caster, new AmbushEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class AmbushEffect : BaseEffect
        {
            public AmbushEffect() : base(0) { }

            public override bool ForcesCriticalAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType)
                => attacker == Target && target != null && target.IsEnemy != attacker.IsEnemy &&
                   target.HpMax > 0 && target.HpCurr >= target.HpMax;
        }
    }

    // ── 717 ~ 719 연쇄 · 추가 피해 · 보호막 ──────────────────────

    // 717은 T4로 재밸런싱했으며 저장 ID를 유지한다(ToughnessRewardCodes).

    /// <summary>718 폭죽놀이 — 4번째 일반행동마다 무작위 적에게 공격력 20% 피해 4발(덧붙는 피해).</summary>
    public sealed class FireworksCode : RewardPassiveCode
    {
        private const int ActionsPerBurst = 4;
        private const int ShotCount = 4;
        private int _actions;
        private bool _subscribed;

        public FireworksCode(PassiveCodeContext context) : base(context, 718) { }

        protected override void OnCast()
        {
            _actions = 0;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
            _subscribed = false;
        }

        private void OnNormalResolved(EventContext context)
        {
            if (!HolderWorking) return;
            _actions++;
            if (_actions % ActionsPerBurst != 0) return;

            for (int shot = 0; shot < ShotCount; shot++)
            {
                // 발마다 대상을 새로 뽑는다 — 앞 발에 쓰러진 적을 다시 겨누지 않는다.
                List<Unit> enemies = FieldEnemies().Where(enemy => enemy.HpCurr > 0).ToList();
                if (enemies.Count == 0) break;
                Unit target = enemies[Random.Range(0, enemies.Count)];

                int damage = StatDamage(0f, BaseEnums.StatType.Attack, 0.20f);
                if (damage <= 0) continue;
                bool crit = RollCritical(target, out float multiplier);
                target.TakeDamage(new DamageContext(Caster, Mathf.RoundToInt(damage * multiplier), CodeType,
                    new List<int> { DamageTag.SingleTarget, DamageTag.PassiveAttack, DamageTag.NonContactAttack, DamageTag.BonusDamage },
                    crit));
            }
        }
    }

    /// <summary>719 과다치유 — 초과 치유를 받으면 자신의 턴당 1회, 방어력 40% 보호막을 더한다.</summary>
    public sealed class OverhealCode : RewardPassiveCode
    {
        private readonly TurnCooldown _cooldown = new(1);
        public OverhealCode(PassiveCodeContext context) : base(context, 719) { }
        protected override void OnCast() { _cooldown.Reset(); Unit.AnyHealed += OnAnyHealed; }
        protected override void OnStop() { Unit.AnyHealed -= OnAnyHealed; _cooldown.Reset(); }
        private void OnAnyHealed(Unit target, Unit source, int effective, int overflow)
        {
            if (target != Caster || overflow <= 0 || !HolderWorking || !_cooldown.TryUse(Caster)) return;
            Caster.AddShield(Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Defense) * 0.40f), Caster);
        }
    }

    // ── 720 ~ 721 파티 오라 ──────────────────────────────────────

    /// <summary>720 봉인진 — 필드에 있으면 행동불능 적에게 아군 전원 피해 +15%. 여러 명이 들면 높은 하나만.</summary>
    public sealed class SealingCircleCode : RewardPassiveCode, IRewardAuraSource
    {
        private const string Family = "sealing";

        public SealingCircleCode(PassiveCodeContext context) : base(context, 720) { }

        public string AuraFamily => Family;
        public float AuraValue => IsActive ? 0.15f : 0f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new SealingAuraEffect(), key: RewardAura.KeyOf(Family),
                    category: BaseEnums.StatusCategory.Neutral);
        }

        private sealed class SealingAuraEffect : BaseEffect
        {
            public SealingAuraEffect() : base(0) { }

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target || target == null || target.IsEnemy == attacker.IsEnemy || !target.isControlled)
                    return 1f;
                float value = RewardAura.Best(Target, Family);
                return value > 0f ? 1f + value : 1f;
            }
        }
    }

    /// <summary>721 소환사 — 필드에 있으면 아군 소환수 피해 +30%(소환술사의 상위, 같은 계열 오라).</summary>
    public sealed class SummonerCode : RewardPassiveCode, IRewardAuraSource
    {
        private const string Family = "summon_damage";

        public SummonerCode(PassiveCodeContext context) : base(context, 721) { }

        public string AuraFamily => Family;
        public float AuraValue => IsActive ? 0.30f : 0f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new SummonAuraEffect(), key: RewardAura.KeyOf(Family),
                    category: BaseEnums.StatusCategory.Neutral);
        }

        private sealed class SummonAuraEffect : BaseEffect
        {
            public SummonAuraEffect() : base(0) { }

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target) return 1f;
                float value = RewardAura.Best(Target, Family);
                if (value <= 0f) return 1f;
                // 소환수가 낸 피해는 공격자를 소환자로 기록하므로 태그도 함께 본다.
                bool summon = attacker.IsSummon || attacker.CountsAsSummon ||
                              (context != null && context.HasTag(DamageTag.SummonAttack));
                return summon ? 1f + value : 1f;
            }
        }
    }

    // ── 722 ~ 725 대가 · 형태별 강화 ─────────────────────────────

    /// <summary>722 유리대포 — 최대 체력 -20%, 방어 20% 무시.</summary>
    public sealed class GlassCannonCode : RewardPassiveCode
    {
        public GlassCannonCode(PassiveCodeContext context) : base(context, 722) { }

        // 최대 체력 감소는 전투 밖 캐릭터 창에도 보여야 한다.
        public override float PermanentStatPercentModifier(BaseEnums.StatType stat)
            => stat == BaseEnums.StatType.MaxHp ? -0.20f : 0f;

        protected override void OnCast()
            => AddStatus(Caster, new ArmorPiercingEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class ArmorPiercingEffect : BaseEffect
        {
            public ArmorPiercingEffect() : base(0) { }

            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target ? 0.8f : 1f;
        }
    }

    /// <summary>723 결전의 일격 — 궁극기 피해 +25%, 궁극기 치명타 확률 +15%p.</summary>
    public sealed class ThunderStrikeCode : RewardPassiveCode
    {
        public ThunderStrikeCode(PassiveCodeContext context) : base(context, 723) { }

        protected override void OnCast()
        {
            AddStatus(Caster, new UltimateDamageBonusEffect(.25f),
                "_dmg", category: BaseEnums.StatusCategory.Neutral);
            AddStatus(Caster, new UltimateCritEffect(), "_crit", category: BaseEnums.StatusCategory.Neutral);
        }

        private sealed class UltimateCritEffect : BaseEffect
        {
            public UltimateCritEffect() : base(0) { }

            public override float CritChanceBonusAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType)
                => attacker == Target && codeType == BaseEnums.CodeType.Ultimate ? 0.15f : 0f;
        }
    }

    /// <summary>724 연환격 — 추가행동 피해 +25%, 추가행동을 마칠 때마다 마나 +5.</summary>
    public sealed class ChainStrikeCode : RewardPassiveCode
    {
        public ChainStrikeCode(PassiveCodeContext context) : base(context, 724) { }

        protected override void OnCast()
        {
            AddStatus(Caster, new ConditionalOutgoingDamageMultiplierEffect(
                    context => context != null && context.HasTag(DamageTag.AdditionalAttack), 1.25f),
                category: BaseEnums.StatusCategory.Neutral);
            ActionScheduler.AnyActionFinished += OnActionFinished;
        }

        protected override void OnStop() => ActionScheduler.AnyActionFinished -= OnActionFinished;

        private void OnActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (unit != Caster || !HolderWorking || !ActionScheduler.IsAdditional(kind)) return;
            Caster.RecoverMana(5);
        }
    }

    /// <summary>725 응징 — 반격 피해 +30%, 반격은 대상 방어 15% 무시.</summary>
    public sealed class RetributionCode : RewardPassiveCode
    {
        public RetributionCode(PassiveCodeContext context) : base(context, 725) { }

        protected override void OnCast()
            => AddStatus(Caster, new RetributionEffect(), category: BaseEnums.StatusCategory.Neutral);

        private sealed class RetributionEffect : BaseEffect
        {
            public RetributionEffect() : base(0) { }

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && context != null && context.HasTag(DamageTag.CounterAttack) ? 1.30f : 1f;

            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && context != null && context.HasTag(DamageTag.CounterAttack) ? 0.85f : 1f;
        }
    }

    // ── 726 ~ 727 도발 · 성장 ────────────────────────────────────

    /// <summary>726 도발의 문장 — 대상 우선도 +1, 적의 공격 대상이 될 때마다 2턴간 방어력 +5%(최대 5중첩).</summary>
    public sealed class BannerEmblemCode : RewardPassiveCode
    {
        private const int MaxStacks = 5;
        private const int StackTurns = 2;
        private bool _subscribed;

        public BannerEmblemCode(PassiveCodeContext context) : base(context, 726) { }

        protected override void OnCast()
        {
            AddStatus(Caster, new TargetPriorityModifierEffect(1), "_prio", category: BaseEnums.StatusCategory.Neutral);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnTargeted, OnTargeted);
            _subscribed = true;
        }

        protected override void OnStop()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnTargeted, OnTargeted);
            _subscribed = false;
        }

        private void OnTargeted(EventContext context)
        {
            Unit attacker = context?.Grantor;
            if (!HolderWorking || attacker == null || attacker.IsEnemy == Caster.IsEnemy) return;

            string key = StatusKey("_def");
            List<UnitStatus> stacks = Caster.GetAllStatuses().Where(status => status.Key == key).ToList();
            if (stacks.Count >= MaxStacks)
            {
                // 가득 찼으면 새로 쌓지 않고 가장 곧 끝날 중첩의 지속만 되살린다.
                UnitStatus oldest = stacks.OrderBy(status => status.RemainingTurns).First();
                oldest.Duration = oldest.ElapsedTurns + StackTurns;
                return;
            }

            AddStatus(Caster, new StatModifierEffect(BaseEnums.StatType.Defense, 0.05f, percent: true), "_def",
                duration: StackTurns, stackPolicy: BaseEnums.StatusStackPolicy.Stack);
        }
    }

    /// <summary>727 축적의 촉진 — 자신이 얻는 영구 중첩 +50%. 소수 모으기는 영구 중첩 쪽이 처리한다.</summary>
    public sealed class MultiStackCode : RewardPassiveCode
    {
        public MultiStackCode(PassiveCodeContext context) : base(context, 727) { }

        public override float PermanentStackGainMultiplier(string key) => 1.5f;

        // 영구 중첩 배율이 전부라 전투 중에 할 일이 없다.
        protected override void OnCast() { }
    }
}
