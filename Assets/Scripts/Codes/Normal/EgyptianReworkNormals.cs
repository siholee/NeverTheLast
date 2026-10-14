using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    public sealed class SetBloodStrike:BaseNormalCode
    {
        public SetBloodStrike(NormalCodeContext context):base(context)=>CodeName="붉은 모래";
        public override int AttackForm=>DamageTag.Slash;
        protected override bool IsContact=>true;
        protected override int BaseDamage(Unit target)=>StatDamage(0,BaseEnums.StatType.Attack,.90f);
        protected override void OnAttackResolved(Unit target,DamageContext context)
        {if(target!=null&&target.isActive&&!context.IsCancelled)ActionDot.Apply(Caster,target,ActionDotKind.Bleed);}
        protected override IEnumerator Deliver(Unit target)
        {
            var context=BuildContext(target);
            yield return DirectAttack.HitOne("set.normal",Caster,target,()=>Hit(target,context),
                presentation:impact=>EgyptianReworkFx.Slash(Caster,target,impact));
        }
    }
    public sealed class HorusSunArrow:BaseNormalCode
    {
        public HorusSunArrow(NormalCodeContext context):base(context)=>CodeName="태양의 화살";
        public override int AttackForm=>DamageTag.Shot;
        protected override int BaseDamage(Unit target)=>StatDamage(0,BaseEnums.StatType.Attack,.80f);
        protected override ProjectileStyle ProjectileFor(Unit target)=>OrionSkadiFx.Arrow(false);
    }
    public sealed class IsisGuardianStrike:BaseNormalCode
    {
        public const int CodeId=503;
        public IsisGuardianStrike(NormalCodeContext context):base(context)=>CodeName="수호령의 별빛";
        public override int AttackForm=>DamageTag.Spell;
        protected override Unit DamageOwner=>Caster?.SummonOwner;
        protected override int BaseDamage(Unit target)=>DamageOwner==null?0:Mathf.RoundToInt(DamageOwner.DefCurr*.35f);
        protected override List<int> GetDamageTags(){var tags=base.GetDamageTags();tags.Add(DamageTag.SummonAttack);return tags;}
        protected override IEnumerator Deliver(Unit target)
        {
            var owner=DamageOwner;if(owner==null||!owner.IsOnField)yield break;
            bool crit=RollCriticalFor(owner,Caster,target,BaseEnums.CodeType.Normal,out float multiplier);
            var context=new DamageContext(owner,Mathf.Max(1,Mathf.RoundToInt(BaseDamage(target)*multiplier)),BaseEnums.CodeType.Normal,GetDamageTags(),crit);
            StampCommand(context);
            yield return DirectAttack.HitOne("isis.guardian",Caster,target,()=>Hit(target,context),
                presentation:impact=>ProjectileFx.Fly(Caster,target,OrionSkadiFx.Arrow(false,.85f),impact));
        }
        protected override void OnAttackResolved(Unit target,DamageContext context)
        {if(target!=null&&target.isActive&&!context.IsCancelled)target.GrantCombatElement(BaseEnums.UnitElement.Geo,source:DamageOwner);}
    }
    public sealed class IsisWovenShield:NormalCode
    {
        public IsisWovenShield(NormalCodeContext context):base(context){CodeName="수호의 매듭";CodeType=BaseEnums.CodeType.Normal;}
        public override bool HasValidTarget()=>Caster!=null&&Caster.IsOnField;
        public override void CastCode(){if(!HasValidTarget())return;Caster.isCasting=true;CurrSkillCoroutine=Caster.StartCoroutine(Resolve());}
        private IEnumerator Resolve()
        {
            AudioManager.PlayAction(Caster,SfxActionSlot.Normal,SfxCuePhase.Cast,CodeName);
            yield return EgyptianReworkFx.Chant(Caster,false);
            if(!HasValidTarget()||Caster.isControlled){StopCode();yield break;}
            int amount=Mathf.RoundToInt(Caster.DefCurr*.35f);
            foreach(var ally in SummonSupportCombat.Allies(Caster,true))if(!ally.IsSummon||!ally.IsUntargetable)ally.AddShield(amount,Caster);
            AudioManager.PlayAction(Caster,SfxActionSlot.Normal,SfxCuePhase.Hit,CodeName);
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(Caster));Caster.isCasting=false;
        }
        public override void StopCode(){base.StopCode();if(Caster==null)return;if(CurrSkillCoroutine!=null)Caster.StopCoroutine(CurrSkillCoroutine);Caster.isCasting=false;}
    }
}
