using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>바루나 — 일반행동 「금줄」(Detail_08 §16.3). 우선도 단일 적에게 공격력 90%, 물 부착. 제방 보호막은 부착 시점에 특성이 준다.</summary>
    public sealed class VarunaSacredRope : BaseNormalCode
    {
        private const float AttackRatio = 0.90f;

        public override int AttackForm => DamageTag.Spell;

        public VarunaSacredRope(NormalCodeContext context) : base(context) => CodeName = "금줄";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal("varuna.normal", Caster, target,
                () => Hit(target, context));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive)
                target.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: Caster);
        }
    }
}
