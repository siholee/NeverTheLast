using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    internal static class VedicIds
    {
        // 상태 ID 대역 (6000~) — 베다 진영 전용
        public const int YamaCurse = 6000;
        public const int ElementMastery = 6001;
        public const int HighVoltage = 6003;
        public const int AgniFlame = 6004;
        public const int Archmage = 6005;
        public const int IndraMark = 6007;
        public const int IndraOverconfidence = 6008;
        public const int VayuCharge = 6009;
        public const int VayuSecondWind = 6010;
        public const int VayuAmbush = 6011;
        public const int DragonSlayer = 6012;
        public const int Wisdom = 6013;
        public const int Genius = 6014;
        public const int Frontrunner = 6015;
        public const int ElementMasteryHydro = 6016;
        public const int ElementMasteryAnemo = 6017;
        public const int ElementMasteryDendro = 6018;
        public const int ElementMasteryGeo = 6019;
    }

    // ══════════════════════════════════════════════════════════════
    // 야마 — 지속피해 코어
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 야마 고유 P — 죽음의 계약.
    /// 필드에 있는 동안 아군 전체의 지속피해 부여량 +25%.
    /// 츠쿠요미의 '저주'(1016)의 상위 코드로, 둘이 겹치면 높은 쪽만 적용된다.
    /// 전수될 때는 원본 대신 저주(1016)가 전수된다.
    /// </summary>
    public sealed class YamaDeathContract : UniquePassiveCode
    {
        public const int CodeId = 261;

        /// <summary>지속피해 증폭 계열의 공용 상태 키. 같은 키를 쓰면 중복 적용되지 않는다.</summary>
        public const string SharedKey = "dot_amplify_aura";

        private const float Multiplier = 1.30f;

        public YamaDeathContract(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "죽음의 계약";
            IgnoresActivationChance = true;
            // 지속피해 증폭 계열의 강화 등급. 같은 계열의 일반 등급과 겹치면 이쪽만 남는다.
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                // ReplaceIfStronger — 저주(1.20)와 겹쳐도 높은 쪽만 남는다.
                ally.AddStatus(BuffStatus.Create(
                    VedicIds.YamaCurse, SharedKey, CodeName, Caster, ally,
                    new DamageOverTimeApplicationEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true,
                    description: "부여하는 지속피해량이 30% 증가합니다."));
            }
        }
    }

    /// <summary>Lv.4 지혜 — INT +4.</summary>
    /// <summary>Lv.? 지혜 — INT 비례 증가. 고정 가산은 고레벨에서 무의미해져 배율로 바꿨다.</summary>
    public sealed class VedicWisdom : PassiveCode
    {
        private const float WisdomMultiplier = 1.03f;

        public VedicWisdom(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "지혜";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                VedicIds.Wisdom, "vedic_wisdom", CodeName, Caster, Caster,
                new PrimaryStatMultiplierEffect(WisdomMultiplier, BaseEnums.PrimaryStat.INT),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true, description: "INT +3%"));
        }
    }

    /// <summary>
    /// 원소 숙련 — <b>자신의 원소</b>를 보유한 적에게 주는 피해 +10%.
    ///
    /// 예전에는 원소마다 코드가 따로여서 일곱 개였다. 어느 캐릭터든 자기 원소짜리 하나만
    /// 골라 들었으므로 갈라 둘 이유가 없었고, 새 원소가 늘 때마다 코드도 함께 늘었다.
    /// 보유자의 원소를 읽는 방식으로 합쳤다 — 상위 코드 <c>원소 정통</c>(1593)과 같은 축이다.
    /// </summary>
    public sealed class ElementMastery : PassiveCode
    {
        private const float Bonus = 0.10f;

        public ElementMastery(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 숙련";
            IgnoresActivationChance = true;
            SupersededByCodeId = VoidElementalMastery.CodeId;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            VedicIds.ElementMastery, "element_mastery", CodeName, Caster, Caster,
            new SelfElementMasteryEffect(Bonus),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: $"자신의 원소를 보유한 적에게 주는 피해 +{Bonus * 100f:F0}%."));
    }

    /// <summary>Lv.44 고전압 — 감전을 생성하면 주는 피해 +25%(3턴).</summary>
    public sealed class YamaHighVoltage : PassiveCode
    {
        private const int Duration = 3;
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        public YamaHighVoltage(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "고전압";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            Unit.AnyElementalReaction += OnReaction;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyElementalReaction -= OnReaction;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnReaction(Unit source, Unit target, string reactionName)
        {
            if (source != Caster || reactionName != "감전") return;
            Caster.AddStatus(BuffStatus.Create(
                VedicIds.HighVoltage, "yama_high_voltage", CodeName, Caster, Caster,
                new OutgoingDamageMultiplierEffect(1.25f),
                duration: Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true, description: "주는 피해 +25%"));
        }
    }

    /// <summary>Lv.65 충전 — 감전 피해가 들어가면 마나를 회복한다(2턴 재사용 대기).</summary>
    public sealed class YamaCharge : PassiveCode
    {
        private const int ChargeCooldownTurns = 2;
        private const int ManaGain = 12;

        private readonly Combat.TurnCooldown _cooldown = new(ChargeCooldownTurns);
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public YamaCharge(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "충전";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _cooldown.Reset();
            if (Caster == null || _registered) return;
            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (!_cooldown.IsReady(Caster)) return;
            if (context?.DamageContext?.CodeType != BaseEnums.CodeType.Effect) return;
            if (context.Target == null ||
                !context.Target.HasStatus(Effects.Negative.ElementalReaction.ShockStatusId)) return;

            _cooldown.Use(Caster);
            Caster.RecoverMana(ManaGain);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 아그니
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 아그니 고유 P — 영원히 타오르는 불꽃.
    /// 필드의 불 원소 아군이 주는 피해 +20%. 동일 계열끼리는 높은 쪽만 적용된다.
    /// </summary>
    public sealed class AgniEternalFlame : UniquePassiveCode
    {
        public const string SharedKey = "fire_ally_damage_aura";
        private const float Multiplier = 1.2f;

        public AgniEternalFlame(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "영원히 타오르는 불꽃";
            IgnoresActivationChance = true;
            // 불 원소 아군 피해 계열의 강화 등급.
            // 고유 패시브라 등급은 Unique다(은·금 사다리 밖).
            
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            foreach (Unit ally in Target.GetAllAllies(Caster)
                         .Where(unit => unit != null && unit.isActive &&
                                        unit.HasCombatElement(BaseEnums.UnitElement.Pyro)))
            {
                ally.AddStatus(BuffStatus.Create(
                    VedicIds.AgniFlame, SharedKey, CodeName, Caster, ally,
                    new OutgoingDamageMultiplierEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "가하는 피해 +20%"));
            }
        }
    }

    /// <summary>Lv.70 대마법사 — 보주 숙련 아군의 특수 피해 치명타 피해 +25%.</summary>
    public sealed class AgniArchmage : PassiveCode
    {
        public const string SharedKey = "orb_crit_damage_aura";

        public AgniArchmage(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "대마법사";
            IgnoresActivationChance = true;
            // 보주 치명타 피해 계열의 강화 등급.
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            foreach (Unit ally in Target.GetAllAllies(Caster)
                         .Where(unit => unit != null && unit.isActive &&
                                        unit.HasProficiency(EquipmentProficiency.Orb)))
            {
                ally.AddStatus(BuffStatus.Create(
                    VedicIds.Archmage, SharedKey, CodeName, Caster, ally,
                    new CritMultiplierBonusEffect(0.25f),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "특수 피해의 치명타 피해 +25%"));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 인드라
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 인드라 고유 P — 뇌정 각인.
    /// 적을 때릴 때마다 2턴 유지되는 표식을 남기고, 4중첩에서 터뜨려
    /// INT의 40%에 해당하는 고정 피해를 준다. 치명타가 적용되지 않는다.
    /// </summary>
    public sealed class IndraThunderMark : UniquePassiveCode
    {
        private const int MarkThreshold = 4;
        private const int MarkDurationTurns = 2;
        private const int BurstPower = 40;

        /// <summary>대상별 (중첩 수, 마지막으로 쌓은 인드라의 턴). 인드라 턴 기준으로 만료를 잰다.</summary>
        private readonly Dictionary<Unit, (int count, int stackedAtTurn)> _marks = new();
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public IndraThunderMark(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "뇌정 각인";
            IgnoresActivationChance = true;
            Transferable = false;   // <전수 불가능>
        }

        public override void CastCode()
        {
            _marks.Clear();
            if (Caster == null || _registered) return;
            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            _marks.Clear();
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            Unit target = context?.Target;
            if (target == null || !target.isActive || context.DamageDealt <= 0) return;
            // 표식이 터뜨린 고정 피해가 다시 표식을 쌓지 않게 한다.
            if (context.DamageContext?.DamageTags?.Contains(DamageTag.TrueDamage) == true) return;

            int count = 1;
            if (_marks.TryGetValue(target, out var mark) &&
                Caster.TurnCount - mark.stackedAtTurn < MarkDurationTurns)
            {
                count = mark.count + 1;
            }

            if (count >= MarkThreshold)
            {
                _marks.Remove(target);
                int damage = Mathf.Max(1, Caster.SkillDamage(BurstPower, BaseEnums.PrimaryStat.INT));
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Passive,
                    new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage, DamageTag.Special }));
                return;
            }

            _marks[target] = (count, Caster.TurnCount);
        }
    }

    /// <summary>Lv.11 선두주자 — 전투 종료 후 획득 EXP +30%.</summary>
    public sealed class IndraFrontrunner : PassiveCode
    {
        public const float ExpBonus = 0.30f;

        public IndraFrontrunner(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "선두주자";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>Lv.20 천재 — INT 집중 훈련 효율 +10%.</summary>
    public sealed class IndraGenius : PassiveCode
    {
        public const float TrainingBonus = 0.10f;

        public IndraGenius(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "천재";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>Lv.30 용살자 — '용' 속성 유닛에게 주는 피해 +40%.</summary>
    public sealed class IndraDragonSlayer : PassiveCode
    {
        public IndraDragonSlayer(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "용살자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                VedicIds.DragonSlayer, "indra_dragon_slayer", CodeName, Caster, Caster,
                new TaggedTargetDamageEffect("Dragon", 1.4f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true, description: "'용' 속성 유닛에게 주는 피해 +40%"));
        }
    }

    /// <summary>Lv.45 자기과신 — 적 처치마다 특수 피해 +5%. 중첩된다.</summary>
    public sealed class IndraOverconfidence : PassiveCode
    {
        private int _stacks;
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        public IndraOverconfidence(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "자기과신";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _stacks = 0;
            if (Caster == null || _registered) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnAnyUnitDied(Unit dead, Unit killer)
        {
            if (killer != Caster || dead == null || dead.IsEnemy == Caster.IsEnemy) return;

            _stacks++;
            Caster.AddStatus(BuffStatus.Create(
                VedicIds.IndraOverconfidence, "indra_overconfidence", CodeName, Caster, Caster,
                new SpecialDamageEffect(1f + _stacks * 0.05f),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"특수 피해 +{_stacks * 5}%"));
        }
    }

    /// <summary>Lv.70 인도자 — 궁극기 발동 후 다른 아군에게 마나를 나눠 준다.</summary>
    public sealed class IndraGuide : PassiveCode
    {
        private const int ManaGrant = 20;

        private bool _registered;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;

        public IndraGuide(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "인도자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _ultimateHandler = _ => GrantMana();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void GrantMana()
        {
            foreach (Unit ally in Target.GetAllAllies(Caster)
                         .Where(unit => unit != null && unit != Caster && unit.isActive))
            {
                ally.RecoverMana(ManaGrant);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 바유
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 바유 고유 P — 정화의 바람.
    /// 궁극기를 쓸 때마다 충전 1(최대 2). 충전이 있고 디버프에 걸린 아군이 있으면
    /// 충전을 소모해 그 아군의 해로운 효과를 지운다.
    /// 전수본은 충전 0.5씩, 최대 1까지만 쌓인다.
    /// </summary>
    public class VayuPurifyingWind : UniquePassiveCode
    {
        private readonly float _chargePerCast;
        private readonly float _maxCharge;

        private float _charge;
        private bool _registered;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;

        public VayuPurifyingWind(PassiveCodeContext context) : this(context, 1f, 2f)
        {
        }

        private VayuPurifyingWind(PassiveCodeContext context, float chargePerCast, float maxCharge)
            : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "정화의 바람";
            IgnoresActivationChance = true;
            _chargePerCast = chargePerCast;
            _maxCharge = maxCharge;
        }

        public override void CastCode()
        {
            _charge = 0f;
            if (Caster == null || _registered) return;
            _ultimateHandler = _ => OnUltimate();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnUltimate()
        {
            _charge = Mathf.Min(_maxCharge, _charge + _chargePerCast);
            TryCleanse();
        }

        /// <summary>
        /// 충전을 태워 정화하는 것은 <b>추가행동</b>이다.
        ///
        /// 예전에는 궁극기가 끝나는 자리에서 곧바로 지워 버렸다. 그러면 정화가 어느 행동에도
        /// 속하지 않아 화면에 잡히지 않고, 추가행동을 세는 코드(니콜·바스테트)도 놓쳤다.
        /// 이제 큐에 한 번 올린 뒤 자기 차례에 지운다 — 예약과 실행이 나뉘므로
        /// <b>대상은 실행 시점에 다시 고른다.</b> 줄 서는 사이에 다른 아군이 정화됐을 수 있다.
        /// </summary>
        private void TryCleanse()
        {
            if (_charge < 1f || Caster == null || !Caster.isActive) return;
            if (!HasAfflictedAlly()) return;

            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                Caster, "vayu_purifying_wind", CodeName, ResolveCleanse);
        }

        private bool HasAfflictedAlly()
            => Target.GetAllAllies(Caster)
                .Any(unit => unit != null && unit.isActive && unit.HasNegativeStatus());

        private void ResolveCleanse()
        {
            if (Caster == null || !Caster.isActive || _charge < 1f) return;

            Unit afflicted = Target.GetAllAllies(Caster)
                .FirstOrDefault(unit => unit != null && unit.isActive && unit.HasNegativeStatus());
            // 줄 서 있는 사이에 아무도 안 아프게 됐다면 충전을 태우지 않는다.
            if (afflicted == null) return;

            _charge -= 1f;
            afflicted.RemoveAllNegativeStatuses();
            Debug.Log($"[정화의 바람] {afflicted.UnitName}의 해로운 효과 제거 (남은 충전 {_charge:0.#})");
        }
    }

    /// <summary>Lv.13 재기의 바람 — 체력 40% 이하로 떨어지면 2턴에 걸쳐 40% 회복. 전투당 1회.</summary>
    public sealed class VayuSecondWind : PassiveCode
    {
        public VayuSecondWind(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "재기의 바람";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                VedicIds.VayuSecondWind, "vayu_second_wind", CodeName, Caster, Caster,
                new ThresholdRegenEffect(0.40f, 0.40f, 4f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "체력 40% 이하로 떨어지면 2턴에 걸쳐 최대 체력의 40%를 회복합니다. 전투당 1회."));
        }
    }

    /// <summary>Lv.30 기습 — 체력이 가득 찬 대상을 때리면 확정 치명타 + 치명타 확률×2를 치명타 피해에 가산.</summary>
    public sealed class VayuAmbush : PassiveCode
    {
        public VayuAmbush(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "기습";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                VedicIds.VayuAmbush, "vayu_ambush", CodeName, Caster, Caster,
                new FullHealthCritEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "최대 체력인 적을 공격하면 확정 치명타가 발생합니다."));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 효과
    // ══════════════════════════════════════════════════════════════


    internal sealed class SpecialDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public SpecialDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.Special) == true ? _multiplier : 1f;
    }

    internal sealed class TaggedTargetDamageEffect : BaseEffect
    {
        private readonly string _tag;
        private readonly float _multiplier;

        public TaggedTargetDamageEffect(string tag, float multiplier) : base(0, multiplier)
        {
            _tag = tag;
            _multiplier = multiplier;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.HasUnitTag(_tag) ? _multiplier : 1f;
    }

    /// <summary>
    /// 레벨 성장분에만 곱해지는 배율.
    /// 성장분을 직접 곱하는 훅이 없으므로, 성장분의 (배율−1)만큼을 가산치로 되돌려 같은 결과를 만든다.
    /// </summary>
    internal sealed class GrowthMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;
        public GrowthMultiplierEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target) return 0;
            int growth = unit.GetGrowthStatValue(stat);
            return Mathf.RoundToInt(growth * (_multiplier - 1f));
        }
    }

    /// <summary>체력이 임계값 아래로 떨어지면 일정 시간에 걸쳐 회복한다. 전투당 1회.</summary>
    internal sealed class ThresholdRegenEffect : BaseEffect
    {
        private readonly float _threshold;
        private readonly float _healRatio;
        private readonly float _duration;

        private bool _used;
        private int _remainingTurns;

        /// <summary>지속 턴 수.</summary>
        private int DurationTurns => Mathf.Max(1, Mathf.RoundToInt(_duration * 0.5f));

        public ThresholdRegenEffect(float threshold, float healRatio, float duration) : base(0)
        {
            _threshold = threshold;
            _healRatio = healRatio;
            _duration = duration;
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive || Target.HpMax <= 0) return;

            if (!_used && _remainingTurns <= 0 && (float)Target.HpCurr / Target.HpMax <= _threshold)
            {
                _used = true;
                _remainingTurns = DurationTurns;
            }

            if (_remainingTurns <= 0) return;

            _remainingTurns--;
            int healPerTurn = Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _healRatio / DurationTurns));
            Target.ModifyHp(Mathf.Min(Target.HpMax, Target.HpCurr + healPerTurn));
        }
    }

    /// <summary>체력이 가득 찬 대상을 때리면 확정 치명타로 만들고 치명타 피해를 부풀린다.</summary>
    internal sealed class FullHealthCritEffect : BaseEffect
    {
        public FullHealthCritEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || target.HpMax <= 0 || context == null) return 1f;
            if (context.CodeType == BaseEnums.CodeType.Effect) return 1f;
            if (target.HpCurr < target.HpMax) return 1f;

            float normalCrit = Mathf.Max(1f, attacker.CritMultiplierCurr);
            float desired = normalCrit + attacker.CritChanceCurr * 2f;
            float applied = context.IsCrit ? normalCrit : 1f;
            context.IsCrit = true;
            return desired / applied;
        }
    }
}
