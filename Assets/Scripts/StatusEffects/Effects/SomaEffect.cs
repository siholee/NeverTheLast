using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    /// <summary>
    /// 소마(Soma) 효과 - 찬드라의 패시브
    /// 방어막을 보유한 아군에게 공격력 10% 증가 버프를 제공
    /// </summary>
    public class SomaEffect : StatusEffect
    {
        public SomaEffect(Unit grantor) : base(grantor, null)
        {
            Stack = 1; // 중첩 불가능
            Duration = 0f; // 영구 지속 (시전자가 죽을 때까지)
        }
        
        /// <summary>
        /// 방어막이 있는 유닛에게만 공격력 버프 적용
        /// </summary>
        public override float AtkMultiplicativeModifier(Unit unit)
        {
            // 방어막이 있을 때만 공격력 10% 증가
            if (unit != null && unit.ShieldCurr > 0)
            {
                return 0.1f; // 공격력 +10%
            }
            return 0f; // 방어막이 없으면 버프 없음
        }
        
        /// <summary>
        /// 소마 효과가 현재 활성화되어 있는지 확인
        /// </summary>
        public bool IsActive(Unit unit)
        {
            return unit != null && unit.ShieldCurr > 0;
        }
    }
}