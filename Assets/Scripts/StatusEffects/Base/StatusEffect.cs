using Entities;
using BaseClasses;

namespace StatusEffects.Base
{
  public abstract class StatusEffect
  {
    public int Stack;
    public float Duration;
    public Unit Grantor;
    public string Identifier;
    public virtual bool IsBeneficial => false;

    protected StatusEffect(Unit grantor, string identifier)
    {
      Grantor = grantor;
      Identifier = identifier;
    }
    
    public virtual float CritChanceAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CritMultiplierAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float ReceivingDamageModifier(Unit unit)
    {
      return 1f;
    }
    public virtual int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
    {
      return 0;
    }
    public virtual float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
    {
      return 1f;
    }
    public virtual float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
    {
      return 1f;
    }
    public virtual float ManaRecoveryMultiplierModifier(Unit unit)
    {
      return 1f;
    }
    public virtual float ShieldBonusAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CodeAccelerationAdditiveModifier(Unit unit)
    {
      return 0f;
    }
  }
}
