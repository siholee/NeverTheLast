using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Test
{
    /// <summary>
    /// 방어막 테스트용 스킬
    /// 1. 자신에게 방어막 부여
    /// 2. 적에게 방어막 관통 공격
    /// </summary>
    public class ShieldTest : UltimateCode
    {
        public ShieldTest(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 5f;
            CodeName = "방어막 테스트";
            CastingDelay = 1f;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 캐스팅 딜레이
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

            Debug.Log($"{Caster.UnitName}이 {CodeName}을 시전했습니다!");

            // 1. 자신에게 방어막 부여 (공격력의 50%)
            int shieldAmount = (int)(Caster.AtkCurr * 0.5f);
            Caster.AddShield(shieldAmount);

            // 2. 방어막 상태 효과도 추가 (시각적 표시용)
            string shieldIdentifier = $"Shield_{Caster.GetInstanceID()}_{Time.time}";
            ShieldEffect shieldEffect = new ShieldEffect(Caster, shieldIdentifier, 0);
            Caster.AddStatusEffect(shieldIdentifier, shieldEffect);

            yield return new WaitForSeconds(0.5f);

            // 3. 가장 가까운 적에게 방어막 관통 공격
            Unit target = SelectNearestEnemy();
            if (target != null)
            {
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = (int)(Caster.AtkCurr * 1.5f * critMultiplier);

                // 방어막 관통 태그 포함
                List<int> damageTags = new List<int> 
                { 
                    DamageTag.SingleTarget, 
                    DamageTag.UltAttack, 
                    DamageTag.ShieldPenetration 
                };

                DamageContext context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, damageTags, isCrit);
                target.TakeDamage(context);

                Debug.Log($"{Caster.UnitName}이 {target.UnitName}에게 방어막 관통 공격! 데미지: {damage}");
            }

            StopCode();
        }

        private Unit SelectNearestEnemy()
        {
            List<Unit> availableEnemies = GetAvailableEnemies();
            if (availableEnemies.Count == 0) return null;

            Unit nearestEnemy = null;
            float minDistance = float.MaxValue;

            foreach (Unit enemy in availableEnemies)
            {
                float distance = Vector3.Distance(Caster.transform.position, enemy.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestEnemy = enemy;
                }
            }

            return nearestEnemy;
        }

        private List<Unit> GetAvailableEnemies()
        {
            GridManager gridManager = GameObject.FindFirstObjectByType<GridManager>();
            if (gridManager == null) return new List<Unit>();

            bool casterIsAlly = gridManager.heroList.Contains(Caster);
            List<Unit> targetList = casterIsAlly ? gridManager.enemyList : gridManager.heroList;

            List<Unit> availableEnemies = new List<Unit>();
            foreach (Unit unit in targetList)
            {
                if (unit && unit.currentCell && unit.currentCell.yPos > 0 && 
                    unit.currentCell.isOccupied && unit.isActive)
                {
                    availableEnemies.Add(unit);
                }
            }

            return availableEnemies;
        }
    }
}