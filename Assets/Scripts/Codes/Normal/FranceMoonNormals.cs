using System.Collections;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;

namespace Codes.Normal
{
    public sealed class MarieIceShot : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Shot;
        public MarieIceShot(NormalCodeContext context) : base(context) => CodeName = "장교의 사격";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, .60f);
        protected override IEnumerator Deliver(Unit target)
        {
            var context = BuildContext(target);
            yield return DirectAttack.HitOne("marie.normal", Caster, target, () => Hit(target, context),
                presentation: impact => Effects.Vfx.VedicTrioFx.Single("marie.normal", Caster, target, impact));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        { if (!context.IsCancelled && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Cryo, source: Caster); }
    }
    public sealed class TsukuyomiMoonShot : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Spell;
        public TsukuyomiMoonShot(NormalCodeContext context) : base(context) => CodeName = "말없는 저주";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, .70f);
        protected override IEnumerator Deliver(Unit target)
        {
            var context = BuildContext(target);
            yield return DirectAttack.HitOne("tsukuyomi.normal", Caster, target, () => Hit(target, context),
                presentation: impact => Effects.Vfx.VedicTrioFx.Single("tsukuyomi.normal", Caster, target, impact));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        { if (!context.IsCancelled) ActionDot.Apply(Caster, target, ActionDotKind.Curse); }
    }
}
