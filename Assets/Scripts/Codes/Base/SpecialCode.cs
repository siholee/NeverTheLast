using BaseClasses;

namespace Codes.Base
{
    /// <summary>
    /// 특정 트리거가 여는 SP 슬롯. 턴을 쓰지 않지만 추가행동으로 세지 않는다.
    /// 로카팔라 개방처럼 여러 명이 함께 나서는 경우 <see cref="OpenOrder"/>가 실행 순서를 정한다.
    /// </summary>
    public abstract class SpecialCode : Code
    {
        protected SpecialCode(SpecialCodeContext context)
        {
            Caster = context.Caster;
            CodeType = BaseEnums.CodeType.Special;
            ActivationType = BaseEnums.CodeActivationType.Special;
        }

        public virtual int OpenOrder => 0;
        public override bool HasValidTarget() => Caster != null && Caster.isActive;

        public override void StopCode()
        {
            if (Caster != null) Caster.isCasting = false;
            Combat.SpecialAction.NotifyResolved(Caster);
        }
    }
}
