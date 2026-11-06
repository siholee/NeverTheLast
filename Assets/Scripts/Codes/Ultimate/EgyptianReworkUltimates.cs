using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class IsisStellarProcession:BaseUltimateCode
    {
        protected override bool RequiresCompletionEvidence=>true;
        public const float AdvanceRatio=.50f;
        public IsisStellarProcession(UltimateCodeContext context):base(context)=>CodeName="별들의 행렬";
        public override bool HasValidTarget()=>Caster!=null&&Caster.IsOnField&&Targets().Any();
        private System.Collections.Generic.IEnumerable<Unit> Targets()=>SummonSupportCombat.Allies(Caster,true).Where(x=>x.IsSummon||x.CountsAsSummon);
        protected override IEnumerator Perform()
        {
            yield return EgyptianReworkFx.Chant(Caster,false,true);
            if(Caster==null||!Caster.IsOnField||Caster.isControlled)yield break;
            var targets=Targets().ToList();
            foreach(var pet in targets)GameManager.Instance?.ActionScheduler?.AdvanceAction(pet,AdvanceRatio);
            AudioManager.PlayAction(Caster,SfxActionSlot.Ultimate,SfxCuePhase.Hit,CodeName);if(targets.Count>0)MarkEffectApplied();
        }
    }
    public sealed class SetCrimsonDesert:BaseUltimateCode
    {
        protected override bool RequiresCompletionEvidence=>true;
        public SetCrimsonDesert(UltimateCodeContext context):base(context)=>CodeName="피로 물든 사막";
        protected override IEnumerator Perform()
        {
            yield return EgyptianReworkFx.Chant(Caster,true,true);
            if(Caster==null||!Caster.IsOnField||Caster.isControlled)yield break;
            foreach(var enemy in AliveEnemies())ActionDot.Apply(Caster,enemy,ActionDotKind.Bleed);
            Caster.Heal(Mathf.RoundToInt(Caster.HpMax*.12f),Caster);
            Caster.ActivePassiveCodes.OfType<SetDesertBlood>().FirstOrDefault()?.ResetBudget();
            AudioManager.PlayAction(Caster,SfxActionSlot.Ultimate,SfxCuePhase.Hit,CodeName);MarkEffectApplied();
        }
    }
}
