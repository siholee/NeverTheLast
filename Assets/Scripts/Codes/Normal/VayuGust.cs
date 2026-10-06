using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 바유 — 일반행동 「돌풍」(Detail_08 §14.3). 우선도 단일 적에게 공격력 90% 피해 뒤 바람을 붙인다. 방깍은 없다.
    /// 다른 원소가 붙어 있던 적이면 이 바람이 확산을 일으키고, 그 확산이 특성의 원소 취약을 남긴다.
    /// </summary>
    public sealed class VayuGust : BaseNormalCode
    {
        public const float AttackRatio = 0.90f;
        private const string FxKey = "vayu.wind_palm";

        public override int AttackForm => DamageTag.Spell;

        public VayuGust(NormalCodeContext context) : base(context) => CodeName = "돌풍";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal(FxKey, Caster, target, () => Hit(target, context));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Anemo, source: Caster);
        }
    }
}
