using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Vfx;
using Entities;

namespace Codes.Normal
{
    public sealed class HimikoEmber : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Spell;

        public HimikoEmber(NormalCodeContext context) : base(context)
        {
            CodeName = "불씨";
            CastingDelay = 0.10f;
        }

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, 1f);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return EmberFx.Volley(Caster, target, () => Hit(target, context));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
            => Caster?.ActivePassiveCodes.OfType<HimikoBlaze>().FirstOrDefault()?.ApplyBlaze(target);
    }

    public sealed class PygmalionEmber : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Spell;

        public PygmalionEmber(NormalCodeContext context) : base(context)
        {
            CodeName = "불씨";
            CastingDelay = 0.10f;
        }

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, 1f);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return EmberFx.Slash(Caster, target, () => Hit(target, context));
        }
    }
}
