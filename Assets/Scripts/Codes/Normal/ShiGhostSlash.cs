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
        public override int AttackForm => DamageTag.Slash;

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
            if (!castCompleted || !Combat.DirectAttack.CasterCanContinue(Caster, false))
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
            bool crit = RollCritical(target, out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(BaseDamage(target) * multiplier));
            var context = new DamageContext(Caster, damage, codeType, tags, crit);
            // 지휘 중이면 원시 피해는 그대로 두고 표지 · 최종 배율만 컨텍스트에 붙인다.
            StampCommand(context);
            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.08f, 0.22f, 0.12f));
            AudioManager.PlayAction(Caster, slot, SfxCuePhase.Draw, CodeName);

            yield return ShiFx.DestructionSlash(Caster, target, () =>
            {
                if (!Combat.DirectAttack.CasterCanContinue(Caster, false) || target == null ||
                    !target.isActive || !Combat.CommandedAttackScope.TryAdmit(context)) return;
                AudioManager.PlayAction(Caster, slot, SfxCuePhase.Hit, CodeName);
                target.TakeDamage(context);
                if (!Combat.DirectAttack.CasterCanContinue(Caster, false)) return;
                if (source == SpecialSource.None)
                {
                    Caster.Invoke(BaseEnums.UnitEventType.OnNormalAttackHit,
                        new EventContext(Caster, target, context));
                    OnAttackResolved(target, context);
                }
                bool killed = !target.isActive || target.HpCurr <= 0 || context.ToughnessBroken;
                SlashResolved?.Invoke(target, source, killed);
            }, context.IsCrit ? 1.16f : 1f);

            // 취소된 지휘의 멸참은 만종도 울리지 않는다(표지 없는 실제 멸참은 늘 허용).
            if (Combat.DirectAttack.CasterCanContinue(Caster, false) && bellTier > 0 && bell != null &&
                Combat.CommandedAttackScope.Allows(context))
                yield return bell.ResolveBell(bellTier, target);
        }

        private List<int> DamageTagsFor(SpecialSource source) => new()
        {
            DamageTag.SingleTarget,
            source == SpecialSource.None ? DamageTag.NormalAttack : DamageTag.SpecialAttack,
            DamageTag.NonContactAttack,
            DamageTag.Slash,
        };

        protected override List<int> GetDamageTags() => DamageTagsFor(_source);

        /// <summary>
        /// 나폴레옹의 지휘 사격 — 일반 멸참과 같은 판정 · 만종 누적 · 처치 재행동 신호를 연다.
        /// 일반행동 완료 알림만 내지 않는다(SkillCoroutine을 거치지 않으므로).
        /// </summary>
        protected override IEnumerator CommandedDeliver(Unit target)
        {
            List<int> tags = DamageTagsFor(SpecialSource.None);
            ShiFinalBell bell = Caster.ActiveUltimateCode as ShiFinalBell;
            int bellTier = bell?.RegisterDestructionSlash() ?? 0;
            yield return DeliverMelcham(target, SpecialSource.None, tags, BaseEnums.CodeType.Normal,
                SfxActionSlot.Normal, bell, bellTier);
        }

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
               (ExecutionEstimate(target) >= target.HpCurr ||
                (target.HasToughness && Combat.Toughness.Estimate(Caster, 30f) >= target.ToughnessCurr));

        public override void StopCode()
        {
            _forcedTarget = null;
            _source = SpecialSource.None;
            base.StopCode();
        }
    }
}
