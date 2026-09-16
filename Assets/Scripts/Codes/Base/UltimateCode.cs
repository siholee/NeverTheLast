using BaseClasses;

namespace Codes.Base
{
  public abstract class UltimateCode : Code
  {
    /// <summary>
    /// 자원이 최대일 때 행동 스케줄러가 자동 시전할지 여부.
    /// 천총운검처럼 패시브가 직접 굴리는 상시형 궁극기는 false를 사용한다.
    /// </summary>
    public virtual bool IsAutoCast => true;
    /// <summary>복합 자원 궁극기는 정상 해결 시점에 직접 소비한다.</summary>
    public virtual bool ConsumesResourceOnResolve => false;

    public UltimateCode(UltimateCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Ultimate;
    }
  }
}
