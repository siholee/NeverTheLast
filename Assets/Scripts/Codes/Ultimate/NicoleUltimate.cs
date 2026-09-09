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
    /// </summary>
    public sealed class NicoleOvercharge : SimpleUltimate
    {
        public NicoleOvercharge(UltimateCodeContext context)
            : base(context, "오버차지", 4f, 0.35f) { }

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
