using BaseClasses;

namespace Codes.Base
{
  public abstract class UltimateCode : Code
  {
    /// <summary>
    /// 자원이 최대일 때 행동 스케줄러가 자동 시전할지 여부.
    /// 라그나로크처럼 자원 자체가 상시 효과인 궁극기는 false를 사용한다.
    /// </summary>
    public virtual bool IsAutoCast => true;

    public UltimateCode(UltimateCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Ultimate;
    }
  }
}
