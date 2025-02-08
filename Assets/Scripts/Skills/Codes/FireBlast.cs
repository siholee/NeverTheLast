using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireBlast : CodeBase
{
  public FireBlast(CodeCreationContext context)
  {
    codeType = CodeType.Normal;
    caster = context.caster;
    cooldown = 2f;
    codeName = "불알";
    duration = 0.5f;
    manaAmount = 10;
    effects = new Dictionary<string, EffectBase>();
  }

  public override IEnumerator StartCode()
  {
    caster.isCastingNormal = true;
    targetUnits = GridManager.Instance.TargetNearestEnemy(caster);
    InstantDamage instantDamage = new(targetUnits, new List<int> { DamageTag.SINGLE_TARGET }, (int)(caster.atk * 1.5f));
    effects.Add("Damage", instantDamage);
    yield return new WaitForSeconds(duration);
    effects["Damage"].ApplyEffect();
    GameManager.Instance.skillManager.DeregisterSkill(caster, this);
  }

  public override IEnumerator StopCode()
  {
    // FireBlast 코드의 종료 부분
    // 이펙트를 제거하는 코드
    foreach (var effect in effects)
    {
      effect.Value.RemoveEffect();
    }
    effects.Clear();
    caster.isCastingNormal = false;
    caster.RecoverMana(manaAmount);
    yield return null;
  }

  public override bool CanCast()
  {

    return GridManager.Instance.TargetNearestEnemy(caster).Count == 1;
  }
}