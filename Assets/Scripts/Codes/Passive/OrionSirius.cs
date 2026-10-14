using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>오리온 — 적의 직접 공격행동 다섯 번을 모아 광역 반격. 다단·보호막 피격도 행동당 한 번.</summary>
    public sealed class OrionSirius : UniquePassiveCode
    {
        public const string ResourceId = "orion_sirius";
        public const int Maximum = 5;
        public const float AttackRatio = 1.60f, HealthRatio = .04f, HealRatio = .05f;
        private bool _working, _pending;
        private int _lastAction = int.MinValue, _generation;
        private Action<EventContext> _hit, _death, _controlEnded;
        private Coroutine _rain;
        public int Charges => Caster?.GetCombatResource(ResourceId) ?? 0;
        public bool Pending => _pending;
        public override int AttackForm => DamageTag.Shot;
        public OrionSirius(PassiveCodeContext context) : base(context)
        { CodeName = "시리우스"; CombatResourceLabels.Register(ResourceId, "시리우스"); }
        private bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true; _pending = false; _generation++; _lastAction = int.MinValue;
            Caster.SetCombatResourceMaximum(ResourceId, Maximum, true);
            _hit = Hit; _death = _ => StopCode();
            _controlEnded = _ => TryQueue();
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _hit);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _death);
            Caster.AddListener(BaseEnums.UnitEventType.OnControlEnds, _controlEnded);
            ActionScheduler.AnyActionFinished += Finished;
        }
        private void Hit(EventContext context)
        {
            var damage = context?.DmgCtx;
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (!Working || damage == null || damage.IsDamageOverTime || damage.ResolvedDamage <= 0 || !DirectAttack.IsDirect(damage) ||
                damage.Attacker.IsEnemy == Caster.IsEnemy || scheduler?.ExecutingUnit == null) return;
            int action = scheduler.CurrentActionId;
            if (_lastAction == action) return;
            _lastAction = action; Gain(1);
        }
        public void Gain(int amount)
        {
            if (!Working || amount <= 0) return;
            Caster.AddCombatResource(ResourceId, amount);
            TryQueue();
        }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind) => TryQueue();
        private void TryQueue()
        {
            if (!Working || _pending || Caster.isControlled || Charges < Maximum || CombatTargets.AliveEnemies(Caster).Count == 0) return;
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;
            int generation = _generation;
            _pending = true;
            bool accepted = scheduler.EnqueueAdditional(Caster, "orion_sirius", "시리우스 · 화살비",
                () => { if (Working && generation == _generation) { Caster.isCasting = true; _rain = Caster.StartCoroutine(Rain(generation)); } },
                () => Working && generation == _generation && Charges >= Maximum && CombatTargets.AliveEnemies(Caster).Count > 0,
                () => { if (generation == _generation) _pending = false; });
            if (!accepted) _pending = false;
        }
        private IEnumerator Rain(int generation)
        {
            bool fired = false;
            try
            {
                var tags = new List<int> { DamageTag.AllTarget, DamageTag.AdditionalAttack, DamageTag.CounterAttack, DamageTag.NonContactAttack, DamageTag.Shot };
                var routing = AttackTargetRouter.Prepare(Caster, CombatTargets.AliveEnemies(Caster), BaseEnums.CodeType.Passive, tags, CodeName);
                yield return routing.ResolvePreAttack();
                if (!Working || generation != _generation || !routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) ||
                    !Targeting.Announce(Caster, routing.Targets) || !Caster.TryConsumeCombatResource(ResourceId, Maximum)) yield break;
                int raw = Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * AttackRatio + Caster.HpMax * HealthRatio);
                AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Cast, CodeName);
                yield return DirectAttack.HitEach("orion.sirius", Caster, routing.Targets, target =>
                {
                    bool crit = RollCritical(target, out float multiplier);
                    AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, CodeName);
                    target.TakeDamage(new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(raw * multiplier)), BaseEnums.CodeType.Passive, new List<int>(tags), crit));
                    fired = true;
                }, stopOnControl: true, presentation: (targets, impact) => OrionSkadiFx.Rain(Caster, targets, impact));
                if (fired && Working && !Caster.isControlled) Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(Caster.HpMax * HealRatio), Caster);
                if (fired) AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Resolve, CodeName);
            }
            finally
            {
                if (generation == _generation) { _pending = false; _rain = null; if (Caster != null) Caster.isCasting = false; }
            }
        }
        public override void StopCode()
        {
            _working = false; _pending = false; _generation++;
            ActionScheduler.AnyActionFinished -= Finished;
            if (_hit != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _hit);
            if (_death != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _death);
            if (_controlEnded != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnControlEnds, _controlEnded);
            if (_rain != null && Caster != null) { Caster.StopCoroutine(_rain); Caster.isCasting = false; }
            _rain = null; _hit = _death = _controlEnded = null;
            Caster?.SetCombatResourceMaximum(ResourceId, Maximum, true);
        }
    }
}
