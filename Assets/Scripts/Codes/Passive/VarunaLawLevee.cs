using System;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 바루나 — 특성 「법이 만든 제방」(Detail_08 §16.2). 자신의 <b>행동</b>으로 적에게 물을 부착하면
    /// 자신에게 현재 방어력 35%의 보호막을 얻는다. 광역이든 확산으로 여럿에게 붙든 행동당 한 번이다.
    ///
    /// 실제 부착 시점(<see cref="Unit.AnyCombatElementGranted"/>)에 일하므로 부착 직후 반응으로 물이 소비돼도 발동한다.
    /// 행동 경계는 스케줄러가 매기는 행동 번호(<see cref="ActionScheduler.CurrentActionId"/>)로 가른다 — 같은 턴의
    /// 일반행동 · 초월기는 서로 다른 행동이라 각각 한 번씩 얻는다. 특수행동에는 물 부착이 없어 이 특성도 일하지 않는다.
    /// 보호막은 <see cref="Unit.AddShield"/>로 누적하며 상한 · 기간 · 덮어쓰기가 없고, 양은 방어력만 본다.
    /// </summary>
    public sealed class VarunaLawLevee : UniquePassiveCode
    {
        public const float DefenseRatio = 0.35f;

        /// <summary>
        /// 제방 보호막을 준 순간(바루나, 행동 번호, 보정 전 보호막량). 같은 바루나의 파티 보호막(초월기 · 특수행동)과
        /// 보호막 이벤트만으로는 가를 수 없어, 연출 · 검증이 특성 발동만 골라 볼 수 있게 연다.
        /// </summary>
        public static event Action<Unit, int, int> AnyLeveeShield;

        private bool _subscribed;
        private int _lastActionId = -1;
        private Action<EventContext> _cleanupHandler;

        public VarunaLawLevee(PassiveCodeContext context) : base(context) => CodeName = "법이 만든 제방";

        /// <summary>현재 방어력 × 비율. 최대 체력 · 특수공격력 · 피해 보너스는 보지 않는다.</summary>
        public static int ShieldAmount(Unit varuna, float ratio)
            => varuna == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(varuna.StatValue(BaseEnums.StatType.Defense) * ratio));

        public override void CastCode()
        {
            if (Caster == null || _subscribed) return;
            _lastActionId = -1;
            Unit.AnyCombatElementGranted += OnElementGranted;
            ActionScheduler.AnyActionStarted += OnActionStarted;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (!_subscribed) return;
            Unit.AnyCombatElementGranted -= OnElementGranted;
            ActionScheduler.AnyActionStarted -= OnActionStarted;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            _subscribed = false;
        }

        // 행동 번호는 라운드마다 1부터 다시 센다. 바루나 자신의 새 행동이 열릴 때 지난 번호를 비워 두어야
        // 번호가 우연히 같아도 새 행동의 첫 부착을 놓치지 않는다.
        private void OnActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (unit == Caster) _lastActionId = -1;
        }

        private void OnElementGranted(Unit source, Unit target, BaseEnums.UnitElement element)
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            if (source != Caster || element != BaseEnums.UnitElement.Hydro) return;
            if (target == null || target.IsEnemy == Caster.IsEnemy) return;

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null || scheduler.ExecutingUnit != Caster) return;
            int actionId = scheduler.CurrentActionId;
            if (actionId == _lastActionId) return;
            _lastActionId = actionId;

            int amount = ShieldAmount(Caster, DefenseRatio);
            if (amount <= 0) return;
            Caster.AddShield(amount, Caster);
            AnyLeveeShield?.Invoke(Caster, actionId, amount);
        }
    }
}
