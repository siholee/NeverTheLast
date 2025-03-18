using BaseClasses;

namespace Codes.Base
{
  public class PassiveCode : Code
  {
    public PassiveCode(PassiveCodeContext context)
    {
      Caster = context.Caster;
    }
  }
}