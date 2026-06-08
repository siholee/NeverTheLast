using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 세이 P1 패시브 (Lv.1) — 지령의 일체<br/>
    /// CON을 계수로 사용하는 기술을 사용할 때, INT의 스탯값도 더한다.<br/>
    /// 라운드 시작 시 PermanentPassiveFlags에 "SeiP1" 플래그를 추가해
    /// Unit.GetEffectiveCon()이 CON+INT를 반환하도록 함.
    /// </summary>
    public class SeiP1Passive : PassiveCode
    {
        public SeiP1Passive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "지령의 일체";
            Caster = context.Caster;
        }

        public override void CastCode()
        {
            // 영구 플래그 추가 (HashSet이므로 중복 없음)
            Caster.PermanentPassiveFlags.Add("SeiP1");
            Debug.Log($"[패시브] {Caster.UnitName} — {CodeName}: " +
                      $"CON 기반 스킬에 INT({Caster.IntStat}) 추가 적용");
        }

        public override void StopCode()
        {
            Caster.PermanentPassiveFlags.Remove("SeiP1");
        }
    }
}
