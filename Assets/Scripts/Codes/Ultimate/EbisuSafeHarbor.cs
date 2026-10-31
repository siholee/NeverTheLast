using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class EbisuSafeHarbor : BaseUltimateCode
    {
        public const float HealRatio = .50f;
        public EbisuSafeHarbor(UltimateCodeContext context) : base(context) => CodeName = "모두 무사히 돌아오도록";
        public override bool HasValidTarget()
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return false;
            if (Caster.HpCurr <= Caster.HpMax * .65f) return true;
            var grid = GridManager.Instance;
            return grid != null && grid.heroList.Concat(grid.enemyList).Concat(grid.summonList).Any(unit =>
                unit != null && unit.isActive && unit.IsOnField && unit.IsEnemy == Caster.IsEnemy &&
                (unit.isControlled || unit.HasNegativeStatus()));
        }
        protected override bool RequiresCompletionEvidence => true;
        protected override IEnumerator Perform()
        {
            if (!Combat.DirectAttack.CasterCanContinue(Caster, true)) yield break;
            Effects.Vfx.PurchasedArcherFx.RosterPreparation(Caster,Caster);
            Caster.Heal(Mathf.RoundToInt(Caster.HpMax * HealRatio), Caster);
            var grid = GridManager.Instance;
            var allies = grid.heroList.Concat(grid.enemyList).Concat(grid.summonList)
                .Where(unit => unit != null && unit.isActive && unit.IsOnField && unit.IsEnemy == Caster.IsEnemy).Distinct().ToList();
            foreach (var ally in allies)
            {
                ally.RemoveAllNegativeStatuses(Caster);
                Effects.Vfx.VfxComposer.PlayAffectedSlot("ebisu.ultimate", 0, Caster, ally);
            }
            MarkEffectApplied();
            Effects.Vfx.VfxComposer.PlayScreen("ebisu.ultimate", Caster, Caster);
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            yield return new WaitForSeconds(.3f);
        }
    }
}
