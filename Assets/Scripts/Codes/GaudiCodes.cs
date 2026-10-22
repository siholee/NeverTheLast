using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>가우디 — 방호를 먼저 세우는 풀 부착 서브탱커.</summary>
    public sealed class GaudiSagradaFamilia : UniquePassiveCode
    {
        public const string Key = "gaudi_sagrada_familia";
        private bool _openingGranted;
        public override int BaseShieldAmount => Caster == null ? 0 : Mathf.RoundToInt(Caster.DefCurr * 1.8f);
        public GaudiSagradaFamilia(PassiveCodeContext context) : base(context) => CodeName = "사그리다 파밀리아";
        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(70040, Key, CodeName, Caster, Caster,
                new ReceivingDamageMultiplierEffect(.85f), isBeneficial: true,
                description: "받는 피해 -15%."));
            if (_openingGranted) return;
            _openingGranted = true;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, ResetOpening);
            ShieldCapability.GrantParty(Caster, BaseShieldAmount);
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, CodeName);
        }
        private void ResetOpening(EventContext _) { _openingGranted = false; Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, ResetOpening); }
        public override void StopCode() => Caster?.RemoveStatusByKey(Key);
    }
}

namespace Codes.Normal
{
    public sealed class GaudiNormalAttack : BaseNormalCode
    {
        public GaudiNormalAttack(NormalCodeContext context) : base(context) => CodeName = "녹음의 아치";
        public override int AttackForm => DamageTag.Spell;
        protected override bool AppliesElementTo(Unit target) => true;
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.SpecialAttack, .8f);
        protected override List<int> GetDamageTags()
        { var tags = base.GetDamageTags(); tags.Add(DamageTag.SpecialDamage); return tags; }
        protected override IEnumerator Deliver(Unit target)
        {
            var damage = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal("gaudi.normal", Caster, target, () => Hit(target, damage));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: Caster);
        }
    }
}

namespace Codes.Ultimate
{
    public sealed class GaudiImmortalLegacy : BaseUltimateCode
    {
        public GaudiImmortalLegacy(UltimateCodeContext context) : base(context) => CodeName = "불멸의 유산";
        public override int AttackForm => DamageTag.Spell;
        public override int BaseShieldAmount => Caster == null ? 0 : Mathf.RoundToInt(Caster.DefCurr * 4f);
        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell, DamageTag.SpecialDamage };
            var routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            ShieldCapability.GrantParty(Caster, BaseShieldAmount);
            foreach (Unit ally in AliveAllies()) Effects.Vfx.VfxComposer.PlayAffectedSlot("gaudi.ultimate", 0, Caster, ally);
            yield return Effects.Vfx.LokapalaFx.Area("gaudi.ultimate", Caster, routing.Targets, () =>
            {
                foreach (Unit target in routing.Targets)
                {
                    if (target == null || !target.isActive) continue;
                    bool crit = RollCritical(out float multiplier);
                    target.TakeDamage(new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(StatDamage(0, BaseEnums.StatType.SpecialAttack, .8f) * multiplier)), BaseEnums.CodeType.Ultimate, new List<int>(tags), crit));
                    if (target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: Caster);
                }
            });
        }
    }
}
