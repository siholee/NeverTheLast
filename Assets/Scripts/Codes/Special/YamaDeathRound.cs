using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Special
{
    /// <summary>
    /// 야마 — 특수행동 「죽음의 순회」(Detail_08 §15.4). 로카팔라 묶음 참여자(순서 +75, 인드라 뒤 · 찬드라 앞)로
    /// 적 전체에 공격력 400%. <c>SpecialAttack</c>만 가지며 궁극기 판정(<c>UltAttack</c>)은 없다.
    /// 번개 부착도 지속피해 즉시 발동도 없다.
    /// </summary>
    public sealed class YamaDeathRound : BaseSpecialCode
    {
        private const float AttackRatio = 4.00f;

        public override int AttackForm => DamageTag.Spell;
        public override int OpenOrder => 75;

        public YamaDeathRound(SpecialCodeContext context) : base(context) => CodeName = "죽음의 순회";

        protected override IEnumerator Perform()
        {
            yield return HitAllEnemies(_ => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>
            {
                DamageTag.AllTarget, DamageTag.SpecialAttack, DamageTag.NonContactAttack, DamageTag.Spell,
            }, presentation: (targets, impact) => Effects.Vfx.LokapalaFx.Area("yama.special", Caster, targets, impact));
        }
    }
}
