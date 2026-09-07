using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class EgyptianStatusIds
    {
        public const int HorusSkyFalcon = 7800;
        public const int AnubisSouls = 7801;
        public const int BastetGrace = 7802;
        public const int PiercingShot = 7803;
        public const int SpearGuard = 7804;
        public const int Reaper = 7805;
        public const int LightArmament = 7806;
        public const int Selfish = 7807;
        public const int ThothToughnessAura = 7808;
        public const int ThothIllusionist = 7809;
        public const int ThothHiddenTruth = 7810;
        public const int IsisDesertRadiance = 7811;
        public const int ThothEyeOfWisdom = 7812;
    }

    /// <summary>호루스 P — 행동 속도를 1.0으로 고정하고 초과 속도 1%마다 STR +1.</summary>
    public sealed class HorusSkyFalcon : UniquePassiveCode
    {
        public HorusSkyFalcon(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "창공의 매";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.HorusSkyFalcon, "horus_sky_falcon", CodeName,
            Caster, Caster, new HorusSkyFalconEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "행동 속도가 1.0으로 고정되고 초과 속도 1%마다 STR이 1 증가합니다."));
    }

    internal sealed class HorusSkyFalconEffect : BaseEffect
    {
        public HorusSkyFalconEffect() : base(0) { }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != BaseEnums.PrimaryStat.STR) return 0;
            float excess = Mathf.Max(0f, unit.GetDerivedActionSpeed() - 1f);
            return Mathf.FloorToInt(excess * 100f + 0.0001f);
        }

        public override float ActionSpeedModifier(Unit unit, float calculatedSpeed)
            => unit == Target ? 1f : calculatedSpeed;
    }

    /// <summary>아누비스 P — 필드의 모든 죽음을 영혼으로 저장한다. 코드 인스턴스가 런 동안 스택을 보존한다.</summary>
    public sealed class AnubisSoulHarvest : UniquePassiveCode
    {
        private int _soulStacks;
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        public AnubisSoulHarvest(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "영혼 수확";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                EgyptianStatusIds.AnubisSouls, "anubis_souls", CodeName,
                Caster, Caster, new AnubisSoulStrengthEffect(() => _soulStacks),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "필드에서 유닛이 쓰러질 때마다 영혼을 얻습니다. 영혼 4개마다 STR +1."));

            if (_registered) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Caster == null || !Caster.isActive || dead == null) return;
            _soulStacks++;
            Caster.RefreshAttributes();
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _cleanupHandler = null;
            _registered = false;
        }
    }

    internal sealed class AnubisSoulStrengthEffect : BaseEffect
    {
        private readonly Func<int> _stacks;
        public AnubisSoulStrengthEffect(Func<int> stacks) : base(0) => _stacks = stacks;

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR
                ? Mathf.Max(0, _stacks?.Invoke() ?? 0) / 4
                : 0;
    }

    /// <summary>바스테트 P — 아군의 추가공격·반격으로 야수의 시선 스택을 쌓는다.</summary>
    public sealed class BastetAirborneHunter : UniquePassiveCode
    {
        public const string ResourceId = "bastet_beast_gaze";
        public const int MaximumStacks = 100;

        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private int _lastStackAction = int.MinValue;

        public BastetAirborneHunter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "야수의 시선";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            Caster.SetCombatResourceMaximum(ResourceId, MaximumStacks, true);
            _lastStackAction = int.MinValue;
            _damageHandler = OnAnyDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Unit.AnyDamageDealt += _damageHandler;
            _registered = true;

            if (Caster.ActiveUltimateCode is Codes.Ultimate.BastetApexExecution ultimate)
                ultimate.StartPassive();
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            Unit attacker = context?.Attacker;
            List<int> tags = context?.DamageContext?.DamageTags;
            if (Caster == null || !Caster.isActive || attacker == null || !attacker.isActive ||
                attacker.IsEnemy != Caster.IsEnemy || attacker.currentCell == null || attacker.currentCell.yPos <= 0 ||
                context.DamageDealt <= 0 || tags == null ||
                (!tags.Contains(DamageTag.AdditionalAttack) && !tags.Contains(DamageTag.CounterAttack))) return;

            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastStackAction == action) return;
            _lastStackAction = action;

            int stacks = Caster.AddCombatResource(ResourceId, 1);
            if (stacks < MaximumStacks) return;
            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "bastet_beast_gaze", CodeName, ResolveBeastGaze);
        }

        private void ResolveBeastGaze()
        {
            if (Caster == null || !Caster.isActive) return;
            int stacks = Caster.GetCombatResource(ResourceId);
            if (stacks < MaximumStacks) return;

            Unit target = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .OrderByDescending(unit => unit.HpMax)
                .ThenByDescending(unit => unit.HpCurr)
                .FirstOrDefault();
            if (target == null) return;

            Caster.TryConsumeCombatResource(ResourceId, stacks);
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                stacks * Caster.GetBaseLuk() * (isCrit ? Caster.CritMultiplierCurr : 1f)));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
                },
                isCrit));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Unit.AnyDamageDealt -= _damageHandler;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            if (Caster.ActiveUltimateCode is Codes.Ultimate.BastetApexExecution ultimate)
                ultimate.StopPassive();
            _damageHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }
    }

    public sealed class EgyptianPiercingShot : PassiveCode
    {
        public EgyptianPiercingShot(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "관통사격"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.PiercingShot, "egypt_piercing_shot", CodeName,
            Caster, Caster, new EgyptianPiercingShotEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "숙련 활을 장착한 화살 공격이 적 내구 4를 무시합니다."));
    }

    internal sealed class EgyptianPiercingShotEffect : BaseEffect
    {
        public EgyptianPiercingShotEffect() : base(0) { }
        public override int DurabilityPenetrationModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && Target.HasEquippedBow() &&
               context?.DamageTags?.Contains(DamageTag.Arrow) == true ? 4 : 0;
    }

    public sealed class AnubisSpearGuard : PassiveCode
    {
        public AnubisSpearGuard(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "창지기"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.SpearGuard, "anubis_spear_guard", CodeName,
            Caster, Caster, new AnubisSpearGuardEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "숙련된 창을 장착하면 STR +3%."));
    }

    internal sealed class AnubisSpearGuardEffect : BaseEffect
    {
        public AnubisSpearGuardEffect() : base(0) { }
        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR &&
               unit.HasEquippedProficiency(EquipmentProficiency.Spear) ? 1.03f : 1f;
    }

    public sealed class AnubisReaper : PassiveCode
    {
        public AnubisReaper(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "저승사자"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.Reaper, "anubis_reaper", CodeName,
            Caster, Caster, new AnubisReaperEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "사령 속성 대상에게 주는 피해가 30% 증가합니다."));
    }

    internal sealed class AnubisReaperEffect : BaseEffect
    {
        public AnubisReaperEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null &&
               (target.HasUnitTag("Undead") || target.HasUnitTag("Necrotic") || target.HasUnitTag("사령"))
                ? 1.30f : 1f;
    }

    /// <summary>
    /// 아누비스 Lv.38 죽은 자의 소생 — 전투가 끝나면 이번 라운드에 쓰러진 아군 하나를 되살린다.
    ///
    /// 되살릴 대상은 <b>죽는 순간에</b> 모아 둔다. <see cref="Unit.DeactivateUnit"/>이
    /// 유닛 ID를 지우고 칸을 비우므로, 라운드가 끝난 뒤에 필드를 훑어서는 누가 아군이었는지
    /// 알아낼 수 없다.
    ///
    /// 소환수는 라운드를 넘기지 않으므로 대상에서 뺀다. 아누비스 자신이 쓰러졌다면
    /// 아무도 일으키지 못한다 — 저승의 문을 여는 쪽이 먼저 누워 있기 때문이다.
    ///
    /// 🔸 지금은 <c>GameManager.RestoreAllyFieldState</c>가 전투 시작 시점의 스냅숏으로
    /// 아군 전원을 되돌리므로 <b>사망이 라운드를 넘지 않는다</b>. 이 코드가 눈에 보이는
    /// 차이를 만들려면 사망이 라운드 밖까지 남는 규칙이 먼저 있어야 한다.
    /// </summary>
    public sealed class AnubisRaiseTheDead : PassiveCode
    {
        public const int CodeId = 102;

        private readonly List<Unit> _fallen = new();
        private Action<EventContext> _roundEndHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public AnubisRaiseTheDead(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "죽은 자의 소생"; IgnoresActivationChance = true; }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _fallen.Clear();
            Unit.AnyUnitDied += OnAnyUnitDied;
            _roundEndHandler = OnRoundEnd;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            _registered = true;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Caster == null || dead == null || dead == Caster) return;
            if (dead.IsEnemy != Caster.IsEnemy || dead.IsSummon || dead.currentCell == null) return;
            if (_fallen.Contains(dead)) return;

            _fallen.Add(dead);
        }

        private void OnRoundEnd(EventContext context)
        {
            if (context?.Grantee != Caster) return;

            if (Caster.isActive)
            {
                List<Unit> candidates = _fallen.Where(unit => unit != null && !unit.isActive).ToList();
                if (candidates.Count > 0)
                {
                    Unit raised = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                    if (raised.ReviveAfterBattle())
                    {
                        Debug.Log($"[죽은 자의 소생] {Caster.UnitName}이(가) {raised.UnitName}을(를) 다시 일으켰다.");
                    }
                }
            }

            StopCode();
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;

            Unit.AnyUnitDied -= OnAnyUnitDied;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            _roundEndHandler = null;
            _cleanupHandler = null;
            _fallen.Clear();
            _registered = false;
        }
    }

    /// <summary>바스테트 Lv.2 — TrainingManager가 타입을 확인해 LUK 훈련량에 10%를 곱한다.</summary>
    public sealed class BastetMasterThief : PassiveCode
    {
        public BastetMasterThief(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "대도"; IgnoresActivationChance = true; }
    }

    public sealed class BastetLightArmament : PassiveCode
    {
        public BastetLightArmament(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "가벼운 무장"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.LightArmament, "bastet_light_armament", CodeName,
            Caster, Caster, new BastetLightArmamentEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "의복류 방어구를 착용하면 DEX +5%."));
    }

    internal sealed class BastetLightArmamentEffect : BaseEffect
    {
        public BastetLightArmamentEffect() : base(0) { }
        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX && unit.HasEquippedClothing() ? 1.05f : 1f;
    }

    public sealed class BastetSelfish : PassiveCode
    {
        public BastetSelfish(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "이기적"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.Selfish, "bastet_selfish", CodeName,
            Caster, Caster, new BastetSelfishEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "추가공격으로 가하는 피해가 20% 증가합니다."));
    }

    internal sealed class BastetSelfishEffect : BaseEffect
    {
        public BastetSelfishEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.AdditionalAttack) == true
                ? 1.20f : 1f;
    }

    /// <summary>세트 P — 전투 시간 8초마다 다음 단일 접촉 피해에 고정 피해와 자가 회복을 붙인다.</summary>
    public sealed class SetBloodOfTheDesert : UniquePassiveCode
    {
        private const float IntervalSeconds = 8f;
        private float _nextReadyAt;
        private bool _registered;
        private bool _resolving;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public SetBloodOfTheDesert(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사막의 피";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _nextReadyAt = (GameManager.Instance?.ActionScheduler?.CombatSeconds ?? 0f) + IntervalSeconds;
            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (_resolving || context?.Attacker != Caster || context.Target == null ||
                context.DamageDealt <= 0) return;
            List<int> tags = context.DamageContext?.DamageTags;
            if (tags == null || !tags.Contains(DamageTag.SingleTarget) ||
                !tags.Contains(DamageTag.ContactAttack)) return;

            float now = GameManager.Instance?.ActionScheduler?.CombatSeconds ?? 0f;
            if (now + 0.0001f < _nextReadyAt) return;
            _nextReadyAt = now + IntervalSeconds;

            int bonus = Mathf.Max(1, Caster.SkillDamage(20, BaseEnums.PrimaryStat.STR));
            if (context.Target.isActive && context.Target.HpCurr > 0)
            {
                _resolving = true;
                try
                {
                    context.Target.TakeDamage(new DamageContext(
                        Caster, bonus, BaseEnums.CodeType.Passive,
                        new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage, DamageTag.AdditionalAttack }));
                }
                finally
                {
                    _resolving = false;
                }
            }

            Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(Caster.HpMax * 0.08f), Caster);
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
            _resolving = false;
        }
    }

    /// <summary>토트 P — 필드에 있는 동안 모든 아군의 강인도 효율 +25%.</summary>
    public sealed class ThothToughnessScholar : UniquePassiveCode
    {
        private readonly List<Unit> _boundAllies = new();
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public ThothToughnessScholar(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "강인도 해석";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            string key = $"thoth_toughness_aura_{Caster.GetEntityId()}";
            foreach (Unit ally in global::Target.GetAllAllies(Caster)
                         .Where(unit => unit != null && unit.isActive))
            {
                ally.AddStatus(BuffStatus.Create(
                    EgyptianStatusIds.ThothToughnessAura, key, CodeName,
                    Caster, ally, new ToughnessEfficiencyEffect(0.25f),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "강인도 효율이 25% 증가합니다."));
                _boundAllies.Add(ally);
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            string key = $"thoth_toughness_aura_{Caster.GetEntityId()}";
            foreach (Unit ally in _boundAllies) ally?.RemoveStatusByKey(key);
            _boundAllies.Clear();
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }
    }

    public sealed class ThothIllusionist : PassiveCode
    {
        public ThothIllusionist(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "환술사"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.ThothIllusionist, "thoth_illusionist", CodeName,
            Caster, Caster, new ToughnessEfficiencyEffect(0.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "피해를 가할 때 강인도 효율이 25% 증가합니다."));
    }

    public sealed class ThothHiddenTruth : PassiveCode
    {
        public ThothHiddenTruth(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "허허실실"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.ThothHiddenTruth, "thoth_hidden_truth", CodeName,
            Caster, Caster, new ToughnessEchoEffect(0.10f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "감소시킨 강인도의 10%를 실제 피해로 더합니다."));
    }

    public sealed class IsisDesertRadiance : UniquePassiveCode
    {
        public const string MarkKey = "isis_desert_radiance";
        private Action<Unit, Unit, BaseEnums.UnitElement> _elementHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public IsisDesertRadiance(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사막의 광휘";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _elementHandler = OnElementGranted;
            _cleanupHandler = _ => StopCode();
            Unit.AnyCombatElementGranted += _elementHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnElementGranted(Unit source, Unit target, BaseEnums.UnitElement element)
        {
            if (source != Caster || target == null || target.IsEnemy == Caster.IsEnemy ||
                element != BaseEnums.UnitElement.Geo) return;

            target.AddStatus(BuffStatus.Create(
                EgyptianStatusIds.IsisDesertRadiance, MarkKey, CodeName,
                Caster, target, new IsisDesertRadianceEffect(),
                duration: Unit.CommonElementAuraDuration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "특수 피해를 10% 더 받습니다."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Unit.AnyCombatElementGranted -= _elementHandler;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }
    }

    /// <summary>이시스 Lv.12 — 효과가 아직 기획되지 않아 코드 슬롯과 이름만 보존한다.</summary>
    public sealed class IsisElement : PassiveCode
    {
        public IsisElement(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "원소"; IgnoresActivationChance = true; }
    }

    internal sealed class ToughnessEfficiencyEffect : BaseEffect
    {
        private readonly float _bonus;
        public ToughnessEfficiencyEffect(float bonus) : base(0, bonus) => _bonus = bonus;
        public override float ToughnessDamageAdditiveModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _bonus : 0f;
    }

    internal sealed class ToughnessEchoEffect : BaseEffect
    {
        private readonly float _ratio;
        public ToughnessEchoEffect(float ratio) : base(0, ratio) => _ratio = ratio;
        public override float ToughnessEchoDamageRatioModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _ratio : 0f;
    }

    internal sealed class IsisDesertRadianceEffect : BaseEffect
    {
        public IsisDesertRadianceEffect() : base(0) { }
        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target && context?.DamageTags?.Contains(DamageTag.Special) == true ? 1.10f : 1f;
    }
}
