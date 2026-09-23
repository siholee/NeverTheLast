using System;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>우치 관련 상태 ID. 다른 테마의 ID 묶음과 겹치지 않는 구간을 쓴다.</summary>
    internal static class UsaStatusIds
    {
        public const int TaoistClones = 6480;
    }

    /// <summary>
    /// 우치 P(380) — 도사란 무엇인가.
    ///
    /// 분신을 최대 <see cref="MaxClones"/>기까지 거느리고, 적중한 공격을 받으면 분신 하나를
    /// 대신 흩어 그 타격을 무효화한다. 분신 자체는 <see cref="SummonCatalog.Clone"/>이 세우는 실제 소환수이므로
    /// 보유 수는 <see cref="Unit.ActiveSummons"/>를 세어 판단한다 — 이 패시브가 따로 세지 않는다.
    /// </summary>
    public sealed class UsaTaoistNature : UniquePassiveCode
    {
        /// <summary>동시에 거느릴 수 있는 분신 수.</summary>
        public const int MaxClones = 3;

        private const string StatusKey = "usa_taoist_clones";

        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public UsaTaoistNature(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "도사란 무엇인가";
            IgnoresActivationChance = true;
        }

        /// <summary>지금 살아 있는 분신의 수.</summary>
        public static int CloneCount(Unit owner)
        {
            if (owner?.ActiveSummons == null) return 0;

            int count = 0;
            foreach (Unit summon in owner.ActiveSummons)
            {
                if (summon != null && summon.isActive) count++;
            }
            return count;
        }

        /// <summary>분신을 더 부를 자리가 남았는가.</summary>
        public static bool HasRoomForClone(Unit owner) => CloneCount(owner) < MaxClones;

        /// <summary>분신을 <paramref name="count"/>기까지, 상한을 넘지 않는 만큼만 부른다.</summary>
        public static int SummonClones(Unit owner, int count)
        {
            if (owner == null || !owner.isActive) return 0;

            int summoned = 0;
            for (int i = 0; i < count && HasRoomForClone(owner); i++)
            {
                if (GridManager.Instance?.SpawnSummon(owner, SummonCatalog.Clone(owner)) == null) break;
                summoned++;
            }
            return summoned;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            Caster.AddStatus(BuffStatus.Create(
                UsaStatusIds.TaoistClones, StatusKey, CodeName, Caster, Caster,
                new UsaCloneGuardEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"분신을 최대 {MaxClones}기까지 거느리며, 피격 시 분신 하나를 흩어 타격을 완전히 무효화합니다."));

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;

            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey(StatusKey);
            _cleanupHandler = null;
            _registered = false;
        }
    }

    /// <summary>
    /// 적중한 피해를 분신 하나로 통째로 막는다.
    /// </summary>
    internal sealed class UsaCloneGuardEffect : BaseEffect
    {
        public UsaCloneGuardEffect() : base(0) { }

        public override bool TryNullifyHit(Unit unit, DamageContext context)
        {
            if (unit == null || unit != Target) return false;

            Unit clone = null;
            foreach (Unit summon in unit.ActiveSummons)
            {
                if (summon == null || !summon.isActive) continue;
                clone = summon;
                break;
            }
            if (clone == null) return false;

            // 분신이 대신 흩어진다. 처치가 아니라 퇴장이라 보상 집계에 남지 않는다.
            clone.Withdraw();
            return true;
        }
    }

    /// <summary>
    /// 느긋함(142) — 메인으로 육성할 때 훈련 보너스와 체력 소모를 함께 25% 줄인다.
    /// 전투에는 관여하지 않는 훈련 전용 코드다.
    /// </summary>
    public sealed class UsaLeisurely : PassiveCode
    {
        private const float TrainingPenalty = -0.25f;
        private const float EnergyCostMultiplier = 0.75f;

        public UsaLeisurely(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "느긋함";
            IgnoresActivationChance = true;
        }

        public override float MainTrainingBonus(BaseEnums.PrimaryStat stat) => TrainingPenalty;

        public override float MainTrainingEnergyCostMultiplier => EnergyCostMultiplier;

        public override void CastCode() { }
        public override void StopCode() { }
    }

    /// <summary>
    /// 정기 회복(143) — 필드에서 적이 쓰러지면 그 적의 최대 체력만큼 자신을 치유한다.
    /// 누가 잡았는지는 따지지 않는다.
    /// </summary>
    public sealed class UsaEssenceRecovery : PassiveCode
    {
        private const string StatusKey = "usa_essence_recovery";

        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public UsaEssenceRecovery(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "정기 회복";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
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
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _cleanupHandler = null;
            _registered = false;
        }

        private void OnAnyUnitDied(Unit dead, Unit killer)
        {
            if (Caster == null || !Caster.isActive || dead == null) return;
            // 적만 센다. 아군이나 아군 소환수가 흩어진 것으로는 회복하지 않는다.
            if (dead.IsEnemy == Caster.IsEnemy) return;

            int healing = Mathf.Max(1, dead.HpMax);
            Caster.ModifyHp(Caster.HpCurr + healing, Caster);
        }

        /// <summary>상태를 남기지 않는 코드라 키는 로그 식별용으로만 쓴다.</summary>
        public static string DebugKey => StatusKey;
    }
}
