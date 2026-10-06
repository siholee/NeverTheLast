using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>히미코 — 고유 표식 「맹화」. 세 번째 표식만 하나의 디버프로 폭발한다.</summary>
    public sealed class HimikoBlaze : UniquePassiveCode
    {
        private const int BlazeStatusId = 70440;
        private const int VulnerabilityStatusId = 70441;
        private const string BlazeKey = "himiko_blaze";
        private const string VulnerabilityKey = "himiko_blaze_vulnerability";

        public HimikoBlaze(PassiveCodeContext context) : base(context) => CodeName = "맹화";

        public void ApplyBlaze(Unit target)
        {
            if (Caster == null || target == null || !target.isActive || target.IsEnemy == Caster.IsEnemy) return;

            UnitStatus mark = BuffStatus.Create(
                BlazeStatusId, BlazeKey, "맹화", Caster, target,
                new MarkerBuffEffect(), duration: 0,
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                category: BaseEnums.StatusCategory.Neutral,
                description: "가벼운 열흔. 3중첩이 되면 하나의 해로운 효과로 폭발합니다.");
            // 공격 피격이 이미 표식 부여를 설명한다. 1~3중첩 모두 별도 상태광을 내지 않는다.
            mark.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            if (!target.TryAddStatus(mark)) return;

            int stacks = target.ActiveStatuses.Count(status => status.Key == BlazeKey);
            if (stacks < 3) return;

            ActionAnnouncer.AnnouncePassive(Caster, this);
            target.RemoveStatusByKey(BlazeKey);
            var vulnerability = BuffStatus.Create(
                VulnerabilityStatusId, VulnerabilityKey, "맹화 폭발", Caster, target,
                new ReceivingDamageMultiplierEffect(1.15f), duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "2턴 동안 받는 피해가 15% 증가합니다.");
            vulnerability.SkipEffectHitRoll = true;
            vulnerability.SkipStatusResistanceRoll = true; // 명중한 세 표식의 파생 효과
            // 실제 상태는 먼저 붙이되, 부여광은 주 폭발이 끝난 뒤 수동으로 한 번만 재생한다.
            vulnerability.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            target.AddStatus(vulnerability);

            EmberFx.Blaze(target, detonate: true);
            target.TakeDamage(new DamageContext(Caster, Mathf.Max(1, Caster.AtkCurr), BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget,
                    DamageTag.PassiveAttack,
                    DamageTag.NonContactAttack,
                    DamageTag.TrueDamage,
                }));

            // 실제 행동을 빼앗지는 않지만, 집중 등은 제어를 받은 것처럼 끊는다.
            target.Invoke(BaseEnums.UnitEventType.OnControlStarts, new EventContext(target, Caster));
        }
    }
}
