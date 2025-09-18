using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
  public class NormalAttack : NormalCode
  {
    private readonly HS_Poolable _prefab;

    public NormalAttack(NormalCodeContext context) : base(context)
    {
      CodeType = BaseEnums.CodeType.Normal;
      Caster = context.Caster;
      Cooldown = 2f;
      CodeName = "기본공격";
      CastingDelay = 0.5f;
      ManaAmount = 10;
      // effects = new Dictionary<string, OldEffectBase>();
      _prefab = GameManager.Instance.sfxManager.ProjectilePrefabs["FireBlast"];
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
      TargetUnits = Target.GetRandomSingleEnemy(Caster);
      bool isCrit = Random.value <= Caster.CritChanceCurr;
      float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
      DamageContext context = new(Caster, (int)(Caster.AtkCurr * 1.0f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SingleTarget }, isCrit);
      Caster.StartCoroutine(FireProjectile(TargetUnits, 0.5f, context));
      Caster.RecoverMana(ManaAmount);
      StopCode();
    }

    public override void StopCode()
    {
      Caster.normalCooldown = Cooldown;
      Caster.isCasting = false;
    }

    private IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
    {
      foreach (var target in targets)
      {
        GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, target, delay);
        yield return new WaitForSeconds(delay);
        target.TakeDamage(context);
      }
    }

    public override bool HasValidTarget()
    {
      return Target.GetRandomSingleEnemy(Caster).Count > 0;
    }
  }
}