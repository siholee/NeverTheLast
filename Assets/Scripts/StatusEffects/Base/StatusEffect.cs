using Entities;

namespace StatusEffects.Base
{
  public abstract class StatusEffect
  {
    public int Stack;
    public float Duration;
    
    public virtual int HpAdditiveModifier(Unit unit)
    {
      return 0;
    }
    public virtual float HpMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual int AtkAdditiveModifier(Unit unit)
    {
      return 0;
    }
    public virtual float AtkMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual int DefAdditiveModifier(Unit unit)
    {
      return 0;
    }
    public virtual float DefMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CritChanceAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CritMultiplierAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CodeAccelerationMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float ReceivingDamageModifier(Unit unit)
    {
      return 1f;
    }
  }
}