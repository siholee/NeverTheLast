using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>피그말리온 궁극기: 8초간 피해 감소·도발·접촉 반격 화상.</summary>
    public class a004_U_LovesPrize : UltimateCode
    {
        public a004_U_LovesPrize(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Cooldown = 4;
            CodeName = "장미의 가시";
            CastingDelay = 0f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            Caster.AddStatus(BuffStatus.Create(
                PygmalionStatusIds.RoseThorns, "pygmalion_rose_thorns", CodeName,
                Caster, Caster, new RoseThornsEffect(),
                duration: 4,   // 8초 → 4턴
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "받는 피해가 50% 감소하고 대상 지정 우선도가 1 증가합니다. 접촉 피해를 받으면 공격자별 1초 내부 쿨다운으로 CON 명중 판정 후 기본 2턴 화상을 부여합니다."));
            StopCode();
            yield return null;
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }
}
