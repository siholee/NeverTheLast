namespace BaseClasses
{
  /// <summary>
  /// 피해 태그. <b>만의 자리가 분류</b>이고, 공격 하나는 원칙적으로 분류마다 하나씩 가진다.
  /// 바스테트 U122처럼 일반행동을 그대로 재사용하는 추가행동은 예외적으로
  /// <see cref="NormalAttack"/>과 <see cref="AdditionalAttack"/>을 함께 가진다.
  ///   10000 대상 범위 · 20000 공격 종류 · 30000 접촉 · 40000 판정 규칙
  ///
  /// 무기 분류(50000)는 없앴다. 기본 공격은 방어력, 지속·반응 및 명시된 특수 피해는
  /// 특수방어력으로 경감한다 — <see cref="Combat.DamageMitigation"/>을 본다.
  /// </summary>
  public class DamageTag
  {
    public const int SingleTarget = 10001;
    public const int MultiTarget = 10002;
    public const int AllTarget = 10003;

    public const int NormalAttack = 20001;
    public const int UltAttack = 20002;
    public const int SpecialAttack = 20003;    // 특정 트리거가 여는 특수행동
    public const int AdditionalAttack = 20005; // 추가행동 — 기본 행동과 별개로 발생
    public const int CounterAttack = 20007;    // 반격 — 피격에 반응해 발생
    public const int PassiveAttack = 20009;    // 특성(패시브)이 직접 가하는 공격
    public const int ElementalReaction = 20010; // 원소 반응 피해 — 특수방어력으로 경감한다

    public const int ContactAttack = 30001;    // 접촉 공격 — 카드가 직접 날아가 부딪힌다
    public const int NonContactAttack = 30002; // 비접촉 공격 — 투사체 · 광역

    public const int ShieldPenetration = 40001; // 방어막 관통
    public const int TrueDamage = 40002;        // 고정 피해 — 방어력 · 특수방어력 경감을 무시한다
    public const int SpecialDamage = 40004;     // #특수 피해 — 특수방어력으로 경감한다

    /// <summary>
    /// 다른 피해에 <b>덧붙어</b> 나간 추가 피해. 덧붙는 효과가 이 태그를 보고 다시 덧붙지 않는다.
    /// 세이의 '창세의 노래'처럼 "피해를 가할 때마다 추가 피해"가 서로를 무한히 부르는 것을 막는다.
    /// </summary>
    public const int BonusDamage = 40003;
  }
}
