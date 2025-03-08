using System.Collections;
using System.Collections.Generic;
using CGT.Pooling;
using UnityEngine;

public class FireBlast : NormalCode
{
  private HS_Poolable prefab;

  public FireBlast(NormalCodeContext context) : base(context)
  {
    codeType = BaseEnums.CodeType.Normal;
    caster = context.caster;
    cooldown = 2f;
    codeName = "불알";
    castingDelay = 0.5f;
    manaAmount = 10;
    // effects = new Dictionary<string, OldEffectBase>();
    prefab = GameManager.Instance.sfxManager.projectilePrefabs["FireBlast"];
  }

  public override void CastCode()
  {
    caster.isCasting = true;
    Debug.Log($"{caster.unitName}({caster.currentCell.xPos}, {caster.currentCell.yPos})이 {codeName} 시전");
    skillCoroutine = caster.StartCoroutine(SkillCoroutine());
  }

  protected override IEnumerator SkillCoroutine()
  {
    // 캐스팅
    float elapsedTime = 0f;
    while (elapsedTime < castingDelay)
    {
      if (caster.isControlled || !caster.isActive)
      {
        Debug.Log($"{caster.unitName}({caster.currentCell.xPos}, {caster.currentCell.yPos})의 {codeName} 시전이 방해됨");
        StopCode();
        yield break;
      }
      elapsedTime += Time.deltaTime;
      yield return null;
    }

    // 효과 처리
    targetUnits = GridManager.Instance.TargetNearestEnemy(caster);
    bool isCrit = Random.value <= caster.critChanceCurr;
    float critMultiplier = isCrit ? caster.critMultiplierCurr : 1f;
    DamageContext context = new(caster, (int)(caster.atkCurr * 1.0f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SINGLE_TARGET }, isCrit);
    caster.StartCoroutine(FireProjectile(targetUnits, 0.5f, context));
    caster.RecoverMana(manaAmount);
    StopCode();
  }

  public override void StopCode()
  {
    caster.normalCooldown = cooldown;
    caster.isCasting = false;
  }

  private IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
  {
    foreach (var target in targets)
    {
      GameManager.Instance.sfxManager.FireSingleProjectile(prefab, caster, target, delay);
      yield return new WaitForSeconds(delay);
      target.TakeDamage(context);
    }
  }

  public override bool HasValidTarget()
  {
    return GridManager.Instance.TargetNearestEnemy(caster).Count > 0;
  }
}