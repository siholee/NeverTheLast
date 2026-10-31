using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 사바흐 — 아즈라엘 7중첩을 모두 소모해 적 전체를 한 번씩 벤다.
    /// 특공 기반 특수피해. 자원·대상·횟수·공유 치명타는 유지한다.
    /// </summary>
    public sealed class SabahAzrael : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Slash;

        public SabahAzrael(UltimateCodeContext context) : base(context) => CodeName = "아즈라엘";

        protected override IEnumerator Perform()
        {
            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.ContactAttack, DamageTag.Slash, DamageTag.SpecialDamage,
            };
            Combat.AttackTargetResolution routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;

            bool isCrit = RollCritical(out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                StatDamage(150f, BaseEnums.StatType.SpecialAttack, 1f) * multiplier));
            var contexts = targets.ToDictionary(target => target, _ => new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>(tags), isCrit));

            yield return SabahFx.Azrael(Caster, targets, target =>
            {
                if (target == null || !target.isActive) return;
                if (!contexts.TryGetValue(target, out DamageContext context)) return;
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                target.TakeDamage(context);
            });
        }
    }
}
