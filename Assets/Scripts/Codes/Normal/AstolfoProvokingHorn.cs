using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;

namespace Codes.Normal
{
    /// <summary>피해 없는 적 가속. 명중한 한 상태에 가속과 다음 행동의 피해 감소를 함께 묶는다.</summary>
    public sealed class AstolfoProvokingHorn : BaseNormalCode
    {
        public const float AdvanceRatio = .20f;
        public const float DamageMultiplier = .75f;
        public AstolfoProvokingHorn(NormalCodeContext context) : base(context) => CodeName = "이쪽이야, 따라와!";
        protected override int BaseDamage(Unit target) => 0;
        public override bool CanJoinCommand => false;
        protected override IEnumerator SkillCoroutine()
        {
            bool completed = true;
            yield return WaitForCast(ok => completed = ok);
            if (!completed) { StopCode(); yield break; }
            var targets = CombatTargets.AliveEnemies(Caster);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            bool resolved = false;
            void Resolve()
            {
                if (resolved) return;
                resolved = true;
                if (!DirectAttack.CasterCanContinue(Caster, false)) return;
                foreach (Unit target in targets.Where(unit => unit != null && unit.isActive)) Apply(Caster, target);
                AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
            }
            yield return Effects.Vfx.LokapalaFx.Area("astolfo.normal", Caster, targets, Resolve);
            Resolve();
            NotifyActionResolved();
            StopCode();
        }
        public static bool Apply(Unit caster, Unit target)
        {
            var status = BuffStatus.Create(71621, "astolfo_provocation", "도발의 뿔피리", caster, target,
                new OutgoingDamageMultiplierEffect(DamageMultiplier), duration: 1,
                category: BaseEnums.StatusCategory.Negative,
                description: "다음 일반행동 완료까지 가하는 피해 -25%. 부여 성공 시 다음 행동 20% 앞당김.");
            status.NormalActionDuration = true;
            status.AddEffect(new HornAdvanceEffect());
            return target.TryAddStatus(status);
        }
        private sealed class HornAdvanceEffect : BaseEffect
        {
            public HornAdvanceEffect() : base(0) { }
            public override void OnApply() => GameManager.Instance?.ActionScheduler?.AdvanceAction(Target, AdvanceRatio);
        }
    }
}
