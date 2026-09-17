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

namespace Codes.Ultimate
{
    /// <summary>
    /// 아베노 세이메이 U — 천지반전(43).
    /// 아군 전체에 자신의 CON×1.2 기준 보호막을 먼저 준 뒤, 적 전체에 CON 위력 40의 특수·비접촉 피해와 에어본을 시도한다.
    ///
    /// 보호막을 먼저 주는 이유: 에어본이 분쇄·면역에 막혀 준비 공격을 끊지 못해도 받아낼 몫은 남아야 한다.
    /// 피해는 결계 해독으로 보호막을 건너뛰고, 에어본이 들어가면 공허의 갑주가 벗겨진다.
    /// </summary>
    public sealed class SeimeiHeavenEarthReversal : SimpleUltimate
    {
        private const int ReversalPower = 40;

        public SeimeiHeavenEarthReversal(UltimateCodeContext context)
            : base(context, "천지반전", 0.6f)
        {
            Power = ReversalPower;
        }

        protected override void Resolve()
        {
            GuardAllies();

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * crit));

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack },
                    isCrit));
                if (!target.isActive || target.HpCurr <= 0) continue;
                if (ControlStatuses.ApplyAirborne(target, Caster)) SeimeiIds.OnControlLanded(Caster, target);
            }
        }

        private void GuardAllies()
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * SeimeiIds.ShieldConRatio));
            bool guardian = Caster.HasLearnedPassiveCode(SeimeiIds.GuardianFormation);

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster).ToList())
            {
                ally.AddShield(amount, Caster);
                if (!guardian) continue;
                ally.AddStatus(BuffStatus.Create(
                    SeimeiIds.GuardianStatus, "seimei_guardian_formation", "수호의 방진", Caster, ally,
                    new ReceivingDamageMultiplierEffect(0.9f),
                    duration: 1,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "받는 피해가 10% 감소합니다."));
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
