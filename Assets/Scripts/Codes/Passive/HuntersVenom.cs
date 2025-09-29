using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 아탈란테의 패시브: 사냥꾼의 독
    /// 공격이 적중한 모든 대상에게 공격력의 10%에 해당하는 맹독 부여
    /// </summary>
    public class HuntersVenom : PassiveCode
    {
        public HuntersVenom(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사냥꾼의 독";
            Caster = context.Caster;
        }

        public override void CastCode()
        {
            // 패시브 효과는 이제 아탈란테의 전용 일반공격과 궁극기에서 직접 처리됨
            // 이 패시브는 더미로 유지하여 호환성 보장
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 패시브 활성화 (효과는 일반공격과 궁극기에서 직접 처리)");
        }

        public override void StopCode()
        {
            // 더미 패시브이므로 특별한 정리 작업 없음
        }
    }
}