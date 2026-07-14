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
        
        public override void OnRemove()
        {
            if (Target != null)
            {
                Target.Priority -= (int)Coefficient;
                Debug.Log($"[도발] {Target.UnitName}의 우선도 복구: {Target.Priority}");
            }
        }
    }
}
