using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using UnityEngine;

namespace Codes.Ultimate
{
    public class a002_U_FinalBell : UltimateCode
    {
        public const string StatusKey = "FinalBell";

        public a002_U_FinalBell(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 4;
            CodeName = "만종";
            CastingDelay = 0f;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // Replace 정책: 재시전 시 8초로 갱신 (구 dict 덮어쓰기와 동일)
            var status = BuffStatus.Create(
                BuffStatusIds.FinalBell, StatusKey, "만종",
                Caster, Caster, new FinalBellBuffEffect(),
                duration: 4,   // 8초 → 4턴
                description: "DEX가 레벨×2만큼 증가합니다.");
            Caster.AddStatus(status);
            Debug.Log($"[만종] {Caster.UnitName}: 4턴간 DEX +{Caster.Level * 2}, 시의 종언 발동 간격 1/3");

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
