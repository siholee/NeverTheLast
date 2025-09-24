using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Ultimate
{
  /// <summary>
  /// 화상 부여 코드
  /// 모든 적에게 화상 상태이상을 부여합니다.
  /// </summary>
  public class ApplyBurn : UltimateCode
  {
    private readonly HS_Poolable _prefab;
    
    public ApplyBurn(UltimateCodeContext context) : base(context)
    {
      CodeType = BaseEnums.CodeType.Ultimate;
      Caster = context.Caster;
      Cooldown = 4f;
      CodeName = "화상부여";
      CastingDelay = 0.5f;
      _prefab = GameManager.Instance.sfxManager.ProjectilePrefabs["FireBlast"]; // 화상에 맞는 이펙트 사용
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
      DamageContext context = new(Caster, 0, BaseEnums.CodeType.Ultimate, new List<int> { DamageTag.AllTarget }, false);
      foreach (var unit in TargetUnits)
      {
        Caster.StartCoroutine(FireProjectile(unit, Random.Range(0.1f, 0.3f), context));
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
      
      // 화상 효과 부여 - 고정 식별자로 중복 적용 시 지속시간 연장
      string identifier = "BurnEffect";
      var burnEffect = new BurnEffect(Caster, identifier);
      target.AddStatusEffect(identifier, burnEffect);
      
      Debug.Log($"{Caster.UnitName}이 {target.UnitName}에게 화상 부여");
    }

    public override bool HasValidTarget()
    {
      return GridManager.Instance.TargetAllEnemies(Caster).Count > 0;
    }
  }
}