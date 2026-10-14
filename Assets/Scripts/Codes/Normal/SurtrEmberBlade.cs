using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;

namespace Codes.Normal
{
    public sealed class SurtrEmberBlade : BaseNormalCode
    {
        public const float AttackRatio = .60f;
        public override int AttackForm => DamageTag.Slash;
        protected override bool IsContact => true;
        public SurtrEmberBlade(NormalCodeContext context) : base(context) => CodeName = "불씨를 가르는 검";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, AttackRatio);
        protected override List<int> GetDamageTags() => new() { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack, DamageTag.Slash };
        protected override IEnumerator Deliver(Unit target)
        {
            var context = BuildContext(target);
            yield return NordFx.SurtrStrike(Caster, target, () => Hit(target, context));
        }
    }
}
