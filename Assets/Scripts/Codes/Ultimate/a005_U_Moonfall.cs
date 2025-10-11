using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Entities.Status;
using Effects.Base;
using Helpers;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 아탈란테의 궁극기: a005_U_Moonfall
    /// 단일 적에게 공격력의 n%에 해당하는 피해를 입힌다.
    /// 이후 유닛 뒤 (y+1, x+-1)에게 범위 피해를 입힌다.
    /// 모든 공격 적중 시 공격력 10%에 해당하는 맹독 부여
    /// </summary>
    public class a005_U_Moonfall : UltimateCode
    {
        private readonly HS_Poolable _prefab;
        
        public a005_U_Moonfall(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 6f;
            CodeName = "a005_U_Moonfall";
            CastingDelay = 1f;
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

            // 주 타겟 선택
            Unit primaryTarget = GetPrimaryTarget();
            if (primaryTarget == null)
            {
                Debug.Log("주 타겟을 찾을 수 없음");
                StopCode();
                yield break;
            }

            // 뒤쪽 타겟들 선택
            List<Unit> backTargets = GetBackTargets(primaryTarget);

            // 크리티컬 계산
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;

            // 피해 계산 (공격력의 200%)
            int primaryDamage = (int)(Caster.AtkCurr * 2.0f * critMultiplier);
            int backDamage = (int)(Caster.AtkCurr * 1.5f * critMultiplier); // 후열은 150%

            // 주 타겟에게 공격
            List<int> primaryTags = new List<int> { Helpers.DamageTag.SingleTarget, Helpers.DamageTag.UltAttack, Helpers.DamageTag.NonContactAttack };
            DamageContext primaryContext = new(Caster, primaryDamage, BaseEnums.CodeType.Ultimate, primaryTags, isCrit);
            Caster.StartCoroutine(FirePrimaryProjectile(primaryTarget, 0.5f, primaryContext));

            // 후열 범위 공격 (0.8초 후)
            if (backTargets.Count > 0)
            {
                List<int> backTags = new List<int> { Helpers.DamageTag.MultiTarget, Helpers.DamageTag.UltAttack, Helpers.DamageTag.NonContactAttack };
                DamageContext backContext = new(Caster, backDamage, BaseEnums.CodeType.Ultimate, backTags, isCrit);
                Caster.StartCoroutine(FireBackAreaAttack(primaryTarget, backTargets, backContext));
            }

            StopCode();
        }

        private Unit GetPrimaryTarget()
        {
            var nearestTargets = GridManager.Instance.TargetNearestEnemy(Caster);
            return nearestTargets.Count > 0 ? nearestTargets[0] : null;
        }

        private List<Unit> GetBackTargets(Unit primaryTarget)
        {
            List<Unit> backTargets = new List<Unit>();
            int targetX = primaryTarget.currentCell.xPos;
            int targetY = primaryTarget.currentCell.yPos;
            
            // 뒤쪽 위치 계산 (y+1, x-1과 x+1)
            int[] backXPositions = { targetX - 1, targetX + 1 };
            int backY = targetY + 1;
            
            Debug.Log($"주 타겟: {primaryTarget.UnitName} at ({targetX}, {targetY}), 후열 검색: y={backY}");
            
            foreach (int backX in backXPositions)
            {
                Unit targetUnit = GridManager.Instance.GetUnitAtPosition(backX, backY);
                if (targetUnit != null && targetUnit.isActive && 
                    targetUnit.IsEnemy == primaryTarget.IsEnemy && 
                    targetUnit != primaryTarget) // 주 타겟과 다른 유닛
                {
                    backTargets.Add(targetUnit);
                    Debug.Log($"뒤쪽 타겟 발견: {targetUnit.UnitName} at ({backX}, {targetY})");
                }
            }
            
            return backTargets;
        }

        private IEnumerator FirePrimaryProjectile(Unit target, float delay, DamageContext context)
        {
            GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, target, delay);
            yield return new WaitForSeconds(delay);
            
            // 피해 적용
            target.TakeDamage(context);
            
            // 맹독 부여 (공격력의 10%)
            ApplyPoisonEffect(target);
            
            Debug.Log($"주 타겟 {target.UnitName}({target.currentCell.xPos}, {target.currentCell.yPos})에게 피해 적용 완료");
        }

        private IEnumerator FireBackAreaAttack(Unit primaryTarget, List<Unit> backTargets, DamageContext context)
        {
            // 주 타겟 위치에서 범위 공격 이펙트 생성 (모든 후열 타겟에게 동시에)
            var effectPrefab = _prefab; // 기본값
            if (GameManager.Instance.sfxManager.ProjectilePrefabs.ContainsKey("NormalAttack"))
            {
                effectPrefab = GameManager.Instance.sfxManager.ProjectilePrefabs["NormalAttack"];
            }
            
            Debug.Log($"주 타겟 {primaryTarget.UnitName}({primaryTarget.currentCell.xPos}, {primaryTarget.currentCell.yPos})에서 후열 범위 공격 시작");
            
            // 모든 후열 타겟에게 동시에 투사체 발사 (주 타겟에서 시작)
            foreach (var target in backTargets)
            {
                GameManager.Instance.sfxManager.FireSingleProjectile(effectPrefab, primaryTarget, target, 0.1f);
            }
            
            // 투사체 도달 시간 대기
            yield return new WaitForSeconds(0.3f);
            
            // 모든 후열 타겟에게 동시에 피해 적용
            foreach (var target in backTargets)
            {
                if (target != null && target.isActive)
                {
                    target.TakeDamage(context);
                    
                    // 맹독 부여 (공격력의 10%)
                    ApplyPoisonEffect(target);
                    
                    Debug.Log($"후열 범위 공격: {target.UnitName}({target.currentCell.xPos}, {target.currentCell.yPos})에게 피해 적용");
                }
            }
            
            Debug.Log($"후열 범위 공격 완료: {backTargets.Count}명의 적에게 동시 공격");
        }
        
        /// <summary>
        /// 아탈란테 궁극기 전용 사냥꾼의 독 부여 (새 Status 시스템)
        /// </summary>
        private void ApplyPoisonEffect(Unit target)
        {
            if (target != null && target.isActive && target.IsEnemy != Caster.IsEnemy)
            {
                // 사냥꾼의 독 상태 생성 (StatusId = 3)
                var huntersVenomStatus = new UnitStatus(3, Caster, target);
                
                // DOT 효과 추가 (EffectId = 1001, 공격력의 10%)
                huntersVenomStatus.AddEffect(1001, 10f);
                
                // Effect 객체 생성 및 할당
                foreach (var effectInstance in huntersVenomStatus.Effects)
                {
                    effectInstance.EffectObject = EffectFactory.CreateEffect(
                        effectInstance.EffectId,
                        effectInstance.Coefficient,
                        Caster,
                        target
                    );
                }
                
                // 상태 적용
                target.AddStatus(huntersVenomStatus);
                
                Debug.Log($"[달빛폭격] {target.UnitName}에게 사냥꾼의 독 상태 부여 (공격력의 10% DOT)");
            }
        }

        public override void StopCode()
        {
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return GridManager.Instance.TargetNearestEnemy(Caster).Count > 0;
        }
    }
}