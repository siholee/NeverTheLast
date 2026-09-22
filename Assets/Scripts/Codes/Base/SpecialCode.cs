using BaseClasses;

namespace Codes.Base
{
    /// <summary>
    /// 특수행동(SP) — 다른 코드가 예약하거나 개방하는 별도 행동.
    ///
    /// 인드라의 '신들의 왕'은 로카팔라 전원을 개방하며, 이아손은 고유 패시브가
    /// 자기 SP를 추가행동 큐에 직접 예약한다.
    ///
    /// <b>추가행동과는 다른 판정이다.</b> 턴을 쓰지 않는다는 성질만 같고,
    /// <c>OnAdditionalActivates</c>가 아니라 <c>OnSpecialActivates</c>를 발행하므로
    /// 바스테트의 '야수의 시선' 같은 추가행동 카운터에는 잡히지 않는다.
    /// 반대로 니콜의 '일렉트릭 필드'는 <i>모든 행동</i>을 세므로 이것도 함께 센다.
    /// </summary>
    public abstract class SpecialCode : Code
    {
        protected SpecialCode(SpecialCodeContext context)
        {
            Caster = context.Caster;
            CodeType = BaseEnums.CodeType.Special;
            ActivationType = BaseEnums.CodeActivationType.Special;
        }

        /// <summary>
        /// 같은 개방 안에서의 순서. <b>작을수록 먼저</b> 나간다.
        /// 바유의 <c>선봉의 바람</c>처럼 남들보다 앞서야 값을 하는 행동이 음수를 쓴다.
        /// </summary>
        public virtual int OpenOrder => 0;

        /// <summary>대상이 없어도 자기 강화·치유는 성립하므로 기본값은 참이다.</summary>
        public override bool HasValidTarget() => Caster != null && Caster.isActive;

        public override void StopCode()
        {
            if (Caster != null && CurrSkillCoroutine != null)
            {
                Caster.StopCoroutine(CurrSkillCoroutine);
                CurrSkillCoroutine = null;
            }
            if (Caster != null) Caster.isCasting = false;
            // 이번 개방에서 몇 명이 남았는지는 여기서만 셀 수 있다. 예약 시점이 아니라
            // <b>실제로 끝난 시점</b>이라야 바유의 버프가 마지막 한 명까지 살아 있다.
            Combat.SpecialAction.NotifyResolved(Caster);
        }
    }
}
