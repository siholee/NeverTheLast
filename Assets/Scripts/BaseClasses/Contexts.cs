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
    /// <summary>스킬의 원소 속성 — 원소 반응 계산에 사용됨. 기본값 Physical = 반응 없음.</summary>
    public readonly BaseEnums.ElementType Element;

    /// <summary>기존 생성자 (Element = Physical 기본값으로 원소 반응 없음)</summary>
    public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags, bool isCrit = false, int penetration = 0)
    {
      this.Attacker = attacker;
      this.Damage = damage;
      this.IsCrit = isCrit;
      this.CodeType = codeType;
      this.DamageTags = damageTags;
      this.Penetration = penetration;
      this.Element = BaseEnums.ElementType.Physical;
    }

    /// <summary>원소 속성 포함 생성자</summary>
    public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags,
                         BaseEnums.ElementType element, bool isCrit = false, int penetration = 0)
    {
      this.Attacker = attacker;
      this.Damage = damage;
      this.IsCrit = isCrit;
      this.CodeType = codeType;
      this.DamageTags = damageTags;
      this.Penetration = penetration;
      this.Element = element;
    }
  }

  public class ControlContext
  {
    public Unit Attacker;
    public readonly int TurnDuration;

    public ControlContext(Unit attacker, int turnDuration)
    {
      this.Attacker = attacker;
      this.TurnDuration = turnDuration;
    }
  }
}