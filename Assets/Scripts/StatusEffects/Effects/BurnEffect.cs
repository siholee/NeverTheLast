using System.Collections.Generic;
using BaseClasses;
using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    public class BurnEffect: StatusEffect, ITemporalEffect, IHpChangeEffect
    {
        private float _burnDamage;
        private float _elapsedTime;

        public BurnEffect(Unit grantor, string identifier, float burnDamage): base(grantor, identifier)
        {
            _burnDamage = burnDamage;
            Duration = 2f; 
            _elapsedTime = 0f;
        }
        
        public int IsTriggered(float deltaTime)
        {
            if (_elapsedTime > Duration)
            {
                return 0;
            }
            float previousTime = _elapsedTime;
            _elapsedTime += deltaTime;
            
            // 이전 시간과 현재 시간 사이에 0.1의 배수가 있는지 확인
            int previousMultiple = (int)(previousTime / 0.1f);
            int currentMultiple = (int)(_elapsedTime / 0.1f);
            
            return currentMultiple - previousMultiple;
        }
        
        public void OnUpdate(EventContext context)
        {
            int iteration = IsTriggered(context.FloatParam);
            for (int i = 0; i < iteration; i++)
            {
                int damage = HpFlatChange();
                DamageContext dmgContext = new DamageContext(Grantor, damage, BaseEnums.CodeType.Effect, new List<int>(), false, 10000000);
                context.Grantee.TakeDamage(dmgContext);
            }

            if (_elapsedTime >= Duration)
            {
                context.Grantee.RemoveStatusEffect(Identifier);
            }
        }
        
        public void UpdateDuration(float duration)
        {
            Duration += duration;
        }
        
        public int HpFlatChange()
        {
            return 0;
        }
        
        public float HpPercentageChange()
        {
            return -_burnDamage;
        }
    }
}