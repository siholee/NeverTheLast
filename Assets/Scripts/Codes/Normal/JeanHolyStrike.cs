using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>잔 — 짧게 내딛은 뒤 대상 상체에 연타 압력을 꽂는 단일 접촉 공격.</summary>
    public sealed class JeanHolyStrike : BaseNormalCode
    {
        public JeanHolyStrike(NormalCodeContext context) : base(context)
        {
            CodeName = "신성한 일격";
            CastingDelay = 0.10f;
        }

        protected override bool IsContact => true;
        protected override int BaseDamage(Unit target)
            => StatDamage(0f, BaseEnums.StatType.Attack, 1f);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return JeanFx.HolyStrike(Caster, target, false, () => Hit(target, context));
        }

        /// <summary>철권제재가 부르는 자식 행동. 일반행동 해결 알림은 내지 않아 기회를 충전하지 않는다.</summary>
        public IEnumerator CastCounter(Unit attacker)
        {
            if (Caster == null || attacker == null || !attacker.isActive) yield break;
            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.AdditionalAttack,
                DamageTag.CounterAttack,
                DamageTag.ContactAttack,
            };
            if (!Targeting.Announce(Caster, new[] { attacker })) yield break;

            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Cast, "철권제재");
            bool crit = RollCritical(out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(BaseDamage(attacker) * multiplier));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Normal, tags, crit);
            yield return JeanFx.HolyStrike(Caster, attacker, true, () =>
            {
                AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, "철권제재");
                Hit(attacker, context, false);
            });
        }
    }
}
