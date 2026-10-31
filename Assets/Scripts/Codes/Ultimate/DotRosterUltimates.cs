using System;
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
    public abstract class DotRosterAreaUltimate : BaseUltimateCode
    {
        private readonly string _key;
        private readonly float _ratio;
        protected DotRosterAreaUltimate(UltimateCodeContext context, string name, string key, float ratio) : base(context)
        { CodeName = name; _key = key; _ratio = ratio; }
        public override int AttackForm => DamageTag.Spell;
        protected override bool RequiresCompletionEvidence => true;
        protected abstract void AfterHit(Unit target);
        protected virtual void AfterArea() { }
        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, AttackForm };
            var routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !DirectAttack.CasterCanContinue(Caster, true) || !Targeting.Announce(Caster, routing.Targets)) yield break;
            yield return Effects.Vfx.LokapalaFx.Area(_key, Caster, routing.Targets, () =>
            {
                foreach (Unit target in routing.Targets.ToList())
                {
                    if (!DirectAttack.CasterCanContinue(Caster, true)) break;
                    if (target == null || !target.IsOnField) continue;
                    target.TakeDamage(BuildContext(StatDamage(0, BaseEnums.StatType.Attack, _ratio), new List<int>(tags), target));
                    MarkEffectApplied();
                    if (target.IsOnField) AfterHit(target);
                }
                if (DirectAttack.CasterCanContinue(Caster, true)) AfterArea();
            });
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
        }
    }
    public sealed class NephthysClosedGarden : DotRosterAreaUltimate
    {
        public NephthysClosedGarden(UltimateCodeContext context) : base(context, "닫히는 정원", "nephthys.ultimate", .80f) { }
        protected override void AfterHit(Unit target)
        { foreach (var passive in Caster.ActivePassiveCodes.OfType<NephthysPoisonGarden>()) passive.ApplyPoisonAndEye(target); }
    }
    public sealed class AtalanteHuntingSeason : DotRosterAreaUltimate
    {
        public AtalanteHuntingSeason(UltimateCodeContext context) : base(context, "사냥의 시간", "atalante.ultimate", 1.50f) { }
        public override int AttackForm => DamageTag.Shot;
        protected override void AfterHit(Unit target) => ActionDot.Apply(Caster, target, ActionDotKind.Poison);
        protected override void AfterArea()
        { foreach (var passive in Caster.ActivePassiveCodes.OfType<AtalantePoisonHunt>()) passive.RefreshPursuits(); }
    }
    public sealed class NeithThunderWeave : BaseUltimateCode
    {
        public const float ShareRatio = .45f;
        private readonly List<Unit> _buffed = new();
        private int _remaining;
        private Action<EventContext> _normal, _end;
        private string Key => "neith_thunder_weave_" + Caster.GetEntityId();
        public NeithThunderWeave(UltimateCodeContext context) : base(context) => CodeName = "천둥의 직조";
        protected override bool RequiresCompletionEvidence => true;
        protected override IEnumerator Perform()
        {
            if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
            Effects.Vfx.PurchasedArcherFx.WeaveCast(Caster);
            ClearBuff();
            // 이전 직조를 제거한 실제 스탯을 읽어 백분율 특공 버프가 있어도 자기 증폭하지 않는다.
            float amount = Mathf.Max(0, Caster.StatValue(BaseEnums.StatType.SpecialAttack)) * ShareRatio;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster).Where(u => u.Element == "Electro"))
            {
                ally.AddStatus(BuffStatus.Create(71661, Key, "천둥의 직조", Caster, ally,
                    new StatModifierEffect(BaseEnums.StatType.SpecialAttack, amount), isBeneficial: true,
                    description: $"특공 +{amount:0.#}. 네이트 일반행동2회 완료까지."));
                _buffed.Add(ally);
                Effects.Vfx.PurchasedArcherFx.WeaveReceive(ally,Caster);
                MarkEffectApplied(); // AddStatus의 실제 강화 표시가 수신자를 알린다.
            }
            _remaining = 2;
            _normal = _ => { if (--_remaining <= 0) ClearBuff(); };
            _end = _ => ClearBuff();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _end);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _end);
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            yield return new WaitForSeconds(.25f);
        }
        protected override void OnStoppedExternally() => ClearBuff();
        private void ClearBuff()
        {
            if (_normal != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            if (_end != null)
            {
                Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _end);
                Caster?.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _end);
            }
            foreach (Unit ally in _buffed) if (ally != null) ally.RemoveStatusByKey(Key);
            _buffed.Clear(); _normal = _end = null;
        }
    }
    public sealed class EreshkigalReturnHome : BaseUltimateCode
    {
        public const float PartyHealRatio = .18f, SelfHealRatio = .30f;
        public EreshkigalReturnHome(UltimateCodeContext context) : base(context) => CodeName = "돌아올 자리는 남아 있다";
        protected override bool RequiresCompletionEvidence => true;
        protected override IEnumerator Perform()
        {
            if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * PartyHealRatio)), Caster);
                MarkEffectApplied(); // Heal의 실제 회복 표시를 중복 재생하지 않는다.
            }
            Caster.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * SelfHealRatio)), Caster);
            yield return Effects.Vfx.LokapalaFx.Area("ereshkigal.ultimate", Caster, AliveEnemies(), () =>
            {
                if (!DirectAttack.CasterCanContinue(Caster, true)) return;
                foreach (Unit enemy in AliveEnemies()) ActionDot.Apply(Caster, enemy, ActionDotKind.Bleed);
            });
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
        }
    }
}
