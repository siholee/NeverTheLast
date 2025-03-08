using System.Collections;
using System.Collections.Generic;
using CGT.Pooling;
using UnityEngine;

public class Laevateinn : UltimateCode
{
  private HS_Poolable prefab;
  public Laevateinn(UltimateCodeContext context) : base(context)
  {
    codeType = BaseEnums.CodeType.Ultimate;
    caster = context.caster;
    cooldown = 10f;
    codeName = "레바테인";
    castingDelay = 2f;
    prefab = GameManager.Instance.sfxManager.projectilePrefabs["Levateinn"];
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
    targetUnits = GridManager.Instance.TargetAllEnemies(caster);
    bool isCrit = Random.value <= caster.critChanceCurr;
    float critMultiplier = isCrit ? caster.critMultiplierCurr : 1f;
    DamageContext context = new(caster, (int)(caster.atkCurr * 3f * critMultiplier), BaseEnums.CodeType.Ultimate, new List<int> { DamageTag.ALL_TARGET }, isCrit);
    foreach (var unit in targetUnits)
    {
      caster.StartCoroutine(FireProjectile(unit, Random.Range(0.1f, 0.5f), context));
    }
    StopCode();
  }

  public override void StopCode()
  {
    caster.ultimateCooldown = cooldown;
    caster.isCasting = false;
  }

  private IEnumerator FireProjectile(Unit target, float delay, DamageContext context)
  {
    GameManager.Instance.sfxManager.FireSingleProjectile(prefab, caster, target, delay);
    yield return new WaitForSeconds(delay);
    target.TakeDamage(context);
  }

  public override bool HasValidTarget()
  {
    return GridManager.Instance.TargetAllEnemies(caster).Count > 0;
  }
}