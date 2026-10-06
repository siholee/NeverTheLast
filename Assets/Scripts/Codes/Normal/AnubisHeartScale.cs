using System.Collections;
using Effects.Vfx;
using BaseClasses;
using Codes.Base;
using Codes.Ultimate;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 아누비스 — 일반행동 「심장의 저울」(Detail_08 §9.2). 우선도 단일 적에게 최대 체력 6% 물리 접촉 피해,
    /// 준 피해의 30%만큼 회복. 사자의 서 동안은 10% · 60% · 적중 대상 바위 부착(§9.3).
    /// 공격력 대신 최대 체력 계수라 영혼으로 늘어난 체력이 그대로 화력이 된다.
    /// </summary>
    public sealed class AnubisHeartScale : BaseNormalCode
    {
        private const float HpRatio = 0.06f;
        private const float HealRatio = 0.30f;
        private const float EmpoweredHpRatio = 0.10f;
        private const float EmpoweredHealRatio = 0.60f;

        public override int AttackForm => DamageTag.Pierce;
        protected override bool IsContact => true;

        public AnubisHeartScale(NormalCodeContext context) : base(context) => CodeName = "심장의 저울";

        private bool Empowered => Caster != null && Caster.HasStatusKey(AnubisBookOfTheDead.StatusKey);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return AnubisFx.HeartScale(Caster, target, Empowered, () => Hit(target, context));
        }

        protected override int BaseDamage(Unit target)
            => StatDamage(0f, BaseEnums.StatType.MaxHp, Empowered ? EmpoweredHpRatio : HpRatio);

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (Caster == null) return;
            bool empowered = Empowered;
            if (context != null && context.ResolvedDamage > 0)
                Caster.Heal(Mathf.RoundToInt(context.ResolvedDamage * (empowered ? EmpoweredHealRatio : HealRatio)), Caster);
            if (empowered && target != null && target.isActive)
                target.GrantCombatElement(BaseEnums.UnitElement.Geo, source: Caster);
        }
    }
}
