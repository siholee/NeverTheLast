using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Ultimate;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>시 — 일반행동 「멸참」. 일반·특성·재행동 모두 같은 판정과 연출을 공유한다.</summary>
    public sealed class ShiGhostSlash : BaseNormalCode
    {
        public const float AttackRatio = 2.5f;

        public enum SpecialSource
        {
            None,
            Trait,
            Reprise,
        }

        private Unit _forcedTarget;
        private SpecialSource _source;
        public event Action<Unit, SpecialSource, bool> SlashResolved;

        public ShiGhostSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "멸참";
            CastingDelay = 0.10f;
        }

        protected override int BaseDamage(Unit target)
            => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator SkillCoroutine()
        {
            SpecialSource source = _source;
            _source = SpecialSource.None;
            // 예약 시점은 유지하고, 사망 사본과 진형·카메라 정리가 끝난 뒤에만 재행동을 보여 준다.
            if (source == SpecialSource.Reprise && GridManager.Instance != null)
                yield return GridManager.Instance.WaitForFieldLayoutSettled();
            bool castCompleted = true;
            yield return WaitForCast(ok => castCompleted = ok);
            if (!castCompleted)
            {
                StopCode();
                yield break;
            }

            TargetUnits = SelectTarget();
            List<int> tags = DamageTagsFor(source);
            BaseEnums.CodeType codeType = source == SpecialSource.None
                ? BaseEnums.CodeType.Normal : BaseEnums.CodeType.Special;
            Combat.AttackTargetResolution routing = Combat.AttackTargetRouter.Prepare(Caster, TargetUnits,
                codeType, tags, CodeName);
            yield return routing.ResolvePreAttack();
            TargetUnits = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, TargetUnits))
            {
                StopCode();
                yield break;
            }

            SfxActionSlot slot = source == SpecialSource.None ? SfxActionSlot.Normal : SfxActionSlot.Special;
            AudioManager.PlayAction(Caster, slot, SfxCuePhase.Cast, CodeName);
            Unit target = TargetUnits[0];
            ShiFinalBell bell = Caster.ActiveUltimateCode as ShiFinalBell;
            int bellTier = bell?.RegisterDestructionSlash() ?? 0;
            yield return DeliverMelcham(target, source, tags, codeType, slot, bell, bellTier);

            if (source == SpecialSource.None) NotifyActionResolved();
            StopCode();
        }

        private IEnumerator DeliverMelcham(Unit target, SpecialSource source, List<int> tags,
            BaseEnums.CodeType codeType, SfxActionSlot slot, ShiFinalBell bell, int bellTier)
        {
            bool crit = RollCritical(out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(BaseDamage(target) * multiplier));
            var context = new DamageContext(Caster, damage, codeType, tags, crit);
            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.08f, 0.22f, 0.12f));
            AudioManager.PlayAction(Caster, slot, SfxCuePhase.Draw, CodeName);

            yield return ShiFx.DestructionSlash(Caster, target, () =>
            {
                if (target == null || !target.isActive) return;
                AudioManager.PlayAction(Caster, slot, SfxCuePhase.Hit, CodeName);
                target.TakeDamage(context);
                if (source == SpecialSource.None)
                {
                    Caster.Invoke(BaseEnums.UnitEventType.OnNormalAttackHit,
                        new EventContext(Caster, target, context));
                    OnAttackResolved(target, context);
                }
                bool killed = !target.isActive || target.HpCurr <= 0;
                SlashResolved?.Invoke(target, source, killed);
            }, context.IsCrit ? 1.16f : 1f);

            if (bellTier > 0 && bell != null)
                yield return bell.ResolveBell(bellTier, target);
        }

        private List<int> DamageTagsFor(SpecialSource source) => new()
        {
            DamageTag.SingleTarget,
            source == SpecialSource.None ? DamageTag.NormalAttack : DamageTag.SpecialAttack,
            DamageTag.NonContactAttack,
        };

        protected override List<int> GetDamageTags() => DamageTagsFor(_source);

        protected override List<Unit> SelectTarget()
        {
            Unit target = _forcedTarget;
            _forcedTarget = null;
            if (target != null && target.isActive && target.IsOnField && !target.IsUntargetable)
                return new List<Unit> { target };
            return base.SelectTarget();
        }

        public bool CastSpecial(Unit target, SpecialSource source)
        {
            if (source == SpecialSource.None || Caster == null || !Caster.isActive ||
                target == null || !target.isActive || !target.IsOnField || target.IsUntargetable)
                return false;

            _forcedTarget = target;
            _source = source;
            CastCode();
            return true;
        }

        public int ExecutionEstimate(Unit target)
        {
            if (Caster == null || target == null) return 0;
            int raw = Mathf.Max(1, Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * AttackRatio));
            float mitigation = UnitStats.MitigationMultiplier(
                target.StatValue(BaseEnums.StatType.Defense), UnitStats.ArmorConstant(Caster.Level));
            return Mathf.Max(1, Mathf.RoundToInt(raw * mitigation));
        }

        public bool CanExecuteFromStats(Unit target)
            => target != null && target.isActive && target.HpCurr > 0 &&
               ExecutionEstimate(target) >= target.HpCurr;

        public override void StopCode()
        {
            _forcedTarget = null;
            _source = SpecialSource.None;
            base.StopCode();
        }
    }
}
