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
  public class FireBlast : NormalCode
  {
    private readonly HS_Poolable _prefab;

    public FireBlast(NormalCodeContext context) : base(context)
    {
      CodeType = BaseEnums.CodeType.Normal;
      Caster = context.Caster;
      Cooldown = 2f;
      CodeName = "불알";
      CastingDelay = 0.5f;
      ManaAmount = 10;
      Element       = BaseEnums.ElementType.Pyro;
      SkillCategory = BaseEnums.SkillCategory.Basic;  // 기본 공격 (SP 생성)
      SpCost        = 0;
      TargetType    = BaseEnums.TargetType.Single;    // 단일 타겟 (BattleManager가 사전 결정)
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
      // 캐스팅 연출
      if (CastingDelay > 0f)
        yield return new WaitForSeconds(CastingDelay);

      // TargetUnits는 BattleManager.ResolveTargetsCoroutine()이 이미 설정함
      if (TargetUnits == null || TargetUnits.Count == 0)
      {
          StopCode();
          yield break;
      }
      bool isCrit = Random.value <= Caster.CritChanceCurr;
      float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
      DamageContext context = new(Caster, (int)(Caster.AtkCurr * 1.0f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SingleTarget }, Element, isCrit);

      // 투사체 발사 및 데미지 적용 대기
      foreach (var target in TargetUnits)
      {
        GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, target, 0.5f);
        yield return new WaitForSeconds(0.5f);
        target.TakeDamage(context);
      }

      Caster.RecoverMana(ManaAmount);
      StopCode();
    }

    public override void StopCode()
    {
      Caster.isCasting = false;
    }

    public override bool HasValidTarget()
    {
      return GridManager.Instance.TargetNearestEnemy(Caster).Count > 0;
    }
  }
}