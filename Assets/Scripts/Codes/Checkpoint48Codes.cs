using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Effects.Negative;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Codes.Checkpoints
{
    public static class Checkpoint48
    {
        public const int BossId = 3202, EscortId = 1121;
        public const int CommandId = 1050, EscortShotId = 1051;
        public const int FrailtyId = 74801;
        public const string FrailtyKey = "checkpoint48_frailty";

        public static bool ApplyFrailty(Unit source, Unit target)
        {
            if (source == null || !source.IsOnField || target == null || !target.IsOnField ||
                source.IsEnemy == target.IsEnemy) return false;
            var status = BuffStatus.Create(FrailtyId, FrailtyKey, "쇠약", source, target,
                new ReceivingDamageMultiplierEffect(1.15f), duration: 2,
                stackPolicy: StatusStackPolicy.Replace, category: StatusCategory.Negative,
                description: "받는 피해 +15%, 받는 치유량 -25%. 자신의 일반행동 2회 완료까지. 재부여 시 기간 갱신.");
            status.AddEffect(new HealingReductionEffect(0.75f));
            status.NormalActionDuration = true;
            status.AppliedActionId = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1;
            target.AddStatus(status);
            return status.ApplicationSucceeded;
        }
    }

    /// <summary>피격 수 대신 실제 일반행동 완료만 지휘를 채운다. 광역/다단이 증원을 가속하지 않는다.</summary>
    public sealed class VoidLegionCommand : UniquePassiveCode
    {
        private readonly Action<EventContext> _normal, _cleanup;
        private bool _registered;
        public int ResolvedUltimates { get; private set; }
        public int AddedEscorts { get; private set; }

        public VoidLegionCommand(PassiveCodeContext context) : base(context)
        {
            CodeName = "군세의 지휘";
            _normal = _ => { if (Caster != null && Caster.IsOnField && !Caster.isControlled) Caster.AddUltimateResource(1); };
            _cleanup = _ => StopCode();
        }

        public override void CastCode()
        {
            if (_registered || Caster == null || !Caster.IsEnemy) return;
            ResolvedUltimates = 0;
            AddedEscorts = 0;
            Caster.ClearUltimateResource();
            Caster.AddListener(UnitEventType.OnNormalActionResolved, _normal);
            Caster.AddListener(UnitEventType.OnDeath, _cleanup);
            Caster.AddListener(UnitEventType.OnRoundEnd, _cleanup);
            _registered = true;
        }

        public int CompleteUltimate()
        {
            var game = GameManager.Instance;
            var grid = GridManager.Instance;
            if (!_registered || Caster == null || !Caster.IsOnField || Caster.isControlled ||
                game?.gameState != GameState.RoundInProgress || grid == null ||
                !Combat.CombatTargets.AliveEnemies(Caster).Any()) return 0;
            ResolvedUltimates++;
            // 빈 자리가 없었던 첫 두 기회도 사용된다. 남은 보충을 뒤에 몰아서 주지 않는다.
            if (ResolvedUltimates > 2) return 0;
            int count = grid.enemyList.Count(u => u != null && u.IsOnField && u.ID == Checkpoint48.EscortId);
            int spawned = 0;
            int column = grid.GetFrontColumn(true);
            for (int row = 1; row <= 2 && count < 2; row++)
            {
                if (grid.enemyList.Any(u => u != null && u.IsOnField && u.currentCell != null &&
                    u.currentCell.xPos == column && u.currentCell.yPos == row)) continue;
                Unit escort = grid.SpawnUnit(column, row, true, Checkpoint48.EscortId,
                    playEntry: true, entryDelay: 0.08f * spawned);
                if (escort == null) continue;
                // 새 개체만 개전 수명을 시작한다. 기존 아군/호위의 버프·HP·AV는 건드리지 않는다.
                escort.Invoke(UnitEventType.OnRoundStart, new EventContext(escort));
                count++;
                spawned++;
            }
            AddedEscorts += spawned;
            if (spawned > 0) grid.RequestFieldLayoutRefresh();
            if (spawned > 0) Combat.ActionAnnouncer.AnnouncePassive(Caster, this);
            return spawned;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(UnitEventType.OnNormalActionResolved, _normal);
            Caster.RemoveListener(UnitEventType.OnDeath, _cleanup);
            Caster.RemoveListener(UnitEventType.OnRoundEnd, _cleanup);
            _registered = false;
        }
    }

    public sealed class VoidLegionStrike : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Strike;
        protected override bool IsContact => true;
        public VoidLegionStrike(NormalCodeContext context) : base(context) => CodeName = "진군의 철퇴";
        protected override int BaseDamage(Unit target) => StatDamage(0, StatType.Attack, 1.1f);
        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return EnemyNormalFx.HeavyStrike(Caster, target, () => Hit(target, context));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.IsOnField && !context.IsCancelled) Checkpoint48.ApplyFrailty(Caster, target);
        }
    }

    public sealed class VoidPunisherShot : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Shot;
        public VoidPunisherShot(NormalCodeContext context) : base(context) => CodeName = "쇠락의 사격";
        protected override int BaseDamage(Unit target) => StatDamage(0, StatType.Attack, 0.6f);
        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return EnemyNormalFx.MirroredVolley(Caster, target, () => Hit(target, context));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.IsOnField && !context.IsCancelled) Checkpoint48.ApplyFrailty(Caster, target);
        }
    }

    public sealed class VoidLegionAdvance : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Spell;
        protected override bool RequiresCompletionEvidence => true;
        public VoidLegionAdvance(UltimateCodeContext context) : base(context) => CodeName = "무너지는 전선";
        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, AttackForm };
            var routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, routing.Targets)) yield break;
            var targets = routing.Targets;
            int index = 0;
            bool applied = false;
            Unit Next() => index < targets.Count ? targets[index++] : null;
            Action Impact(Unit target)
            {
                DamageContext context = BuildContext(StatDamage(0, StatType.Attack, 0.8f), new List<int>(tags), target);
                return () =>
                {
                    if (!Combat.DirectAttack.CasterCanContinue(Caster, false) || target == null || !target.IsOnField) return;
                    AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                    target.TakeDamage(context);
                    MarkEffectApplied();
                    applied = true;
                };
            }
            yield return EnemyNormalFx.RepeatedUltimate("enemy.ultimate.void_breath", Caster,
                targets.Count, 0.05f, Next, Impact, 0.30f, 1.34f, 0.04f);
            if (!applied || !Combat.DirectAttack.CasterCanContinue(Caster, false)) yield break;
            Caster.ActivePassiveCodes.OfType<VoidLegionCommand>().FirstOrDefault()?.CompleteUltimate();
        }
    }
}
