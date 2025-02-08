using Mono.Cecil.Cil;
using UnityEngine;

public class CooldownController
{
  public Unit unit;
  public float passiveCd;
  public float normalCd;
  public float ultimateCd;

  public CooldownController()
  {
    passiveCd = 0;
    normalCd = 0;
    ultimateCd = 0;
  }

  public void SetUnit(Unit unit)
  {
    this.unit = unit;
    passiveCd = 0;
    normalCd = 0;
    ultimateCd = unit.ultimateCode != null ? unit.ultimateCode.cooldown : 0;
  }

  public void UpdateCooldown(float deltaTime)
  {
    if (unit == null)
    {
      return;
    }
    if (unit.isActive && !unit.isControlled)
    {
      passiveCd = Mathf.Max(0, passiveCd - deltaTime * unit.cooldownRate);
      normalCd = !unit.isCastingNormal ? Mathf.Max(0, normalCd - deltaTime * unit.cooldownRate) : normalCd;
      ultimateCd = !unit.isCastingUltimate ? Mathf.Max(0, ultimateCd - deltaTime * unit.cooldownRate) : ultimateCd;
    }
  }

  public void ResetCooldown(CodeBase.CodeType type)
  {
    if (unit == null)
    {
      return;
    }
    switch (type)
    {
      case CodeBase.CodeType.Passive:
        passiveCd = unit.passiveCode.cooldown;
        break;
      case CodeBase.CodeType.Normal:
        normalCd = unit.normalCode.cooldown;
        break;
      case CodeBase.CodeType.Ultimate:
        ultimateCd = unit.ultimateCode.cooldown;
        break;
    }
  }

  public CodeBase.CodeType CheckCodeTypeToCast()
  {
    if (unit == null)
    {
      return CodeBase.CodeType.None;
    }
    if (unit.isControlled || !unit.isActive || unit.isCastingNormal || unit.isCastingUltimate)
    {
      return CodeBase.CodeType.None;
    }
    if (passiveCd <= 0)
    {
      return CodeBase.CodeType.Passive;
    }
    if (normalCd <= 0)
    {
      if (ultimateCd <= 0 && unit.currentMana >= unit.maxMana)
      {
        return CodeBase.CodeType.Ultimate;
      }
      return CodeBase.CodeType.Normal;
    }
    return CodeBase.CodeType.None;
  }
}