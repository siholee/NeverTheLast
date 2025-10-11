using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;
using Helpers;
using UnityEngine;

namespace Effects.Neutral
{
    /// <summary>
    /// 가시 효과
    /// 피격 시 공격이 접촉이라면 공격자에게 화상을 부여합니다.
    /// </summary>
    public class ThornEffect : BaseEffect
    {
        public ThornEffect(int effectId, float coefficient = 100f) : base(effectId, coefficient)
        {
            EffectName = "가시";
            EffectDescription = "접촉 피해를 입으면 공격자에게 화상을 부여합니다.";
            Category = BaseEnums.EffectCategory.Neutral;
        }
        
        public override void OnApply()
        {
            if (Target != null)
            {
                // OnTakingDamage 이벤트에 리스너 등록
                Target.AddListener<EventContext>(BaseEnums.UnitEventType.OnTakingDamage, OnDamageTaken);
                Debug.Log($"[가시] {Target.UnitName}에게 가시 효과 적용");
            }
        }
        
        private void OnDamageTaken(EventContext context)
        {
            if (context.DmgCtx == null || context.DmgCtx.Attacker == null) return;
            
            // 접촉 피해인지 확인
            bool isContactAttack = !context.DmgCtx.DamageTags.Contains(DamageTag.NonContactAttack);
            
            if (isContactAttack)
            {
                // 공격자에게 화상 부여 (StatusId = 2)
                var burnStatus = new UnitStatus(2, Target, context.DmgCtx.Attacker);
                burnStatus.AddEffect(1002, 2f); // PercentDOT 2%
                
                // Effect 객체 생성 및 할당
                foreach (var effectInstance in burnStatus.Effects)
                {
                    effectInstance.EffectObject = EffectFactory.CreateEffect(
                        effectInstance.EffectId,
                        effectInstance.Coefficient,
                        Target,
                        context.DmgCtx.Attacker
                    );
                }
                
                context.DmgCtx.Attacker.AddStatus(burnStatus);
                Debug.Log($"[가시] {context.DmgCtx.Attacker.UnitName}이 접촉 피해로 화상을 입었습니다!");
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
                // OnTakingDamage 이벤트 리스너 제거
                Target.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnTakingDamage, OnDamageTaken);
                Debug.Log($"[가시] {Target.UnitName}에게서 가시 효과 제거");
            }
        }
        
        public override BaseEffect Clone()
        {
            var clone = new ThornEffect(EffectId, Coefficient)
            {
                Caster = this.Caster,
                Target = this.Target
            };
            return clone;
        }
    }
}
