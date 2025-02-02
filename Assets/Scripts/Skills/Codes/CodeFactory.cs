public static class CodeFactory
{
  public static CodeBase CreateCode(int codeId, CodeCreationContext context)
  {
    switch (codeId)
    {
      case 1:
        return new HolyEnchant(context);
      case 2:
        return new FireBlast(context);
      case 3:
        return new Laevateinn(context);
      default:
        return null;
    }
  }
}