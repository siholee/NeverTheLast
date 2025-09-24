using System.Collections.Generic;
using BaseClasses;
using Entities;
using StatusEffects.Base;
using UnityEngine;

namespace StatusEffects.Effects
{
    public class BurnEffect: StatusEffect, ITemporalEffect, IHpChangeEffect
    {
        private float _elapsedTime;
        private const float BurnPercentage = 0.02f; // 최대체력의 2%

        public BurnEffect(Unit grantor, string identifier): base(grantor, identifier)
        {
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
                // 최대체력의 2%에 해당하는 고정 피해
                int damage = Mathf.RoundToInt(context.Grantee.HpMax * BurnPercentage);
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
            // 화상은 퍼센테이지 기반 데미지이므로 고정값은 0
            return 0;
        }
        
        public float HpPercentageChange()
        {
            // 최대체력의 2% 손실
            return -BurnPercentage;
        }
    }
}