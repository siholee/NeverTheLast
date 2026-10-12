using System.Collections;
using BaseClasses;
using Codes.Base;
using Combat;
using UnityEngine;
namespace Codes.Ultimate
{
    public sealed class KuberaTreasury : BaseUltimateCode
    {
        public KuberaTreasury(UltimateCodeContext context) : base(context) => CodeName = "복리의 보고";
        public override bool HasValidTarget() => PartyTreasury.CanUse(Caster);
        protected override bool RequiresCompletionEvidence => true;
        public static int Interest(int available) => 1 + Mathf.FloorToInt(Mathf.Max(0, available) * .25f);
        protected override IEnumerator Perform()
        {
            yield return new WaitForSeconds(.16f);
            if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
            if (PartyTreasury.Charge(Caster, Interest(PartyTreasury.Available)) > 0) Effects.Vfx.LokapalaFx.ResourceGained(Caster);
            MarkEffectApplied(); yield return new WaitForSeconds(.20f);
        }
    }
}
