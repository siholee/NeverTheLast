using System.Runtime.CompilerServices;
using BaseClasses;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 지휘 사격 한 건의 표지(나폴레옹 일제 사격, Detail_08 §20.3). 상태를 바꾸지 않는 순수 토큰이다.
    ///
    /// 지휘로 만들어진 <b>일반 공격 피해 컨텍스트마다</b> <see cref="Stamp"/>로 붙는다. 붙는 순간 컨텍스트의
    /// <see cref="DamageContext.OutgoingDamageMultiplier"/>에 최종 배율(50%)을 <b>한 번</b> 곱한다 — 이 필드는
    /// <c>Unit.CalculateFinalDamage</c>가 이미 공통으로 곱하므로 원시 피해 · 치명 계산은 100% 그대로다.
    /// 표지는 컨텍스트에 묶여 있어 전역 행위자 등록처럼 반응 · 덧붙는 피해 · 이후의 일반행동으로 새지 않는다.
    ///
    /// 표지된 컨텍스트는 그 지휘가 살아 있을 때만 판정을 연다(<see cref="TryAdmit"/>). 취소 · 종료 · 지휘자나 공격자의
    /// 사망 · 이탈 · 제어 뒤에 늦게 도착한 판정은 피해 · 적중 이벤트 · 원소 · 표식을 모두 열지 않는다. 종료 뒤에도 값이
    /// 거짓으로 남으므로, 코드 쪽의 지휘 참조가 지워진 뒤 도착한 콜백도 배율 없는 피해로 새지 않는다.
    /// </summary>
    public sealed class CommandedAttackScope
    {
        /// <summary>지휘 사격의 최종 피해 배율.</summary>
        public const float FinalDamageRatio = 0.50f;

        private static readonly ConditionalWeakTable<DamageContext, CommandedAttackScope> Stamps = new();

        public CommandedAttackScope(Unit commander, Unit attacker, Unit lockedTarget, float ratio = FinalDamageRatio)
        {
            Commander = commander;
            Attacker = attacker;
            LockedTarget = lockedTarget;
            Ratio = Mathf.Max(0f, ratio);
        }

        /// <summary>지휘자. null이면 지휘자 조건을 보지 않는다.</summary>
        public Unit Commander { get; }
        /// <summary>실제로 치는 행동자(소환수면 소환수 자신). 피해 명의는 컨텍스트가 따로 든다.</summary>
        public Unit Attacker { get; }
        public Unit LockedTarget { get; }
        public float Ratio { get; }
        public bool Cancelled { get; private set; }
        public bool Ended { get; private set; }
        public int HitsLanded { get; private set; }
        public int StampedContexts { get; private set; }

        private static bool Standing(Unit unit) => unit != null && unit.isActive && unit.IsOnField && !unit.isControlled;

        /// <summary>지금 판정을 열 수 있는가. 공격자와 지휘자가 서 있고 제어되지 않았으며 취소 · 종료되지 않았다.</summary>
        public bool Active => !Cancelled && !Ended && Standing(Attacker) && (Commander == null || Standing(Commander));

        public void Cancel() => Cancelled = true;
        public void End() => Ended = true;

        /// <summary>
        /// 컨텍스트를 이 지휘의 피해로 표시하고 최종 배율을 한 번 곱한다. 일반 공격 태그의 직접 피해만 받는다.
        /// 이미 표시된 컨텍스트(광역 재사용)는 다시 곱하지 않는다. 취소·종료된 지휘도 표시는 한다 — 그래야 늦은 판정이 막힌다.
        /// </summary>
        public bool Stamp(DamageContext context)
        {
            if (context == null || Stamps.TryGetValue(context, out _)) return false;
            if (!DirectAttack.IsDirect(context) || !context.HasTag(DamageTag.NormalAttack)) return false;
            context.OutgoingDamageMultiplier *= Ratio;
            Stamps.Add(context, this);
            StampedContexts++;
            return true;
        }

        public static CommandedAttackScope Of(DamageContext context)
            => context != null && Stamps.TryGetValue(context, out CommandedAttackScope scope) ? scope : null;

        /// <summary>표지 없는 피해는 늘 허용, 표지된 피해는 그 지휘가 살아 있을 때만.</summary>
        public static bool Allows(DamageContext context)
        {
            CommandedAttackScope scope = Of(context);
            return scope == null || scope.Active;
        }

        /// <summary>판정 직전에 부른다. 허용되면 지휘 적중으로 센다.</summary>
        public static bool TryAdmit(DamageContext context)
        {
            CommandedAttackScope scope = Of(context);
            if (scope == null) return true;
            if (!scope.Active) return false;
            scope.HitsLanded++;
            return true;
        }

        /// <summary>
        /// 컨텍스트 하나가 대상에게 실제로 들어갈 피해의 결정적 추정. <c>Unit.CalculateFinalDamage</c>와 같은 순서로
        /// 컨텍스트 배율 · 전장 · 공격자 주는 피해 · 방어 인정 배율(공격자 · 보유자) · 받는 피해 · 경감을 곱한다.
        /// 질의 훅만 읽고 상태를 바꾸지 않는다. 보호막 · 사망 방지는 반영하지 않는다.
        /// </summary>
        public static int EstimateFinalDamage(DamageContext context, Unit target)
        {
            if (context == null || target == null) return 0;
            float outgoing = Mathf.Max(0f, context.OutgoingDamageMultiplier);
            float defenseMultiplier = Mathf.Max(0f, context.DefenseStatMultiplier);
            Unit attacker = context.Attacker;
            if (attacker != null)
            {
                outgoing *= Battlefield.OutgoingMultiplier(attacker);
                foreach (BaseEffect effect in attacker.CombatEffects())
                {
                    outgoing *= effect.OutgoingDamageModifier(attacker, target, context);
                    defenseMultiplier *= effect.DefenseStatMultiplierModifier(attacker, target, context);
                }
            }

            float receiving = 1f;
            foreach (BaseEffect effect in target.CombatEffects())
            {
                receiving *= effect.ReceivingDamageModifier(target, context);
                defenseMultiplier *= effect.OwnedDefenseStatMultiplierModifier(target, context);
            }

            float mitigation = 1f;
            if (DamageMitigation.TryGetDefenseStat(context, out BaseEnums.StatType stat))
            {
                int level = attacker != null ? attacker.Level : target.Level;
                mitigation = UnitStats.MitigationMultiplier(target.StatValue(stat) * defenseMultiplier, UnitStats.ArmorConstant(level));
            }
            float scaled = context.Damage * outgoing * mitigation * receiving;
            return scaled <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(scaled));
        }
    }
}
