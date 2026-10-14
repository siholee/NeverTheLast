using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Vfx;
using Managers;
using UnityEngine;
namespace Codes.Ultimate
{
    public sealed class OrionStarPiercer : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Shot;
        protected override bool RequiresCompletionEvidence => true;
        public OrionStarPiercer(UltimateCodeContext context) : base(context) => CodeName = "별을 꿰뚫는 화살";
        protected override IEnumerator Perform()
        {
            var target = PickPriorityEnemy(); if (target == null) yield break;
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Shot };
            var routing = PrepareTargets(new[] { target }, tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) || !Targeting.Announce(Caster, routing.Targets)) yield break;
            bool fired = false;
            yield return DirectAttack.HitEach("orion.ultimate", Caster, routing.Targets, enemy =>
            {
                int raw = Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * 2.20f + Caster.HpMax * .06f);
                Managers.AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                enemy.TakeDamage(BuildContext(raw, new List<int>(tags), enemy)); fired = true; MarkEffectApplied();
            }, stopOnControl: true, presentation: (targets, impact) => OrionSkadiFx.Star(Caster, targets[0], impact));
            if (!fired || !DirectAttack.CasterCanContinue(Caster, true)) yield break;
            Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(Caster.HpMax * .12f), Caster);
            foreach (var passive in Caster.ActivePassiveCodes) if (passive is OrionSirius sirius) sirius.Gain(2);
        }
    }
}
