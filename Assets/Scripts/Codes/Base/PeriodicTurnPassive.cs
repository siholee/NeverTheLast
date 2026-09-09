using System;
using BaseClasses;

namespace Codes.Base
{
    /// <summary>
    /// "N턴마다 한 번" 발동하는 패시브의 공용 뼈대.
    ///
    /// 예전에는 각 코드가 <c>OnTurnStart</c>에 직접 붙어 초 단위 누적기를 굴렸다.
    /// 전투가 턴제로 정리된 뒤에도 그 누적기가 남아 <b>같은 주기 로직이 파일마다 복사</b>돼 있었고,
    /// 초 축은 DEX가 빠를수록 같은 초에 턴이 더 많이 들어가 "빠를수록 주기가 늦게 오는" 역전을 만들었다.
    /// 이제 주기는 <b>보유자의 자기 턴 수</b>로만 센다 — 빠른 유닛이 더 자주 받는 것이 상식에 맞는다.
    ///
    /// 사망·라운드 종료 때 스스로 구독을 끊으므로 파생 클래스는 <see cref="OnPeriodElapsed"/>만 채우면 된다.
    /// </summary>
    public abstract class PeriodicTurnPassive : PassiveCode
    {
        private readonly int _intervalTurns;
        private readonly bool _fireOnStart;

        private Action<EventContext> _turnHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;
        private int _turnsSinceFire;

        /// <param name="intervalTurns">발동 간격(보유자의 턴 수). 1 이상.</param>
        /// <param name="fireOnStart">전투 시작 시점에 한 번 먼저 발동할지.</param>
        protected PeriodicTurnPassive(PassiveCodeContext context, int intervalTurns, bool fireOnStart = false)
            : base(context)
        {
            _intervalTurns = intervalTurns < 1 ? 1 : intervalTurns;
            _fireOnStart = fireOnStart;
            CodeType = BaseEnums.CodeType.Passive;
            IgnoresActivationChance = true;
        }

        protected int IntervalTurns => _intervalTurns;
        protected bool Registered => _registered;

        /// <summary>주기가 찼을 때 호출된다.</summary>
        protected abstract void OnPeriodElapsed();

        /// <summary>주기를 처음부터 다시 센다. 발동 조건을 코드 쪽에서 소비했을 때 쓴다.</summary>
        protected void ResetPeriod() => _turnsSinceFire = 0;

        /// <summary>다음 자기 턴에 곧바로 다시 시도한다. 자리가 없어 소환에 실패한 경우 등에 쓴다.</summary>
        protected void RetryNextTurn() => _turnsSinceFire = _intervalTurns - 1;

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _turnsSinceFire = 0;
            _turnHandler = OnTurnStart;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;

            OnRegistered();
            if (_fireOnStart) OnPeriodElapsed();
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;

            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _turnHandler = null;
            _cleanupHandler = null;
            _registered = false;
            _turnsSinceFire = 0;

            OnUnregistered();
        }

        protected virtual void OnRegistered() { }
        protected virtual void OnUnregistered() { }

        /// <summary>주기 계산을 건너뛸 조건. 기절 등으로 쉬는 턴을 세지 않게 할 때 덮어쓴다.</summary>
        protected virtual bool CanTick() => Caster != null && Caster.isActive;

        private void OnTurnStart(EventContext context)
        {
            if (context.Grantee != Caster || !CanTick()) return;

            _turnsSinceFire++;
            if (_turnsSinceFire < _intervalTurns) return;

            _turnsSinceFire = 0;
            OnPeriodElapsed();
        }
    }
}
