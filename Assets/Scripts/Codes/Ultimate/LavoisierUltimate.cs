using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class LavoisierExperiment : UltimateCode
    {
        public override bool ConsumesResourceOnResolve => true;

        public LavoisierExperiment(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "실험 결과를 발표하겠습니다!";
            CastingDelay = 0.6f;
            MaxStage = 1;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive
            && Caster.Chemistry?.CanReact == true && CombatTargets.AliveEnemies(Caster).Count > 0;

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast || Caster.isControlled || !HasValidTarget()) { StopCode(); yield break; }

            LavoisierChemistry chemistry = Caster.Chemistry;
            chemistry.Resize();
            List<Unit> enemies = CombatTargets.AliveEnemies(Caster);
            float batch = chemistry.Reagents.Consume();
            bool critical = Random.value < Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(ReagentInventory.Damage(
                Caster.GetBaseInt(), Caster.GetBaseLuk(), batch) * (critical ? Caster.CritMultiplierCurr : 1f)));
            // 시약과 준비도를 먼저 태운 후 발동 신호를 보낸다. 취소된 시전은 여기까지 오지 않는다.
            Caster.ResolveDeferredUltimate();
            foreach (Unit target in enemies)
            {
                if (target == null || !target.isActive) continue;
                var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack }, critical);
                target.TakeDamage(context);
                if (target.isActive && !context.IsCancelled)
                    target.GrantCombatElement(BaseEnums.UnitElement.Pyro, Unit.CommonElementAuraDuration, Caster);
            }
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster != null) Caster.isCasting = false;
        }
    }
}
