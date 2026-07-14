using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Ultimate
{
    public class a012_U_Soma : UltimateCode
    {
        private const float SpeedBuffDuration = 8f;

        public a012_U_Soma(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 8f;
            CodeName = "소마";
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            float elapsedTime = 0f;
            while (elapsedTime < CastingDelay)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    StopCode();
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            int shieldAmount = Mathf.Max(1, Caster.GetBaseCon());
            Caster.AddShield(shieldAmount);
            Caster.NotifyBeneficialEffectReceived(Caster);

            float speedBonus = Mathf.Max(0f, Caster.GetBaseInt() * 0.01f);
            foreach (Unit ally in GetRearAllies())
            {
                if (ally == null || !ally.isActive) continue;

                string identifier = $"SomaSpeed_{Caster.GetEntityId()}";
                ally.AddStatusEffect(identifier, new CodeAccelerationEffect(Caster, identifier, speedBonus, SpeedBuffDuration));
                Debug.Log($"[소마] {ally.UnitName}에게 {SpeedBuffDuration}초간 공격속도 +{speedBonus:P0}");
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
            return Caster != null && Caster.isActive;
        }

        private List<Unit> GetRearAllies()
        {
            if (GridManager.Instance == null)
            {
                return Target.GetAllAllies(Caster);
            }

            int frontColumn = GridManager.Instance.GetFrontColumn(Caster.IsEnemy);
            return Target.GetAllAllies(Caster)
                .Where(ally => ally != null && ally.currentCell != null && ally.currentCell.xPos != frontColumn)
                .ToList();
        }
    }

    public class QuetzalcoatlYorisUltimate : UltimateCode
    {
        public QuetzalcoatlYorisUltimate(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "요리스틀리의 맹세";
            Cooldown = 0.5f;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddCombatResource("Yoris", 1);
            Caster.ultimateCooldown = Cooldown;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive && Caster.GetCombatResource("Yoris") < Caster.GetCombatResourceMaximum("Yoris");
        }
    }
}
