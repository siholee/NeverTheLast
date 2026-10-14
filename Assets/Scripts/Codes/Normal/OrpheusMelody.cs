using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    public sealed class OrpheusMelody : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Spell;
        public OrpheusMelody(NormalCodeContext context) : base(context) => CodeName = "애도의 선율";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, .8f);
        protected override IEnumerator Deliver(Unit target)
        {
            var context = BuildContext(target);
            context.ToughnessReduction = 15f;
            yield return Combat.DirectAttack.HitOne("orpheus.melody", Caster, target, () => Hit(target, context));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive && context.ResolvedDamage > 0)
                target.GrantCombatElement(BaseEnums.UnitElement.Anemo, source: Caster);
        }
    }
}
