using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 니콜 N — 단일 적에게 고정 위력 75 + INT×0.8의 특수 피해를 주고 30% 확률로 번개를 부착한다.
    ///
    /// 확률은 스탯을 타지 않는 고정값이다. 니콜의 전기 공급은 고유 패시브가 아군 쪽을,
    /// 이 일반행동이 적 쪽을 맡는 구조라 적 부착만 확률로 눌러 둔다.
    /// </summary>
    public sealed class NicoleArcShot : BaseNormalCode
    {
        private const float ElectroChance = 0.30f;
        private const int NormalFlatPower = 75;
        private const float NormalIntCoefficient = 0.8f;

        public NicoleArcShot(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = NormalFlatPower;
            PowerStatCoefficient = NormalIntCoefficient;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack, DamageTag.Arrow,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            if (Random.value > ElectroChance) return;

            target.GrantCombatElement(
                BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
