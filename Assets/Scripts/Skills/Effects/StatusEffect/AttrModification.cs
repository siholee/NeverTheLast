using System.Collections.Generic;

public class AttrModification : StatusEffect
{
  public Dictionary<int, int> attributeModification;

  public AttrModification(string effectName, Unit caster, Unit target, float duration, Dictionary<int, int> modifications)
  {
    this.caster = caster;
    this.effectName = effectName;
    targets = new List<Unit> { target };
    attributeModification = modifications;
    if (duration < 0)
    {
      persistantType = PersistantType.Permanent;
    }
    else
    {
      timer = duration;
    }
  }

  public AttrModification(string effectName, Unit caster, List<Unit> targets, float duration, Dictionary<int, int> modifications)
  {
    this.caster = caster;
    this.effectName = effectName;
    this.targets = targets;
    attributeModification = modifications;
  }

  public override void ApplyEffect()
  {
    foreach (Unit target in targets)
    {
      foreach (KeyValuePair<int, int> modification in attributeModification)
      {
        switch (modification.Key)
        {
          case AttrMod.ATK_ADD:
            target.atkAdditiveBuff += modification.Value;
            break;
          case AttrMod.ATK_MUL:
            target.atkMultiplicativeBuff += modification.Value;
            break;
          case AttrMod.DEF_ADD:
            target.defAdditiveBuff += modification.Value;
            break;
          case AttrMod.DEF_MUL:
            target.defMultiplicativeBuff += modification.Value;
            break;
          case AttrMod.HP_ADD:
            target.hpAdditiveBuff += modification.Value;
            break;
          case AttrMod.HP_MUL:
            target.hpMultiplicativeBuff += modification.Value;
            break;
          case AttrMod.CRIT_CHANCE_ADD:
            target.critChanceBuff += modification.Value;
            break;
          case AttrMod.CRIT_DMG_ADD:
            target.critDamageBuff += modification.Value;
            break;
          case AttrMod.CD_ADD:
            target.cooldownAdditiveBuff += modification.Value;
            break;
          case AttrMod.CD_MUL:
            target.cooldownMultiplicativeBuff += modification.Value;
            break;
        }
      }
    }
  }

  public override void RemoveEffect()
  {
    foreach (Unit target in targets)
    {
      foreach (KeyValuePair<int, int> modification in attributeModification)
      {
        switch (modification.Key)
        {
          case AttrMod.ATK_ADD:
            target.atkAdditiveBuff -= modification.Value;
            break;
          case AttrMod.ATK_MUL:
            target.atkMultiplicativeBuff -= modification.Value;
            break;
          case AttrMod.DEF_ADD:
            target.defAdditiveBuff -= modification.Value;
            break;
          case AttrMod.DEF_MUL:
            target.defMultiplicativeBuff -= modification.Value;
            break;
          case AttrMod.HP_ADD:
            target.hpAdditiveBuff -= modification.Value;
            break;
          case AttrMod.HP_MUL:
            target.hpMultiplicativeBuff -= modification.Value;
            break;
          case AttrMod.CRIT_CHANCE_ADD:
            target.critChanceBuff -= modification.Value;
            break;
          case AttrMod.CRIT_DMG_ADD:
            target.critDamageBuff -= modification.Value;
            break;
          case AttrMod.CD_ADD:
            target.cooldownAdditiveBuff -= modification.Value;
            break;
          case AttrMod.CD_MUL:
            target.cooldownMultiplicativeBuff -= modification.Value;
            break;
        }
      }
    }
  }
}