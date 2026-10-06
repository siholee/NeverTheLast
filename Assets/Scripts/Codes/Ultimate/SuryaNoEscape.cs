using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using Managers;

namespace Codes.Ultimate
{
    /// <summary>
    /// 수리야 — 초월기 「도망칠 곳은 없어」(Detail_08 §18.4). 현상수배된 적에게 공격력 150% 단일 #사격 뒤 불을 붙이고,
    /// 탄환을 2발까지 채우며 다음 실제 응답 사격 한 발을 110%로 강화한다(강탄은 겹치지 않고, 쏘는 순간 쓴다).
    /// 자기 초월기는 특성 응답을 부르지 않는다.
    /// </summary>
    public sealed class SuryaNoEscape : BaseUltimateCode
    {
        public const float AttackRatio = 1.50f;

        public override int AttackForm => DamageTag.Shot;

        public SuryaNoEscape(UltimateCodeContext context) : base(context) => CodeName = "도망칠 곳은 없어";

        protected override bool RequiresCompletionEvidence => true;

        protected override IEnumerator Perform()
        {
            SuryaBeforeSunset passive = Caster.ActivePassiveCodes.OfType<SuryaBeforeSunset>().FirstOrDefault();
            Unit wanted = passive?.ResolveTarget() ?? PickPriorityEnemy();
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Shot };
            Combat.AttackTargetResolution routing = PrepareTargets(wanted != null ? new List<Unit> { wanted } : new List<Unit>(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || targets.Count == 0 || !Combat.Targeting.Announce(Caster, targets)) yield break;

            Unit target = targets[0];
            DamageContext context = BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), tags, target);
            yield return Combat.DirectAttack.HitOne("surya.ultimate", Caster, target, () =>
            {
                if (!target.isActive) return;
                MarkEffectApplied();
                target.TakeDamage(context);
                if (target.isActive && Combat.DirectAttack.CasterCanContinue(Caster, false)) target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster);
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            }, presentation: impact => Effects.Vfx.VedicTrioFx.Single("surya.ultimate", Caster, target, impact));
            if (!Combat.DirectAttack.CasterCanContinue(Caster, false)) yield break;
            passive?.Reload();
            passive?.Empower();
        }
    }
}
