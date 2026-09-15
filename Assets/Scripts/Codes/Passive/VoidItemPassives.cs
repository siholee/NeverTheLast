using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 공허 부품이 주는 장비 전용 패시브 셋.
    ///
    /// 셋 중 둘이 <b>공허 테마가 실제로 주는 압박</b>에 대응한다 — 씨앗의 자폭 한 방과
    /// 사방에서 날아드는 원소 부착이다. 나머지 하나만 공격 축이다.
    /// 세 코드 모두 장비를 벗으면 상시 상태까지 함께 걷힌다.
    /// </summary>
    public static class VoidItemCodeIds
    {
        public const int CrusherArm = 421;
        public const int ShedScale = 422;
        public const int EightfoldRing = 423;
    }

    public static class VoidItemStatusIds
    {
        public const int CrusherArm = 6421;
        public const int ShedScale = 6422;
        public const int EightfoldRing = 6423;
    }

    /// <summary>
    /// 크러셔의 팔 — 단일 대상 공격 피해 +15%.
    ///
    /// 대상 수로 잡은 것은 이미 있는 장비 코드와 축을 겹치지 않기 위해서다
    /// (페리페테스는 베기, 에게우스는 치명타 피해). 대검은 한 놈을 크게 치는 무기라
    /// 광역에 붙지 않는 편이 분류의 성격과도 맞는다.
    /// </summary>
    public sealed class CrusherArmItemPassive : PersistentStatusPassive
    {
        private const float Bonus = 0.15f;

        public CrusherArmItemPassive(PassiveCodeContext context)
            : base(context, VoidItemStatusIds.CrusherArm, "item_crusher_arm", "크러셔의 팔",
                "단일 대상 공격의 피해가 15% 증가합니다. 광역 공격에는 적용되지 않습니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new CrusherArmEffect(Bonus);
    }

    internal sealed class CrusherArmEffect : BaseEffect
    {
        private readonly float _bonus;

        public CrusherArmEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || context?.DamageTags == null) return 1f;
            return context.DamageTags.Contains(DamageTag.SingleTarget) ? 1f + _bonus : 1f;
        }
    }

    /// <summary>
    /// 탈피한 비늘 — 한 번에 받는 피해가 최대 체력의 40%를 넘지 않는다.
    ///
    /// 씨앗 자폭의 두 번째 답이다. 대폭발 한 방이 후열 체력의 85~90%인데 여기서 40%로 잘린다.
    /// 방어막을 두르거나 이 갑옷을 입거나 — 경갑 숙련이 걸려 있어 아무나 가져가지는 못한다.
    /// </summary>
    public sealed class ShedScaleItemPassive : PersistentStatusPassive
    {
        private const float CapRatio = 0.40f;

        public ShedScaleItemPassive(PassiveCodeContext context)
            : base(context, VoidItemStatusIds.ShedScale, "item_shed_scale", "탈피한 비늘",
                "한 번에 받는 피해가 최대 체력의 40%를 넘지 않습니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new ShedScaleEffect(CapRatio);
    }

    internal sealed class ShedScaleEffect : BaseEffect
    {
        private readonly float _capRatio;

        public ShedScaleEffect(float capRatio) : base(0, capRatio) => _capRatio = capRatio;

        public override bool IsBeneficial => true;

        public override float IncomingDamageCapRatio(Unit unit, DamageContext context)
            => unit == Target ? _capRatio : 0f;
    }

    /// <summary>
    /// 여덟 갈래 고리 — 자신이 부착하는 원소의 지속 +1턴.
    ///
    /// 공허 테마의 교훈 그 자체다. 부착이 한 턴 더 남으면 두 번째 원소를 얹을 여유가 생기고,
    /// 그것이 곧 원소 반응이다. 반응 피해를 직접 올리지 않은 것은 <c>IReactionAmplifier</c>
    /// 계열이 이미 있고 그쪽은 <b>가장 높은 하나만</b> 세기 때문이다 — 원소술사를 든 파티에서
    /// 이 반지가 조용히 무효가 되면 산 보람이 없다.
    /// </summary>
    public sealed class EightfoldRingItemPassive : PersistentStatusPassive
    {
        private const int ExtraTurns = 1;

        public EightfoldRingItemPassive(PassiveCodeContext context)
            : base(context, VoidItemStatusIds.EightfoldRing, "item_eightfold_ring", "여덟 갈래 고리",
                "자신이 부착하는 원소의 지속이 1턴 늘어납니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new EightfoldRingEffect(ExtraTurns);
    }

    internal sealed class EightfoldRingEffect : BaseEffect
    {
        private readonly int _extraTurns;

        public EightfoldRingEffect(int extraTurns) : base(0, extraTurns) => _extraTurns = extraTurns;

        public override bool IsBeneficial => true;

        public override int GrantedElementDurationAdditiveModifier(Unit source)
            => source == Target ? _extraTurns : 0;
    }
}
