using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public sealed class OctaviaEncirclement : UniquePassiveCode
    {
        public const int MarkId = 73701;
        private bool _working, _refreshing;
        private readonly HashSet<(int action, Unit attacker, Unit target)> _handled = new();
        private readonly HashSet<Unit> _auraTargets = new();
        public string MarkKey => "octavia_encirclement_" + Caster.GetEntityId();
        public string AuraKey => "octavia_defense_" + Caster.GetEntityId();
        public bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public OctaviaEncirclement(PassiveCodeContext context) : base(context) => CodeName = "함대의 포위망";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            Unit.AnyDamageDealt += Damaged;
            Unit.AnyAttributesUpdated += AttributesChanged;
            Unit.AnyUnitDied += Died;
            ActionScheduler.AnyActionStarted += Started;
            ActionScheduler.AnyActionFinished += Finished;
            RefreshAura();
        }
        public long Marks(Unit target) => target?.GetStatuses().FirstOrDefault(x => x.Key == MarkKey)?.Effects
            .Select(x => x.EffectObject).OfType<EncirclementCounter>().FirstOrDefault()?.Count ?? 0;
        public void ClearMarks(Unit target) => target?.RemoveStatusByKey(MarkKey);
        private void Damaged(DamageResolvedContext resolved)
        {
            var hit = resolved?.DamageContext;
            if (!Working || hit == null || hit.IsCancelled || hit.IsDamageOverTime || hit.IsSharedDamage ||
                hit.CodeType == BaseEnums.CodeType.Effect || hit.HasTag(DamageTag.BonusDamage) || hit.HasTag(DamageTag.ElementalReaction) ||
                resolved.DamageDealt <= 0 || resolved.Attacker == null || resolved.Attacker.IsEnemy != Caster.IsEnemy ||
                resolved.Target == null || !resolved.Target.isActive || resolved.Target.IsEnemy == Caster.IsEnemy ||
                (resolved.Attacker == Caster && hit.HasTag(DamageTag.UltAttack))) return;
            var scheduler = GameManager.Instance?.ActionScheduler;
            int action = scheduler?.ExecutingUnit != null ? scheduler.CurrentActionId : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(hit);
            if (!_handled.Add((action, resolved.Attacker, resolved.Target))) return;
            long count = Marks(resolved.Target);
            var mark = BuffStatus.Create(MarkId, MarkKey, "포위", Caster, resolved.Target,
                new EncirclementCounter(count == long.MaxValue ? count : count + 1), 0,
                category: BaseEnums.StatusCategory.Neutral, description: $"포위 {count + 1}중첩. 옥타비아의 초월기 피해 증가, 적중 후 소비.");
            mark.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            resolved.Target.AddStatus(mark);
        }
        private sealed class EncirclementCounter : BaseEffect
        {
            public readonly long Count;
            public EncirclementCounter(long count) : base(0) => Count = count;
        }
        private sealed class FleetDefense : BaseEffect
        {
            private readonly OctaviaEncirclement _source;
            public FleetDefense(OctaviaEncirclement source) : base(0) => _source = source;
            public override bool IsBeneficial => true;
            public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat)
                => stat == BaseEnums.StatType.SpecialDefense && _source.Working ? _source.Caster.StatValue(BaseEnums.StatType.SpecialAttack) * .30f : 0;
        }
        private void AttributesChanged(Unit unit) { if (unit == Caster) RefreshAura(); }
        private void Started(Unit actor, ActionScheduler.ActionKind kind, string label) => RefreshAura();
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            int id = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1;
            _handled.RemoveWhere(x => x.action == id);
        }
        private void Died(Unit unit, Unit killer) { if (unit == Caster) StopCode(); }
        private void RefreshAura()
        {
            if (!Working || _refreshing) return;
            _refreshing = true;
            try
            {
                foreach (Unit ally in SummonSupportCombat.Allies(Caster))
                {
                    if (!ally.HasStatusKey(AuraKey))
                    {
                        var aura = BuffStatus.Create(73702, AuraKey, "함대의 방호", Caster, ally, new FleetDefense(this), 0,
                            isBeneficial: true, description: "옥타비아의 현재 특공30%만큼 특방 증가.");
                        aura.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
                        ally.AddStatus(aura); _auraTargets.Add(ally);
                    }
                    else ally.RefreshAttributes();
                }
            }
            finally { _refreshing = false; }
        }
        public override void StopCode()
        {
            _working = false;
            Unit.AnyDamageDealt -= Damaged; Unit.AnyAttributesUpdated -= AttributesChanged; Unit.AnyUnitDied -= Died;
            ActionScheduler.AnyActionStarted -= Started; ActionScheduler.AnyActionFinished -= Finished;
            foreach (Unit ally in _auraTargets) if (ally != null) ally.RemoveStatusByKey(AuraKey);
            _auraTargets.Clear(); _handled.Clear();
        }
    }

    public sealed class AgrippaUnsinkableFormation : UniquePassiveCode
    {
        private bool _working;
        private readonly HashSet<Unit> _holders = new();
        public bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public string AuraKey => "agrippa_formation_" + Caster.GetEntityId();
        public override int BaseShieldAmount => StatDamage(0, BaseEnums.StatType.Defense, .80f);
        public AgrippaUnsinkableFormation(PassiveCodeContext context) : base(context) => CodeName = "불침의 진형";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            ActionScheduler.AnyActionStarted += Started; Unit.AnyUnitDied += Died;
            ApplyAura();
            foreach (Unit ally in SummonSupportCombat.Allies(Caster)) ally.AddShield(BaseShieldAmount, Caster);
        }
        private void Started(Unit actor, ActionScheduler.ActionKind kind, string label) => ApplyAura();
        private void Died(Unit unit, Unit killer) { if (unit == Caster) StopCode(); }
        private void ApplyAura()
        {
            if (!Working) return;
            foreach (Unit ally in SummonSupportCombat.Allies(Caster))
            {
                if (ally.HasStatusKey(AuraKey)) continue;
                var status = BuffStatus.Create(73703, AuraKey, CodeName, Caster, ally, new FormationPower(this), 0,
                    isBeneficial: true, description: "보호막이 있으면 초월기 피해+20%.");
                status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId; ally.AddStatus(status); _holders.Add(ally);
            }
        }
        private sealed class FormationPower : BaseEffect
        {
            private readonly AgrippaUnsinkableFormation _source;
            public FormationPower(AgrippaUnsinkableFormation source) : base(0) => _source = source;
            public override bool IsBeneficial => true;
            public override float UltimateDamageBonus(Unit actor) => actor == Target && _source.Working && actor.ShieldCurr > 0 ? .20f : 0;
        }
        public override void StopCode()
        {
            _working = false; ActionScheduler.AnyActionStarted -= Started; Unit.AnyUnitDied -= Died;
            foreach (Unit ally in _holders) if (ally != null) ally.RemoveStatusByKey(AuraKey);
            _holders.Clear();
        }
    }
}

