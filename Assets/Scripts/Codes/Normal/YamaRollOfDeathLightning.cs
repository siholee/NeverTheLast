using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 야마 — 일반행동 「명부의 낙뢰」(Detail_08 §15.4). 우선도 단일 적에게 공격력 130%, 번개 부착,
    /// 그 대상의 모든 지속피해를 q 비율로 즉시 발동한다. 발동은 부착 <b>뒤</b>라 이번 부착이 만든 감전도 참여한다.
    /// </summary>
    public sealed class YamaRollOfDeathLightning : BaseNormalCode
    {
        private const float AttackRatio = 1.30f;

        public override int AttackForm => DamageTag.Spell;

        public YamaRollOfDeathLightning(NormalCodeContext context) : base(context) => CodeName = "명부의 낙뢰";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal("yama.normal", Caster, target,
                () => Hit(target, context));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster);
            DamageOverTimeDetonation.Detonate(target, YamaDeathVerdict.NormalRatio(Caster), Caster);
        }
    }
}
