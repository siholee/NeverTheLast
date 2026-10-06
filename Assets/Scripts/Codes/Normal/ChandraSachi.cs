using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;

namespace Codes.Normal
{
    /// <summary>찬드라 — 일반행동 「샤친」. 최대 체력 50%의 특수·비접촉 단일 피해.</summary>
    public sealed class ChandraSachi : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Spell;

        private const float MaxHpRatio = 0.50f;

        public ChandraSachi(NormalCodeContext context) : base(context)
        {
            CodeName = "샤친";
            CastingDelay = 0.12f;
        }

        protected override int BaseDamage(Unit target)
            => StatDamage(0f, BaseEnums.StatType.MaxHp, MaxHpRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return ChandraFx.Sachi(Caster, target, () => Hit(target, context), context.IsCrit ? 1.15f : 1f);
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget,
            DamageTag.NormalAttack,
            DamageTag.NonContactAttack,
            DamageTag.SpecialDamage,
            DamageTag.Spell,
        };
    }
}
