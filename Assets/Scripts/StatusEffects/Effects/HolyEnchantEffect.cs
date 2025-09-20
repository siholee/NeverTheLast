using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
  public class HolyEnchantEffect : StatusEffect
  {
    public HolyEnchantEffect(Unit grantor) : base(grantor, null)
    {
      Stack = 1;
      Duration = 0f;
    }
    
    public override float AtkMultiplicativeModifier(Unit unit)
    {
      return 0.2f;
    }

    public override float CritChanceAdditiveModifier(Unit unit)
    {
      return 0.1f;
    }
  }
}