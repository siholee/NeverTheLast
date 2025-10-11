using System.Collections.Generic;
using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 퍼센트 기반 지속 피해(DOT) 효과
    /// 최대 체력의 일정 비율만큼 지속 피해 (예: 최대체력의 2%)
    /// </summary>
    public class PercentDamageOverTimeEffect : BaseEffect
    {
        private float _elapsedTime;
        private float _damageInterval = 0.1f; // 0.1초마다 피해
        
        public PercentDamageOverTimeEffect(int effectId, float coefficient = 2f) : base(effectId, coefficient)
        {
            EffectName = "퍼센트 지속 피해";
            EffectDescription = $"최대 체력의 {coefficient}%에 해당하는 지속 피해";
            Category = BaseEnums.EffectCategory.Negative;
            _elapsedTime = 0f;
        }
        
        public override void OnApply()
        {
            _elapsedTime = 0f;
            Debug.Log($"[Percent DOT] {Target.UnitName}에게 퍼센트 지속 피해 효과 적용 (계수: {Coefficient}%)");
        }
        
        public override void OnRoundStart()
        {
            // DOT는 매 프레임 처리되므로 라운드 시작 시 특별한 처리 없음
        }
        
        public override void OnUpdate(float deltaTime)
        {
            float previousTime = _elapsedTime;
            _elapsedTime += deltaTime;
            
            // 0.1초마다 피해 적용
            int previousMultiple = (int)(previousTime / _damageInterval);
            int currentMultiple = (int)(_elapsedTime / _damageInterval);
            int triggerCount = currentMultiple - previousMultiple;
            
            for (int i = 0; i < triggerCount; i++)
            {
                // 최대 체력의 퍼센트로 피해 계산
                int damage = Mathf.RoundToInt(Target.HpMax * Coefficient / 100f);
                DamageContext dmgContext = new DamageContext(
                    Caster, 
                    damage, 
                    BaseEnums.CodeType.Effect, 
                    new List<int>(), 
                    false, 
                    10000000
                );
                Target.TakeDamage(dmgContext);
            }
        }
        
        public override void OnRemove()
        {
            Debug.Log($"[Percent DOT] {Target.UnitName}에게서 퍼센트 지속 피해 효과 제거");
        }
        
        public override BaseEffect Clone()
        {
            var clone = new PercentDamageOverTimeEffect(EffectId, Coefficient)
            {
                Caster = this.Caster,
                Target = this.Target
            };
            return clone;
        }
    }
}
