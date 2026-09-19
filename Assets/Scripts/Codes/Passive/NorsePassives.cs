using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>프레이아·로키·스카디가 쓰는 상태 ID 대역.</summary>
    public static class NorseStatusIds
    {
        public const int Phytoncide = 5320;
        public const int PhytoncideHarvest = 5321;
        public const int Medicine = 5322;
        public const int Leadership = 5323;
        public const int WarChief = 5324;
        public const int BaldrMark = 5325;
        public const int Summoner = 5326;
        public const int CryoMastery = 5328;
        public const int CryoAffinity = 5329;
        public const int Elementalist = 5330;
    }

    // ══════════════════════════════════════════════════════════════
    // 프레이아
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 프레이아 고유 P — 피톤치드.
    ///
    /// 전투 중 쌓인 순수치유량을 궁극기가 정산하는 순간, 그 양에 비례한 STR을
    /// 아군 전체에게 2턴간 얹는다. 기록 자체는 <see cref="Unit.RoundEffectiveHealingDone"/>이
    /// 이미 들고 있으므로 여기서는 <b>환산과 지급만</b> 한다.
    ///
    /// 분모가 프레이아의 최대 체력에 연동되어 레벨이 올라도 체감 배율이 유지된다.
    /// CON이 주스탯이 되면서 최대 체력과 치유량이 같은 스탯을 타므로 비율은 저절로 맞는다.
    /// </summary>
    public sealed class FreyaPhytoncide : UniquePassiveCode
    {
        /// <summary>STR 1을 사는 데 필요한 순수치유량 = 최대 체력의 이 비율.</summary>
        private const float HpRatioPerPoint = 0.1f;
        private const int MaxBonus = 25;
        private const int Duration = 2;

        public FreyaPhytoncide(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "피톤치드";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Phytoncide, "freya_phytoncide", CodeName,
            Caster, Caster, new MarkerBuffEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "순수치유량을 기록합니다. 정산하면 아군 전체가 그 양에 비례한 STR을 2턴간 얻습니다."));

        /// <summary>
        /// 궁극기가 기록을 비울 때 부른다. 기록을 비우는 쪽이 정산의 주인이라
        /// 여기서는 값만 환산하고 <see cref="Unit.ResetEffectiveHealingRecord"/>는 건드리지 않는다.
        /// </summary>
        public int Settle(int effectiveHealing)
        {
            if (Caster == null || !Caster.isActive) return 0;

            int divisor = Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * HpRatioPerPoint));
            int bonus = Mathf.Clamp(effectiveHealing / divisor, 0, MaxBonus);
            if (bonus <= 0) return 0;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NorseStatusIds.PhytoncideHarvest, "freya_phytoncide_harvest", CodeName,
                    Caster, ally, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.STR, bonus),
                    duration: Duration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"STR이 {bonus} 증가합니다."));
            }

            Debug.Log($"[피톤치드] 순수치유량 {effectiveHealing} 정산 → 아군 전체 STR +{bonus} ({Duration}턴)");
            return bonus;
        }
    }

    /// <summary>Lv.30 의술 — 부여하는 치유·보호막 +25%. `의신의 가호`(32)의 일반 등급.</summary>
    public sealed class FreyaMedicine : PassiveCode
    {
        public FreyaMedicine(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "의술";
            IgnoresActivationChance = true;
            // 일반 등급. 의신의 가호(32)를 배웠으면 발동 자체가 막힌다.
            SupersededByCodeId = AsclepiusDivineMedicine.CodeId;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Medicine, "freya_medicine", CodeName,
            Caster, Caster, new MedicineEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "부여하는 치유와 보호막이 25% 증가합니다. 의신의 가호와 중첩되지 않습니다."));
    }

    /// <summary>Lv.40 리더쉽 — 필드에 있는 동안 아군 전체가 가하는 피해 +10%.</summary>
    public sealed class FreyaLeadership : PassiveCode
    {
        public const string SharedKey = "leadership_damage_aura";
        private const float Multiplier = 1.1f;

        public FreyaLeadership(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "리더쉽";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AuraTargets.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NorseStatusIds.Leadership, SharedKey, CodeName, Caster, ally,
                    new OutgoingDamageMultiplierEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "가하는 피해 +10%"));
            }
        }
    }

    /// <summary>Lv.61 전사장 — 전열 아군에게 STR +8. 전열 여부는 매 질의마다 다시 본다.</summary>
    public sealed class FreyaWarChief : PassiveCode
    {
        public const string SharedKey = "warchief_front_str";
        private const int Bonus = 8;

        public FreyaWarChief(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "전사장";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AuraTargets.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NorseStatusIds.WarChief, SharedKey, CodeName, Caster, ally,
                    new FrontRowStatEffect(BaseEnums.PrimaryStat.STR, Bonus),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "전열에 있는 동안 STR +8"));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 로키
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 로키의 표식. 발드르의 살해자가 남긴다.
    /// 치유량 −25%와 방어력 −20%를 한 상태에 함께 담아 표식 하나가 통째로 붙고 떨어지게 한다.
    ///
    /// 공용 <see cref="HealingReductionStatus"/>(−50%)를 쓰지 않는 것은 의도적이다.
    /// 표식은 지속시간이 없고 로키가 거두기 전까지 남으므로 턴을 세는 공용 상태와 수명이 다르다.
    /// </summary>
    internal sealed class LokiMarkEffect : BaseEffect
    {
        private const float HealingMultiplier = 0.75f;
        private const float DefenseMultiplier = 0.8f;

        public LokiMarkEffect() : base(0) => Category = BaseEnums.EffectCategory.Negative;

        public override float HealingReceivedMultiplierModifier(Unit unit)
            => unit == Target ? HealingMultiplier : 1f;

        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? DefenseMultiplier : 1f;
    }

    /// <summary>
    /// 로키 고유 P — 펜리르.
    ///
    /// 소환수 펜리르를 거느린다. 펜리르는 제 DEX로 움직이며 스스로 물어뜯고(501),
    /// 여기에 더해 <b>로키가 표식을 새긴 적이 공격받을 때</b> 추가행동으로 한 번 더 문다.
    ///
    /// 방아쇠는 <b>아군 한 명당 한 번</b>이고 로키가 일반행동을 해결할 때 전부 되돌아온다.
    /// 로키와 펜리르 자신은 방아쇠에서 빠지므로 한 주기의 상한은 넷이다.
    /// 이 제한이 없으면 다타수 아군 하나가 한 행동에 열 번 넘게 물게 해 위력이 무너진다.
    /// </summary>
    public sealed class LokiFenrir : UniquePassiveCode
    {
        /// <summary>표식 반응 물기의 위력. 무는 것은 펜리르라 펜리르의 STR로 친다.</summary>
        private const int BitePower = 40;

        private const int BleedTurns = 2;

        /// <summary>출혈 강도는 공허 늑대 계열과 같은 규격(턴당 최대 체력 3%)을 쓴다.</summary>
        private const float BleedMaxHpPercent = 3f;

        private const string MarkKey = "loki_baldr_mark";

        private readonly HashSet<Unit> _spentTriggers = new();
        private Unit _marked;
        private int _biteSequence;
        private bool _summonedThisRound;
        private bool _registered;

        private Action<EventContext> _turnHandler;
        private Action<EventContext> _normalResolvedHandler;
        private Action<EventContext> _markedDamageHandler;
        private Action<EventContext> _markedDeathHandler;
        private Action<EventContext> _cleanupHandler;

        public LokiFenrir(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "펜리르";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _summonedThisRound = false;
            _turnHandler = OnOwnerTurnStart;
            _normalResolvedHandler = OnOwnerNormalActionResolved;
            _markedDamageHandler = OnMarkedDamaged;
            _markedDeathHandler = _ => ClearMark();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normalResolvedHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        /// <summary>
        /// 첫 턴에 펜리르를 불러낸다.
        ///
        /// 패시브가 붙는 시점은 로키가 아직 칸에 자리를 잡는 도중이라 소환이 성립하지 않는다.
        /// 자기 턴이 열리면 로키는 확실히 전장에 서 있으므로 그때 부른다.
        /// 한 라운드에 한 번만 부르므로 <b>쓰러진 펜리르는 다시 오지 않는다.</b>
        /// </summary>
        private void OnOwnerTurnStart(EventContext context)
        {
            if (Caster == null || !_registered || _summonedThisRound) return;
            if (context?.Grantee != Caster || !Caster.IsOnField) return;

            _summonedThisRound = true;
            GridManager.Instance?.SpawnSummon(Caster, SummonCatalog.Fenrir(Caster));
        }

        /// <summary>로키가 일반행동을 마치면 아군 전원의 방아쇠가 되돌아온다.</summary>
        private void OnOwnerNormalActionResolved(EventContext context)
        {
            if (Caster == null || !_registered || context?.Grantee != Caster) return;
            _spentTriggers.Clear();
        }

        /// <summary>
        /// 표식을 옮긴다. 표식은 언제나 하나뿐이라 새로 새기면 앞의 것은 사라진다.
        /// 궁극기 <c>발드르의 살해자</c>만 부른다.
        /// </summary>
        public void MarkTarget(Unit target)
        {
            if (Caster == null || !_registered || target == null || !target.isActive) return;

            ClearMark();

            _marked = target;
            _spentTriggers.Clear();
            target.AddStatus(BuffStatus.Create(
                NorseStatusIds.BaldrMark, MarkKey, "표식",
                Caster, target, new LokiMarkEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "받는 치유량 25% 감소, 방어력 20% 감소. 펜리르가 이 대상을 노립니다."));

            target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _markedDamageHandler);
            target.AddListener(BaseEnums.UnitEventType.OnDeath, _markedDeathHandler);
        }

        private void ClearMark()
        {
            if (_marked == null) return;

            _marked.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _markedDamageHandler);
            _marked.RemoveListener(BaseEnums.UnitEventType.OnDeath, _markedDeathHandler);
            _marked.RemoveStatusByKey(MarkKey);
            _marked = null;
            _spentTriggers.Clear();
        }

        /// <summary>
        /// 표식 대상이 맞았다. 때린 아군 하나당 한 번 펜리르의 추가행동을 예약한다.
        ///
        /// 지속피해는 <see cref="BaseEnums.CodeType.Effect"/>라 여기서 걸러진다.
        /// 로키와 펜리르가 빠지는 것은 자기 공격이 자기 추가행동을 부르는 되먹임을 끊기 위해서다.
        /// </summary>
        private void OnMarkedDamaged(EventContext context)
        {
            Unit attacker = context?.Grantor;
            if (Caster == null || !_registered || _marked == null || context?.Grantee != _marked) return;
            if (attacker == null || !attacker.isActive || attacker.IsEnemy == _marked.IsEnemy) return;
            if (context.DmgCtx == null || context.DmgCtx.ResolvedDamage <= 0 ||
                context.DmgCtx.CodeType == BaseEnums.CodeType.Effect) return;

            Unit fenrir = Fenrir();
            if (fenrir == null || attacker == Caster || attacker == fenrir) return;
            if (!_spentTriggers.Add(attacker)) return;

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;

            Unit prey = _marked;
            scheduler.EnqueueAdditional(fenrir, $"loki_fenrir_bite_{_biteSequence++}", CodeName,
                () => ResolveBite(prey));
        }

        private void ResolveBite(Unit prey)
        {
            Unit fenrir = Fenrir();
            if (fenrir == null || prey == null || !prey.isActive || prey.IsUntargetable) return;

            Summons.Deal(fenrir, prey, BitePower, BaseEnums.PrimaryStat.STR,
                DamageTag.ContactAttack, DamageTag.Physical);

            if (!prey.isActive) return;

            // 출혈 판정은 무는 쪽의 현재 LUK%다. 공허 늑대 계열과 같은 규격이다.
            float chance = Mathf.Clamp01(fenrir.GetBaseLuk() * 0.01f);
            if (UnityEngine.Random.value >= chance) return;

            BleedStatus.Apply(prey, fenrir, BleedTurns, BleedMaxHpPercent, CodeName);
        }

        private Unit Fenrir() => Caster?.ActiveSummons
            .FirstOrDefault(summon => summon != null && summon.isActive);

        public override void StopCode()
        {
            if (Caster == null || !_registered) return;

            ClearMark();
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normalResolvedHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _summonedThisRound = false;
            _registered = false;
        }
    }

    /// <summary>Lv.25 소환사 — 필드의 모든 소환수가 가하는 피해 +25%.</summary>
    public sealed class LokiSummoner : PassiveCode
    {
        public LokiSummoner(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "소환사";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Summoner, "loki_summoner", CodeName,
            Caster, Caster, new SummonMasterEffect(1.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "필드의 모든 소환수가 가하는 피해 +25%. 중첩되지 않습니다."));
    }

    // ══════════════════════════════════════════════════════════════
    // 스카디
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 스카디 고유 P — 북방의 수호자.
    /// 일반행동을 해결할 때마다 반격 스택을 최대치까지 채운다. 적에게 피격되면 한 스택을
    /// 소비해 공격자에게 고정 위력 80 + STR×0.5의 접촉 물리 반격을 예약한다.
    /// </summary>
    public sealed class SkadiNorthernGuardian : UniquePassiveCode, ICounterAttackProvider
    {
        public const string ResourceId = "skadi_northern_guardian";
        public const int MaxStacks = 3;

        private Action<EventContext> _damageHandler;
        private Action<EventContext> _actionHandler;
        private Action<EventContext> _cleanupHandler;
        private int _counterSequence;
        private bool _registered;

        public SkadiNorthernGuardian(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "북방의 수호자";
            Power = 80;
            PowerStatCoefficient = 0.5f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            Caster.SetCombatResourceMaximum(ResourceId, MaxStacks, resetCurrent: true);
            _counterSequence = 0;
            _damageHandler = OnAfterDamageTaken;
            _actionHandler = OnNormalActionResolved;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnNormalActionResolved(EventContext context)
        {
            if (Caster == null || !_registered || context?.Grantee != Caster) return;
            Caster.AddCombatResource(ResourceId, MaxStacks);
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            Unit attacker = context?.Grantor;
            if (Caster == null || !_registered || !Caster.isActive || Caster.HpCurr <= 0 ||
                attacker == null || !attacker.isActive || attacker.IsEnemy == Caster.IsEnemy ||
                context.DmgCtx == null || context.DmgCtx.ResolvedDamage <= 0 ||
                Caster.GetCombatResource(ResourceId) <= 0) return;

            var scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;

            string key = $"skadi_northern_guardian_{_counterSequence++}";
            scheduler.EnqueueAdditional(Caster, key, CodeName, () => ResolveCounter(attacker));
        }

        /// <summary>
        /// 스택은 <b>예약이 아니라 실제 반격이 나갈 때</b> 태운다.
        /// 큐가 풀리기 전에 공격자가 쓰러지면 반격이 통째로 취소되는데,
        /// 예약 시점에 태우면 아무 일도 없이 스택만 사라진다.
        /// </summary>
        private void ResolveCounter(Unit attacker)
        {
            if (Caster == null || !Caster.isActive || attacker == null || !attacker.isActive ||
                attacker.IsUntargetable) return;
            if (!Caster.TryConsumeCombatResource(ResourceId, 1)) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));
            attacker.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.CounterAttack,
                    DamageTag.ContactAttack, DamageTag.Physical,
                },
                isCrit));
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            if (_registered)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }

            Caster.SetCombatResourceMaximum(ResourceId, MaxStacks, resetCurrent: true);
            _registered = false;
        }
    }

    /// <summary>Lv.12 원소 친화 - 얼음 — 자신이 얼음을 보유한 동안 빙결된 적을 때리면 확정 치명타.</summary>
    public sealed class SkadiCryoAffinity : PassiveCode
    {
        public SkadiCryoAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 얼음";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.CryoAffinity, "skadi_cryo_affinity", CodeName,
            Caster, Caster, new FrozenTargetCritEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "얼음 원소를 보유한 동안 빙결된 적을 공격하면 반드시 치명타가 됩니다."));
    }

    /// <summary>Lv.60 원소술사 — 아군 전체가 원소 반응으로 만든 피해 +25%.</summary>
    public sealed class SkadiElementalist : PassiveCode
    {
        public SkadiElementalist(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소술사";
            IgnoresActivationChance = true;
            // 금색 상위는 공허의 용이 드는 에테르(1591)다. 같은 상태 키를 쓰므로
            // 서로 다른 유닛이 들었을 때는 높은 쪽만 남는다.
            SupersededByCodeId = VoidAether.CodeId;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Elementalist, "skadi_elementalist", CodeName,
            Caster, Caster, new ReactionAmplifierEffect(0.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "아군 전체가 원소 반응으로 만든 피해 +25%. 상위 코드와 중첩되지 않습니다."));
    }

    // ══════════════════════════════════════════════════════════════
    // 효과 구현
    // ══════════════════════════════════════════════════════════════

    /// <summary>필드 전역 버프의 대상 목록을 뽑는 공용 헬퍼.</summary>
    internal static class AuraTargets
    {
        public static List<Unit> Allies(Unit caster)
        {
            List<Unit> allies = global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            if (caster != null && caster.isActive && !allies.Contains(caster)) allies.Add(caster);
            return allies;
        }
    }

    /// <summary>의술 — 부여 치유·보호막 +25%.</summary>
    internal sealed class MedicineEffect : BaseEffect
    {
        private const float Multiplier = 1.25f;

        public MedicineEffect() : base(0, Multiplier) { }

        /// <summary>
        /// 등급 대체는 <see cref="PassiveCode.SupersededByCodeId"/>가 발동 단계에서 막는다.
        /// 여기 남은 판정은 <b>배운 기록 없이</b> 장비가 의신의 가호를 부여한 경우를 위한 것이다.
        /// </summary>
        private bool Suppressed =>
            Target != null && Target.HasStatus(AsclepiusStatusIds.DivineMedicine);

        public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            => source == Target && !Suppressed ? Multiplier : 1f;

        public override float OutgoingShieldMultiplierModifier(Unit source, Unit target)
            => source == Target && !Suppressed ? Multiplier : 1f;
    }

    /// <summary>전열에 있는 동안에만 붙는 스탯 보너스.</summary>
    internal sealed class FrontRowStatEffect : BaseEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amount;

        public FrontRowStatEffect(BaseEnums.PrimaryStat stat, int amount) : base(0, amount)
        {
            _stat = stat;
            _amount = amount;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != _stat) return 0;
            if (unit.currentCell == null || GridManager.Instance == null) return 0;
            return unit.currentCell.xPos == GridManager.Instance.GetFrontColumn(unit.IsEnemy) ? _amount : 0;
        }
    }

    /// <summary>펜리르 — 로키의 2턴마다 현재 체력이 가장 낮은 적을 문다.</summary>
    /// <summary>소환사 — 아군 전체 소환수 피해 배율. <see cref="Combat.Summons"/>가 최댓값 하나만 읽는다.</summary>
    internal sealed class SummonMasterEffect : BaseEffect
    {
        private readonly float _multiplier;

        public SummonMasterEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float SummonDamageMultiplierModifier(Unit unit) => _multiplier;
        public override float AlliedSummonDamageMultiplierModifier(Unit unit, Unit summonOwner) => _multiplier;
    }

    /// <summary>
    /// 원소 친화 - 얼음 — 얼음을 두른 채 빙결된 적을 때리면 확정 치명타.
    ///
    /// 치명타 확률은 <c>AttributesUpdate</c> 시점에 한 번 굳으므로 대상별 조건을 담을 수 없다.
    /// 대신 바유 `기습`과 같은 방식으로, 피해가 계산되는 시점에 대상을 보고
    /// 치명타가 아니었다면 배율을 보정해 결과적으로 확정 치명타로 만든다.
    /// </summary>
    internal sealed class FrozenTargetCritEffect : BaseEffect
    {
        public FrozenTargetCritEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || context == null) return 1f;
            if (context.CodeType == BaseEnums.CodeType.Effect) return 1f;
            if (!attacker.HasCombatElement(BaseEnums.UnitElement.Cryo) || !target.IsFrozen) return 1f;

            float critMultiplier = Mathf.Max(1f, attacker.CritMultiplierCurr);
            float applied = context.IsCrit ? critMultiplier : 1f;
            context.IsCrit = true;
            return critMultiplier / applied;
        }
    }

    /// <summary>원소 반응 피해 증폭. <see cref="ElementalReaction.FieldReactionMultiplier"/>가 최댓값 하나만 읽는다.</summary>
    internal sealed class ReactionAmplifierEffect : BaseEffect, IReactionAmplifier
    {
        public float ReactionDamageBonus { get; }

        public ReactionAmplifierEffect(float bonus) : base(0, bonus) => ReactionDamageBonus = bonus;
    }
}
