using BaseClasses;

namespace Codes.Base
{
  public class NormalCode : Code
  {
    /// <summary>
    /// (사문) 일반공격 1회로 얻던 궁극기 자원.
    /// 자원이 전투 시간 × INT로 차오르게 바뀌면서 더 이상 쓰이지 않는다.
    /// 데이터·프리팹 호환을 위해 필드만 남겨 둔다.
    /// </summary>
    public int ManaAmount;

    public NormalCode(NormalCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Active;
    }
  }
}
