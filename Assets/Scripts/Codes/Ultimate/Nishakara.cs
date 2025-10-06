using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 찬드라의 궁극기: 나샤카라(Nishakara)
    /// 가장 전열에 있는 아군 전체에게 자신의 방어력 50%에 해당하는 방어막을 부여한다.
    /// </summary>
    public class Nishakara : UltimateCode
    {
        public Nishakara(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 8f;
            CodeName = "나샤카라";
            CastingDelay = 1.5f;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 캐스팅 딜레이
            float elapsedTime = 0f;
            while (elapsedTime < CastingDelay)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    Debug.Log($"{Caster.UnitName}의 {CodeName} 시전이 방해됨");
                    StopCode();
                    yield break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Debug.Log($"{Caster.UnitName}이 {CodeName}을 시전했습니다!");

            // 가장 전열에 있는 아군들 타겟팅
            List<Unit> frontRowAllies = Target.GetFrontRowAlliesTarget(Caster);
            int shieldAmount = (int)(Caster.DefCurr * 0.5f); // 방어력의 50%

            foreach (Unit ally in frontRowAllies)
            {
                if (ally != null && ally.isActive)
                {
                    // 방어막 부여
                    ally.AddShield(shieldAmount);

                    // 시각적 표시를 위한 방어막 상태 효과 추가
                    string shieldIdentifier = $"Shield_{ally.GetInstanceID()}_{Time.time}";
                    ShieldEffect shieldEffect = new ShieldEffect(Caster, shieldIdentifier, 0);
                    ally.AddStatusEffect(shieldIdentifier, shieldEffect);

                    Debug.Log($"{ally.UnitName}에게 {shieldAmount}의 방어막 부여! (전열 보호)");
                }
            }

            if (frontRowAllies.Count == 0)
            {
                Debug.Log("전열에 아군이 없어 방어막을 부여할 수 없습니다.");
            }
            else
            {
                Debug.Log($"전열의 {frontRowAllies.Count}명의 아군에게 방어막 부여 완료!");
            }

            StopCode();
        }

        public override void StopCode()
        {
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            // 전열에 아군이 있는지 확인
            return Target.GetFrontRowAlliesTarget(Caster).Count > 0;
        }
    }
}