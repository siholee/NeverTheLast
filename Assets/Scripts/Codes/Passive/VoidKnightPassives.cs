using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidKnightCodeIds
    {
        public const int VoidArmor = 1490;
    }

    public static class VoidKnightStatusIds
    {
        public const int VoidArmor = 7930;
    }

    /// <summary>
    /// 공허의 기사 P 공허의 갑주 — 보호막을 두른 동안 가하는 피해 +30%.
    /// 자기 턴을 시작할 때 보호막이 없으면 CON×0.5를 새로 두른다.
    ///
    /// <b>방어를 공격으로 바꾸는 축이다.</b> 같은 전열이라도 선봉대는 도발로 버티는 쪽이고
    /// 기사는 보호막이 곧 화력이라, 플레이어에게 "보호막을 깨서 −30%를 만든다"는
    /// 선택지가 생긴다. 스스로 다시 두르므로 한 번 깨는 것으로 끝나지는 않는다.
    /// </summary>
    public sealed class VoidKnightArmor : PersistentStatusPassive
    {
        private const float DamageBonus = 0.30f;
        private const float ShieldConCoefficient = 0.5f;

        private Action<EventContext> _turnHandler;

        public VoidKnightArmor(PassiveCodeContext context)
            : base(context, VoidKnightStatusIds.VoidArmor, "void_knight_armor", "공허의 갑주",
                $"보호막을 보유한 동안 가하는 피해가 {DamageBonus * 100f:F0}% 증가합니다. "
                + $"자기 턴 시작 시 보호막이 없으면 CON×{ShieldConCoefficient}의 보호막을 얻습니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new ShieldedDamageBonusEffect(DamageBonus);

        protected override void OnRegistered()
        {
            _turnHandler = OnTurnStart;
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            RefillShield();
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            _turnHandler = null;
        }

        private void OnTurnStart(EventContext context)
        {
            if (Caster == null || !Registered || !Caster.isActive) return;
            RefillShield();
        }

        private void RefillShield()
        {
            if (Caster.ShieldCurr > 0) return;
            Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ShieldConCoefficient)), Caster);
        }
    }

    internal sealed class ShieldedDamageBonusEffect : BaseEffect
    {
        private readonly float _bonus;
        public ShieldedDamageBonusEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && Target.ShieldCurr > 0 ? 1f + _bonus : 1f;
    }
}
