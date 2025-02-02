using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireBlast: CodeBase
{
  private float duration = 0.5f;
  public FireBlast(CodeCreationContext context)
  {
    caster = context.caster;
    targetUnits = context.targetUnits;
    cooldown = context.cooldown;
    codeName = context.name;
    InstantDamage instantDamage = new(targetUnits, new List<int> {DamageTag.SINGLE_TARGET}, (int)(caster.atk * 1.5f));
    effects.Add("Damage", instantDamage);
  }

  public override IEnumerator StartCode()
  {
    yield return new WaitForSeconds(duration);
    effects["Damage"].ApplyEffect();
    StartCoroutine(StopCode());
  }

  public override IEnumerator StopCode()
  {
    // FireBlast 코드의 종료 부분
    // 이펙트를 제거하는 코드
    foreach (var effect in effects)
    {
      effect.Value.RemoveEffect();
    }
    yield return null;
  }
}