using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public sealed class HorusShatteringHorizon : UniquePassiveCode
    {
        private bool _working;
        private int _generation, _serial;
        public HorusShatteringHorizon(PassiveCodeContext context) : base(context) => CodeName = "무너지는 지평선";
        public override float PermanentStatPercentModifier(BaseEnums.StatType stat) => stat == BaseEnums.StatType.SpecialDefense ? .30f : 0;
        private bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true; _generation++;
            Toughness.Broken += Broken; Unit.AnyUnitDied += Died;
        }
        private void Broken(CombatDefeatContext context)
        {
            if (!Working || context?.Grantee == null || context.Grantee.IsEnemy != Caster.IsEnemy || context.Grantor?.IsEnemy == Caster.IsEnemy) return;
            int generation = _generation;
            Unit target = context.Grantor;
            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(Caster, "horus_sun_" + generation + "_" + (++_serial), "일륜 소사",
                () => (Caster.ActiveNormalCode as Codes.Normal.HorusStarbreaker)?.CastSunAdditional(target),
                () => Working && generation == _generation && CombatTargets.AliveEnemies(Caster).Count > 0);
        }
        private void Died(Unit unit, Unit killer) { if (unit == Caster) StopCode(); }
        public override void StopCode()
        {
            _working = false; _generation++;
            Toughness.Broken -= Broken; Unit.AnyUnitDied -= Died;
            Caster?.RemoveStatusByKey(Codes.Ultimate.HorusSolarRelease.ReleaseKey);
        }
    }
}

namespace Codes.Normal
{
    public sealed class HorusStarbreaker : BaseNormalCode
    {
        private int _version;
        public override int AttackForm => DamageTag.Shot;
        public override string AnnouncementName => Released && !IsCommanded ? "일륜 소사" : CodeName;
        private bool Released => Caster != null && Caster.HasStatusKey(Codes.Ultimate.HorusSolarRelease.ReleaseKey);
        public HorusStarbreaker(NormalCodeContext context) : base(context) => CodeName = "파성의 화살";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, 1.40f);
        protected override IEnumerator Deliver(Unit target)
        {
            int version = _version; var context = BuildContext(target);
            yield return VfxComposer.PlaySingleTarget("horus.normal", Caster, target, () => { if (version == _version) Hit(target, context); });
        }
        protected override IEnumerator SkillCoroutine()
        {
            if (!Released) { yield return base.SkillCoroutine(); yield break; }
            int version = ++_version;
            bool ready = true; yield return WaitForCast(x => ready = x);
            if (!ready || version != _version) { StopCode(); yield break; }
            Unit primary = SelectTarget().FirstOrDefault();
            bool hit = false;
            yield return SunRoutine(primary, false, version, () => hit = true);
            if (version == _version && hit && DirectAttack.CasterCanContinue(Caster, true)) NotifyActionResolved();
            StopCode();
        }
        public void CastSunAdditional(Unit originalTarget)
        {
            EndStaleCommand(); Caster.isCasting = true;
            int version = ++_version;
            CurrSkillCoroutine = Caster.StartCoroutine(AdditionalRoutine(originalTarget, version));
        }
        private IEnumerator AdditionalRoutine(Unit primary, int version)
        {
            yield return SunRoutine(primary, true, version, null);
            StopCode();
        }
        private IEnumerator SunRoutine(Unit primary, bool additional, int version, Action applied)
        {
            List<Unit> enemies = GetAvailableEnemies();
            if (primary == null || !primary.isActive || !enemies.Contains(primary)) primary = SelectTarget().FirstOrDefault();
            if (primary == null) yield break;
            enemies.Remove(primary); enemies.Insert(0, primary);
            var tags = new List<int> { DamageTag.MultiTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Shot };
            if (additional) tags.Add(DamageTag.AdditionalAttack);
            var routing = AttackTargetRouter.Prepare(Caster, enemies, BaseEnums.CodeType.Normal, tags, "일륜 소사");
            yield return routing.ResolvePreAttack();
            if (version != _version || !routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) || !Targeting.Announce(Caster, routing.Targets)) yield break;
            primary = routing.Targets.FirstOrDefault();
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, "일륜 소사");
            yield return VfxComposer.PlayMultiTargetHits("horus.sun", Caster, routing.Targets, target =>
            {
                if (version != _version || !DirectAttack.CasterCanContinue(Caster, true) || target == null || !target.isActive) return;
                bool main = target == primary;
                float raw = Caster.StatValue(BaseEnums.StatType.Attack) * (main ? 1f : .50f) +
                    Caster.StatValue(BaseEnums.StatType.SpecialAttack) * (main ? 1.60f : .80f);
                bool crit = RollCritical(target, tags, out float multiplier);
                var context = new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(raw * multiplier)), BaseEnums.CodeType.Normal, new List<int>(tags), crit)
                    { ToughnessReduction = main ? 30 : 15 };
                Hit(target, context, false);
                AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, "일륜 소사");
                if (!context.IsCancelled && context.ResolvedDamage > 0) applied?.Invoke();
            }, .04f);
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
    public sealed class HorusSolarRelease : BaseUltimateCode
    {
        public const string ReleaseKey = "horus_solar_release";
        private int _version;
        protected override bool RequiresCompletionEvidence => true;
        protected override void OnStoppedExternally() => _version++;
        public HorusSolarRelease(UltimateCodeContext context) : base(context) => CodeName = "일륜 해방";
        public override bool HasValidTarget() => Caster != null && Caster.IsOnField && AliveEnemies().Count > 0;
        protected override IEnumerator Perform()
        {
            int version = ++_version;
            yield return VfxComposer.PlaySingleTarget("horus.ultimate", Caster, Caster, () =>
            {
                if (version != _version || !DirectAttack.CasterCanContinue(Caster, true)) return;
                var status = BuffStatus.Create(73704, ReleaseKey, CodeName, Caster, Caster, new MarkerBuffEffect(), 3,
                    isBeneficial: true, description: "실제 일반행동3회 동안 일륜 소사로 대체. 추가행동은 기간을 소모하지 않음.");
                status.NormalActionDuration = true; status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
                Caster.AddStatus(status); MarkEffectApplied();
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            });
        }
    }
}
