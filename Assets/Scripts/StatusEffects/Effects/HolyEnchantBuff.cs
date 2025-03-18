using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
  public class HolyEnchantBuff : StatusEffect
  {
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