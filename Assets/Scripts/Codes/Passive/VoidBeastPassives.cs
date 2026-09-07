using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>공허의 멧돼지(야수) 계열 코드 ID.</summary>
    public static class VoidBeastCodeIds
    {
        public const int BruteForce = 1460;
        public const int StoneSkin = 1461;
        public const int Arrogance = 1462;
        public const int SteelSkin = 1463;
        public const int AllDayLong = 1464;
    }

    public static class VoidBeastStatusIds
    {
        public const int BruteForce = 7900;
        public const int StoneSkin = 7901;
        public const int SteelSkin = 7902;
        public const int Arrogance = 7903;
    }

    /// <summary>
    /// 공허의 멧돼지 P 우격다짐 — 자신에게 걸린 디버프 하나당 CON·LUK +1%.
    ///
    /// <b>맞을수록 단단해지고 잘 터진다.</b> 디버프를 거는 쪽이 스스로 이 유닛을 키우게 되므로,
    /// 제어와 약화를 퍼붓는 편성일수록 오히려 정면 돌파가 어려워진다.
    /// 중첩 상한은 두지 않는다 — 디버프 개수 자체가 이미 천장이다.
    /// </summary>
    public sealed class VoidBeastBruteForce : PersistentStatusPassive
    {
        public const float PerDebuff = 0.01f;

        public VoidBeastBruteForce(PassiveCodeContext context)
            : base(context, VoidBeastStatusIds.BruteForce, "void_beast_brute_force", "우격다짐",
                "자신이 보유한 디버프 하나당 CON·LUK이 1% 증가합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidBeastBruteForceEffect();
    }

    internal sealed class VoidBeastBruteForceEffect : BaseEffect
    {
        public VoidBeastBruteForceEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target ||
                (stat != BaseEnums.PrimaryStat.CON && stat != BaseEnums.PrimaryStat.LUK)) return 1f;

            int debuffs = unit.GetAllStatuses()
                .Count(status => status != null && status.Category == BaseEnums.StatusCategory.Negative);
            return 1f + debuffs * VoidBeastBruteForce.PerDebuff;
        }
    }

    /// <summary>
    /// 바위피부(1461)와 그 금색 상위 코드 강철피부(1463) — 유닛 자신의 내구도를 올린다.
    ///
    /// 내구도는 받는 피해에서 <b>고정으로 깎아내는</b> 값이라 장비가 없는 적에게는 유일한 경감 수단이다.
    /// 둘을 같이 배우면 <see cref="PassiveCode.SupersededByCodeId"/>가 은색 쪽을 재운다.
    /// </summary>
    public sealed class VoidBeastHardSkin : PersistentStatusPassive
    {
        private readonly int _durability;

        public VoidBeastHardSkin(PassiveCodeContext context, int statusId, string statusKey,
            string name, int durability, int supersededByCodeId, BaseEnums.CodeGrade grade)
            : base(context, statusId, statusKey, name, $"내구도가 {durability} 증가합니다.")
        {
            _durability = durability;
            Transferable = false;
            Grade = grade;
            SupersededByCodeId = supersededByCodeId;
        }

        protected override BaseEffect CreateInitialEffect() => new FlatDurabilityEffect(_durability);
    }

    internal sealed class FlatDurabilityEffect : BaseEffect
    {
        private readonly int _durability;
        public FlatDurabilityEffect(int durability) : base(0, durability) => _durability = durability;

        public override int DurabilityAdditiveModifier(Unit unit) => unit == Target ? _durability : 0;
    }

    /// <summary>
    /// 오만(1462) — 적을 처치할 때마다 이번 전투 동안 물리 피해 +5%가 쌓인다.
    ///
    /// 중첩은 상태를 여러 개 붙이는 대신 <b>배율을 키운 상태 하나</b>로 표현한다(철벽과 같은 이유).
    /// 지속피해는 태그가 비어 있어 물리로 세지 않는다 — <c>Physical</c> 태그를 직접 확인한다.
    /// </summary>
    public sealed class VoidBeastArrogance : PersistentStatusPassive
    {
        public const float PerKill = 0.05f;

        private Action<EventContext> _killHandler;
        private int _stacks;

        public VoidBeastArrogance(PassiveCodeContext context)
            : base(context, VoidBeastStatusIds.Arrogance, "void_beast_arrogance", "오만",
                $"적을 처치할 때마다 이번 전투 동안 물리 피해가 {PerKill * 100f:F0}% 증가합니다. 중첩됩니다.")
        {
            Transferable = false;
        }

        // 처치에 반응해 처음 붙는다. 전투 시작 시점에는 아직 아무 효과도 없다.
        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _stacks = 0;
            _killHandler = OnKill;
            Caster.AddListener(BaseEnums.UnitEventType.OnKill, _killHandler);
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnKill, _killHandler);
            _killHandler = null;
            _stacks = 0;
        }

        private void OnKill(EventContext context)
        {
            if (Caster == null || !Registered || context?.Grantee != Caster || !Caster.isActive) return;

            _stacks++;
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster,
                new PhysicalDamageBonusEffect(PerKill * _stacks),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"물리 피해 +{PerKill * _stacks * 100f:F0}% ({_stacks}중첩)"));
        }
    }

    internal sealed class PhysicalDamageBonusEffect : BaseEffect
    {
        private readonly float _bonus;
        public PhysicalDamageBonusEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.Physical) == true
                ? 1f + _bonus
                : 1f;
    }
}
