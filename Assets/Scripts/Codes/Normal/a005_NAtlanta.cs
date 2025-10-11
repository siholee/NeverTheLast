using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Entities.Status;
using Effects.Base;
using Helpers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 아탈란테의 일반공격: a005-N아탈란테
    /// 자신의 공격력 80%에 해당하는 단일공격 & 적에게 공격력의 10%에 해당하는 '맹독' 부여
    /// </summary>
    public class a005_NAtlanta : BaseNormalCode
    {
        public a005_NAtlanta(NormalCodeContext context) : base(context)
        {
            CodeName = "a005-N아탈란테";
        }

        /// <summary>
        /// 아탈란테는 공격력의 80%로 데미지 계산
        /// </summary>
        protected override int CalculateDamage(float critMultiplier)
        {
            return (int)(Caster.AtkCurr * 0.8f * critMultiplier);
        }

        /// <summary>
        /// 아탈란테는 항상 비접촉 공격 (처형자이지만 예외)
        /// </summary>
        protected override List<int> GetDamageTags()
        {
            List<int> tags = new List<int> { Helpers.DamageTag.SingleTarget, Helpers.DamageTag.NormalAttack, Helpers.DamageTag.NonContactAttack };
            return tags;
        }

        /// <summary>
        /// 아탈란테의 추가 효과: 사냥꾼의 독 부여 (새 Status 시스템)
        /// </summary>
        protected override IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            // 사냥꾼의 독 상태 생성 (StatusId = 3)
            var huntersVenomStatus = new UnitStatus(3, Caster, target);
            
            // DOT 효과 추가 (EffectId = 1001, 공격력의 10%)
            huntersVenomStatus.AddEffect(1001, 10f);
            
            // Effect 객체 생성 및 할당
            foreach (var effectInstance in huntersVenomStatus.Effects)
            {
                effectInstance.EffectObject = EffectFactory.CreateEffect(
                    effectInstance.EffectId,
                    effectInstance.Coefficient,
                    Caster,
                    target
                );
            }
            
            // 상태 적용
            target.AddStatus(huntersVenomStatus);
            
            Debug.Log($"[아탈란테] {target.UnitName}에게 사냥꾼의 독 상태 부여 (공격력의 10% DOT)");
            
            yield return null;
        }
    }
}