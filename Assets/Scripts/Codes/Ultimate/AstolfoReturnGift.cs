using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class AstolfoReturnGift : BaseUltimateCode
    {
        public const float AdditionalCritShare = .30f;
        public const float CounterExtraShare = .30f;
        public const float HealAttackRatio = 6f;
        public const float AutoHealThreshold = .90f;
        public const string BuffKey = "astolfo_counter_cheer";
        public AstolfoReturnGift(UltimateCodeContext context) : base(context) => CodeName = "되돌려줄 시간이야!";
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField &&
            AliveAllies().Any(ally => ally.HpCurr <= ally.HpMax * AutoHealThreshold);
        protected override bool RequiresCompletionEvidence => true;
        protected override IEnumerator Perform()
        {
            if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
            float crit = Mathf.Max(0, Caster.StatValue(BaseEnums.StatType.CritDamage));
            float bonus = crit * AdditionalCritShare, counterBonus = crit * CounterExtraShare;
            foreach (Unit ally in AliveAllies().Where(unit => unit.isActive))
            {
                var status = BuffStatus.Create(71622, BuffKey, "유쾌한 반격", Caster, ally,
                    new CounterCritEffect(bonus, counterBonus), duration: 2, isBeneficial: true,
                    description: $"추가행동 치명타 피해 +{bonus * 100f:0.#}%p, 반격이면 추가 +{counterBonus * 100f:0.#}%p. 자신의 일반행동 2회 완료까지.");
                status.NormalActionDuration = true;
                if (ally.TryAddStatus(status)) MarkEffectApplied();
                Effects.Vfx.VfxComposer.PlayAffectedSlot("astolfo.ultimate", 0, Caster, ally);
            }
            bool resolved = false;
            void Resolve()
            {
                if (resolved) return;
                resolved = true;
                if (!DirectAttack.CasterCanContinue(Caster, true)) return;
                int healing = StatDamage(0, BaseEnums.StatType.Attack, HealAttackRatio);
                foreach (Unit ally in AliveAllies())
                {
                    if (!DirectAttack.CasterCanContinue(Caster, true)) break;
                    ally.Heal(healing, Caster);
                    MarkEffectApplied();
                }
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            }
            yield return Effects.Vfx.LokapalaFx.Area("astolfo.ultimate", Caster, AliveAllies(), Resolve);
            Resolve();
        }
        public sealed class CounterCritEffect : BaseEffect
        {
            private readonly float _counterBonus;
            public CounterCritEffect(float bonus, float counterBonus) : base(0, bonus) => _counterBonus = counterBonus;
            public override bool IsBeneficial => true;
            public override float TaggedCritDamageBonus(Unit attacker, IReadOnlyList<int> tags)
                => attacker == Target && tags != null &&
                    (tags.Contains(DamageTag.AdditionalAttack) || tags.Contains(DamageTag.CounterAttack))
                    ? Coefficient + (tags.Contains(DamageTag.CounterAttack) ? _counterBonus : 0f) : 0;
        }
    }
}
