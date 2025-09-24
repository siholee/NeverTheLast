using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

      // 타겟 선택 로직
      TargetUnits = SelectTarget();
      
      if (TargetUnits.Count == 0)
      {
        StopCode();
        yield break;
      }

      bool isCrit = Random.value <= Caster.CritChanceCurr;
      float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
      
      // 접촉/비접촉 태그 결정
      List<int> damageTags = GetDamageTags();
      
      // 디버그 로그
      string contactType = damageTags.Contains(DamageTag.ContactAttack) ? "접촉" : "비접촉";
      Debug.Log($"{Caster.UnitName}이 {TargetUnits[0].UnitName}에게 {contactType} 일반공격을 시전했습니다.");
      
      DamageContext context = new(Caster, (int)(Caster.AtkCurr * 1.0f * critMultiplier), BaseEnums.CodeType.Normal, damageTags, isCrit);
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
      return GetAvailableEnemies().Count > 0;
    }
    
    /// <summary>
    /// 타겟 선택 로직 - 기존 타겟을 우선시하되, 더 높은 우선도의 적이 있으면 타겟 변경
    /// </summary>
    private List<Unit> SelectTarget()
    {
      // 모든 가용 적 목록 가져오기
      List<Unit> availableEnemies = GetAvailableEnemies();
      
      if (availableEnemies.Count == 0)
        return new List<Unit>();
      
      // 현재 타겟이 유효한지 확인
      if (Caster.currentNormalTarget != null && 
          Caster.currentNormalTarget.isActive && 
          availableEnemies.Contains(Caster.currentNormalTarget))
      {
        // 더 높은 우선도의 적이 있는지 확인
        Unit higherPriorityEnemy = availableEnemies
          .Where(enemy => enemy.Priority > Caster.currentNormalTarget.Priority)
          .OrderByDescending(enemy => enemy.Priority)
          .FirstOrDefault();
        
        if (higherPriorityEnemy != null)
        {
          // 더 높은 우선도의 적으로 타겟 변경
          Caster.currentNormalTarget = higherPriorityEnemy;
        }
        // 현재 타겟 유지
      }
      else
      {
        // 새로운 타겟 선택 (우선도 기반)
        Caster.currentNormalTarget = availableEnemies
          .OrderByDescending(enemy => enemy.Priority)
          .ThenBy(enemy => Random.value) // 같은 우선도면 랜덤
          .FirstOrDefault();
      }
      
      return Caster.currentNormalTarget != null ? 
        new List<Unit> { Caster.currentNormalTarget } : 
        new List<Unit>();
    }
    
    /// <summary>
    /// 공격 가능한 적 목록 가져오기
    /// </summary>
    private List<Unit> GetAvailableEnemies()
    {
      GridManager gridManager = GameObject.FindFirstObjectByType<GridManager>();
      if (gridManager == null)
        return new List<Unit>();
      
      bool casterIsAlly = gridManager.heroList.Contains(Caster);
      List<Unit> targetList = casterIsAlly ? gridManager.enemyList : gridManager.heroList;
      
      // 필드에 있고 활성화된 유닛만 필터링 (yPos > 0)
      return targetList.Where(unit => 
        unit && 
        unit.currentCell && 
        unit.currentCell.yPos > 0 && 
        unit.currentCell.isOccupied && 
        unit.isActive
      ).ToList();
    }
    
    /// <summary>
    /// 시너지에 따른 데미지 태그 결정
    /// </summary>
    private List<int> GetDamageTags()
    {
      List<int> tags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack };
      
      // 아탈란테 예외 처리 (항상 비접촉)
      if (Caster.ID == 5) // 아탈란테 ID
      {
        tags.Add(DamageTag.NonContactAttack);
        return tags;
      }
      
      // 직업 시너지에 따른 접촉/비접촉 결정
      bool isContact = false;
      
      foreach (int synergy in Caster.Synergies)
      {
        switch (synergy)
        {
          case 7:  // 파수꾼
          case 8:  // 투사  
          case 9:  // 처형자
            isContact = true;
            break;
          case 10: // 사수
          case 11: // 마법사
          case 12: // 책략가
          case 13: // 메카닉
            isContact = false;
            break;
        }
      }
      
      tags.Add(isContact ? DamageTag.ContactAttack : DamageTag.NonContactAttack);
      return tags;
    }
  }
}