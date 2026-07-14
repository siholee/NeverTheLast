using System.Collections;
using BaseClasses;
using Codes.Base;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Ultimate
{
    public class a002_U_FinalBell : UltimateCode
    {
        public a002_U_FinalBell(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 8f;
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
            var effect = new FinalBellEffect(Caster);
            Caster.AddStatusEffect(FinalBellEffect.StatusIdentifier, effect);
            Debug.Log($"[만종] {Caster.UnitName}: 8초간 DEX +{Caster.Level * 2}, 시의 종언 발동 간격 1/3");

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
