using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Managers;
using UnityEngine;
namespace Codes.Ultimate
{
    public sealed class SkadiIcicleSpike : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Spell;
        protected override bool RequiresCompletionEvidence => true;
        public SkadiIcicleSpike(UltimateCodeContext context) : base(context) => CodeName = "고드름 스파이크";
        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            var routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) || !Targeting.Announce(Caster, routing.Targets)) yield break;
            bool fired = false;
            yield return DirectAttack.HitEach("skadi.ultimate", Caster, routing.Targets, target =>
            {
                Managers.AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                target.TakeDamage(BuildContext(StatDamage(0, BaseEnums.StatType.Attack, 1.80f), new List<int>(tags), target));
                fired = true; MarkEffectApplied();
                if (target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Cryo, source: Caster);
            }, stopOnControl: true, presentation: (targets, impact) => OrionSkadiFx.Rain(Caster, targets, impact, true));
            if (fired && DirectAttack.CasterCanContinue(Caster, true)) Caster.AddShield(Mathf.RoundToInt(Caster.DefCurr * 1.20f), Caster);
        }
    }
}
