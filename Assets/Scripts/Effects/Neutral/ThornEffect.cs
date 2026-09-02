using BaseClasses;
using Effects.Base;
using Effects.Negative;
using Entities;
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
            
            // 접촉 태그가 붙은 공격에만 되받아친다. 지속피해는 태그 목록이 비어 있으므로
            // '비접촉이 아니면 접촉'으로 뒤집어 물으면 화상 틱마다 시전자를 태우게 된다.
            bool isContactAttack = context.DmgCtx.DamageTags != null &&
                                   context.DmgCtx.DamageTags.Contains(DamageTag.ContactAttack);
            
            if (isContactAttack)
            {
                Unit attacker = context.DmgCtx.Attacker;
                if (ElementalReaction.TryApplyBurn(Target, attacker))
                    Debug.Log($"[가시] {attacker.UnitName}이 접촉 피해로 화상을 입었습니다!");
                else
                    Debug.Log($"[가시] {attacker.UnitName}이 화상에 저항했습니다.");
            }
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
    }
}
