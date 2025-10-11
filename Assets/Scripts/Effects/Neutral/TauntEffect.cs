using BaseClasses;
using Effects.Base;
using UnityEngine;

namespace Effects.Neutral
{
    /// <summary>
    /// 도발 효과
    /// 우선도를 증가시켜 적의 공격을 끌어당깁니다.
    /// </summary>
    public class TauntEffect : BaseEffect
    {
        private int _originalPriority;
        
        public TauntEffect(int effectId, float coefficient = 1f) : base(effectId, coefficient)
        {
            EffectName = "도발";
            EffectDescription = $"우선도를 {coefficient} 증가시킵니다.";
            Category = BaseEnums.EffectCategory.Neutral;
        }
        
        public override void OnApply()
        {
            if (Target != null)
            {
                _originalPriority = Target.Priority;
                Target.Priority += (int)Coefficient;
                Debug.Log($"[도발] {Target.UnitName}의 우선도: {_originalPriority} → {Target.Priority}");
            }
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
            if (Target != null)
            {
                Target.Priority -= (int)Coefficient;
                Debug.Log($"[도발] {Target.UnitName}의 우선도 복구: {Target.Priority}");
            }
        }
        
        public override BaseEffect Clone()
        {
            var clone = new TauntEffect(EffectId, Coefficient)
            {
                Caster = this.Caster,
                Target = this.Target
            };
            return clone;
        }
    }
}
