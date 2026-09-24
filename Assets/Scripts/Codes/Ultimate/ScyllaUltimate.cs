using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 스킬라 U(1920) — 혼돈의 소용돌이. 적 전체에게 200 + INT×3을 퍼붓고 물을 부착한다.
    ///
    /// <b>여왕의 진노</b>(1브레이크)가 열려 있으면 전기까지 함께 얹는다. 공허의 파도가 이미
    /// 판을 적셔 두었으므로, 진노 이후의 소용돌이는 곧 필드 전체 감전이다.
    /// </summary>
    public sealed class ScyllaChaosMaelstrom : VoidElementalUltimate
    {
        private const int BasePower = 200;
        private const float IntCoefficient = 3f;

        public ScyllaChaosMaelstrom(UltimateCodeContext context)
            : base(context, "혼돈의 소용돌이", 0.6f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            bool wrath = ScyllaCombat.HasQueensWrath(Caster);
            int power = BasePower + Mathf.RoundToInt(Caster.GetBaseInt() * IntCoefficient);

            foreach (Unit enemy in Enemies())
            {
                if (enemy == null || !enemy.isActive || enemy.IsUntargetable) continue;

                int damage = RollDamage(power, BaseEnums.PrimaryStat.INT, out bool isCrit);
                enemy.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));

                // 물이 먼저, 전기가 나중이다. 순서가 뒤집히면 감전이 아니라 부착만 남는다.
                AttachOwnElement(enemy);
                if (wrath && enemy.isActive)
                {
                    enemy.GrantCombatElement(
                        BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
