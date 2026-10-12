using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class OrpheusLament : BaseUltimateCode
    {
        public const string BuffKey = "orpheus_lament";
        public OrpheusLament(UltimateCodeContext context) : base(context) => CodeName = "비탄의 연주";
        public static float Bonus(Unit source)
        {
            float power = source.StatValue(BaseEnums.StatType.SpecialAttack);
            return .10f + .40f * power / (power + UnitStats.ArmorConstant(source.Level));
        }
        public override bool HasValidTarget() => AliveAllies().Count > 0;
        protected override bool RequiresCompletionEvidence => true;
        protected override IEnumerator Perform()
        {
            yield return Combat.DirectAttack.HitOne("orpheus.lament", Caster, Caster, () =>
            {
                if (!Combat.DirectAttack.CasterCanContinue(Caster, true)) return;
                float bonus = Bonus(Caster);
                foreach (Unit ally in AliveAllies())
                {
                    var status = BuffStatus.Create(70225, BuffKey, "비탄의 연주", Caster, ally,
                        new ToughnessEfficiencyEffect(bonus), duration: 3, isBeneficial: true,
                        description: $"강인도 삭감 효율 +{bonus:P0}. 자신의 일반행동 3회 지속.");
                    status.NormalActionDuration = true;
                    ally.AddStatus(status);
                    MarkEffectApplied();
                }
                Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate, Managers.SfxCuePhase.Hit, CodeName);
            });
        }
    }
}
