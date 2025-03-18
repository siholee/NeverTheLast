using BaseClasses;

namespace Codes.Base
{
  public class NormalCode : Code
  {
    public int ManaAmount; // 생성 마나량

    public NormalCode(NormalCodeContext context)
    {
      Caster = context.Caster;
    }
  }
}