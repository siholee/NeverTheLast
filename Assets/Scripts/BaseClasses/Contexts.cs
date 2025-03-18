using System.Collections.Generic;
using Entities;

namespace BaseClasses
{
  public class PassiveCodeContext
  {
    public Unit Caster;
  }

  public class NormalCodeContext
  {
    public Unit Caster;
  }

  public class UltimateCodeContext
  {
    public Unit Caster;
  }

  public class DamageContext
  {
    public readonly Unit Attacker;
    public readonly int Damage;
    public readonly bool IsCrit;
    public BaseEnums.CodeType CodeType;
    public List<int> DamageTags;
    public readonly int Penetration;

    public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags, bool isCrit = false, int penetration = 0)
    {
      this.Attacker = attacker;
      this.Damage = damage;
      this.IsCrit = isCrit;
      this.CodeType = codeType;
      this.DamageTags = damageTags;
      this.Penetration = penetration;
    }
  }

  public class ControlContext
  {
    public Unit Attacker;
    public readonly float Duration;

    public ControlContext(Unit attacker, float duration)
    {
      this.Attacker = attacker;
      this.Duration = duration;
    }
  }
}