namespace Codes.Normal
{
    public sealed class OctaviaRisingTide : BaseNormalCode
    {
        private int _version;
        public override int AttackForm => DamageTag.Spell;
        public OctaviaRisingTide(NormalCodeContext context) : base(context) => CodeName = "밀려드는 파도";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, .70f);
        protected override List<Unit> SelectTarget() => GetAvailableEnemies();
        protected override List<int> GetDamageTags() => new() { DamageTag.AllTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Spell };
        protected override IEnumerator Deliver(Unit target)
        {
            int version = _version;
            var context = BuildContext(target);
            yield return VfxComposer.PlaySingleTarget("octavia.normal", Caster, target, () => { if (version == _version) Hit(target, context); });
        }
        public override void StopCode()
        {
            _version++;
            if (Caster != null && CurrSkillCoroutine != null) { Caster.StopCoroutine(CurrSkillCoroutine); CurrSkillCoroutine = null; }
            base.StopCode();
        }
    }

    public sealed class AgrippaReorganize : BaseNormalCode
    {
        private int _version;
        public override int BaseShieldAmount => StatDamage(0, BaseEnums.StatType.Defense, .40f);
        public AgrippaReorganize(NormalCodeContext context) : base(context) => CodeName = "전열 재정비";
        protected override int BaseDamage(Unit target) => 0;
        public override bool HasValidTarget() => Caster != null && Caster.IsOnField;
        protected override IEnumerator SkillCoroutine()
        {
            int version = ++_version;
            bool ready = true; yield return WaitForCast(x => ready = x);
            if (!ready) { StopCode(); yield break; }
            var allies = SummonSupportCombat.Allies(Caster).ToList();
            Targeting.Announce(Caster, allies);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            yield return VfxComposer.PlayMultiTargetHits("agrippa.normal", Caster, allies, ally =>
            {
                if (version != _version || !DirectAttack.CasterCanContinue(Caster, true) || !ally.IsOnField) return;
                ally.AddShield(BaseShieldAmount, Caster);
                AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
            }, .04f);
            if (version == _version && DirectAttack.CasterCanContinue(Caster, true)) NotifyActionResolved();
            StopCode();
        }
        public override void StopCode()
        {
            _version++;
            if (Caster != null && CurrSkillCoroutine != null) { Caster.StopCoroutine(CurrSkillCoroutine); CurrSkillCoroutine = null; }
            base.StopCode();
        }
    }
}

