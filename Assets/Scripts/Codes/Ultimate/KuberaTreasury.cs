using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 쿠베라 — 초월기 「보고 개방」(Detail_08 §11.4). 보물 8을 모두 소모해
    ///   1) 적 전체에 공격력 160%
    ///   2) 2턴 동안 아군 소환수 피해 +15%(후원과 합산, 모든 소환 방식)
    ///   3) 다수형(Swarm) 소환자마다 소환수 1기 외부 생성 — 동반형에게는 대체 효과 없이 2의 피해 증가만 남는다.
    /// </summary>
    public sealed class KuberaTreasury : BaseUltimateCode
    {
        private const float AttackRatio = 1.60f;
        private const int BuffStatusId = 70651;
        private const string BuffKey = "kubera_treasury";
        private const float SummonDamageShare = 0.15f;
        private const int BuffTurns = 2;

        public override int AttackForm => DamageTag.Spell;

        public KuberaTreasury(UltimateCodeContext context) : base(context) => CodeName = "보고 개방";

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            Combat.AttackTargetResolution routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (routing.CanContinue && Combat.Targeting.Announce(Caster, targets))
            {
                yield return Effects.Vfx.LokapalaFx.Area("kubera.treasury", Caster, targets, () =>
                {
                    foreach (Unit target in targets.ToList())
                    {
                        if (target == null || !target.isActive) continue;
                        target.TakeDamage(BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>(tags), target));
                    }
                    ApplySupport();
                });
            }
            else ApplySupport();

            yield return new WaitForSeconds(0.15f);

            void ApplySupport()
            {
                // 원래 피해→각 아군 상태→외부 소환 순서를 같은 정점 콜백 안에서 그대로 실행한다.
                foreach (Unit ally in AliveAllies())
                {
                    ally.AddStatus(BuffStatus.Create(BuffStatusId, BuffKey, CodeName, Caster, ally,
                        new ConditionalSummonDamageEffect(SummonDamageShare), duration: BuffTurns,
                        stackPolicy: BaseEnums.StatusStackPolicy.Replace, isBeneficial: true,
                        description: "2턴 동안 소환수 피해 +15%."));
                    Summons.RequestExtra(ally, KuberaPatronage.TreasuryTag);
                }
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            }
        }

        /// <summary>같은 쿠베라의 후원과 몫을 더한다(<see cref="KuberaPatronage.FamilyMultiplier"/>). 후원이 꺼져도 남은 턴 동안 +15%는 그대로다.</summary>
        private sealed class ConditionalSummonDamageEffect : BaseEffect, KuberaPatronage.ISummonDamageShare
        {
            public ConditionalSummonDamageEffect(float share) : base(0, share * 100f) => SummonDamageShare = share;
            public override bool IsBeneficial => true;
            public float SummonDamageShare { get; }

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => KuberaPatronage.FamilyMultiplier(this, attacker, context);
        }
    }
}
