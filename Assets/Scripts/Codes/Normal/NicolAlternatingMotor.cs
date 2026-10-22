using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    public sealed class NicolAlternatingMotor : BaseNormalCode
    {
        protected override bool AppliesElementTo(Unit target) => true;
        public const int Bounces = 6;
        public const float AttackRatio = .50f;
        public NicolAlternatingMotor(NormalCodeContext context) : base(context) => CodeName = "교류 회전자계 유도 잔동기";
        public override int AttackForm => DamageTag.Spell;
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, AttackRatio);
        protected override List<int> GetDamageTags() => new() { DamageTag.MultiTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Spell };
        protected override IEnumerator SkillCoroutine()
        {
            bool completed = true;
            yield return WaitForCast(ok => completed = ok);
            if (!completed) { StopCode(); yield break; }
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            yield return Bounce();
            NotifyActionResolved();
            StopCode();
        }
        protected override IEnumerator CommandedDeliver(Unit target) => Bounce();
        private IEnumerator Bounce()
        {
            for (int i = 0; i < Bounces && DirectAttack.CasterCanContinue(Caster, true); i++)
            {
                var enemies = CombatTargets.AliveEnemies(Caster);
                if (enemies.Count == 0) break;
                var target = enemies[Random.Range(0, enemies.Count)];
                var routing = AttackTargetRouter.Prepare(Caster, new[] { target }, CodeType, GetDamageTags(), CodeName);
                yield return routing.ResolvePreAttack();
                if (!routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true)) break;
                target = routing.Targets[0];
                if (!Targeting.Announce(Caster, routing.Targets)) break;
                var context = BuildContext(target);
                yield return Effects.Vfx.LokapalaFx.Normal("nicol.normal", Caster, target, () =>
                { if (DirectAttack.CasterCanContinue(Caster, true) && target != null && target.IsOnField) Hit(target, context); });
            }
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        { if (target != null && target.IsOnField) target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster); }
    }
}
