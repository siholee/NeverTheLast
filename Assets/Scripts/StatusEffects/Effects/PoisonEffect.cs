using StatusEffects.Base;

namespace StatusEffects.Effects
{
    public class PoisonEffect: StatusEffect, ITemporalEffect, IHpChangeEffect
    {
        private int _poisonDamage;
        private float _elapsedTime;

        public PoisonEffect(int poisonDamage)
        {
            _poisonDamage = poisonDamage;
            Duration = 2f; 
            _elapsedTime = 0f;
        }
        
        public bool IsTriggered(float deltaTime)
        {
            if (_elapsedTime < Duration)
            {
                return false;
            }
            float previousTime = _elapsedTime;
            _elapsedTime += deltaTime;
            
            // 이전 시간과 현재 시간 사이에 0.1의 배수가 있는지 확인
            int previousMultiple = (int)(previousTime / 0.1f);
            int currentMultiple = (int)(_elapsedTime / 0.1f);
            
            return currentMultiple > previousMultiple;
        }
        
        public void UpdateDuration(float duration)
        {
            Duration += duration;
        }
        
        public int HpFlatChange()
        {
            return -_poisonDamage;
        }
        
        public float HpPercentageChange()
        {
            return 0f;
        }
    }
}