using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities.Status;
using Managers;

namespace Codes.Ultimate
{
    /// <summary>잔 — 3턴 동안 우선도·추가공격 피해·피해 경감을 함께 얻는 자기 강화.</summary>
    public sealed class JeanTensionRising : BaseUltimateCode
    {
        private const int StatusId = 7007;
        private const string StatusKey = "jean_tension_rising";
        private const int DurationTurns = 3;

        public JeanTensionRising(UltimateCodeContext context) : base(context)
            => CodeName = "텐션 오르는데!";

        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;

        protected override IEnumerator Perform()
        {
            yield return JeanFx.TensionRising(Caster, () =>
            {
                var status = BuffStatus.Create(
                    StatusId, StatusKey, CodeName, Caster, Caster,
                    new TargetPriorityModifierEffect(1),
                    duration: DurationTurns,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "3턴 동안 타겟 우선도 +1, 추가공격 피해 +20%, 받는 피해 -10%(접촉이면 총 -20%).");
                status.AddEffect(new ConditionalOutgoingDamageMultiplierEffect(
                    context => context != null && context.HasTag(DamageTag.AdditionalAttack), 1.20f));
                status.AddEffect(new ConditionalReceivingDamageMultiplierEffect(
                    context => context != null && context.HasTag(DamageTag.ContactAttack), 0.80f, 0.90f));
                // 자신만 받는 버프는 칼춤 시전 하나로 읽히게 하고 자동 affected를 겹치지 않는다.
                status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
                Caster.AddStatus(status);
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Resolve, CodeName);
            });
        }
    }
}
