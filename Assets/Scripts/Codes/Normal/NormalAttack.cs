using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Normal
{
  public class NormalAttack : NormalCode
  {
    private readonly HS_Poolable _prefab;

    public NormalAttack(NormalCodeContext context) : base(context)
    {
      CodeType = BaseEnums.CodeType.Normal;
      Caster = context.Caster;
      CodeName = "일반행동";
      CastingDelay = 0.5f;
      CodeTags = new List<int> { DamageTag.Physical };
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
      if (Caster.HasEquippedBow() && damageTags.Contains(DamageTag.NonContactAttack) &&
          !damageTags.Contains(DamageTag.Arrow))
        damageTags.Add(DamageTag.Arrow);
      
      // 디버그 로그
      string contactType = damageTags.Contains(DamageTag.ContactAttack) ? "접촉" : "비접촉";
      Debug.Log($"{Caster.UnitName}이 {TargetUnits[0].UnitName}에게 {contactType} 일반행동을 시전했습니다.");
      
      DamageContext context = new(Caster, Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(50) * critMultiplier)), BaseEnums.CodeType.Normal, damageTags, isCrit);
      // 예전에는 2초를 줬다. 투사체가 너무 오래 떠 있어 그 사이 대상이 다른 공격에
      // 쓰러지면 빈자리로 날아갔다. 다른 일반행동과 같은 0.5초로 맞춘다.
      yield return FireProjectile(TargetUnits, 0.5f, context);
      Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
      // 궁극기 자원은 전투 시간으로 찬다(Unit.AccrueUltimateResource). 여기서 또 주지 않는다.
      StopCode();
    }

    public override void StopCode()
    {
      Caster.isCasting = false;
    }

    private IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
    {
      foreach (var target in targets)
      {
        SfxManager sfx = GameManager.Instance?.sfxManager;
        bool melee = sfx != null && sfx.TryPlayMeleeAttack(Caster, target, context);
        if (melee)
        {
          yield return new WaitForSeconds(SfxManager.MeleeImpactDelay);
        }
        else
        {
          // 물리(화살·투척)는 곡선형, 마법·특수는 직선형으로 날아간다.
          // 피해는 투사체가 닿은 뒤에 들어간다.
          ProjectilePathType path = ProjectileFlight.PathFor(context);
          var token = new ProjectileImpactToken();
          sfx?.FireElementalProjectile(
            Caster, target, delay, path, ProjectileFlight.DataFor(path), _prefab, token.MarkImpact, context);
          yield return ProjectileFlight.WaitForImpact(token, delay);
        }
        target.TakeDamage(context);
        Caster.Invoke(BaseEnums.UnitEventType.OnNormalAttackHit, new EventContext(Caster, target, context));
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
      List<Unit> availableEnemies = GetAvailableEnemies();
      
      if (availableEnemies.Count == 0)
        return new List<Unit>();

      int maxPriority = availableEnemies.Max(enemy => enemy.Priority);
      List<Unit> highestPriorityEnemies = availableEnemies
        .Where(enemy => enemy.Priority == maxPriority)
        .ToList();

      if (Caster.currentNormalTarget != null &&
          Caster.currentNormalTarget.isActive &&
          highestPriorityEnemies.Contains(Caster.currentNormalTarget))
      {
        return new List<Unit> { Caster.currentNormalTarget };
      }

      Caster.currentNormalTarget = highestPriorityEnemies[Random.Range(0, highestPriorityEnemies.Count)];
      
      return Caster.currentNormalTarget != null ? 
        new List<Unit> { Caster.currentNormalTarget } : 
        new List<Unit>();
    }
    
    /// <summary>
    /// 공격 가능한 적 목록 가져오기
    /// </summary>
    private List<Unit> GetAvailableEnemies()
    {
      GridManager gridManager = GameObject.FindAnyObjectByType<GridManager>();
      if (gridManager == null)
        return new List<Unit>();
      
      bool casterIsAlly = gridManager.heroList.Contains(Caster);
      List<Unit> targetList = casterIsAlly ? gridManager.enemyList : gridManager.heroList;
      
      // 전장에 서 있는 유닛만 남긴다. 칸이 없는 소환수도 여기서 함께 걸러진다.
      return targetList.Where(unit => unit && unit.IsOnField).ToList();
    }
    
    /// <summary>
    /// 기본 일반행동 데미지 태그
    /// </summary>
    private List<int> GetDamageTags()
    {
      return new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Physical, DamageTag.NonContactAttack };
    }
  }
}
