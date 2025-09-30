using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
  public class Laevateinn : UltimateCode
  {
    private readonly HS_Poolable _prefab;
    public Laevateinn(UltimateCodeContext context) : base(context)
    {
      CodeType = BaseEnums.CodeType.Ultimate;
      Caster = context.Caster;
      Cooldown = 10f;
      CodeName = "레바테인";
      CastingDelay = 2f;
      _prefab = GameManager.Instance.sfxManager.ProjectilePrefabs["Levateinn"];
    }

    public override void CastCode()
    {
      Caster.isCasting = true;
      Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
      CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
    }

    protected override IEnumerator SkillCoroutine()
    {
      // 캐스팅
      float elapsedTime = 0f;
      while (elapsedTime < CastingDelay)
      {
        if (Caster.isControlled || !Caster.isActive)
        {
          Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})의 {CodeName} 시전이 방해됨");
          StopCode();
          yield break;
        }
        elapsedTime += Time.deltaTime;
        yield return null;
      }

      // 효과 처리
      TargetUnits = GridManager.Instance.TargetAllEnemies(Caster);
      bool isCrit = Random.value <= Caster.CritChanceCurr;
      float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
      DamageContext context = new(Caster, (int)(Caster.AtkCurr * 3f * critMultiplier), BaseEnums.CodeType.Ultimate, new List<int> { DamageTag.AllTarget }, isCrit);
      foreach (var unit in TargetUnits)
      {
        Caster.StartCoroutine(FireProjectile(unit, Random.Range(0.1f, 0.5f), context));
      }
      StopCode();
    }

    public override void StopCode()
    {
      Caster.ultimateCooldown = Cooldown;
      Caster.isCasting = false;
    }

    private IEnumerator FireProjectile(Unit target, float delay, DamageContext context)
    {
      GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, target, delay);
      yield return new WaitForSeconds(delay);
      target.TakeDamage(context);
    }

    public override bool HasValidTarget()
    {
      return GridManager.Instance.TargetAllEnemies(Caster).Count > 0;
    }
  }
}