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

namespace Codes.Test
{
    /// <summary>
    /// 적 전용 테스트 궁극기 코드
    /// 공격력의 150%에 해당하는 단일 피해
    /// </summary>
    public class EnemyTestUltimate : UltimateCode
    {
        private readonly HS_Poolable _prefab;

        public EnemyTestUltimate(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            CodeName = "적의 궁극기";
            CastingDelay = 0.8f;
            _prefab = GameManager.Instance.sfxManager.ProjectilePrefabs["FireBlast"];
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}이 {CodeName} 시전");
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
                    Debug.Log($"{Caster.UnitName}의 {CodeName} 시전이 방해됨");
                    StopCode();
                    yield break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 타겟 선택
            TargetUnits = SelectTarget();
            
            if (TargetUnits.Count == 0)
            {
                StopCode();
                yield break;
            }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            
            // 공격력의 150% 피해
            int damage = Mathf.RoundToInt(Caster.AtkCurr * 1.5f * critMultiplier);
            
            List<int> damageTags = new List<int> 
            { 
                DamageTag.SingleTarget, 
                DamageTag.UltAttack,
                DamageTag.NonContactAttack
            };
            
            DamageContext context = new(Caster, damage, BaseEnums.CodeType.Ultimate, damageTags, isCrit);
            Caster.StartCoroutine(FireProjectile(TargetUnits, 0.6f, context));
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
                // 베지어 곡선 (높은 아크)
                ProjectilePathData pathData = ProjectilePathData.CreateBezier(5f);
                GameManager.Instance.sfxManager.FireSingleProjectile(
                    _prefab, 
                    Caster, 
                    target, 
                    delay, 
                    ProjectilePathType.BezierCurve, 
                    pathData
                );
                yield return new WaitForSeconds(delay);
                target.TakeDamage(context);
            }
        }

        public override bool HasValidTarget()
        {
            return GetAvailableEnemies().Count > 0;
        }
        
        private List<Unit> SelectTarget()
        {
            List<Unit> availableEnemies = GetAvailableEnemies();
            
            if (availableEnemies.Count == 0)
                return new List<Unit>();
            
            // 가장 우선도가 높은 적 선택
            Unit target = availableEnemies
                .OrderByDescending(enemy => enemy.Priority)
                .ThenBy(enemy => Random.value)
                .FirstOrDefault();
            
            return target != null ? new List<Unit> { target } : new List<Unit>();
        }
        
        private List<Unit> GetAvailableEnemies()
        {
            GridManager gridManager = GameObject.FindFirstObjectByType<GridManager>();
            if (gridManager == null)
                return new List<Unit>();
            
            bool casterIsAlly = gridManager.heroList.Contains(Caster);
            List<Unit> targetList = casterIsAlly ? gridManager.enemyList : gridManager.heroList;
            
            return targetList.Where(unit => 
                unit && 
                unit.currentCell && 
                unit.currentCell.yPos > 0 && 
                unit.currentCell.isOccupied && 
                unit.isActive
            ).ToList();
        }
    }
}
