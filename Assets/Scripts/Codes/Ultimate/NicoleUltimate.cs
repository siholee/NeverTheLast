using System.Collections.Generic;
using BaseClasses;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 니콜 U — 오버차지.
    /// 아군 전체에게 3턴간 니콜의 LUK만큼 치명타 피해(%)를 준다.
    /// 번개를 두른 아군은 추가로 +20%p를 받는다.
    /// 이어서 적 전체에게 고정 위력 50 + INT×0.8의 특수 피해를 주고 2턴간 방어력 20% 감소를 건다.
    ///
    /// 뒤의 피해·디버프는 버퍼 궁극기가 전투에 직접 손을 대게 하려는 몫이다. 방어력 감소는
    /// 디버프이므로 사바흐의 아즈라엘을 적 수만큼 한꺼번에 채운다.
    /// </summary>
    public sealed class NicoleOvercharge : SimpleUltimate
    {
        private const int BurstFlatPower = 50;
        private const float BurstIntCoefficient = 0.8f;
        private const float ArmorMultiplier = 0.8f;
        private const int ArmorBreakTurns = 2;

        public NicoleOvercharge(UltimateCodeContext context)
            : base(context, "오버차지", 0.35f)
        {
            Power = BurstFlatPower;
            PowerStatCoefficient = BurstIntCoefficient;
            PowerStat = BaseEnums.PrimaryStat.INT;
        }

        protected override void Resolve()
        {
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NicoleStatusIds.Overcharge, $"nicole_overcharge_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new NicoleOverchargeEffect(),
                    duration: 3,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "니콜의 LUK%만큼 치명타 피해 증가 (번개 보유 시 +20%p, 3턴)."));
            }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * crit));
            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };

            foreach (Unit enemy in Enemies())
            {
                enemy.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (enemy == null || !enemy.isActive || enemy.HpCurr <= 0) continue;
                enemy.AddStatus(BuffStatus.Create(
                    NicoleStatusIds.ArmorBreak, $"nicole_armor_break_{Caster.GetEntityId()}", "과전류",
                    Caster, enemy, new ArmorShredEffect(ArmorMultiplier),
                    duration: ArmorBreakTurns,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    description: $"{ArmorBreakTurns}턴 동안 방어력이 20% 감소합니다."));
            }
        }
    }

    /// <summary>
    /// 치명타 피해 증가분을 걸 때 굳히지 않고 매번 다시 잰다.
    /// 지속 중에 번개를 두르게 되면 그 시점부터 추가분이 붙어야 고유 패시브와 맞물린다.
    /// </summary>
    internal sealed class NicoleOverchargeEffect : BaseEffect
    {
        /// <summary>번개를 두른 대상에게 얹는 추가 치명타 피해(%p).</summary>
        private const int ElectroBonus = 20;

        public NicoleOverchargeEffect() : base(0) { }

        public override float CritMultiplierAdditiveModifier(Unit unit)
        {
            if (unit != Target || Caster == null || !Caster.isActive) return 0f;

            int amount = Mathf.Max(0, Caster.GetBaseLuk());
            if (Target.HasCombatElement(BaseEnums.UnitElement.Electro)) amount += ElectroBonus;
            return amount * 0.01f;
        }
    }
}
