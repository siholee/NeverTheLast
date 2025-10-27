using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Test
{
    /// <summary>
    /// 적 전용 테스트 패시브 코드
    /// 아군 전체에게 공격력 10% 버프 부여 (중복 불가)
    /// </summary>
    public class EnemyTestPassive : PassiveCode
    {
        private const float AttackBuffPercent = 0.1f;
        private bool _isApplied = false;

        public EnemyTestPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "적의 전술 지휘";
            Caster = context.Caster;
        }

        public override void CastCode()
        {
            if (_isApplied) return;
            
            // 아군 전체에게 공격력 10% 버프 부여
            var allyList = Caster.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            
            foreach (var ally in allyList)
            {
                if (ally != null && ally.isActive)
                {
                    // UnitStatus로 공격력 버프 부여 (StatusId = 9999는 테스트용)
                    var buffStatus = new UnitStatus(9999, Caster, ally);
                    buffStatus.AddEffect(2001, 10f); // AtkMultiplicative 10%
                    
                    // Effect 객체 생성
                    foreach (var effectInstance in buffStatus.Effects)
                    {
                        effectInstance.EffectObject = EffectFactory.CreateEffect(
                            effectInstance.EffectId,
                            effectInstance.Coefficient,
                            Caster,
                            ally
                        );
                    }
                    
                    ally.AddStatus(buffStatus);
                }
            }
            
            _isApplied = true;
            Debug.Log($"[{CodeName}] {Caster.UnitName}이(가) 아군 전체에게 공격력 10% 버프 부여");
        }
    }
}
