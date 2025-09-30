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

namespace Codes.Base
{
    /// <summary>
    /// 모든 일반공격의 기본 클래스
    /// 공통 로직과 기본 구현을 제공하고, 상속 클래스에서 필요한 부분을 오버라이드
    /// </summary>
    public abstract class BaseNormalCode : NormalCode
    {
        protected readonly HS_Poolable _prefab;

        protected BaseNormalCode(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            Caster = context.Caster;
            Cooldown = 2f;
            CastingDelay = 0.5f;
            ManaAmount = 10;
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

            // 크리티컬 계산
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            
            // 접촉/비접촉 태그 결정
            List<int> damageTags = GetDamageTags();
            
            // 데미지 계산 (하위 클래스에서 오버라이드 가능)
            int damage = CalculateDamage(critMultiplier);
            
            // 디버그 로그
            string contactType = damageTags.Contains(Helpers.DamageTag.ContactAttack) ? "접촉" : "비접촉";
            Debug.Log($"{Caster.UnitName}이 {TargetUnits[0].UnitName}에게 {contactType} 일반공격을 시전했습니다.");
            
            DamageContext context = new(Caster, damage, BaseEnums.CodeType.Normal, damageTags, isCrit);
            Caster.StartCoroutine(FireProjectile(TargetUnits, 0.5f, context));
            
            // 추가 효과 처리 (하위 클래스에서 오버라이드 가능)
            yield return ApplyAdditionalEffects(TargetUnits[0], context);
            
            Caster.RecoverMana(ManaAmount);
            StopCode();
        }

        public override void StopCode()
        {
            Caster.normalCooldown = Cooldown;
            Caster.isCasting = false;
        }

        /// <summary>
        /// 데미지 계산 로직 (하위 클래스에서 오버라이드)
        /// </summary>
        protected virtual int CalculateDamage(float critMultiplier)
        {
            return (int)(Caster.AtkCurr * 1.0f * critMultiplier);
        }

        /// <summary>
        /// 추가 효과 적용 (하위 클래스에서 오버라이드)
        /// </summary>
        protected virtual IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            yield return null; // 기본적으로는 추가 효과 없음
        }

        protected virtual IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
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
        protected virtual List<Unit> SelectTarget()
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
        protected List<Unit> GetAvailableEnemies()
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
        protected virtual List<int> GetDamageTags()
        {
            List<int> tags = new List<int> { Helpers.DamageTag.SingleTarget, Helpers.DamageTag.NormalAttack };
            
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
            
            tags.Add(isContact ? Helpers.DamageTag.ContactAttack : Helpers.DamageTag.NonContactAttack);
            return tags;
        }
    }
}