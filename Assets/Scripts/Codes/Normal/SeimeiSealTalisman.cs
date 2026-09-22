using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 세이메이 N — 봉인부(43). 단일 적에게 CON 위력 45, 특수·비접촉.
    ///
    /// 첫 일반행동과 이후 3회마다(1·4·7…) 봉인부가 준비되어, 피해 뒤 고정 기절 1턴을 시도한다.
    /// 제어 분쇄·면역은 그대로 받고, 시도했으면 실패해도 봉인부를 쓴다.
    /// 원소를 붙이지 않는다 — 직접 기절과 원소 반응 기절이 뜻하지 않게 겹치는 것을 줄인다.
    /// </summary>
    public sealed class SeimeiSealTalisman : BaseNormalCode
    {
        private const int SealInterval = 3;
        private int _actions;

        public SeimeiSealTalisman(NormalCodeContext context) : base(context)
        {
            CodeName = "봉인부";
            Power = 45;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        /// <summary>이번 회차에 봉인부가 준비되었는가. 회차는 실제로 끝난 일반행동만 센다.</summary>
        private bool SealReady => _actions % SealInterval == 0;

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };

        /// <summary>
        /// 봉인부가 준비되었으면 제어가 값진 대상을 먼저 고른다 —
        /// 준비 공격 중 → 갑주 보유 → 분쇄에 막히지 않는 대상 → 보통 대상.
        /// </summary>
        protected override List<Unit> SelectTarget()
        {
            if (!SealReady) return base.SelectTarget();

            List<Unit> available = GetAvailableEnemies();
            List<Unit> controllable = available
                .Where(unit => !unit.isControlled && ControlStatuses.DiminishedTurns(unit, 1) > 0)
                .ToList();

            Unit pick = CombatTargets.PickByPriority(controllable.Where(CoastFrontline.IsCharging).ToList())
                        ?? CombatTargets.PickByPriority(controllable.Where(CoastFrontline.IsArmored).ToList())
                        ?? CombatTargets.PickByPriority(controllable);
            return pick != null ? new List<Unit> { pick } : base.SelectTarget();
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (!SealReady || target == null || !target.isActive || target.HpCurr <= 0) return;

            if (ControlStatuses.ApplyFixedStun(target, Caster, 1))
                SeimeiIds.OnControlLanded(Caster, target);

            if (Caster.HasLearnedPassiveCode(SeimeiIds.SealEcho))
            {
                target.AddStatus(BuffStatus.Create(
                    SeimeiIds.SealEchoStatus, "seimei_seal_echo", "봉인의 잔향", Caster, target,
                    new OutgoingDamageMultiplierEffect(0.9f),
                    duration: 2,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    description: "주는 피해가 10% 감소합니다."));
            }
        }

        protected override System.Collections.IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            _actions++;
            yield return null;
        }
    }
}
