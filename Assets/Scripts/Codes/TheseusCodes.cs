using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>테세우스 — 초월기 뒤 실제 일반행동 두 번에 파장을 잇는다. 전투 사이에는 남기지 않는다.</summary>
    public sealed class TheseusReturningWave : UniquePassiveCode
    {
        public const string ResourceId = "theseus_wave";
        public const int Charges = 2;
        public const float AttachmentChance = .25f;
        private bool _active;
        public TheseusReturningWave(PassiveCodeContext context) : base(context) => CodeName = "되돌아오는 파도";
        public override void CastCode()
        {
            if (_active || Caster == null) return;
            _active = true;
            CombatResourceLabels.Register(ResourceId, "파장");
            Caster.SetCombatResourceMaximum(ResourceId, Charges, resetCurrent: true);
        }
        public void Recharge()
        {
            if (!_active || !DirectAttack.CasterCanContinue(Caster, true)) return;
            Caster.SetCombatResourceMaximum(ResourceId, Charges, resetCurrent: true);
            Caster.AddCombatResource(ResourceId, Charges);
        }
        public bool TryConsume() => _active && Caster.TryConsumeCombatResource(ResourceId, 1);
        public override void StopCode()
        {
            _active = false;
            if (Caster != null) Caster.SetCombatResourceMaximum(ResourceId, Charges, resetCurrent: true);
        }
    }
}

namespace Codes.Normal
{
    public sealed class TheseusSeaCleaver : BaseNormalCode
    {
        public const float AttackRatio = 1.8f;
        public const float WaveRatio = 1f;
        public override int AttackForm => DamageTag.Slash;
        private bool _resolved;
        private int _deliveryVersion;
        public TheseusSeaCleaver(NormalCodeContext context) : base(context) => CodeName = "해로를 가르는 검";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, AttackRatio);
        protected override void OnAttackResolved(Unit target, DamageContext context)
            => _resolved = CommandedAttackScope.Of(context) == null;
        protected override IEnumerator Deliver(Unit target)
        {
            int version = ++_deliveryVersion;
            _resolved = false;
            DamageContext context = BuildContext(target);
            yield return VfxComposer.PlaySingleTarget("theseus.normal", Caster, target, () =>
            {
                if (version == _deliveryVersion) Hit(target, context);
            });
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (version != _deliveryVersion || !_resolved || scheduler?.ExecutingUnit != Caster || scheduler.ActingKind != ActionScheduler.ActionKind.Normal ||
                !DirectAttack.CasterCanContinue(Caster, true)) yield break;
            var passive = Caster.ActivePassiveCodes.OfType<Codes.Passive.TheseusReturningWave>().FirstOrDefault();
            var targets = GetAvailableEnemies();
            if (targets.Count == 0 || passive == null || Caster.GetCombatResource(Codes.Passive.TheseusReturningWave.ResourceId) <= 0) yield break;
            var waveTags = new List<int> { DamageTag.AllTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            var routing = AttackTargetRouter.Prepare(Caster, targets, BaseEnums.CodeType.Normal, waveTags, "되돌아오는 파도");
            yield return routing.ResolvePreAttack();
            if (version != _deliveryVersion || !routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) ||
                !Targeting.Announce(Caster, routing.Targets) || !passive.TryConsume()) yield break;
            targets = routing.Targets;
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Cast, "되돌아오는 파도");
            yield return VfxComposer.PlayMultiTargetHits("theseus.wave", Caster, targets, victim =>
            {
                if (version != _deliveryVersion || !DirectAttack.CasterCanContinue(Caster, true) || victim == null || !victim.isActive) return;
                var tags = new List<int>(waveTags);
                bool crit = RollCritical(victim, tags, out float multiplier);
                var wave = new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(
                    StatDamage(0, BaseEnums.StatType.Attack, WaveRatio) * multiplier)), BaseEnums.CodeType.Normal, tags, crit)
                    { AppliesElement = true };
                victim.TakeDamage(wave);
                if (!DirectAttack.CasterCanContinue(Caster, true) || !victim.isActive) return;
                victim.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: Caster,
                    baseChance: Codes.Passive.TheseusReturningWave.AttachmentChance, hitScaling: EffectHitScaling.Proportional);
                AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, "되돌아오는 파도");
            }, .04f);
        }
        public override void StopCode()
        {
            _deliveryVersion++;
            _resolved = false;
            if (Caster != null && CurrSkillCoroutine != null)
            {
                Caster.StopCoroutine(CurrSkillCoroutine);
                CurrSkillCoroutine = null;
            }
            base.StopCode();
        }
    }
}

namespace Codes.Ultimate
{
    public sealed class TheseusReturningCurrent : BaseUltimateCode
    {
        public const float AttackRatio = 2.8f;
        public override int AttackForm => DamageTag.Slash;
        protected override bool AppliesElement => true;
        protected override bool RequiresCompletionEvidence => true;
        private int _castVersion;
        protected override void OnStoppedExternally() => _castVersion++;
        public TheseusReturningCurrent(UltimateCodeContext context) : base(context) => CodeName = "귀환하는 해류";
        protected override IEnumerator Perform()
        {
            int version = ++_castVersion;
            Unit enemy = PickPriorityEnemy();
            if (enemy == null) yield break;
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Slash };
            var routing = PrepareTargets(new List<Unit> { enemy }, tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            enemy = routing.Targets.FirstOrDefault();
            yield return VfxComposer.PlaySingleTarget("theseus.ultimate", Caster, enemy, () =>
            {
                if (version != _castVersion || !DirectAttack.CasterCanContinue(Caster, true) || enemy == null || !enemy.isActive) return;
                enemy.TakeDamage(BuildContext(StatDamage(0, BaseEnums.StatType.Attack, AttackRatio), tags, enemy));
                if (!DirectAttack.CasterCanContinue(Caster, true)) return;
                MarkEffectApplied();
                if (enemy.isActive) enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: Caster);
                if (!DirectAttack.CasterCanContinue(Caster, true)) return;
                float missing = 1f - Mathf.Clamp01(Caster.HpCurr / (float)Mathf.Max(1, Caster.HpMax));
                Caster.Heal(Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * (1f + missing)), Caster);
                Caster.ActivePassiveCodes.OfType<Codes.Passive.TheseusReturningWave>().FirstOrDefault()?.Recharge();
                VfxComposer.PlayAffectedSlot("theseus.ultimate", 0, Caster, Caster);
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            });
        }
    }
}
