using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
  /// <summary>
  /// 홀리 인챈트 효과 - 중첩 불가능
  /// 공격력 +20%, 치명타율 +10% 증가
  /// </summary>
  public class HolyEnchantEffect : StatusEffect
  {
    public HolyEnchantEffect(Unit grantor) : base(grantor, null)
    {
      Stack = 1; // 중첩 불가능
      Duration = 0f; // 영구 지속 (시전자가 죽을 때까지)
    }
    
    public override float AtkMultiplicativeModifier(Unit unit)
    {
      return 0.2f; // 공격력 +20%
    }

    public override float CritChanceAdditiveModifier(Unit unit)
    {
      return 0.1f; // 치명타율 +10%
    }
  }
}