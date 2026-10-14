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
    public sealed class HorusRoyalInvestiture:BaseUltimateCode
    {
        protected override bool RequiresCompletionEvidence=>true;
        public const string BuffKey="horus_royal_investiture";
        public HorusRoyalInvestiture(UltimateCodeContext context):base(context)=>CodeName="지평선의 왕관";
        private Unit Heir=>Caster?.ActivePassiveCodes.OfType<HorusRoyalHeir>().FirstOrDefault()?.Heir;
        public override bool HasValidTarget()=>Caster!=null&&Caster.IsOnField&&Heir!=null;
        protected override IEnumerator Perform()
        {
            yield return EgyptianReworkFx.Chant(Caster,false,true);
            if(Caster==null||!Caster.IsOnField||Caster.isControlled)yield break;
            Unit heir=Heir;if(heir==null)yield break;
            var status=BuffStatus.Create(73521,BuffKey,CodeName,Caster,heir,new RoyalPower(),2,isBeneficial:true,
                description:"일반행동2회 동안 초월기 피해+30%. 부여 직후 최대 마나30% 충전.");
            status.NormalActionDuration=true;heir.AddStatus(status);
            heir.AddUltimateResource(Mathf.RoundToInt(heir.ManaMax*.30f));
            AudioManager.PlayAction(Caster,SfxActionSlot.Ultimate,SfxCuePhase.Hit,CodeName);MarkEffectApplied();
        }
        private sealed class RoyalPower:BaseEffect
        {public RoyalPower():base(0){}public override bool IsBeneficial=>true;public override float UltimateDamageBonus(Unit actor)=>actor==Target ? .30f : 0;}
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
