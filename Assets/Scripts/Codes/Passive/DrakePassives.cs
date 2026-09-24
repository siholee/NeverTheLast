using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>드레이크 전투 상수. 위력과 자원 이름을 한 곳에 둔다.</summary>
    public static class DrakeCombat
    {
        /// <summary>럭키세븐 중첩 자원. 아군의 추가행동마다 1씩 쌓인다.</summary>
        public const string LuckySevenResource = "drake_lucky_seven";

        /// <summary>이 수를 채우면 궁극기가 나간다.</summary>
        public const int LuckySevenThreshold = 7;

        /// <summary>사략면장이 반응하는 잔여 체력 비율.</summary>
        public const float HoundHpThreshold = 0.25f;

        public const int HoundPower = 50;
        public const float HoundStrCoefficient = 0.5f;

        /// <summary>처치 보상 골드 = 대상 레벨 × 이 값.</summary>
        public const int HoundGoldPerLevel = 10;
    }

    /// <summary>
    /// 드레이크 P(211) — 사략면장.
    ///
    /// <b>아군 누구의 공격이든</b> 끝난 뒤 그 대상이 최대 체력 25% 이하로 남아 있으면,
    /// 드레이크가 곧장 추가행동 '골드 하운드'로 마무리를 노린다.
    /// 한 행동에 여러 번 적중해도 예약은 한 번만 걸린다 — 행동 ID로 눌러 둔다.
    /// </summary>
    public sealed class DrakePrivateerCharter : UniquePassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private int _lastQueuedAction = int.MinValue;
        private bool _resolving;
        private bool _registered;

        public DrakePrivateerCharter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사략면장";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _damageHandler = OnAnyDamageDealt;
            _cleanupHandler = _ => StopCode();
            Unit.AnyDamageDealt += _damageHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;

            // 럭키세븐은 마나가 아니라 아군의 추가행동을 자원으로 쓴다. 여기서 굴리기 시작한다.
            if (Caster.ActiveUltimateCode is Codes.Ultimate.DrakeLuckySeven ultimate)
                ultimate.StartPassive();
        }

        public override void StopCode()
        {
            if (!_registered) return;

            if (Caster?.ActiveUltimateCode is Codes.Ultimate.DrakeLuckySeven ultimate)
                ultimate.StopPassive();
            Unit.AnyDamageDealt -= _damageHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _damageHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || context?.Target == null) return;
            if (context.Attacker == null || context.Attacker.IsEnemy != Caster.IsEnemy) return;
            // 골드 하운드가 낸 피해에 스스로 다시 반응하면 대상이 죽을 때까지 연쇄된다.
            // CurrentActionId는 추가행동마다 새로 발급되므로 이 재귀를 막아 주지 못한다.
            if (_resolving) return;

            Unit prey = context.Target;
            if (!prey.isActive || prey.IsUntargetable || prey.IsEnemy == Caster.IsEnemy) return;
            if (prey.HpMax <= 0) return;
            if (prey.HpCurr > prey.HpMax * DrakeCombat.HoundHpThreshold) return;

            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastQueuedAction == action) return;
            _lastQueuedAction = action;

            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "drake_gold_hound", "골드 하운드", () => ResolveHound(prey));
        }

        /// <summary>
        /// 골드 하운드 — 단일 타격. 이 타격으로 실제로 잡았을 때만 현상금이 붙는다.
        /// 골드는 <see cref="InventoryManager.AddGold"/>로 바로 들어가고, 전투 결산 골드에
        /// 함께 잡히므로 골드 보너스(상인·마네키네코·약탈)의 영향을 그대로 받는다.
        /// </summary>
        private void ResolveHound(Unit prey)
        {
            if (Caster == null || !Caster.isActive) return;
            if (prey == null || !prey.isActive || prey.IsUntargetable) return;

            bool wasAlive = prey.isActive;
            _resolving = true;
            try
            {
            int power = DrakeCombat.HoundPower +
                Mathf.RoundToInt(Caster.GetBaseStr() * DrakeCombat.HoundStrCoefficient);
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));
            prey.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.NonContactAttack,
                },
                isCrit));

            if (!wasAlive || prey.isActive) return;

            int bounty = Mathf.Max(0, prey.Level) * DrakeCombat.HoundGoldPerLevel;
            if (bounty <= 0) return;

            int paid = Mathf.RoundToInt(bounty * RewardModifiers.GoldMultiplier());
            GameManager.Instance?.inventoryManager?.AddGold(paid);
            Debug.Log($"[드레이크] 골드 하운드 현상금 {paid} (기본 {bounty})");
            }
            finally { _resolving = false; }
        }
    }

    /// <summary>
    /// 약탈(149) — 필드에서 적이 쓰러질 때마다 전투 후 골드가 5%씩 는다. 중첩된다.
    /// 골드 배율은 상태가 정리된 뒤에도 쓰이므로 <see cref="RewardModifiers"/>가 코드를 직접 훑는다.
    /// </summary>
    public sealed class DrakePlunder : PassiveCode
    {
        /// <summary>처치 하나당 붙는 골드 보너스.</summary>
        public const float GoldBonusPerKill = 0.05f;

        private Action<Unit, Unit> _deathHandler;
        private Action<EventContext> _resetHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        /// <summary>이번 전투에서 이 코드가 센 처치 수.</summary>
        public int Stacks { get; private set; }

        public DrakePlunder(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "약탈";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            Stacks = 0;
            _deathHandler = OnAnyUnitDied;
            _resetHandler = _ => Stacks = 0;
            _cleanupHandler = _ => StopCode();
            Unit.AnyUnitDied += _deathHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
            // 라운드가 끝나거나 쓰러지면 반드시 구독을 끊는다. 아군은 라운드마다 새로 스폰되므로
            // 여기서 끊지 않으면 죽은 인스턴스의 구독이 정적 이벤트에 그대로 쌓인다.
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            Unit.AnyUnitDied -= _deathHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _deathHandler = null;
            _resetHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void OnAnyUnitDied(Unit dead, Unit killer)
        {
            if (Caster == null || dead == null) return;
            if (dead.IsEnemy == Caster.IsEnemy) return;
            Stacks++;
        }
    }

    /// <summary>
    /// 히폴리테 전투 상수. '사냥감' 표식이 이 캐릭터의 축이다.
    /// </summary>
    public static class HippolyteCombat
    {
        public const int PreyStatusId = 6470;
        public const string PreyStatusKey = "hippolyte_prey";

        public const float PreyDefenseDown = 0.10f;
        public const float PreyHealingDown = 0.20f;
        public const int PreyPriorityBonus = 2;

        public const int CommandPower = 80;
        public const float CommandStrCoefficient = 1.2f;
        public const float SupportStrCoefficient = 0.4f;

        /// <summary>밀림의 가호가 한 번에 주는 DEX 비율. 아마조네스는 두 배를 받는다.</summary>
        public const float BlessingDexRatio = 0.03f;
        public const string AmazonessTag = "Amazoness";

        /// <summary>필드에 '사냥감'을 달고 있는 적이 있는가.</summary>
        public static bool AnyPrey(Unit caster)
            => caster != null && CombatTargets.AliveEnemies(caster)
                .Any(enemy => enemy != null && enemy.HasStatusKey(PreyStatusKey));
    }
}
