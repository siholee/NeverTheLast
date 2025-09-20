using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Entities;
using Managers;

public static class Target
{
    // 타겟팅 타입 정의
    public enum TargetType
    {
        SingleTarget = 1,    // 단일 대상
        NineSquareArea = 2,  // 9칸 범위 (대상 중심 3x3)
        All = 3              // 전체 대상
    }

    // 진영 정의
    public enum TargetFaction
    {
        Same,      // 같은 진영 (아군)
        Opposite   // 반대 진영 (적군)
    }

    /// <summary>
    /// 메인 타겟팅 함수 - caster와 targetType에 따라 적절한 타겟들을 반환
    /// </summary>
    /// <param name="caster">스킬을 사용하는 유닛</param>
    /// <param name="targetType">타겟팅 타입 (1:단일, 2:9칸범위, 3:전체)</param>
    /// <param name="faction">타겟할 진영 (Same:아군, Opposite:적군)</param>
    /// <returns>타겟 유닛들의 리스트</returns>
    public static List<Unit> GetTargets(Unit caster, int targetType, TargetFaction faction = TargetFaction.Opposite)
    {
        TargetType type = (TargetType)targetType;
        
        switch (type)
        {
            case TargetType.SingleTarget:
                return GetSingleTarget(caster, faction);
            case TargetType.NineSquareArea:
                return GetNineSquareAreaTargets(caster, faction);
            case TargetType.All:
                return GetAllTargets(caster, faction);
            default:
                Debug.LogWarning($"Unknown target type: {targetType}");
                return new List<Unit>();
        }
    }

    /// <summary>
    /// 단일 타겟 선택 - 우선도 기반으로 하나의 대상을 선택
    /// </summary>
    private static List<Unit> GetSingleTarget(Unit caster, TargetFaction faction)
    {
        List<Unit> availableTargets = GetAvailableTargets(caster, faction);
        
        if (availableTargets.Count == 0)
            return new List<Unit>();

        Unit selectedTarget = SelectTargetByPriority(availableTargets);
        return new List<Unit> { selectedTarget };
    }

    /// <summary>
    /// 9칸 범위 타겟 선택 - 우선 단일 타겟을 선택한 후, 그 주변 9칸 내의 모든 대상을 포함
    /// </summary>
    private static List<Unit> GetNineSquareAreaTargets(Unit caster, TargetFaction faction)
    {
        List<Unit> availableTargets = GetAvailableTargets(caster, faction);
        
        if (availableTargets.Count == 0)
            return new List<Unit>();

        // 먼저 중심이 될 타겟을 선택
        Unit centerTarget = SelectTargetByPriority(availableTargets);
        Vector2 centerPos = new Vector2(centerTarget.currentCell.xPos, centerTarget.currentCell.yPos);

        // 중심 타겟 주변 9칸 범위 내의 모든 타겟을 찾기
        List<Unit> areaTargets = new List<Unit>();
        
        foreach (Unit target in availableTargets)
        {
            Vector2 targetPos = new Vector2(target.currentCell.xPos, target.currentCell.yPos);
            float distance = Vector2.Distance(centerPos, targetPos);
            
            // 1칸 범위 내 (대각선 포함 약 1.41)
            if (distance <= 1.5f)
            {
                areaTargets.Add(target);
            }
        }

        return areaTargets;
    }

    /// <summary>
    /// 전체 타겟 선택 - 해당 진영의 모든 대상을 선택
    /// </summary>
    private static List<Unit> GetAllTargets(Unit caster, TargetFaction faction)
    {
        return GetAvailableTargets(caster, faction);
    }

    /// <summary>
    /// 타겟 가능한 유닛들을 진영에 따라 필터링
    /// </summary>
    private static List<Unit> GetAvailableTargets(Unit caster, TargetFaction faction)
    {
        GridManager gridManager = GameObject.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("GridManager not found!");
            return new List<Unit>();
        }

        bool casterIsAlly = gridManager.heroList.Contains(caster);
        List<Unit> targetList;

        if (faction == TargetFaction.Same)
        {
            // 같은 진영을 타겟
            targetList = casterIsAlly ? gridManager.heroList : gridManager.enemyList;
        }
        else
        {
            // 반대 진영을 타겟
            targetList = casterIsAlly ? gridManager.enemyList : gridManager.heroList;
        }

        // 필드에 있는 유닛만 필터링 (yPos > 0)
        return targetList.Where(unit => unit && unit.currentCell && unit.currentCell.yPos > 0 && unit.currentCell.isOccupied).ToList();
    }

    /// <summary>
    /// 우선도 기반으로 타겟을 선택 (높은 우선도 우선, 같은 우선도면 랜덤)
    /// </summary>
    private static Unit SelectTargetByPriority(List<Unit> targets)
    {
        if (targets.Count == 0)
            return null;

        if (targets.Count == 1)
            return targets[0];

        // 우선도별로 그룹화
        var priorityGroups = targets.GroupBy(unit => unit.Priority)
                                  .OrderByDescending(group => group.Key);

        // 가장 높은 우선도 그룹 선택
        var highestPriorityGroup = priorityGroups.First().ToList();

        // 같은 우선도가 여러 개면 랜덤 선택
        if (highestPriorityGroup.Count == 1)
        {
            return highestPriorityGroup[0];
        }
        else
        {
            int randomIndex = Random.Range(0, highestPriorityGroup.Count);
            return highestPriorityGroup[randomIndex];
        }
    }

    // 편의 함수들 - 자주 사용되는 패턴들
    
    /// <summary>
    /// 랜덤 단일 적 공격
    /// </summary>
    public static List<Unit> GetRandomSingleEnemy(Unit caster)
    {
        return GetTargets(caster, (int)TargetType.SingleTarget, TargetFaction.Opposite);
    }

    /// <summary>
    /// 가장 가까운 단일 적 공격 (기존 방식과 호환성 유지)
    /// </summary>
    public static List<Unit> GetNearestSingleEnemy(Unit caster)
    {
        GridManager gridManager = GameObject.FindFirstObjectByType<GridManager>();
        if (gridManager == null) return new List<Unit>();
        
        List<Unit> nearestEnemies = gridManager.TargetNearestEnemy(caster);
        return nearestEnemies;
    }

    /// <summary>
    /// 단일 아군 타겟
    /// </summary>
    public static List<Unit> GetSingleAlly(Unit caster)
    {
        return GetTargets(caster, (int)TargetType.SingleTarget, TargetFaction.Same);
    }

    /// <summary>
    /// 9칸 범위 적 공격
    /// </summary>
    public static List<Unit> GetNineSquareEnemies(Unit caster)
    {
        return GetTargets(caster, (int)TargetType.NineSquareArea, TargetFaction.Opposite);
    }

    /// <summary>
    /// 모든 적 공격
    /// </summary>
    public static List<Unit> GetAllEnemies(Unit caster)
    {
        return GetTargets(caster, (int)TargetType.All, TargetFaction.Opposite);
    }

    /// <summary>
    /// 모든 아군 타겟
    /// </summary>
    public static List<Unit> GetAllAllies(Unit caster)
    {
        return GetTargets(caster, (int)TargetType.All, TargetFaction.Same);
    }
}