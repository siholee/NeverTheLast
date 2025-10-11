using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Positive
{
    /// <summary>
    /// 받는 피해 감소 효과
    /// 데미지를 입을 때 받는 피해가 일정 비율 감소합니다.
    /// 
    /// 참고: 이 효과는 Unit의 AttributesUpdate()에서 처리되지 않으므로
    /// 실제로는 StatusEffect를 통해 구현하는 것이 더 적합합니다.
    /// 현재는 개념 증명(PoC)용 구현입니다.
    /// </summary>
    public class DamageReductionEffect : BaseEffect
    {
        public DamageReductionEffect(int effectId, float coefficient = 25f) : base(effectId, coefficient)
        {
            EffectName = "받는 피해 감소";
            EffectDescription = $"받는 피해가 {coefficient}% 감소합니다.";
            Category = BaseEnums.EffectCategory.Positive;
        }
        
        public override void OnApply()
        {
            Debug.Log($"[피해 감소] {Target.UnitName}에게 받는 피해 {Coefficient}% 감소 효과 적용");
            Debug.LogWarning($"[피해 감소] 현재 구현은 개념 증명용입니다. 실제 피해 감소는 StatusEffect를 통해 구현해야 합니다.");
        }
        
        public override void OnRoundStart()
        {
            // 라운드 시작 시 특별한 처리 없음
        }
        
        public override void OnUpdate(float deltaTime)
        {
            // 매 프레임 특별한 처리 없음
        }
        
        public override void OnRemove()
        {
            Debug.Log($"[피해 감소] {Target.UnitName}에게서 받는 피해 감소 효과 제거");
        }
        
        public override BaseEffect Clone()
        {
            var clone = new DamageReductionEffect(EffectId, Coefficient)
            {
                Caster = this.Caster,
                Target = this.Target
            };
            return clone;
        }
    }
}
