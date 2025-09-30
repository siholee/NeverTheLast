using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Helpers;
using StatusEffects.Effects;
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
        /// 아탈란테의 추가 효과: 맹독 부여
        /// </summary>
        protected override IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            // 맹독 효과 적용 (공격력의 10%)
            // 각 맹독이 개별적으로 스택되도록 고유한 identifier 생성
            string identifier = $"PoisonEffect_{Caster.GetInstanceID()}_{target.GetInstanceID()}_{System.DateTime.Now.Ticks}";
            var poisonEffect = new PoisonEffect(Caster, identifier, (int)(Caster.AtkCurr * 0.1f));
            target.AddStatusEffect(identifier, poisonEffect);
            
            Debug.Log($"{Caster.UnitName}이 {target.UnitName}에게 맹독을 부여했습니다. (데미지: {(int)(Caster.AtkCurr * 0.1f)}, ID: {identifier})");
            
            yield return null;
        }
    }
}