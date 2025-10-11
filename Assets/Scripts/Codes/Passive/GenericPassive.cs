using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 범용 패시브 코드
    /// 실질적인 기능은 없지만 패시브 이름과 설명을 표시하기 위한 용도
    /// 실제 효과는 다른 곳(일반공격, 궁극기, 상태효과 등)에서 구현됨
    /// </summary>
    public class GenericPassive : PassiveCode
    {
        public GenericPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = context.Name ?? "패시브";
            Caster = context.Caster;
        }

        public override void CastCode()
        {
            // 범용 패시브는 실제 로직이 없음 (이름과 설명만 표시용)
            Debug.Log($"{Caster.UnitName}의 패시브 [{CodeName}] 활성화 (효과는 다른 시스템에서 처리됨)");
        }

        public override void StopCode()
        {
            // 정리 작업 없음
        }

        public override bool HasValidTarget()
        {
            return true;
        }
    }
}
