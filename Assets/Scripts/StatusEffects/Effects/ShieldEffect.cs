using BaseClasses;
using Entities;
using StatusEffects.Base;
using UnityEngine;

namespace StatusEffects.Effects
{
    /// <summary>
    /// 방어막을 부여하는 상태 효과
    /// 적용 시 즉시 방어막을 부여하고, 라운드 종료까지 유지
    /// </summary>
    public class ShieldEffect : StatusEffect
    {
        private int _shieldAmount;
        private bool _isApplied;

        public ShieldEffect(Unit grantor, string identifier, int shieldAmount) : base(grantor, identifier)
        {
            _shieldAmount = shieldAmount;
            _isApplied = false;
            Duration = float.MaxValue; // 라운드 종료까지 지속
        }

        /// <summary>
        /// 방어막 효과 적용 (한 번만 실행)
        /// </summary>
        public void ApplyShield(Unit target)
        {
            if (!_isApplied)
            {
                target.AddShield(_shieldAmount);
                _isApplied = true;
                Debug.Log($"{Grantor.UnitName}이 {target.UnitName}에게 {_shieldAmount}의 방어막을 부여했습니다.");
            }
        }

        /// <summary>
        /// 스택 증가 시 방어막 추가 부여
        /// </summary>
        public void IncreaseStack(Unit target, int additionalShield)
        {
            target.AddShield(additionalShield);
            _shieldAmount += additionalShield;
            Stack++;
            Debug.Log($"{target.UnitName}의 방어막이 {additionalShield}만큼 추가되었습니다. (스택: {Stack})");
        }
    }
}