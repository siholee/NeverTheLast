namespace BaseClasses
{
  public class DamageTag
  {
    public const int SingleTarget = 10001;
    public const int MultiTarget = 10002;
    public const int AllTarget = 10003;
    public const int SplitDamage = 10004;
    public const int NormalAttack = 20001;
    public const int UltAttack = 20002;
    public const int Physical = 20003; // 물리
    public const int Special = 20004; // 특수
    public const int ContactAttack = 30001;    // 접촉 공격
    public const int NonContactAttack = 30002; // 비접촉 공격
    public const int ShieldPenetration = 40001; // 방어막 관통
    public const int TrueDamage = 40002;        // 고정 피해 — 방어력 감쇠를 무시한다
    public const int Slash = 50001; // 베기
  }
}
