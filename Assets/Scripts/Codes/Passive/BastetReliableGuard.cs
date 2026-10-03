using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>바스테트 — 직접 공격 대상에 경호 자격 아군이 없을 때 한 명이 대신 맞는다.</summary>
    public sealed class BastetReliableGuard : UniquePassiveCode, IAttackTargetInterceptor,
        IRedirectInterventionVfx
    {
        public BastetReliableGuard(PassiveCodeContext context) : base(context)
            => CodeName = "든든한 경호대가 있다고!";

        public Unit Redirect(AttackTargetRoutingContext context, Unit originalTarget)
        {
            if (Caster == null || originalTarget != Caster || context?.Attacker == null) return null;
            if (!Caster.isActive || !Caster.IsOnField || context.Attacker.IsEnemy == Caster.IsEnemy) return null;

            List<Unit> allies = Caster.IsEnemy
                ? GridManager.Instance?.enemyList
                : GridManager.Instance?.heroList;
            if (allies == null) return null;

            // 이미 맞는 목록에 경호 자격자가 있으면 대상 수를 늘리거나 중복 피격시키지 않는다.
            if (context.SelectedTargets.Any(unit => unit != Caster && IsEligible(unit))) return null;

            Unit guard = allies.Where(IsEligible)
                .OrderByDescending(unit => unit.HasArchetypeTag("Counter"))
                .ThenByDescending(unit => unit.Priority)
                .ThenByDescending(IsFront)
                .ThenByDescending(unit => unit.HpMax > 0 ? unit.HpCurr / (float)unit.HpMax : 0f)
                .ThenBy(unit => unit.ID)
                .FirstOrDefault();
            if (guard == null) return null;

            return guard;
        }

        public IEnumerator PlayIntervention(AttackTargetRoutingContext context, Unit protectedUnit,
            Unit redirectedTo)
        {
            if (Caster == null || redirectedTo == null) yield break;
            ActionAnnouncer.AnnouncePassive(Caster, this);
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Resolve, CodeName);
            yield return BastetFx.GuardIntervention(Caster, redirectedTo);
        }

        private bool IsEligible(Unit unit)
            => unit != null && unit != Caster && unit.isActive && unit.IsOnField && !unit.IsUntargetable &&
               unit.IsEnemy == Caster.IsEnemy &&
               (unit.HasArchetypeTag("Sturdy") || unit.HasArchetypeTag("Counter"));

        private static bool IsFront(Unit unit)
            => unit?.currentCell != null && unit.currentCell.yPos > 0 && Math.Abs(unit.currentCell.xPos) == 1;
    }
}
