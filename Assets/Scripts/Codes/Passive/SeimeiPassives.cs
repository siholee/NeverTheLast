using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>아베노 세이메이(43)의 코드와 상태 ID.</summary>
    public static class SeimeiIds
    {
        public const int UnitId = 43;

        public const int BarrierDecode = 243;
        public const int SealEcho = 115;
        public const int ExposedGap = 116;
        public const int GuardianFormation = 117;
        public const int LingeringBarrier = 118;

        public const int AuraStatus = 9810;
        public const int SealEchoStatus = 9811;
        public const int ExposedGapStatus = 9812;
        public const int GuardianStatus = 9813;
        public const int LingeringBarrierStatus = 9814;

        /// <summary>천지반전 · 남겨진 결계가 주는 보호막의 CON 계수.</summary>
        public const float ShieldConRatio = 1.2f;

        /// <summary>
        /// 새 제어를 건 뒤의 후처리. 드러난 틈(116)을 배웠으면 받는 피해 +10% 2턴.
        /// 봉인부와 천지반전이 같은 규칙을 쓰도록 여기 모았다.
        /// </summary>
        public static void OnControlLanded(Unit seimei, Unit target)
        {
            if (seimei == null || target == null || !target.isActive) return;
            if (!seimei.HasLearnedPassiveCode(ExposedGap)) return;

            target.AddStatus(BuffStatus.Create(
                ExposedGapStatus, "seimei_exposed_gap", "드러난 틈", seimei, target,
                new ReceivingDamageMultiplierEffect(1.1f),
                duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "받는 피해가 10% 증가합니다."));
        }
    }

    /// <summary>
    /// 아베노 세이메이 P — 결계 해독(243). 세이메이가 필드에 살아 있는 동안 자신을 포함한
    /// 같은 진영 전원의 피해가 보호막을 건너뛴다.
    ///
    /// 방어막 관통은 방어력 관통이 아니다 — 방어력·내구·체력 구간 경계는 그대로 받는다.
    /// 판정은 피해 해결 시점이라 세이메이가 쓰러진 뒤 도착한 공격은 관통하지 않는다.
    /// 해안 전선의 공허의 갑주를 제어 없이 넘기는 두 번째 해법이다.
    /// </summary>
    public sealed class SeimeiBarrierDecode : PersistentStatusPassive
    {
        public SeimeiBarrierDecode(PassiveCodeContext context)
            : base(context, SeimeiIds.AuraStatus, "seimei_barrier_decode", "결계 해독",
                "필드에 있는 동안 자신을 포함한 아군 전체의 피해가 보호막을 건너뜁니다. 방어력은 그대로 받습니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new BarrierDecodeAuraEffect();
    }

    internal sealed class BarrierDecodeAuraEffect : BaseEffect
    {
        public BarrierDecodeAuraEffect() : base(0) { }

        public override bool CountsAsReagentBuff => false;
        public override bool IsBeneficial => true;

        public override bool GrantsAlliedShieldPenetration(Unit owner, Unit attacker)
            => owner == Target && owner.isActive && owner.HpCurr > 0 &&
               attacker != null && attacker.IsEnemy == owner.IsEnemy;
    }

    /// <summary>
    /// 봉인의 잔향(115). 봉인부 회차의 일반행동이 대상에게 주는 피해 −10%(2턴)를 건다.
    /// 효과는 봉인부(43)가 이 코드를 배웠는지로 직접 읽는다.
    /// </summary>
    public sealed class SeimeiSealEcho : PassiveCode
    {
        public SeimeiSealEcho(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "봉인의 잔향";
            IgnoresActivationChance = true;
        }

        public override void CastCode() { }
    }

    /// <summary>드러난 틈(116). 세이메이가 새 제어를 건 적은 받는 피해 +10%(2턴). <see cref="SeimeiIds.OnControlLanded"/>가 읽는다.</summary>
    public sealed class SeimeiExposedGap : PassiveCode
    {
        public SeimeiExposedGap(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "드러난 틈";
            IgnoresActivationChance = true;
        }

        public override void CastCode() { }
    }

    /// <summary>수호의 방진(117). 천지반전의 보호막을 받은 아군은 받는 피해 −10%(1턴). 천지반전이 읽는다.</summary>
    public sealed class SeimeiGuardianFormation : PassiveCode
    {
        public SeimeiGuardianFormation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "수호의 방진";
            IgnoresActivationChance = true;
        }

        public override void CastCode() { }
    }

    /// <summary>
    /// 남겨진 결계(118). 전투에서 처음 쓰러질 때 살아 있는 아군 전원에게 자신의 CON×1.2 기준 보호막을 한 번 준다.
    /// 관통 오라는 사망과 함께 끊긴다 — 결계는 보호막으로만 남는다.
    /// </summary>
    public sealed class SeimeiLingeringBarrier : PassiveCode
    {
        private Action<EventContext> _deathHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _used;

        public SeimeiLingeringBarrier(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "남겨진 결계";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _deathHandler != null) return;
            _used = false;
            _deathHandler = _ => Release();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        private void Release()
        {
            if (_used || Caster == null) return;
            _used = true;

            int amount = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * SeimeiIds.ShieldConRatio));
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster)
                         .Where(unit => unit != Caster && unit.HpCurr > 0).ToList())
            {
                ally.AddShield(amount, Caster);
            }
            Debug.Log($"[남겨진 결계] {Caster.UnitName}이(가) 쓰러지며 아군에게 보호막 {amount}을(를) 남겼습니다.");
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _deathHandler = null;
            _cleanupHandler = null;
        }
    }
}
