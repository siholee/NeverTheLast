using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities.Status;
using Effects.Base;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 피그말리온의 궁극기: a004_U_LovesPrize (사랑의 대가)
    /// 자신에게 8초간 핏빛 장미 상태를 부여합니다.
    /// 핏빛 장미: 행동불가 + 도발 + 가시 효과 (접촉 피해 시 화상 부여)
    /// </summary>
    public class a004_U_LovesPrize : UltimateCode
    {
        public a004_U_LovesPrize(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 8f;
            CodeName = "사랑의 대가";
            CastingDelay = 0f; // 즉시 시전
        }

        public override void CastCode()
        {
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 핏빛 장미 상태 생성 (StatusId = 4)
            var bloodyRoseStatus = new UnitStatus(4, Caster, Caster);
            
            // 행동불가 효과 추가 (EffectId = 2001)
            bloodyRoseStatus.AddEffect(2001, 0f);
            
            // 도발 효과 추가 (EffectId = 2002)
            bloodyRoseStatus.AddEffect(2002, 0f);
            
            // 가시 효과 추가 (EffectId = 2003, 화상 부여)
            bloodyRoseStatus.AddEffect(2003, 0f);
            
            // Effect 객체 생성 및 할당
            foreach (var effectInstance in bloodyRoseStatus.Effects)
            {
                effectInstance.EffectObject = EffectFactory.CreateEffect(
                    effectInstance.EffectId,
                    effectInstance.Coefficient,
                    Caster,
                    Caster
                );
            }
            
            // 상태 적용
            Caster.AddStatus(bloodyRoseStatus);
            
            Debug.Log($"[사랑의 대가] {Caster.UnitName}에게 핏빛 장미 상태 부여 (8초, 행동불가+도발+가시)");
            
            StopCode();
            yield return null;
        }

        public override void StopCode()
        {
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive;
        }
    }
}
