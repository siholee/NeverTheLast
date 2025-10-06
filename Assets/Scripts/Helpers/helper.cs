namespace Helpers
{
  public class AttrMod
  {
    public const int AtkAdd = 1;
    public const int AtkMul = 2;
    public const int DefAdd = 3;
    public const int DefMul = 4;
    public const int HpAdd = 5;
    public const int HpMul = 6;
    public const int CritChanceAdd = 7;
    public const int CritDmgAdd = 8;
    public const int CdAdd = 9;
    public const int CdMul = 10;
  }

  public class DamageTag
  {
    public const int SingleTarget = 10001;
    public const int MultiTarget = 10002;
    public const int AllTarget = 10003;
    public const int NormalAttack = 20001;
    public const int UltAttack = 20002;
    public const int ContactAttack = 30001;    // 접촉 공격
    public const int NonContactAttack = 30002; // 비접촉 공격
    public const int ShieldPenetration = 40001; // 방어막 관통
  }

  public class Helper
  {
  }
}