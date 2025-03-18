using BaseClasses;

namespace Codes.Base
{
  public abstract class UltimateCode : Code
  {
    public UltimateCode(UltimateCodeContext context)
    {
      Caster = context.Caster;
    }
  }
}