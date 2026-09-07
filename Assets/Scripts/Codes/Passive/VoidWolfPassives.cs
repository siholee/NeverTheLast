using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>공허의 늑대 계열 코드 ID.</summary>
    public static class VoidWolfCodeIds
    {
        public const int BloodScent = 1470;
        public const int Vampire = 1471;
        public const int DarkLord = 1472;
    }

    public static class VoidWolfStatusIds
    {
        public const int BloodScent = 7910;
        public const int Vampire = 7911;
        public const int DarkLord = 7912;
    }

    /// <summary>
    /// 공허의 늑대 P 피 냄새 — 자신이 출혈을 걸면 <b>같은 턴 수만큼</b> 치유량 감소도 함께 건다.
    ///
    /// 출혈은 최대 체력 비례라 회복으로 상쇄되기 쉽다. 회복을 함께 막아야 지속피해가
    /// 실제 압박이 되므로, 늑대는 물어뜯은 상처가 아물지 않게 만드는 쪽으로 굴린다.
    /// 확률 판정은 출혈 쪽에서 이미 한 번 했으므로 여기서는 확정으로 붙인다.
    /// </summary>
    public sealed class VoidWolfBloodScent : PersistentStatusPassive
    {
        private Action<Unit, Unit, int> _bleedHandler;

        public VoidWolfBloodScent(PassiveCodeContext context)
            : base(context, VoidWolfStatusIds.BloodScent, "void_wolf_blood_scent", "피 냄새",
                "자신이 출혈을 부여하면 같은 턴 수만큼 치유량 감소(-50%)도 함께 부여합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        // 출혈 부여에 반응해서만 움직인다. 전투 시작 시점에는 붙일 효과가 없다.
        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _bleedHandler = OnBleedApplied;
            BleedStatus.Applied += _bleedHandler;
        }

        protected override void OnUnregistered()
        {
            BleedStatus.Applied -= _bleedHandler;
            _bleedHandler = null;
        }

        private void OnBleedApplied(Unit source, Unit target, int turns)
        {
            if (Caster == null || !Registered || source != Caster) return;
            if (target == null || !target.isActive || turns <= 0) return;

            HealingReductionStatus.Apply(target, Caster, turns, CodeName);
        }
    }

    /// <summary>
    /// 흡혈귀(1471)와 그 금색 상위 코드 어둠의 군주(1472) —
    /// <b>공격으로 자신을 회복할 때만</b> 회복량이 늘어난다.
    ///
    /// 판정은 <c>Unit.ModifyHp</c>가 넘겨 주는 <c>attackTriggered</c>를 쓴다. 즉 회복이
    /// <see cref="BaseEnums.UnitEventType.OnDamageDealt"/> 처리 중에 일어나야 인정된다.
    /// 아이템 '아리아드네의 실타래'(406)와 같은 축이며, 곱해져 함께 적용된다.
    /// </summary>
    public sealed class VoidWolfBloodthirst : PersistentStatusPassive
    {
        private readonly float _multiplier;

        public VoidWolfBloodthirst(PassiveCodeContext context, int statusId, string statusKey,
            string name, float bonus, int supersededByCodeId, BaseEnums.CodeGrade grade)
            : base(context, statusId, statusKey, name,
                $"적에게 피해를 입혀 자신을 회복할 때 회복량이 {bonus * 100f:F0}% 증가합니다.")
        {
            _multiplier = 1f + bonus;
            Transferable = false;
            Grade = grade;
            SupersededByCodeId = supersededByCodeId;
        }

        protected override BaseEffect CreateInitialEffect() => new AttackLifestealBonusEffect(_multiplier);
    }

    internal sealed class AttackLifestealBonusEffect : BaseEffect
    {
        private readonly float _multiplier;
        public AttackLifestealBonusEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float HealingReceivedMultiplierModifier(Unit unit, Unit source, bool attackTriggered)
            => unit == Target && source == unit && attackTriggered ? _multiplier : 1f;
    }
}
