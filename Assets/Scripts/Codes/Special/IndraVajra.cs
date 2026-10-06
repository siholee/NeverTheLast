using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Special
{
    /// <summary>
    /// 인드라 — 특수행동 「바즈라」(Detail_08 §13.3). 적 전체에 공격력 150%.
    /// <c>SpecialAttack</c>과 <c>UltAttack</c>을 함께 가져 데바의 왕(+25%)과 궁극기 피해 증가 효과를 모두 받는다.
    /// 아군이 깔아 둔 디버프 · 버프를 다 받은 뒤 치도록 묶음 순서는 +50이다.
    /// </summary>
    public sealed class IndraVajra : BaseSpecialCode
    {
        private const float AttackRatio = 1.50f;

        public override int AttackForm => DamageTag.Spell;
        public override int OpenOrder => 50;

        public IndraVajra(SpecialCodeContext context) : base(context) => CodeName = "바즈라";

        protected override IEnumerator Perform()
        {
            yield return HitAllEnemies(_ => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>
            {
                DamageTag.AllTarget, DamageTag.SpecialAttack, DamageTag.UltAttack,
                DamageTag.NonContactAttack, DamageTag.Spell,
            }, presentation: (targets, impact) => Effects.Vfx.LokapalaFx.Area("indra.vajra", Caster, targets, impact));
        }
    }
}
