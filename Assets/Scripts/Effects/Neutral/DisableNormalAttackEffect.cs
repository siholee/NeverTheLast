using BaseClasses;
using Effects.Base;
using Entities;
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
        
        // 턴제에서는 normalCooldown이 의미를 잃었다(행동 주기는 AV가 전담한다).
        // 상태를 들고 있는 동안 스케줄러가 이 훅을 보고 일반공격 예약을 건너뛴다.
        public override bool BlocksNormalAttack(Unit unit) => unit == Target;

        public override void OnApply()
        {
            if (Target != null) Debug.Log($"[행동불가] {Target.UnitName}의 일반 공격이 비활성화되었습니다.");
        }

        public override void OnRemove()
        {
            if (Target != null) Debug.Log($"[행동불가] {Target.UnitName}의 일반 공격이 활성화되었습니다.");
        }
    }
}
