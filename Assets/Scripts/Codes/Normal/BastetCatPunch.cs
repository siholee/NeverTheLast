using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;

namespace Codes.Normal
{
    /// <summary>바스테트 — 짧게 내딛고 대상 초상화에 한 번 꽂는 접촉 물리 공격.</summary>
    public sealed class BastetCatPunch : BaseNormalCode
    {
        public BastetCatPunch(NormalCodeContext context) : base(context)
        {
            CodeName = "냥냥펀치";
            CastingDelay = 0.10f;
        }

        protected override bool IsContact => true;
        protected override int BaseDamage(Unit target)
            => StatDamage(0f, BaseEnums.StatType.Attack, 1f);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return BastetFx.CatPunch(Caster, target, false, () => Hit(target, context));
        }

        public void CastAdditional(Unit originalTarget)
        {
            if (Caster == null || originalTarget == null) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(AdditionalRoutine(originalTarget));
        }

        private IEnumerator AdditionalRoutine(Unit originalTarget)
        {
            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.AdditionalAttack,
                DamageTag.ContactAttack,
            };
            AttackTargetResolution routing = AttackTargetRouter.Prepare(Caster, new[] { originalTarget },
                BaseEnums.CodeType.Normal, tags, "미운 놈 한 대 더 때리기");
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Targeting.Announce(Caster, targets))
            {
                StopCode();
                yield break;
            }

            Unit target = targets[0];
            // 추가행동 표기는 스케줄러가 담당한다. 연출과 음향은 일반 냥냥펀치를 그대로 반복한다.
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            bool crit = RollCritical(out float multiplier);
            int damage = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(BaseDamage(target) * multiplier));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Normal, tags, crit);
            yield return BastetFx.CatPunch(Caster, target, true, () =>
            {
                AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
                Hit(target, context, false);
            });
            StopCode();
        }
    }
}
