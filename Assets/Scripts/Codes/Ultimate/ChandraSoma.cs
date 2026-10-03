using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>찬드라 — 초월기 「소마」. 가장 공격력이 높은 아군 한 명을 3턴 강화하고 월광을 얻는다.</summary>
    public sealed class ChandraSoma : BaseUltimateCode
    {
        private const int DurationTurns = 3;
        private const float TargetAttackRatio = 0.10f;
        private const float CasterMaxHpRatio = 0.10f;
        private const int StatusId = 7061;
        private const string StatusKey = "chandra_soma";

        public ChandraSoma(UltimateCodeContext context) : base(context) => CodeName = "소마";

        public override bool HasValidTarget() => AliveAllies().Count > 0;

        protected override IEnumerator Perform()
        {
            Unit target = AliveAllies()
                .OrderByDescending(unit => unit.StatValue(BaseEnums.StatType.Attack))
                .ThenBy(unit => unit.GetEntityId())
                .FirstOrDefault();
            if (target == null) yield break;

            float attackBonus = target.StatValue(BaseEnums.StatType.Attack) * TargetAttackRatio +
                                Caster.HpMax * CasterMaxHpRatio;
            // 버프 판정은 월광 수렴이 끝나 성역이 닫히는 정점에 한 번만 맞춘다.
            yield return ChandraFx.Soma(Caster, target, AliveAllies(), () =>
            {
                var status = BuffStatus.Create(
                    StatusId, StatusKey, "소마", Caster, target,
                    new StatModifierEffect(BaseEnums.StatType.Attack, attackBonus),
                    duration: DurationTurns,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"{DurationTurns}턴 동안 공격력 +{attackBonus:0}.");
                // 조합 정의의 affected 슬롯이 실제 부여 직후 한 번 재생하므로 자동 상태 연출은 막는다.
                status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
                target.AddStatus(status);
                Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                    Managers.SfxCuePhase.Hit, CodeName);
                (Caster.ActivePassiveCodes.OfType<ChandraNishakara>().FirstOrDefault())
                    ?.GainMoonlight(playVfx: false);
            });
            yield return new WaitForSeconds(0.10f);
        }
    }
}
