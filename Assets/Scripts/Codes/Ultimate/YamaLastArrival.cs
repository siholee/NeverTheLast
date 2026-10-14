using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 야마 — 초월기 「언젠가 다다를 마지막」(Detail_08 §15.4). 적 전체에 공격력 120%, 대상별 번개 부착,
    /// 그 대상의 모든 지속피해를 min(1, 1.5q) 비율로 즉시 발동한다. 대상마다 피해 → 부착 → 발동 순이다.
    /// </summary>
    public sealed class YamaLastArrival : BaseUltimateCode
    {
        private const float AttackRatio = 1.20f;

        public override int AttackForm => DamageTag.Spell;

        public YamaLastArrival(UltimateCodeContext context) : base(context) => CodeName = "언젠가 다다를 마지막";

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            AttackTargetResolution routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Targeting.Announce(Caster, targets)) yield break;

            yield return Effects.Vfx.LokapalaFx.Area("yama.ultimate", Caster, targets, () =>
            {
                float ratio = YamaDeathVerdict.UltimateRatio(Caster);
                foreach (Unit target in targets.ToList())
                {
                    if (target == null || !target.isActive) continue;
                    target.TakeDamage(BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>(tags), target));
                    if (!target.isActive) continue;
                    target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster);
                    DamageOverTimeDetonation.Detonate(target, ratio, Caster);
                }
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            });
            yield return new WaitForSeconds(0.15f);
        }
    }
}
