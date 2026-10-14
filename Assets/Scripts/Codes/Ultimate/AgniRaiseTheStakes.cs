using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Managers;

namespace Codes.Ultimate
{
    /// <summary>
    /// 아그니 — 초월기 「내가 판을 키워 주지」(Detail_08 §17.4). 피해는 없다.
    /// 자신을 포함한 필드 아군 전원에게 궁극기 피해 +20%를 걸고(판돈), 판돈 동안 아그니의 특성 추공이 궁극기 판정을 함께 받는다.
    /// 판돈은 아그니의 <b>일반행동 완료 3회</b>로만 줄어든다(초월기 · 특수행동 · 추공으로는 줄지 않는다). 재시전은 3회로 갱신한다.
    /// 시전하면 아군별 특성 표를 모두 되돌린다 — 이미 지나간 초월기를 다시 재생하거나 대기 중인 표를 복제하지는 않는다.
    /// </summary>
    public sealed class AgniRaiseTheStakes : BaseUltimateCode
    {
        public override bool SupportsUltimateDamage => true;
        public AgniRaiseTheStakes(UltimateCodeContext context) : base(context) => CodeName = "내가 판을 키워 주지";

        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField && AliveEnemies().Count > 0;

        protected override bool RequiresCompletionEvidence => true;

        protected override IEnumerator Perform()
        {
            AgniBurnToTheEnd passive = Caster.ActivePassiveCodes.OfType<AgniBurnToTheEnd>().FirstOrDefault();
            yield return Combat.DirectAttack.HitOne("agni.ultimate", Caster, Caster, () =>
            {
                if (passive != null) { passive.StartStakes(); MarkEffectApplied(); }
            }, presentation: impact => Effects.Vfx.VedicTrioFx.AgniUltimate(Caster, Combat.CombatTargets.AliveAlliesIncludingSelf(Caster), impact));
        }
    }
}
