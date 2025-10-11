using System.Collections.Generic;
using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 지속 피해(DOT) 효과
    /// 시전자의 공격력에 비례한 지속 피해 (예: 공격력의 20%)
    /// </summary>
    public class DamageOverTimeEffect : BaseEffect
    {
        private float _elapsedTime;
        private float _damageInterval = 0.1f; // 0.1초마다 피해
        
        public DamageOverTimeEffect(int effectId, float coefficient = 100f) : base(effectId, coefficient)
        {
            EffectName = "지속 피해";
            EffectDescription = $"시전자 공격력의 {coefficient}%에 해당하는 지속 피해";
            Category = BaseEnums.EffectCategory.Negative;
            _elapsedTime = 0f;
        }
        
        public override void OnApply()
        {
            _elapsedTime = 0f;
            Debug.Log($"[DOT] {Target.UnitName}에게 지속 피해 효과 적용 (계수: {Coefficient}%)");
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
                int damage = Mathf.RoundToInt(Caster.AtkCurr * Coefficient / 100f);
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
            Debug.Log($"[DOT] {Target.UnitName}에게서 지속 피해 효과 제거");
        }
        
        public override BaseEffect Clone()
        {
            var clone = new DamageOverTimeEffect(EffectId, Coefficient)
            {
                Caster = this.Caster,
                Target = this.Target
            };
            return clone;
        }
    }
}
