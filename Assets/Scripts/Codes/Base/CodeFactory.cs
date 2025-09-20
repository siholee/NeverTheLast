using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;

namespace Codes.Base
{
  public static class CodeFactory
  {
    public static PassiveCode CreatePassiveCode(int codeId, PassiveCodeContext context)
    {
      return codeId switch
      {
        1 => new HolyEnchant(context),
        _ => null,
      };
    }

    // 더 이상 사용되지 않음 - 모든 유닛이 NormalAttack을 사용
    /*
    public static NormalCode CreateNormalCode(int codeId, NormalCodeContext context)
    {
      return codeId switch
      {
        1 => new NormalAttack(context), // FireBlast 대신 NormalAttack 사용
        2 => new AuricMandate(context),
        _ => null,
      };
    }
    */

    public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
    {
      return codeId switch
      {
        1 => new Laevateinn(context),
        2 => new ApplyPoison(context),
        _ => null,
      };
    }
  }
}