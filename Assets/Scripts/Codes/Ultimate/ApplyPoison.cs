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
  public class ApplyPoison : UltimateCode
  {
    private readonly HS_Poolable _prefab;
    public ApplyPoison(UltimateCodeContext context) : base(context)
    {
      CodeType = BaseEnums.CodeType.Ultimate;
      Caster = context.Caster;
      Cooldown = 4f;
      CodeName = "독부여";
      CastingDelay = 0.5f;
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
      DamageContext context = new(Caster, 0, BaseEnums.CodeType.Ultimate, new List<int> { DamageTag.AllTarget }, false);
      foreach (var unit in TargetUnits)
      {
        Caster.StartCoroutine(FireProjectile(unit, Random.Range(0.1f, 0.1f), context));
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
      string identifier = $"PoisonEffect{Random.Range(0f, 100f)}";
      var buffEffect = new PoisonEffect(Caster, identifier, (int)(Caster.AtkCurr * 0.1f));
      target.AddStatusEffect(identifier, buffEffect);
    }

    public override bool HasValidTarget()
    {
      return GridManager.Instance.TargetAllEnemies(Caster).Count > 0;
    }
  }
}