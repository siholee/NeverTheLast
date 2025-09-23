using System;
using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 아탈란테의 패시브: 사냥꾼의 독
    /// 공격이 적중한 모든 대상에게 공격력의 10%에 해당하는 맹독 부여
    /// </summary>
    public class HuntersVenom : PassiveCode
    {
        public HuntersVenom(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사냥꾼의 독";
            Caster = context.Caster;
        }

        public override void CastCode()
        {
            // 시전자가 피해를 입힌 후에 독을 부여하는 이벤트 등록
            Action<EventContext> onAfterDamageTaken = null;
            
            onAfterDamageTaken = (damageInfo) =>
            {
                // 시전자가 공격자인 경우에만 독 부여
                if (damageInfo.DmgCtx != null && damageInfo.DmgCtx.Attacker == Caster)
                {
                    ApplyPoisonToTarget(damageInfo.Grantee);
                }
            };

            // 시전자 사망 시 효과를 멈추기 위한 이벤트 핸들러 생성
            Action<EventContext> onDeathHandler = null;
            onDeathHandler = (deathInfo) =>
            {
                StopCode();
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
                // 모든 적의 OnAfterDamageTaken 이벤트에서 핸들러 제거
                var allEnemies = GridManager.Instance.TargetAllEnemies(Caster);
                foreach (var enemy in allEnemies)
                {
                    if (enemy != null && enemy.isActive)
                    {
                        enemy.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, onAfterDamageTaken);
                    }
                }
            };

            // 모든 적의 OnAfterDamageTaken 이벤트에 핸들러 등록
            var enemies = GridManager.Instance.TargetAllEnemies(Caster);
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.isActive)
                {
                    enemy.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, onAfterDamageTaken);
                }
            }

            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);

            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
        }

        private void ApplyPoisonToTarget(Unit target)
        {
            if (target != null && target.isActive && target.IsEnemy != Caster.IsEnemy)
            {
                // 코루틴으로 다음 프레임에 실행하여 Collection modified 에러 방지
                Caster.StartCoroutine(ApplyPoisonCoroutine(target));
            }
        }

        private System.Collections.IEnumerator ApplyPoisonCoroutine(Unit target)
        {
            yield return null; // 한 프레임 대기
            
            if (target != null && target.isActive)
            {
                // 공격력의 10%에 해당하는 독 부여
                int poisonDamage = Mathf.RoundToInt(Caster.AtkCurr * 0.1f);
                string identifier = $"HuntersVenomPoison_{Caster.currentCell.xPos}_{Caster.currentCell.yPos}_{UnityEngine.Random.Range(0f, 100f)}";
                var poisonEffect = new PoisonEffect(Caster, identifier, poisonDamage);
                target.AddStatusEffect(identifier, poisonEffect);
                
                Debug.Log($"{Caster.UnitName}이 {target.UnitName}에게 사냥꾼의 독 부여 (피해: {poisonDamage})");
            }
        }

        public override void StopCode()
        {
            // 패시브 스킬이므로 특별한 정리 작업 없음
        }
    }
}