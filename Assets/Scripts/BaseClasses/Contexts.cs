using System.Collections.Generic;
using Entities;

namespace BaseClasses
{
  public class PassiveCodeContext
  {
    public Unit Caster;
    public string Name; // 패시브 이름 (GenericPassive용)
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
      Attacker = attacker;
      Damage = damage;
      IsCrit = isCrit;
      CodeType = codeType;
      DamageTags = damageTags;
      Penetration = penetration;
    }
  }

  public class EventContext
  {
    public readonly Unit Grantee;
    public readonly Unit Grantor;
    public readonly DamageContext DmgCtx;
    public readonly float FloatParam;
    
    public EventContext(Unit grantee, Unit grantor = null, DamageContext dmgCtx = null, float floatParam = 0f)
    {
      Grantee = grantee;
      Grantor = grantor;
      DmgCtx = dmgCtx;
      FloatParam = floatParam;
    }
  }

  public class ControlContext
  {
    public Unit Attacker;
    public readonly float Duration;

    public ControlContext(Unit attacker, float duration)
    {
      Attacker = attacker;
      Duration = duration;
    }
  }
}