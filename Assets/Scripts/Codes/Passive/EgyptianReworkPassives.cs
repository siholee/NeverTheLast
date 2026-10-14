using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public sealed class IsisThreefoldGuard : UniquePassiveCode, ISummonerPolicy
    {
        public const string KindPrefix = "isis_guardian_";
        public static readonly float[] Speeds = {60,80,100};
        private static readonly string[] Names = {"매듭의 수호령","눈의 수호령","날개의 수호령"};
        private bool _working;
        private Action<EventContext> _normal,_death;
        public override int AttackForm => DamageTag.Spell;
        public IsisThreefoldGuard(PassiveCodeContext context):base(context)=>CodeName="세 겹의 수호";
        public SummonStyle Style=>SummonStyle.Cohort;
        public int Cap=>3;
        public SummonSpec Spec
        {
            get
            {
                int i=Enumerable.Range(0,3).FirstOrDefault(index=>!Summons.Of(Caster).Any(x=>x.SummonKind==KindPrefix+index));
                return new SummonSpec{Id=904+i,Kind=KindPrefix+i,Name=Names[i],Element="Geo",PortraitKey="ISIS_PORTRAIT",
                    PortraitOverride=Effects.Vfx.EgyptianReworkFx.GuardianGlyph(i),Hp=1,Speed=Speeds[i],
                    NormalCodeId=Codes.Normal.IsisGuardianStrike.CodeId,Targetable=false};
            }
        }
        public void OnOverflow(Unit owner,Unit oldest) { }
        public override void CastCode()
        {
            if(_working||Caster==null)return;
            _working=true;Summons.Register(Caster,this);
            RestoreGuardians();
            _normal=_=>RestoreGuardians();_death=_=>Teardown(true);
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved,_normal);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath,_death);
        }
        private void RestoreGuardians()
        {
            if(!_working||Caster==null||!Caster.IsOnField)return;
            while(Summons.Of(Caster).Count<Cap)
            {
                Unit guardian=Summons.Create(Caster);
                if(guardian==null)break;
                guardian.gameObject.AddComponent<Entities.View.SummonCardView>().Bind(guardian);
            }
        }
        public override void StopCode()=>Teardown(false);
        private void Teardown(bool died)
        {
            _working=false;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved,_normal);
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath,_death);
            if(!died)
                foreach(var pet in Summons.Of(Caster))
                {
                    pet.StopAllCoroutines();pet.ActiveNormalCode?.StopCode();pet.isCasting=false;pet.isActive=false;
                    GridManager.Instance?.ForgetSummon(pet);
                }
            Summons.Unregister(Caster);
        }
    }

    public sealed class HorusRoyalHeir : UniquePassiveCode
    {
        private bool _working;
        private Unit _heir;
        private Action<EventContext> _death;
        public string AuraKey=>$"horus_heir_{Caster?.GetEntityId()}";
        public bool Working=>_working&&Caster!=null&&Caster.IsOnField;
        public float Bonus=>Working?Mathf.Min(.55f,.25f+.15f*SummonSupportCombat.PowerRatio(Caster)):0;
        public Unit Heir { get { SelectHeir();return _heir; } }
        public HorusRoyalHeir(PassiveCodeContext context):base(context)=>CodeName="태양의 후계자";
        private bool Eligible(Unit unit)=>unit!=null&&unit!=Caster&&unit.IsOnField&&!unit.IsSummon&&
            unit.IsEnemy==Caster.IsEnemy&&unit.UltimateResourceType==BaseEnums.UltimateResourceType.Mana&&
            unit.ActiveUltimateCode!=null&&unit.ActiveUltimateCode.SupportsUltimateDamage;
        public override void CastCode()
        {
            if(_working||Caster==null)return;
            _working=true;SelectHeir();ActionScheduler.AnyActionStarted+=Started;
            _death=_=>StopCode();Caster.AddListener(BaseEnums.UnitEventType.OnDeath,_death);
        }
        private void Started(Unit actor,ActionScheduler.ActionKind kind,string label)=>SelectHeir();
        private void SelectHeir()
        {
            if(!Working)return;
            if(Eligible(_heir)&&_heir.HasStatusKey(AuraKey))return;
            if(_heir!=null)_heir.RemoveStatusByKey(AuraKey);
            _heir=SummonSupportCombat.Allies(Caster).FirstOrDefault(Eligible);
            if(_heir==null)return;
            _heir.AddStatus(BuffStatus.Create(73520,AuraKey,CodeName,Caster,_heir,new HeirEffect(this),0,isBeneficial:true,
                description:"호루스가 필드에 있는 동안 초월기 피해 증가(특공 비례, 최대55%). 마나형 공격 초월기 아군1명 지정."));
            ActionAnnouncer.AnnouncePassive(Caster,this);
        }
        public override void StopCode()
        {
            _working=false;ActionScheduler.AnyActionStarted-=Started;
            if(_heir!=null)_heir.RemoveStatusByKey(AuraKey);
            _heir=null;Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath,_death);
        }
        private sealed class HeirEffect:BaseEffect
        {
            private readonly HorusRoyalHeir _source;
            public HeirEffect(HorusRoyalHeir source):base(0)=>_source=source;
            public override bool IsBeneficial=>true;
            public override float UltimateDamageBonus(Unit actor)=>actor==Target&&_source.Working?_source.Bonus:0;
        }
    }

    public sealed class SetDesertBlood : UniquePassiveCode
    {
        public const int CounterCap=2;
        private readonly HashSet<int> _handled=new();
        private Coroutine _counter;
        private bool _working;
        private int _budget,_window,_generation,_serial;
        private Action<EventContext> _hit,_normal,_death;
        public int Budget=>_budget;
        public override int AttackForm=>DamageTag.Slash;
        public string GuardKey=>$"set_blood_guard_{Caster?.GetEntityId()}";
        public SetDesertBlood(PassiveCodeContext context):base(context)=>CodeName="사막의 피";
        private bool Working=>_working&&Caster!=null&&Caster.IsOnField;
        public override void CastCode()
        {
            if(_working||Caster==null)return;
            _working=true;_generation++;ResetBudget();
            Caster.AddStatus(BuffStatus.Create(73323,GuardKey,CodeName,Caster,Caster,new BloodGuard(),0,isBeneficial:true,
                description:"출혈 상태인 적에게 받는 피해10% 감소. 적 직접 공격에 단일 반격(공70%)·출혈 부여·흡혈(최대체력3%). 자기 일반행동 사이2회."));
            _hit=OnHit;_normal=_=>ResetBudget();_death=_=>StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken,_hit);
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved,_normal);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath,_death);
        }
        public void ResetBudget(){_budget=CounterCap;_window++;_handled.Clear();}
        private void OnHit(EventContext evt)
        {
            var hit=evt?.DmgCtx;var scheduler=GameManager.Instance?.ActionScheduler;
            if(!Working||Caster.isControlled||_budget<=0||hit==null||hit.ResolvedDamage<=0||!DirectAttack.IsDirect(hit)||
                hit.HasTag(DamageTag.CounterAttack)||hit.Attacker==null||hit.Attacker.IsEnemy==Caster.IsEnemy||
                scheduler?.ExecutingUnit==null||!CombatTargets.AliveEnemies(Caster).Contains(hit.Attacker)||!_handled.Add(scheduler.CurrentActionId))return;
            int window=_window,generation=_generation;Unit target=hit.Attacker;bool reserved=true;
            _budget--;
            void Refund(){if(!reserved)return;reserved=false;if(Working&&generation==_generation&&window==_window)_budget=Mathf.Min(CounterCap,_budget+1);}
            bool queued=scheduler.EnqueueAdditional(Caster,"set_counter_"+(++_serial),"사막의 피 · 반격",()=>
            {
                reserved=false;Caster.isCasting=true;_counter=Caster.StartCoroutine(Counter(target,generation));
            },()=>Working&&generation==_generation&&!Caster.isControlled&&CombatTargets.AliveEnemies(Caster).Contains(target),Refund);
            if(!queued)Refund();
        }
        private IEnumerator Counter(Unit target,int generation)
        {
            try
            {
                var tags=new List<int>{DamageTag.SingleTarget,DamageTag.AdditionalAttack,DamageTag.CounterAttack,DamageTag.ContactAttack,DamageTag.Slash};
                var routing=AttackTargetRouter.Prepare(Caster,new[]{target},BaseEnums.CodeType.Passive,tags,CodeName);
                yield return routing.ResolvePreAttack();
                if(!Working||generation!=_generation||!routing.CanContinue||!DirectAttack.CasterCanContinue(Caster,true)||!Targeting.Announce(Caster,routing.Targets))yield break;
                target=routing.Targets.First();
                bool crit=RollCritical(target,out float multiplier);
                var context=new DamageContext(Caster,Mathf.Max(1,Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack)*.70f*multiplier)),BaseEnums.CodeType.Passive,tags,crit);
                AudioManager.PlayAction(Caster,SfxActionSlot.Passive,SfxCuePhase.Cast,CodeName);
                yield return DirectAttack.HitOne("set.counter",Caster,target,()=>
                {
                    AudioManager.PlayAction(Caster,SfxActionSlot.Passive,SfxCuePhase.Hit,CodeName);
                    target.TakeDamage(context);
                    if(!context.IsCancelled&&target.isActive)ActionDot.Apply(Caster,target,ActionDotKind.Bleed);
                    if(context.ResolvedDamage>0&&Working)Caster.Heal(Mathf.RoundToInt(Caster.HpMax*.03f),Caster);
                },true,presentation:impact=>Effects.Vfx.EgyptianReworkFx.Slash(Caster,target,impact));
            }
            finally{if(generation==_generation&&Caster!=null){Caster.isCasting=false;_counter=null;}}
        }
        public override void StopCode()
        {
            _working=false;_generation++;_budget=0;_handled.Clear();
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken,_hit);
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved,_normal);
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath,_death);
            if(Caster!=null){if(_counter!=null)Caster.StopCoroutine(_counter);Caster.RemoveStatusByKey(GuardKey);Caster.isCasting=false;}
            _counter=null;
        }
        private sealed class BloodGuard:BaseEffect
        {
            public BloodGuard():base(0){}
            public override bool IsBeneficial=>true;
            public override float ReceivingDamageModifier(Unit unit,DamageContext context)=>unit==Target&&context?.Attacker!=null&&context.Attacker.HasStatusKey(ActionDot.Key(ActionDotKind.Bleed)) ? .90f : 1;
        }
    }
}
