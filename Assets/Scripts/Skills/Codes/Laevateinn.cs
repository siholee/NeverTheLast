using System.Collections;
using System.Collections.Generic;

public class Laevateinn : CodeBase
{
  public Laevateinn(CodeCreationContext context)
  {
    caster = context.caster;
    targetUnits = context.targetUnits;
    cooldown = context.cooldown;
    codeName = context.name;
    effects = new Dictionary<string, EffectBase>
    {
        { "Damage", new InstantDamage(targetUnits, new List<int> {DamageTag.ALL_TARGET}, (int)(caster.atk * 3f)) }
    };
  }

  public override IEnumerator StartCode()
  {
    foreach (var unit in targetUnits)
    {
      foreach (var effect in effects)
      {
        effect.Value.ApplyEffect();
      }
    }
    yield return StopCode();
  }

  public override IEnumerator StopCode()
  {
    foreach (var unit in targetUnits)
    {
      foreach (var effect in effects)
      {
        effect.Value.RemoveEffect();
      }
    }
    yield return null;
  }
}