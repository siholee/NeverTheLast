using System.Collections.Generic;
using Entities;

namespace BaseClasses
{
  public class PassiveCodeContext
  {
    public Unit Caster;
    public string Name; // 데이터에 등록된 패시브 표시 이름
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
    public bool IsCrit;
    public BaseEnums.CodeType CodeType;
    public List<int> DamageTags;
    public readonly int Penetration;
    /// <summary>내구 고정 경감 중 무시할 양. 전부 무시는 DurabilityPenetration 태그를 사용한다.</summary>
    public readonly int DurabilityPenetration;
    public float DefenseStatMultiplier;
    public bool IsCancelled;

    public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags,
      bool isCrit = false, int penetration = 0, int durabilityPenetration = 0)
    {
      Attacker = attacker;
      Damage = damage;
      IsCrit = isCrit;
      CodeType = codeType;
      DamageTags = damageTags;
      Penetration = penetration;
      DurabilityPenetration = System.Math.Max(0, durabilityPenetration);
      DefenseStatMultiplier = 1f;
      IsCancelled = false;
    }
  }

  public class DamageResolvedContext
  {
    public readonly Unit Attacker;
    public readonly Unit Target;
    public readonly DamageContext DamageContext;
    public readonly int DamageDealt;

    public DamageResolvedContext(Unit attacker, Unit target, DamageContext damageContext, int damageDealt)
    {
      Attacker = attacker;
      Target = target;
      DamageContext = damageContext;
      DamageDealt = damageDealt;
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

    /// <summary>행동 불가 <b>턴</b> 수. 전투가 턴제이므로 초가 아니다.</summary>
    public readonly int Duration;

    public ControlContext(Unit attacker, int durationTurns)
    {
      Attacker = attacker;
      Duration = durationTurns;
    }
  }
}
