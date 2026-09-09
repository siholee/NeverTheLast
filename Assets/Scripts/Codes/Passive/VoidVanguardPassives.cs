using Effects.Buffs;
using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Neutral;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidVanguardCodeIds
    {
        public const int VoidOath = 1570;
        public const int PhalanxFormation = 1571;
    }

    public static class VoidVanguardStatusIds
    {
        public const int VoidOath = 7950;
        public const int PhalanxFormation = 7951;
    }

    /// <summary>
    /// 공허의 선봉대 P 공허의 맹세 — 전투 시작 시, 그리고 <b>도발이 풀린 뒤 맞이하는 자기 턴</b>에
    /// 3턴 도발과 CON 비례 보호막을 다시 두른다. 도발 중에는 받는 피해가 15% 줄어든다.
    ///
    /// 주기를 "3턴마다"로 잡지 않은 이유가 있다. 도발은 3턴인데 유닛의 행동 간격은 DEX가 정하므로
    /// 고정 주기로 걸면 <b>도발이 끊긴 구간이 생기거나</b> 아직 남아 있는데 덧걸려 보호막만 쌓인다.
    /// 끊긴 것을 확인하고 다시 거는 쪽이 "항상 도발 중"이라는 의도에 맞는다.
    /// </summary>
    public sealed class VoidVanguardOath : PersistentStatusPassive
    {
        private const int TauntTurns = 3;
        private const float ShieldConCoefficient = 1.0f;
        private const float DamageReduction = 0.15f;

        private Action<EventContext> _turnHandler;

        public VoidVanguardOath(PassiveCodeContext context)
            : base(context, VoidVanguardStatusIds.VoidOath, "void_vanguard_oath", "공허의 맹세",
                $"전투 시작 시와 도발이 풀린 뒤 자기 턴에 {TauntTurns}턴 도발과 "
                + $"CON×{ShieldConCoefficient} 보호막을 얻습니다. 도발 중에는 받는 피해가 "
                + $"{DamageReduction * 100f:F0}% 감소합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new TauntedMitigationEffect(DamageReduction);

        protected override void OnRegistered()
        {
            _turnHandler = OnTurnStart;
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Renew();
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            _turnHandler = null;
        }

        private void OnTurnStart(EventContext context)
        {
            if (Caster == null || !Registered || !Caster.isActive) return;
            if (Taunt.Has(Caster)) return;   // 아직 도발 중이면 덧걸지 않는다
            Renew();
        }

        private void Renew()
        {
            Taunt.Apply(Caster, Caster, TauntTurns);
            Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ShieldConCoefficient)), Caster);
        }
    }

    internal sealed class TauntedMitigationEffect : BaseEffect
    {
        private readonly float _reduction;
        public TauntedMitigationEffect(float reduction) : base(0, reduction) => _reduction = reduction;

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target && Taunt.Has(Target) ? 1f - _reduction : 1f;
    }

    /// <summary>
    /// 방진(1571) — 자신이 전열에 서 있으면 <b>후열 아군</b>이 받는 피해가 10% 줄어든다.
    /// 앞줄이 버티는 동안 뒷줄이 오래 사는 진형 코드다.
    /// </summary>
    public sealed class VoidPhalanxFormation : PersistentStatusPassive
    {
        private const float Reduction = 0.10f;

        private Action<EventContext> _turnHandler;

        public VoidPhalanxFormation(PassiveCodeContext context)
            : base(context, VoidVanguardStatusIds.PhalanxFormation, "void_phalanx_formation", "방진",
                $"자신이 전열에 있으면 후열 아군이 받는 피해가 {Reduction * 100f:F0}% 감소합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _turnHandler = _ => Refresh();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Refresh();
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            _turnHandler = null;
            Clear();
        }

        /// <summary>전열/후열은 칸의 x 절댓값으로 본다 — 1이 앞줄, 2가 뒷줄이다.</summary>
        private static bool IsFrontRow(Unit unit)
            => unit?.currentCell != null && Mathf.Abs(unit.currentCell.xPos) == 1;

        private void Refresh()
        {
            if (Caster == null || !Registered || !Caster.isActive) return;

            Clear();
            if (!IsFrontRow(Caster)) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)
                         .Where(unit => unit != Caster && !IsFrontRow(unit) && unit.currentCell != null))
            {
                ally.AddStatus(Effects.Buffs.BuffStatus.Create(
                    StatusId, StatusKey, CodeName, Caster, ally,
                    new ReceivingDamageMultiplierEffect(1f - Reduction),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: Description));
            }
        }

        private void Clear()
        {
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.RemoveStatusByKey(StatusKey);
            }
        }
    }
}
