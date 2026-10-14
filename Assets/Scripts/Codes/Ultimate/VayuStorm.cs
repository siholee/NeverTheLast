using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 바유 — 초월기 「만류의 회랑」(Detail_08 §14.4). 적 전체에 공격력 100% 피해 뒤 <b>방어력만</b> -20%(대상 2턴).
    /// 특수방어력은 건드리지 않고 바람도 붙이지 않는다 — 동료가 깔아 둔 부착을 불필요하게 덮지 않기 위해서다.
    /// </summary>
    public sealed class VayuStorm : BaseUltimateCode
    {
        public const float AttackRatio = 1.00f;
        public const float DefenseCut = 0.20f;
        public const int CutTurns = 2;

        public override int AttackForm => DamageTag.Spell;

        public VayuStorm(UltimateCodeContext context) : base(context) => CodeName = "만류의 회랑";

        protected override bool RequiresCompletionEvidence => true;

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            Combat.AttackTargetResolution routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;

            bool resolved = false;
            void Resolve()
            {
                if (resolved) return;
                resolved = true;
                if (!Combat.DirectAttack.CasterCanContinue(Caster, true)) return;
                foreach (Unit target in targets.ToList())
                {
                    if (!Combat.DirectAttack.CasterCanContinue(Caster, true)) break;
                    if (target == null || !target.isActive) continue;
                    MarkEffectApplied();
                    target.TakeDamage(BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>(tags), target));
                    if (!target.isActive || !Combat.DirectAttack.CasterCanContinue(Caster, true)) continue;
                    VayuErosion.Apply(Caster, target, DefenseCut, CutTurns);
                }
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            }
            yield return Effects.Vfx.LokapalaFx.Area("vayu.storm", Caster, targets, Resolve);
            // 연출 콜백이 오지 않아도 판정은 정확히 한 번 연다.
            Resolve();
            yield return new WaitForSeconds(0.15f);
        }
    }
}
