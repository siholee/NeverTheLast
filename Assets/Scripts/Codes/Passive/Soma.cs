using System;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 찬드라의 패시브: 소마(Soma)
    /// 전투 시작 시 아군 전체에게 자신의 방어력 60%에 해당하는 방어막 부여
    /// </summary>
    public class Soma : PassiveCode
    {
        public Soma(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "소마";
            Caster = context.Caster;
        }

        public override void CastCode()
        {
            Debug.Log($"[소마 패시브] {Caster.UnitName}의 소마 패시브가 활성화되었습니다.");

            // 라운드 시작 시 방어막 부여를 위한 이벤트 핸들러 등록
            Action<EventContext> onRoundStartHandler = (eventContext) =>
            {
                Debug.Log($"[소마 패시브] OnRoundStart 이벤트 발생 - {Caster.UnitName}");
                GrantShieldOnCombatStart();
            };

            Debug.Log($"[소마 패시브] {Caster.UnitName}의 OnRoundStart 이벤트 핸들러 등록");

            // 이벤트 핸들러 등록
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, onRoundStartHandler);
        }

        /// <summary>
        /// 전투 시작 시 아군 전체에게 시전자 방어력의 60%에 해당하는 방어막 부여
        /// </summary>
        protected void GrantShieldOnCombatStart()
        {
            Debug.Log($"[소마 패시브] GrantShieldOnCombatStart 메서드 시작 - {Caster.UnitName}");
            
            var targetUnits = Target.GetAllAllies(Caster);
            int shieldAmount = Mathf.RoundToInt(Caster.DefCurr * 0.6f);
            
            Debug.Log($"[소마 패시브] 방어막 양: {shieldAmount} (방어력 {Caster.DefCurr}의 60%)");
            Debug.Log($"[소마 패시브] 대상 유닛 수: {targetUnits.Count}");
            
            foreach (var targetUnit in targetUnits)
            {
                if (targetUnit != null && targetUnit.isActive)
                {
                    targetUnit.AddShield(shieldAmount);
                    Debug.Log($"[소마 패시브] {targetUnit.UnitName}에게 {shieldAmount}의 방어막 부여 완료");
                }
                else
                {
                    Debug.Log($"[소마 패시브] 대상 유닛이 null이거나 비활성 상태");
                }
            }
        }

        public override void StopCode()
        {
            Debug.Log($"[소마 패시브] {Caster.UnitName}의 소마 패시브 중지");
            // 소마는 지속 효과가 없으므로 특별한 정리 작업 없음
        }
    }
}