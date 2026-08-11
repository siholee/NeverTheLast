using System;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 세이의 초기 패시브. 라운드 시작 시 아군 전원의 치명타율을 올린다.
    /// 시전자가 죽으면 부여한 상태를 회수한다.
    /// </summary>
    public class HolyEnchant : PassiveCode
    {
        public HolyEnchant(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "성광 부여";
            Caster = context.Caster;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            ApplyHolyEnchant();
            // 시전자 사망 시 효과를 멈추기 위한 이벤트 핸들러 생성
            Action<EventContext> onDeathHandler = null;
            // 익명함수를 변수화해 이벤트 핸들러에서 삭제가 가능하도록 함
            onDeathHandler = (deathInfo) =>
            {
                StopCode();
                // 부여한 상태효과를 해제하고 스스로를 제거해 일회성 처리로 만듬
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
            };

            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            // 시전자의 사망 이벤트에 핸들러 등록
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
        }

        protected void ApplyHolyEnchant()
        {
            var targetUnits = GridManager.Instance.TargetAllAllies(Caster);
            foreach (var targetUnit in targetUnits)
            {
                // 고정 키를 사용하여 중첩 방지 (Ignore 정책)
                var status = BuffStatus.Create(
                    BuffStatusIds.HolyEnchant, "HolyEnchantBuff", "성광 부여",
                    Caster, targetUnit, new CritChanceBuffEffect(0.1f),
                    stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                    isBeneficial: true,
                    description: "치명타율 +10%");
                targetUnit.AddStatus(status);
            }
        }

        public override void StopCode()
        {
            var targetUnits = GridManager.Instance.TargetAllAllies(Caster);
            foreach (var targetUnit in targetUnits)
            {
                targetUnit.RemoveStatusByKey("HolyEnchantBuff");
            }
        }
    }
}