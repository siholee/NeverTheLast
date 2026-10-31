using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    public sealed class EbisuLuckyCast : BaseNormalCode
    {
        protected override bool AppliesElementTo(Unit target) => true;
        public EbisuLuckyCast(NormalCodeContext context) : base(context) => CodeName = "행운을 낚는 한 수";
        public override int AttackForm => DamageTag.Spell;
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.MaxHp, .06f);
        protected override IEnumerator Deliver(Unit target)
        {
            var context = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal("ebisu.normal", Caster, target, () => Hit(target, context));
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster);
        }
    }
}
