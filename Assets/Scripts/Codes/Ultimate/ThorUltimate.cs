using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 토르 U — 천둥의 신.
    ///
    /// 적 전체에게 CON 위력 150의 피해를 주고 치유량 감소를 3턴 건다.
    /// <b>CON을 타는 것이 이 궁극기의 전부다.</b> 뇌신의 박자가 대체행동마다 CON을 3씩 올리므로
    /// 전투가 길어질수록 이 한 방이 커진다. 체력이 두꺼워지는 것과 화력이 오르는 것이 같은 축이다.
    ///
    /// 그래서 토르의 CON 기초값은 보스치고 낮게 잡혀 있다. 두께는 `알파 개체`(1541)와
    /// `완전함`(1546)의 최대 체력 배수가 만들고, 여기서는 순수한 CON만 읽는다.
    /// </summary>
    public sealed class ThorGodOfThunder : SimpleUltimate
    {
        private const int ThunderPower = 150;
        private const int HealCutTurns = 3;
        private const float HealCutRatio = 0.25f;

        public ThorGodOfThunder(UltimateCodeContext context)
            : base(context, "천둥의 신", 0.7f) { Power = ThunderPower; }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(ThunderPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target.isActive)
                {
                    HealingReductionStatus.Apply(target, Caster, HealCutTurns, CodeName, HealCutRatio);
                }
            }
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Enemies().Count > 0;
    }
}
