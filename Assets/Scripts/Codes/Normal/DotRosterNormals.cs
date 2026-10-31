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
    public abstract class DotRosterNormal : BaseNormalCode
    {
        protected readonly string VfxKey;
        protected DotRosterNormal(NormalCodeContext context, string key, string name) : base(context) { VfxKey = key; CodeName = name; }
        public override int AttackForm => DamageTag.Spell;
        protected override IEnumerator Deliver(Unit target)
        {
            var damage = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal(VfxKey, Caster, target, () => Hit(target, damage));
        }
    }
    public sealed class NephthysPoisonSeed : DotRosterNormal
    {
        public NephthysPoisonSeed(NormalCodeContext context) : base(context, "nephthys.normal", "독의 씨앗") { }
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, .80f);
        protected override void OnAttackResolved(Unit target, DamageContext damage)
        { foreach (var passive in Caster.ActivePassiveCodes.OfType<NephthysPoisonGarden>()) passive.ApplyPoisonAndEye(target); }
    }
    public sealed class AtalanteHuntingArrow : DotRosterNormal
    {
        public const float PursuitRatio = 1.10f;
        public AtalanteHuntingArrow(NormalCodeContext context) : base(context, "atalante.normal", "추적의 화살") { }
        public override int AttackForm => DamageTag.Shot;
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, 1.10f);
        protected override void OnAttackResolved(Unit target, DamageContext damage)
        { if (!damage.HasTag(DamageTag.AdditionalAttack)) ActionDot.Apply(Caster, target, ActionDotKind.Poison); }
        public void CastPursuit(Unit target)
        { Caster.isCasting = true; CurrSkillCoroutine = Caster.StartCoroutine(Pursue(target)); }
        private IEnumerator Pursue(Unit target)
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.AdditionalAttack, DamageTag.NonContactAttack, DamageTag.Shot };
            var routing = AttackTargetRouter.Prepare(Caster, new[] { target }, BaseEnums.CodeType.Normal, tags, "상처를 쫓는 화살");
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) { StopCode(); yield break; }
            target = routing.Targets[0];
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            bool critical = RollCritical(target, tags, out float multiplier);
            var damage = new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(StatDamage(0, BaseEnums.StatType.Attack, PursuitRatio) * multiplier)), BaseEnums.CodeType.Normal, tags, critical);
            yield return Effects.Vfx.LokapalaFx.Normal("atalante.normal", Caster, target, () => Hit(target, damage));
            // 실제 추가행동이다. 일반행동 완료·마나·추격 횟수 초기화는 발행하지 않는다.
            StopCode();
        }
    }
    public sealed class NeithThunderArrow : DotRosterNormal
    {
        protected override bool AppliesElementTo(Unit target) => true;
        public NeithThunderArrow(NormalCodeContext context) : base(context, "neith.normal", "뇌사의 화살") { }
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.SpecialAttack, .90f);
        protected override void OnAttackResolved(Unit target, DamageContext damage)
        { if (target != null && target.IsOnField) target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster); }
    }
    public sealed class EreshkigalUnendingWound : DotRosterNormal
    {
        public EreshkigalUnendingWound(NormalCodeContext context) : base(context, "ereshkigal.normal", "닫히지 않는 상처") { }
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.MaxHp, .06f);
        protected override void OnAttackResolved(Unit target, DamageContext damage) => ActionDot.Apply(Caster, target, ActionDotKind.Bleed);
    }
}
