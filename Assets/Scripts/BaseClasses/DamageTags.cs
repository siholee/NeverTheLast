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
    public const int AdditionalAttack = 20005; // 추가 공격 — 기본 행동과 별개로 발생
    public const int SummonAttack = 20006;     // 소환수의 공격 — 소환자의 주는 피해 버프를 받지 않는다
    public const int CounterAttack = 20007;    // 반격 — 피격 또는 체력 소모에 반응해 발생
    public const int ContactAttack = 30001;    // 접촉 공격
    public const int NonContactAttack = 30002; // 비접촉 공격
    public const int ShieldPenetration = 40001; // 방어막 관통
    public const int TrueDamage = 40002;        // 고정 피해 — 방어력 감쇠를 무시한다
    public const int DurabilityPenetration = 40003; // 내구 관통 — 고정 경감(내구)을 무시한다
    public const int ToughnessEcho = 40004;     // 강인도 감소량을 실피해로 전환한 추가 피해 — 강인도를 다시 깎지 않는다
    public const int Slash = 50001; // 베기
    public const int Pierce = 50002; // 찌르기
    public const int Arrow = 50003; // 화살
  }
}