namespace Codes.Ultimate
{
    public sealed class OctaviaClosingHorizon : BaseUltimateCode
    {
        private int _version;
        public override int AttackForm => DamageTag.Spell;
        protected override bool AppliesElement => true;
        protected override bool RequiresCompletionEvidence => true;
        protected override void OnStoppedExternally() => _version++;
        public OctaviaClosingHorizon(UltimateCodeContext context) : base(context) => CodeName = "닫히는 수평선";
        protected override IEnumerator Perform()
        {
            int version = ++_version;
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            var routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            var passive = Caster.ActivePassiveCodes.OfType<Codes.Passive.OctaviaEncirclement>().FirstOrDefault();
            yield return VfxComposer.PlayMultiTargetHits("octavia.ultimate", Caster, routing.Targets, target =>
            {
                if (version != _version || !DirectAttack.CasterCanContinue(Caster, true) || target == null || !target.isActive) return;
                float ratio = 1.8f * (1f + .08f * (passive?.Marks(target) ?? 0));
                var hit = BuildContext(StatDamage(0, BaseEnums.StatType.Attack, ratio), tags, target);
                target.TakeDamage(hit);
                if (hit.IsCancelled || hit.ResolvedDamage <= 0) return;
                MarkEffectApplied(); passive?.ClearMarks(target);
                if (DirectAttack.CasterCanContinue(Caster, true) && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: Caster);
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            }, .04f);
        }
    }

    public sealed class AgrippaWinningPosition : BaseUltimateCode
    {
        private int _version;
        public override int BaseShieldAmount => StatDamage(0, BaseEnums.StatType.Defense, 1.60f);
        protected override bool RequiresCompletionEvidence => true;
        protected override void OnStoppedExternally() => _version++;
        public AgrippaWinningPosition(UltimateCodeContext context) : base(context) => CodeName = "승리를 위한 포석";
        public override bool HasValidTarget() => Caster != null && Caster.IsOnField;
        protected override IEnumerator Perform()
        {
            int version = ++_version;
            var allies = SummonSupportCombat.Allies(Caster).ToList();
            Targeting.Announce(Caster, allies);
            yield return VfxComposer.PlayMultiTargetHits("agrippa.ultimate", Caster, allies, ally =>
            {
                if (version != _version || !DirectAttack.CasterCanContinue(Caster, true) || !ally.IsOnField) return;
                ally.AddShield(BaseShieldAmount, Caster); MarkEffectApplied();
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            }, .04f);
            if (version != _version || !DirectAttack.CasterCanContinue(Caster, true)) yield break;
            foreach (Unit ally in allies.Where(x => x != Caster && x.IsOnField && x.UltimateResourceType == BaseEnums.UltimateResourceType.Mana))
                ally.AddUltimateResource(Mathf.RoundToInt(ally.ManaMax * .20f));
        }
    }
}
