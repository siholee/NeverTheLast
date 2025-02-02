using System.Collections.Generic;
using Unity.VisualScripting;

public class AttrMod
{
  public const int ATK_ADD = 1;
  public const int ATK_MUL = 2;
  public const int DEF_ADD = 3;
  public const int DEF_MUL = 4;
  public const int HP_ADD = 5;
  public const int HP_MUL = 6;
  public const int CRIT_CHANCE_ADD = 7;
  public const int CRIT_DMG_ADD = 8;
  public const int CD_ADD = 9;
  public const int CD_MUL = 10;
}

public class DamageTag
{
  public const int SINGLE_TARGET = 10001;
  public const int MULTI_TARGET = 10002;
  public const int ALL_TARGET = 10003;
  public const int NORMAL_ATTACK = 20001;
  public const int ULT_ATTACK = 20002;
}

public class Helper
{
}