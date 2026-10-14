using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>인드라 — 일반행동 「뇌정」. 우선도 단일 적에게 공격력 110%, 번개 부착(Detail_08 §13.2).</summary>
    public sealed class IndraThunderbolt : BaseNormalCode
    {
        private const float AttackRatio = 1.10f;

        public override int AttackForm => DamageTag.Spell;

        public IndraThunderbolt(NormalCodeContext context) : base(context) => CodeName = "뇌정";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal("indra.thunderbolt", Caster, target,
                () => Hit(target, context));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive)
                target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster);
        }
    }
}
