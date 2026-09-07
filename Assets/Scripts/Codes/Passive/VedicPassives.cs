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
        public const int ElementMasteryElectro = 6001;
        public const int ElementMasteryPyro = 6002;
        public const int HighVoltage = 6003;
        public const int AgniFlame = 6004;
        public const int Archmage = 6005;
        public const int AllOrNothing = 6006;
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
                    new DotAmplifyEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true,
                    description: "부여하는 지속피해량이 30% 증가합니다."));
            }
        }
    }

    /// <summary>Lv.4 지혜 — INT +4.</summary>
    public sealed class VedicWisdom : PassiveCode
    {
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
                new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.INT, 4),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true, description: "INT +4"));
        }
    }

    /// <summary>원소 숙련 — 지정 원소를 가진 적에게 주는 피해 +10%.</summary>
    public abstract class ElementMasteryPassive : PassiveCode
    {
        private readonly BaseEnums.UnitElement _element;
        private readonly int _statusId;
        private readonly string _key;

        protected ElementMasteryPassive(PassiveCodeContext context, string name,
            BaseEnums.UnitElement element, int statusId, string key) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
            _element = element;
            _statusId = statusId;
            _key = key;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                _statusId, _key, CodeName, Caster, Caster,
                new ElementMasteryEffect(_element),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"{_element} 원소를 보유한 적에게 주는 피해 +10%"));
        }
    }

    public sealed class ElectroMastery : ElementMasteryPassive
    {
        public ElectroMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 번개", BaseEnums.UnitElement.Electro,
            VedicIds.ElementMasteryElectro, "mastery_electro") { }
    }

    public sealed class PyroMastery : ElementMasteryPassive
    {
        public PyroMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 불", BaseEnums.UnitElement.Pyro,
            VedicIds.ElementMasteryPyro, "mastery_pyro") { }
    }

    public sealed class HydroMastery : ElementMasteryPassive
    {
        public HydroMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 물", BaseEnums.UnitElement.Hydro,
            VedicIds.ElementMasteryHydro, "mastery_hydro") { }
    }

    public sealed class AnemoMastery : ElementMasteryPassive
    {
        public AnemoMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 바람", BaseEnums.UnitElement.Anemo,
            VedicIds.ElementMasteryAnemo, "mastery_anemo") { }
    }

    public sealed class DendroMastery : ElementMasteryPassive
    {
        public DendroMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 풀", BaseEnums.UnitElement.Dendro,
            VedicIds.ElementMasteryDendro, "mastery_dendro") { }
    }

    public sealed class GeoMastery : ElementMasteryPassive
    {
        public GeoMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 바위", BaseEnums.UnitElement.Geo,
            VedicIds.ElementMasteryGeo, "mastery_geo") { }
    }

    /// <summary>Lv.44 고전압 — 감전을 생성하면 주는 피해 +25%(6초).</summary>
    public sealed class YamaHighVoltage : PassiveCode
    {
        private const int Duration = 3;   // 6초 → 3턴
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
                new FlatOutgoingDamageEffect(1.25f),
                duration: Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true, description: "주는 피해 +25%"));
        }
    }

    /// <summary>Lv.65 충전 — 감전 피해가 들어가면 마나를 회복한다(4초 재사용 대기).</summary>
    public sealed class YamaCharge : PassiveCode
    {
        private const float ChargeCooldown = 2;
        private const int ManaGain = 12;

        private float _readyAt;
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
            _readyAt = 0f;
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
            if (Time.time < _readyAt) return;
            if (context?.DamageContext?.CodeType != BaseEnums.CodeType.Effect) return;
            if (context.Target == null ||
                !context.Target.HasStatus(Effects.Negative.ElementalReaction.ShockStatusId)) return;

            _readyAt = Time.time + ChargeCooldown;
            Caster.RecoverMana(ManaGain);
        }
    }

    /// <summary>
    /// Lv.80 전부 아니면 전무 — 덱 전원이 로카팔라이거나 자신만 로카팔라일 때
    /// 레벨 성장분이 1.2배가 된다.
    /// </summary>
    public sealed class AllOrNothing : PassiveCode
    {
        public const string LokapalaTag = "Lokapala";

        public AllOrNothing(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "전부 아니면 전무";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            List<Unit> allies = Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit.isActive).ToList();
            int lokapala = allies.Count(unit => unit.HasUnitTag(LokapalaTag));

            bool allLokapala = lokapala == allies.Count;
            bool onlySelf = lokapala == 1 && Caster.HasUnitTag(LokapalaTag);
            if (!allLokapala && !onlySelf) return;

            Caster.AddStatus(BuffStatus.Create(
                VedicIds.AllOrNothing, "all_or_nothing", CodeName, Caster, Caster,
                new GrowthMultiplierEffect(1.2f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "레벨에 따른 성장 스탯이 1.2배가 됩니다."));
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
            Grade = BaseEnums.CodeGrade.Enhanced;
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
                    new FlatOutgoingDamageEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "가하는 피해 +20%"));
            }
        }
    }

    /// <summary>Lv.71 대마법사 — 보주 숙련 아군의 특수 피해 치명타 피해 +25%.</summary>
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
                    new SpecialCritDamageEffect(0.25f),
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
    /// 적을 때릴 때마다 4초 유지되는 표식을 남기고, 4중첩에서 터뜨려
    /// INT의 40%에 해당하는 고정 피해를 준다. 치명타가 적용되지 않는다.
    /// </summary>
    public sealed class IndraThunderMark : UniquePassiveCode
    {
        private const int MarkThreshold = 4;
        private const float MarkDuration = 4f;
        private const int BurstPower = 40;

        private readonly Dictionary<Unit, (int count, float expireAt)> _marks = new();
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
            if (_marks.TryGetValue(target, out var mark) && Time.time < mark.expireAt)
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

            _marks[target] = (count, Time.time + MarkDuration);
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

    /// <summary>Lv.92 인도자 — 궁극기 발동 후 다른 아군에게 마나를 나눠 준다.</summary>
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

        private void TryCleanse()
        {
            if (_charge < 1f) return;

            Unit afflicted = Target.GetAllAllies(Caster)
                .FirstOrDefault(unit => unit != null && unit.isActive && unit.HasNegativeStatus());
            if (afflicted == null) return;

            _charge -= 1f;
            afflicted.RemoveAllNegativeStatuses();
            Debug.Log($"[정화의 바람] {afflicted.UnitName}의 해로운 효과 제거 (남은 충전 {_charge:0.#})");
        }
    }

    /// <summary>Lv.13 재기의 바람 — 체력 40% 이하로 떨어지면 4초에 걸쳐 40% 회복. 전투당 1회.</summary>
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

    /// <summary>부여하는 지속피해량 배율. 죽음의 계약과 저주가 함께 쓴다.</summary>
    public sealed class DotAmplifyEffect : BaseEffect
    {
        private readonly float _multiplier;
        public DotAmplifyEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float DamageOverTimeApplicationMultiplier(Unit unit) => _multiplier;
    }

    internal sealed class ElementMasteryEffect : BaseEffect
    {
        private readonly BaseEnums.UnitElement _element;
        public ElementMasteryEffect(BaseEnums.UnitElement element) : base(0) => _element = element;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.HasCombatElement(_element) ? 1.1f : 1f;
    }

    internal sealed class FlatOutgoingDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public FlatOutgoingDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _multiplier : 1f;
    }

    internal sealed class SpecialDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public SpecialDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.Special) == true ? _multiplier : 1f;
    }

    internal sealed class SpecialCritDamageEffect : BaseEffect
    {
        private readonly float _bonus;
        public SpecialCritDamageEffect(float bonus) : base(0, bonus) => _bonus = bonus;
        public override float CritMultiplierAdditiveModifier(Unit unit) => unit == Target ? _bonus : 0f;
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

        /// <summary>지속 턴 수. 생성자는 아직 초를 받으므로 1턴 = 2초로 환산한다.</summary>
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
