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

  public static NormalCode CreateNormalCode(int codeId, NormalCodeContext context)
  {
    return codeId switch
    {
      1 => new FireBlast(context),
      2 => new AuricMandate(context),
      _ => null,
    };
  }

  public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
  {
    return codeId switch
    {
      1 => new Laevateinn(context),
      _ => null,
    };
  }
}