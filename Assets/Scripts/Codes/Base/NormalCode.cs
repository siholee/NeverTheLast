using BaseClasses;

namespace Codes.Base
{
  public class NormalCode : Code
  {
    public NormalCode(NormalCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Active;
    }
  }
}
