using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;

namespace Codes.Passive
{
    public static class VoidDeerCodeIds
    {
        public const int ElementalResonance = 1540;
    }

    public static class VoidDeerStatusIds
    {
        public const int ElementalResonance = 7980;
    }

    /// <summary>
    /// 공허의 사슴 P 원소 감응 — 공격 대상이 자신과 같은 원소를 두르고 있으면 가하는 피해 +40%.
    ///
    /// 사슴의 일반행동이 스스로 자기 원소를 부착하므로, <b>한 번 때린 대상을 다시 때릴 때</b>
    /// 조건이 성립한다. 원소를 지우거나 덮어씌우는 쪽이 정석 대응이 된다.
    /// 기본형(공허)은 부착할 원소가 없어 이 패시브가 놀지만, 대신 원소 반응도 맞지 않는다.
    /// </summary>
    public sealed class VoidDeerResonance : PersistentStatusPassive
    {
        private const float Bonus = 0.40f;

        public VoidDeerResonance(PassiveCodeContext context)
            : base(context, VoidDeerStatusIds.ElementalResonance, "void_deer_resonance", "원소 감응",
                $"공격 대상이 자신과 같은 원소를 보유하고 있으면 가하는 피해가 {Bonus * 100f:F0}% 증가합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new SelfElementMasteryEffect(Bonus);
    }
}
