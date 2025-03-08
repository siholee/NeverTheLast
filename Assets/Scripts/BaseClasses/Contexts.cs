using System.Collections.Generic;

public class PassiveCodeContext
{
  public Unit caster;
}

public class NormalCodeContext
{
  public Unit caster;
}

public class UltimateCodeContext
{
  public Unit caster;
}

public class DamageContext
{
  public Unit attacker;
  public int damage;
  public bool isCrit;
  public BaseEnums.CodeType codeType;
  public List<int> damageTags;
  public int penetration;

  public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags, bool isCrit = false, int penetration = 0)
  {
    this.attacker = attacker;
    this.damage = damage;
    this.isCrit = isCrit;
    this.codeType = codeType;
    this.damageTags = damageTags;
    this.penetration = penetration;
  }
}

public class ControlContext
{
  public Unit attacker;
  public float duration;

  public ControlContext(Unit attacker, float duration)
  {
    this.attacker = attacker;
    this.duration = duration;
  }
}