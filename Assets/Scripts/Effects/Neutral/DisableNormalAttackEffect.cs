using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Neutral
{
    /// <summary>
    /// 행동불가 효과
    /// 일반 공격을 사용할 수 없는 상태
    /// </summary>
    public class DisableNormalAttackEffect : BaseEffect
    {
        public DisableNormalAttackEffect(int effectId, float coefficient = 100f) : base(effectId, coefficient)
        {
            EffectName = "행동불가";
            EffectDescription = "일반 공격을 사용할 수 없습니다.";
            Category = BaseEnums.EffectCategory.Neutral;
        }
        
        public override void OnApply()
        {
            // normalCooldown을 매우 높은 값으로 설정하여 일반 공격 불가능하게 만듦
            if (Target != null)
            {
                Target.normalCooldown = float.MaxValue;
                Debug.Log($"[행동불가] {Target.UnitName}의 일반 공격이 비활성화되었습니다.");
            }
        }
        
        public override void OnRoundStart()
        {
            // 매 라운드 행동불가 유지
            if (Target != null)
            {
                Target.normalCooldown = float.MaxValue;
            }
        }
        
        public override void OnUpdate(float deltaTime)
        {
            // 지속적으로 일반 공격 쿨다운을 최댓값으로 유지
            if (Target != null && Target.normalCooldown < float.MaxValue)
            {
                Target.normalCooldown = float.MaxValue;
            }
        }
        
        public override void OnRemove()
        {
            // 행동불가 해제 시 normalCooldown 초기화
            if (Target != null)
            {
                Target.normalCooldown = 0f;
                Debug.Log($"[행동불가] {Target.UnitName}의 일반 공격이 활성화되었습니다.");
            }
        }
        
        public override BaseEffect Clone()
        {
            var clone = new DisableNormalAttackEffect(EffectId, Coefficient)
            {
                Caster = this.Caster,
                Target = this.Target
            };
            return clone;
        }
    }
}
