using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 드레이크 N(11) — 단일 타격 뒤 추가행동 '협상'을 곧바로 예약한다.
    ///
    /// 협상은 <b>이 일반행동이 때린 대상</b>을 따라간다. 그 대상이 사라졌거나 더는 우선도가
    /// 가장 높지 않으면 그때 다시 고른다 — 상대가 바뀌면 협상도 상대를 바꾼다.
    /// </summary>
    public sealed class DrakeNegotiation : BaseNormalCode
    {
        private const int NegotiationPower = 50;
        private const float NegotiationDexCoefficient = 0.5f;

        public DrakeNegotiation(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 50;
            PowerStat = BaseEnums.PrimaryStat.STR;
            PowerStatCoefficient = 0.5f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash };
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            base.OnAttackResolved(target, context);
            if (Caster == null || !Caster.isActive) return;

            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "drake_negotiation", "가장 합리적인 협상", () => ResolveNegotiation(target));
        }

        /// <summary>협상 — 일반공격으로 친다. 같은 대상을 물고 늘어지는 것이 본질이다.</summary>
        private void ResolveNegotiation(Unit preferred)
        {
            if (Caster == null || !Caster.isActive) return;

            Unit target = preferred != null && preferred.isActive && !preferred.IsUntargetable
                ? preferred
                : CombatTargets.PickByPriority(CombatTargets.AliveEnemies(Caster));
            if (target == null) return;

            int power = NegotiationPower +
                Mathf.RoundToInt(Caster.GetBaseDex() * NegotiationDexCoefficient);
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.DEX) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));

            // 명세상 '일반공격으로 간주'한다 — NormalAttack 태그를 함께 달아
            // '일반행동마다' 계열 패시브가 이 타격도 세도록 한다.
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.NonContactAttack,
                },
                isCrit));
        }
    }

    /// <summary>히폴리테 N(27) — 단일 적에게 50 + STR×0.8. 찌르기다.</summary>
    public sealed class HippolyteThrust : BaseNormalCode
    {
        public HippolyteThrust(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 50;
            PowerStat = BaseEnums.PrimaryStat.STR;
            PowerStatCoefficient = 0.8f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce };
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce,
        };
    }

    /// <summary>
    /// 히미코 N(44) — 아군 전체의 신탁 중첩을 모두 거둬 적 전체에게 한 번에 쏟는다.
    /// 중첩이 하나도 없으면 피해도 없다. 거두는 것이 먼저고, 터뜨리는 것은 그 결과다.
    /// </summary>
    public sealed class HimikoOracleBurst : BaseNormalCode
    {
        public HimikoOracleBurst(NormalCodeContext context) : base(context)
        {
            CodeName = "신탁 개방";
            Power = 0;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            int stacks = 0;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                int owned = Mathf.Max(0, ally.GetCombatResource(HimikoCombat.OracleResource));
                if (owned <= 0) continue;
                ally.TryConsumeCombatResource(HimikoCombat.OracleResource, owned);
                stacks += owned;
            }

            if (stacks > 0)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.GetBaseInt() * HimikoCombat.BurstIntCoefficient * stacks));
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                if (isCrit) damage = Mathf.RoundToInt(damage * Caster.CritMultiplierCurr);

                foreach (Unit enemy in CombatTargets.AliveEnemies(Caster)
                             .Where(unit => unit != null && !unit.IsUntargetable).ToList())
                {
                    enemy.TakeDamage(new DamageContext(
                        Caster, damage, BaseEnums.CodeType.Normal,
                        new List<int>
                        {
                            DamageTag.AllTarget, DamageTag.NormalAttack,
                            DamageTag.Special, DamageTag.NonContactAttack,
                        },
                        isCrit));
                }
            }

            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>엘리자베스 N(12) — 피해 없이 아군 전체에게 200 + STR×0.8 방어막을 두른다.</summary>
    public sealed class ElizabethAegis : BaseNormalCode
    {
        public ElizabethAegis(NormalCodeContext context) : base(context)
        {
            CodeName = "장미의 방벽";
            Power = 0;
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            int shield = Mathf.Max(1, ElizabethCombat.ShieldFlat +
                Mathf.RoundToInt(Caster.GetBaseStr() * ElizabethCombat.ShieldStrCoefficient));
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddShield(shield, Caster);
            }

            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }
}
