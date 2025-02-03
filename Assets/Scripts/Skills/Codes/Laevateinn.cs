using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laevateinn : CodeBase
{
  public Laevateinn(CodeCreationContext context)
  {
    caster = context.caster;
    cooldown = 10f;
    codeName = "레바테인";
    effects = new Dictionary<string, EffectBase>();
  }

  public override IEnumerator StartCode()
  {
    caster.isCasting = true;
    targetUnits = GridManager.Instance.TargetAllEnemies(caster);
    effects = new Dictionary<string, EffectBase>
    {
        { "Damage", new InstantDamage(targetUnits, new List<int> {DamageTag.ALL_TARGET}, (int)(caster.atk * 3f)) }
    };
    yield return new WaitForSeconds(3f);
    foreach (var effect in effects)
    {
      effect.Value.ApplyEffect();
    }
    GameManager.Instance.skillManager.DeregisterSkill(caster, this);
    yield return null;
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
    effects.Clear();
    caster.isCasting = false;
    yield return null;
  }

  public override bool CanCast()
  {
    return GridManager.Instance.TargetAllEnemies(caster).Count > 0;
  }
}