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
      // 캐스팅 연출
      if (CastingDelay > 0f)
        yield return new WaitForSeconds(CastingDelay);

      // 효과 처리 - 전체 대상
      TargetUnits = GridManager.Instance.TargetAllEnemies(Caster);
      bool isCrit = Random.value <= Caster.CritChanceCurr;
      float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
      DamageContext context = new(Caster, (int)(Caster.AtkCurr * 3f * critMultiplier), BaseEnums.CodeType.Ultimate, new List<int> { DamageTag.AllTarget }, isCrit);

      // 모든 대상에 투사체 발사 및 데미지 적용
      float maxDelay = 0f;
      foreach (var unit in TargetUnits)
      {
        float delay = Random.Range(0.1f, 0.5f);
        if (delay > maxDelay) maxDelay = delay;
        GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, unit, delay);
      }
      // 가장 긴 투사체가 도착할 때까지 대기
      yield return new WaitForSeconds(maxDelay + 0.1f);

      // 데미지 일괄 적용
      foreach (var unit in TargetUnits)
      {
        if (unit.isActive)
        {
          unit.TakeDamage(context);
        }
      }

      StopCode();
    }

    public override void StopCode()
    {
      Caster.isCasting = false;
    }

    public override bool HasValidTarget()
    {
      return GridManager.Instance.TargetAllEnemies(Caster).Count > 0;
    }
  }
}