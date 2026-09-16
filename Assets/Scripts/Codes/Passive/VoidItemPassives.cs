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

    /// <summary>시구르드·브륀힐드의 전용 장비가 주는 코드. 공통 보상 풀에 들어간다.</summary>
    public static class OathItemCodeIds
    {
        public const int Gram = 424;
        public const int FafnirsBlood = 425;
        public const int Vafrlogi = 426;
    }

    public static class OathItemStatusIds
    {
        public const int Gram = 6424;
        public const int FafnirsBlood = 6425;
        public const int Vafrlogi = 6426;
    }

    /// <summary>
    /// 그람 — 베기 분류 공격이 대상의 내구를 무시한다.
    ///
    /// 시구르드의 N·U·협공 몫이 전부 베기라 본인에게 가장 크지만,
    /// 한손검·대검을 든 아군이면 누구나 값을 본다. T4는 역사적 유물에만 주는 규칙에 맞는다.
    /// </summary>
    public sealed class GramItemPassive : PersistentStatusPassive
    {
        public GramItemPassive(PassiveCodeContext context)
            : base(context, OathItemStatusIds.Gram, "item_gram", "그람",
                "베기 분류 공격이 대상의 내구를 무시합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new GramEffect();
    }

    internal sealed class GramEffect : BaseEffect
    {
        public GramEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override int DurabilityPenetrationModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || context?.DamageTags == null) return 0;
            if (!context.DamageTags.Contains(DamageTag.Slash)) return 0;
            // 내구 전량을 무시한다. 대상의 내구를 읽을 수 없으면 충분히 큰 값으로 덮는다.
            return target != null ? Mathf.Max(0, target.DurabilityCurr) : 0;
        }
    }

    /// <summary>
    /// 파프니르의 피 — 잔타는 튕겨 내고 <b>큰 한 방에는 뚫린다.</b>
    ///
    /// 용의 피로 굳은 살갗에 <b>등의 한 점</b>만 남았다는 신화를 그대로 옮겼다.
    /// 다타수 적에게 강하고 보스의 한 방에 약한, 성격이 뚜렷한 방어구가 된다.
    /// </summary>
    public sealed class FafnirsBloodItemPassive : PersistentStatusPassive
    {
        /// <summary>물리 피해 감소폭.</summary>
        public const float Reduction = 0.20f;

        /// <summary>이 비율 이상을 한 번에 받으면 감소가 통째로 꺼진다.</summary>
        public const float BreakpointRatio = 0.20f;

        public FafnirsBloodItemPassive(PassiveCodeContext context)
            : base(context, OathItemStatusIds.FafnirsBlood, "item_fafnirs_blood", "파프니르의 피",
                "받는 물리 피해가 20% 감소합니다. 단 한 번에 최대 체력의 20% 이상을 받으면 적용되지 않습니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new FafnirsBloodEffect(Reduction, BreakpointRatio);
    }

    internal sealed class FafnirsBloodEffect : BaseEffect
    {
        private readonly float _reduction;
        private readonly float _breakpoint;

        public FafnirsBloodEffect(float reduction, float breakpoint) : base(0, reduction)
        {
            _reduction = reduction;
            _breakpoint = breakpoint;
        }

        public override bool IsBeneficial => true;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.DamageTags == null) return 1f;
            if (!context.DamageTags.Contains(DamageTag.Physical)) return 1f;
            if (Target.HpMax <= 0) return 1f;

            // 한 방이 문턱을 넘으면 비늘이 뚫린다 — 등의 한 점이다.
            if (context.Damage >= Target.HpMax * _breakpoint) return 1f;
            return 1f - _reduction;
        }
    }

    /// <summary>바프르로기 — 궁극기를 쓰면 2턴간 받는 피해가 30% 줄어든다. 불의 고리가 둘러선다.</summary>
    public sealed class VafrlogiItemPassive : PersistentStatusPassive
    {
        public const float Reduction = 0.30f;
        public const int DurationTurns = 2;

        public VafrlogiItemPassive(PassiveCodeContext context)
            : base(context, OathItemStatusIds.Vafrlogi, "item_vafrlogi", "바프르로기",
                "궁극기를 사용하면 2턴 동안 받는 피해가 30% 감소합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VafrlogiEffect(Reduction, DurationTurns);
    }

    internal sealed class VafrlogiEffect : BaseEffect
    {
        private const int RingStatusId = 6427;

        private readonly float _reduction;
        private readonly int _durationTurns;
        private System.Action<EventContext> _handler;

        public VafrlogiEffect(float reduction, int durationTurns) : base(0, reduction)
        {
            _reduction = reduction;
            _durationTurns = durationTurns;
        }

        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ => RaiseRing();
            Target.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
            _handler = null;
        }

        private void RaiseRing()
        {
            if (Target == null || !Target.isActive) return;
            Target.AddStatus(Effects.Buffs.BuffStatus.Create(
                RingStatusId, "item_vafrlogi_ring", "바프르로기", Target, Target,
                new Effects.Buffs.ReceivingDamageMultiplierEffect(1f - _reduction), _durationTurns));
        }
    }

    /// <summary>
    /// 궁니르 — <b>자신의 공격은 회피되지 않는다.</b>
    ///
    /// 오딘에게서 뺏은 창이다. 그가 `예언`으로 앞을 보고 피하던 그 창은
    /// 애초에 빗나가지 않는 물건이었다.
    ///
    /// 회피는 파생값이 0이라 상태·장비로만 생긴다. 지금 출처는 에퀴테스의 `기병의 회피`와
    /// 오딘의 `예언` 둘뿐이며, 이 창이 그 둘을 한꺼번에 지운다.
    /// </summary>
    public sealed class GungnirItemPassive : PersistentStatusPassive
    {
        public const int CodeId = 427;
        public const int StatusId = 6428;

        public GungnirItemPassive(PassiveCodeContext context)
            : base(context, StatusId, "item_gungnir", "궁니르",
                "자신의 공격은 회피되지 않습니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new GungnirEffect();
    }

    internal sealed class GungnirEffect : BaseEffect
    {
        public GungnirEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override bool IgnoresEvasion(Unit attacker) => attacker == Target;
    }

    /// <summary>아스완 세트가 주는 장비 전용 패시브. 테마의 두 축(화상·사령)을 그대로 쓴다.</summary>
    public static class AswanItemCodeIds
    {
        public const int CanopicJar = 428;
        public const int AshenKhopesh = 429;
        public const int WraithGreaves = 430;
        public const int SceptreOfAmun = 431;
    }

    /// <summary>봉인된 카노푸스 — 자신이 부여하는 지속피해가 25% 증가한다.</summary>
    public sealed class CanopicJarItemPassive : PersistentStatusPassive
    {
        public const float Bonus = 0.25f;

        public CanopicJarItemPassive(PassiveCodeContext context)
            : base(context, 6429, "item_canopic_jar", "봉인된 카노푸스",
                "자신이 부여하는 지속피해가 25% 증가합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect()
            => new Effects.Buffs.DamageOverTimeApplicationEffect(1f + Bonus);
    }

    /// <summary>
    /// 재의 코페쉬 — <b>불이 붙은 적에게</b> 주는 피해 +25%.
    ///
    /// 아스완 앞 절반이 화상을 깔아 놓는 테마라 그 판에서 가장 값을 하지만,
    /// 불을 다루는 아군(수르트·아그니)이 들면 어디서든 자기 힘으로 조건을 만든다.
    /// </summary>
    public sealed class AshenKhopeshItemPassive : PersistentStatusPassive
    {
        public const float Bonus = 0.25f;

        public AshenKhopeshItemPassive(PassiveCodeContext context)
            : base(context, 6430, "item_ashen_khopesh", "재의 코페쉬",
                "불 원소가 부착된 적에게 주는 피해가 25% 증가합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new AshenKhopeshEffect(Bonus);
    }

    internal sealed class AshenKhopeshEffect : BaseEffect
    {
        private readonly float _bonus;

        public AshenKhopeshEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null &&
               target.HasAttachedElement(BaseEnums.UnitElement.Pyro)
                ? 1f + _bonus
                : 1f;
    }

    /// <summary>
    /// 사령의 각반 — 전투당 한 번, 치명 피해를 막고 최대 체력의 30%로 일어선다.
    ///
    /// 되살아나는 사령에게서 벗겨 낸 물건이다. `명계의 재림`과 같은 축이지만
    /// 장비라 누구나 한 번은 버틴다.
    /// </summary>
    public sealed class WraithGreavesItemPassive : PersistentStatusPassive
    {
        public const float ReviveRatio = 0.30f;

        public WraithGreavesItemPassive(PassiveCodeContext context)
            : base(context, 6431, "item_wraith_greaves", "사령의 각반",
                "전투당 1회, 치명 피해를 막고 최대 체력의 30%로 일어섭니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new WraithGreavesEffect(ReviveRatio);
    }

    internal sealed class WraithGreavesEffect : BaseEffect
    {
        private readonly float _ratio;
        private bool _used;

        public WraithGreavesEffect(float ratio) : base(0, ratio) => _ratio = ratio;

        public override bool IsBeneficial => true;

        public override void OnApply() => _used = false;

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit != Target) return false;

            _used = true;
            Target.ModifyHp(Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _ratio)), Target);
            Debug.Log($"[사령의 각반] {Target.UnitName}이(가) 한 번 일어섰다");
            return true;
        }
    }

    /// <summary>
    /// 아문의 홀 — 궁극기로 주는 피해 +30%.
    ///
    /// 스택을 모아 한 번에 터뜨리는 아문·라의 축을 장비로 옮겼다.
    /// 역사적 유물이라 T4를 받는다.
    /// </summary>
    public sealed class SceptreOfAmunItemPassive : PersistentStatusPassive
    {
        public const float Bonus = 0.30f;

        public SceptreOfAmunItemPassive(PassiveCodeContext context)
            : base(context, 6432, "item_sceptre_of_amun", "아문의 홀",
                "궁극기로 주는 피해가 30% 증가합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new SceptreOfAmunEffect(Bonus);
    }

    internal sealed class SceptreOfAmunEffect : BaseEffect
    {
        private readonly float _bonus;

        public SceptreOfAmunEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags != null &&
               context.DamageTags.Contains(DamageTag.UltAttack)
                ? 1f + _bonus
                : 1f;
    }
}
