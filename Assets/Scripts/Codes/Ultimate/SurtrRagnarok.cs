using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class SurtrRagnarok : BaseUltimateCode
    {
        public const int HealthThresholdPercent = 60;
        public const float AttackRatio = 1.80f, HealthRatio = .14f, CostRatio = .20f, HealthThreshold = HealthThresholdPercent / 100f;
        public int LastHealthPaid { get; private set; }
        public int LastRawDamage { get; private set; }
        public override int AttackForm => DamageTag.Slash;
        protected override bool RequiresCompletionEvidence => true;
        public SurtrRagnarok(UltimateCodeContext context) : base(context) => CodeName = "아직 꺼지지 않은 종말";
        protected override IEnumerator Perform()
        {
            LastHealthPaid = LastRawDamage = 0;
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Slash };
            var routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) || !Targeting.Announce(Caster, routing.Targets)) yield break;
            int maxHp = Caster.HpMax;
            // Mono의 중간 정밀도 차이로 정확히 60%인 정수 HP가 문턱 아래로 판정되지 않게 한다.
            bool empowered = (long)Caster.HpCurr * 100 >= (long)maxHp * HealthThresholdPercent;
            if (empowered) LastHealthPaid = HealthCost.Pay(Caster, Mathf.RoundToInt(maxHp * CostRatio));
            LastRawDamage = Mathf.Max(1, Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * AttackRatio +
                (LastHealthPaid > 0 ? maxHp * HealthRatio : 0)));
            yield return DirectAttack.HitEach("surtr.ultimate", Caster, routing.Targets, target =>
            {
                target.TakeDamage(BuildContext(LastRawDamage, new List<int>(tags), target));
                MarkEffectApplied();
                if (target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster);
            }, stopOnControl: true, presentation: (targets, impact) => NordFx.SurtrUltimate(Caster, targets, LastHealthPaid > 0, impact));
        }
    }
}
