using System;
using BaseClasses;
using Codes.Passive;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 츠쿠요미 일반행동. 단일 적에게 INT 기반 위력 40의 특수 피해를 입히고 월광 침식(2턴 지속피해)과
    /// 달의 위상 디버프 하나를 남긴다. 위상은 상현 → 보름 → 하현 순으로 돈다.
    ///
    /// 원소는 붙이지 않는다. 츠쿠요미의 몫은 지속피해와 디버프를 까는 것이다.
    /// </summary>
    public sealed class TsukuyomiLightningBolt : Sachi
    {
        private const int PhaseCount = 3;

        private Action<EventContext> _resetHandler;
        private int _phaseIndex;
        private bool _registered;

        public TsukuyomiLightningBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
        }

        public override void CastCode()
        {
            RegisterReset();
            base.CastCode();
        }

        /// <summary>
        /// 위상은 전투마다 상현부터 다시 센다. 코드 인스턴스는 라운드를 넘어 살아남으므로
        /// 라운드 경계에서 되돌린다. 행동마다 떼면 라운드 시작 이벤트를 놓치므로 한 번만 단다.
        /// </summary>
        private void RegisterReset()
        {
            if (_registered || Caster == null) return;
            _resetHandler = _ => _phaseIndex = 0;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _resetHandler);
            _registered = true;
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            TsukuyomiMoonlight.ApplyMoonrot(Caster, target);
            // 실제로 맞힌 행동만 순서를 넘긴다. 빗나간 행동이 위상 하나를 건너뛰게 하지 않는다.
            TsukuyomiMoonlight.ApplyPhase(Caster, target, (TsukuyomiPhase)_phaseIndex);
            _phaseIndex = (_phaseIndex + 1) % PhaseCount;
        }
    }
}
