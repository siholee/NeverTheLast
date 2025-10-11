using Effects.Base;
using Effects.Negative;
using Effects.Neutral;
using Effects.Positive;
using Entities;
using UnityEngine;

namespace Effects.Base
{
    /// <summary>
    /// Effect 팩토리 클래스
    /// Effect ID를 기반으로 적절한 Effect 인스턴스를 생성합니다.
    /// </summary>
    public static class EffectFactory
    {
        /// <summary>
        /// Effect ID와 계수를 받아 Effect 인스턴스 생성
        /// </summary>
        /// <param name="effectId">효과 ID</param>
        /// <param name="coefficient">효과 계수</param>
        /// <param name="caster">시전자</param>
        /// <param name="target">대상</param>
        /// <returns>생성된 Effect 인스턴스</returns>
        public static BaseEffect CreateEffect(int effectId, float coefficient, Unit caster, Unit target)
        {
            BaseEffect effect = effectId switch
            {
                // Negative Effects (1000-1999)
                1001 => new DamageOverTimeEffect(effectId, coefficient),
                1002 => new PercentDamageOverTimeEffect(effectId, coefficient),
                
                // Neutral Effects (2000-2999)
                2001 => new DisableNormalAttackEffect(effectId, coefficient),
                2002 => new TauntEffect(effectId, coefficient),
                2003 => new ThornEffect(effectId, coefficient),
                
                // Positive Effects (3000-3999)
                3001 => new DamageReductionEffect(effectId, coefficient),
                
                _ => null
            };
            
            if (effect != null)
            {
                effect.Caster = caster;
                effect.Target = target;
                Debug.Log($"[EffectFactory] Effect 생성: ID={effectId}, 계수={coefficient}, 시전자={caster?.UnitName}, 대상={target?.UnitName}");
            }
            else
            {
                Debug.LogError($"[EffectFactory] 알 수 없는 Effect ID: {effectId}");
            }
            
            return effect;
        }
        
        /// <summary>
        /// Effect 이름 반환 (UI 표시용)
        /// </summary>
        public static string GetEffectName(int effectId)
        {
            return effectId switch
            {
                1001 => "지속 피해",
                1002 => "퍼센트 지속 피해",
                2001 => "행동불가",
                2002 => "도발",
                2003 => "가시",
                3001 => "받는 피해 감소",
                _ => "알 수 없는 효과"
            };
        }
        
        /// <summary>
        /// Effect 설명 반환 (UI 표시용)
        /// </summary>
        public static string GetEffectDescription(int effectId)
        {
            return effectId switch
            {
                1001 => "매 프레임 지속 피해를 입힙니다.",
                1002 => "최대 체력의 일정 비율만큼 지속 피해를 입힙니다.",
                2001 => "일반 공격을 사용할 수 없습니다.",
                2002 => "우선도가 증가하여 적의 공격을 끌어당깁니다.",
                2003 => "접촉 피해를 입으면 공격자에게 화상을 부여합니다.",
                3001 => "받는 피해가 감소합니다.",
                _ => ""
            };
        }
    }
}
