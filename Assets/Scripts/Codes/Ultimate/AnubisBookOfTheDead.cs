using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 아누비스 — 초월기 「사자의 서」(Detail_08 §9.3). 아누비스 자신의 3턴(영혼 150 이정표 뒤 4턴) 동안 심장의 저울을 강화한다.
    /// 재시전은 같은 상태를 교체해 지속을 새로 채운다. 집중이 아니므로 제어에 걸려도 끊기지 않는다.
    /// 피격 마나가 주 충전원이라 맞을수록 자주 돈다 — 우선도를 받쳐 줘야 하는 이유다.
    /// </summary>
    public sealed class AnubisBookOfTheDead : BaseUltimateCode
    {
        public const string StatusKey = "anubis_book_of_the_dead";
        private const int StatusId = 71210;
        private const int BaseTurns = 3;
        private const int LongTurns = 4;

        public AnubisBookOfTheDead(UltimateCodeContext context) : base(context) => CodeName = "사자의 서";

        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;

        protected override IEnumerator Perform()
        {
            int souls = Caster.GetPermanentStacks(AnubisSoulHarvest.SoulKey);
            int turns = souls >= AnubisSoulHarvest.MilestoneLongerBook ? LongTurns : BaseTurns;
            var status = BuffStatus.Create(StatusId, StatusKey, CodeName, Caster, Caster, new MarkerBuffEffect(),
                duration: turns, stackPolicy: BaseEnums.StatusStackPolicy.Replace, isBeneficial: true,
                description: $"{turns}턴 동안 심장의 저울 피해 최대 체력 10% · 회복 60% · 적중 대상 바위 부착.");
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            Effects.Vfx.AnubisFx.Book(Caster);
            Caster.AddStatus(status);
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Resolve, CodeName);
            yield return new WaitForSeconds(0.48f);
        }
    }
}
