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
    /// <summary>
    /// 아탈란테의 액티브: 달빛 폭격
    /// 단일 적에게 공격력의 n%에 해당하는 피해를 입힌다.
    /// 이후 유닛 뒤 (y+1, x+-1)에게 범위 피해를 입힌다.
    /// </summary>
    public class Moonfall : UltimateCode
    {
        private readonly HS_Poolable _prefab;
        
        public Moonfall(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 6f;
            CodeName = "달빛 폭격";
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

            // 첫 번째 타겟 선정 (가장 가까운 적)
            var primaryTarget = GetPrimaryTarget();
            if (primaryTarget != null)
            {
                // 주요 공격: 공격력의 200% 피해
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int primaryDamage = Mathf.RoundToInt(Caster.AtkCurr * 2f * critMultiplier);
                
                DamageContext primaryContext = new(Caster, primaryDamage, BaseEnums.CodeType.Ultimate, 
                    new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack }, isCrit);

                // 주 공격 실행 및 완료 대기
                yield return Caster.StartCoroutine(FirePrimaryProjectile(primaryTarget, 0.2f, primaryContext));
                
                // 주 공격 완료 후 약간의 딜레이
                yield return new WaitForSeconds(0.3f);
                
                // 뒤쪽 범위 공격 실행
                var backTargets = GetBackTargets(primaryTarget);
                if (backTargets.Count > 0)
                {
                    int backDamage = Mathf.RoundToInt(Caster.AtkCurr * 1.5f * critMultiplier);
                    DamageContext backContext = new(Caster, backDamage, BaseEnums.CodeType.Ultimate, 
                        new List<int> { DamageTag.MultiTarget, DamageTag.UltAttack, DamageTag.NonContactAttack }, isCrit);
                    
                    // 모든 후열 타겟에게 동시에 공격 (주 타겟 위치에서 시작)
                    yield return Caster.StartCoroutine(FireBackAreaAttack(primaryTarget, backTargets, backContext));
                }
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
            
            int primaryX = primaryTarget.currentCell.xPos;
            int primaryY = primaryTarget.currentCell.yPos;
            
            // 후방 범위: x+1, y±1
            int backX = primaryX + 1;
            
            // y-1, y, y+1 위치 확인 (3개 범위)
            for (int yOffset = -1; yOffset <= 1; yOffset++) // -1, 0, +1 모두 확인
            {
                int targetY = primaryY + yOffset;
                
                // 해당 위치에 유닛이 있는지 확인
                Unit targetUnit = GridManager.Instance.GetUnitAtPosition(backX, targetY);
                
                // 유닛이 존재하고, 활성화되어 있으며, 타겟과 같은 진영인 경우에만 추가
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
            
            // 피해 적용 (독은 패시브에서 자동으로 부여됨)
            target.TakeDamage(context);
            
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
                    Debug.Log($"후열 범위 공격: {target.UnitName}({target.currentCell.xPos}, {target.currentCell.yPos})에게 피해 적용");
                }
            }
            
            Debug.Log($"후열 범위 공격 완료: {backTargets.Count}명의 적에게 동시 공격");
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