#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private sealed class QaLifecycleEffect:BaseEffect
        {
            private readonly Action _turn,_remove;
            public QaLifecycleEffect(Action turn=null,Action remove=null):base(0){_turn=turn;_remove=remove;}
            public override void OnOwnerTurn()=>_turn?.Invoke();
            public override void OnRemove()=>_remove?.Invoke();
        }
        private IEnumerator ScenarioCoreSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            var source=AliveAllies().First();var ally=AliveAllies()[1];var enemies=AliveEnemies();
            void Check(string name,bool ok,string value="")=>YvCheck(name,"사양 충족",value,ok);
            source.TryGrantRewardCode(704);source.CastPassiveCode();source.ModifyHp(source.HpMax-100);
            int hpBeforeLifesteal=source.HpCurr;
            var foe=enemies[0];foe.RemoveShield(foe.ShieldCurr);foe.ModifyHp(10);
            var hit=new DamageContext(source,5000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage});
            foe.TakeDamage(hit);
            Check("오버킬 실피해 체력10으로 제한",hit.ResolvedDamage==10&&hit.HpDamage==10,hit.ResolvedDamage+"/"+hit.HpDamage);
            Check("오버킬 흡혈 실제 피해10의8%만 회복",source.HpCurr-hpBeforeLifesteal==1,(source.HpCurr-hpBeforeLifesteal).ToString());
            source.RemoveRewardCode(704);
            int shieldEvents=0;void Shield(Unit target,Unit caster,int amount){if(target==foe)shieldEvents++;}
            Unit.AnyShieldGranted+=Shield;
            int corpseShield=foe.ShieldCurr;
            try {foe.AddShield(100,source);foe.SetShield(100,source);}
            finally {Unit.AnyShieldGranted-=Shield;}
            Check("죽은 유닛 보호막·이벤트 차단",foe.ShieldCurr==corpseShield&&shieldEvents==0);
            var dying=enemies[1];dying.RemoveShield(dying.ShieldCurr);dying.ModifyHp(10);
            int deaths=0,kills=GameManager.Instance.KillCount;bool blocked=false,deathHealBlocked=false;
            void Died(Unit unit,Unit attacker)
            {
                if(unit!=dying)return;
                deaths++;
                if(deaths==1)
                {
                    var nested=new DamageContext(source,100,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage});
                    unit.TakeDamage(nested);blocked=nested.IsCancelled;unit.Heal(10000,source);deathHealBlocked=unit.HpCurr<=0;unit.Die(source);
                }
            }
            Unit.AnyUnitDied+=Died;
            try {dying.TakeDamage(new DamageContext(source,5000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));}
            finally {Unit.AnyUnitDied-=Died;}
            Check("사망 재진입·처치 보상 정확히1회",deaths==1&&blocked&&GameManager.Instance.KillCount==kills+1,deaths+"/"+(GameManager.Instance.KillCount-kills));
            Check("사망 처리 중 치유로 시체 체력 생성 차단",deathHealBlocked);
            int bTicks=0,bRemoved=0;
            ally.AddStatus(BuffStatus.Create(997901,"qa_turn_a","검증 A",source,ally,new QaLifecycleEffect(turn:()=>ally.RemoveStatusByKey("qa_turn_b")),duration:3));
            ally.AddStatus(BuffStatus.Create(997902,"qa_turn_b","검증 B",source,ally,new QaLifecycleEffect(turn:()=>bTicks++,remove:()=>bRemoved++),duration:3));
            ally.BeginTurn();
            Check("정화된 상태의 뒤늦은 틱 차단",bTicks==0&&bRemoved==1,bTicks+"/"+bRemoved);
            ally.RemoveStatusByKey("qa_turn_a");
            ally.DebugClearStatuses();
            int removedA=0,removedB=0,added=0;float defenseBefore=ally.DefCurr;
            ally.AddStatus(BuffStatus.Create(997908,"qa_clear_stat","검증 방어",source,ally,new StatModifierEffect(BaseEnums.StatType.Defense,100f)));
            ally.AddStatus(BuffStatus.Create(997903,"qa_clear_b","검증 B",source,ally,new QaLifecycleEffect(remove:()=>removedB++)));
            ally.AddStatus(BuffStatus.Create(997904,"qa_clear_a","검증 A",source,ally,new QaLifecycleEffect(remove:()=>
            {
                removedA++;ally.RemoveStatusByKey("qa_clear_b");ally.DebugClearStatuses();
                if(ally.TryAddStatus(BuffStatus.Create(997905,"qa_clear_new","검증 새 상태",source,ally,new QaLifecycleEffect())))added++;
            })));
            ally.DebugClearStatuses();
            Check("전체 정리 재진입·재부착 차단",removedA==1&&removedB==1&&added==0&&!ally.GetAllStatuses().Any(),removedA+"/"+removedB+"/"+added);
            Check("전체 정리 후 파생 방어력 즉시 복원",Mathf.Approximately(ally.DefCurr,defenseBefore),ally.DefCurr.ToString());
            ally.ModifyHp(1);
            ally.AddStatus(BuffStatus.Create(997906,"qa_hp_shrink","검증 체력 감소",source,ally,new StatModifierEffect(BaseEnums.StatType.MaxHp,-.75f,percent:true),isBeneficial:true));
            Check("살아 있는 체력1 재계산 보존",ally.HpCurr==1&&ally.isActive,ally.HpCurr.ToString());
            ally.RemoveStatusByKey("qa_hp_shrink");ally.RestoreHpOutsideCombat(ally.HpMax);
            var existing=BuffStatus.Create(997907,"qa_extend","검증 연장",source,ally,new QaLifecycleEffect(),duration:2,stackPolicy:BaseEnums.StatusStackPolicy.ExtendDuration,category:BaseEnums.StatusCategory.Negative);
            ally.AddStatus(existing);UnitStatus reported=null;
            void Negative(Unit caster,Unit target,UnitStatus status){if(target==ally&&status.Key=="qa_extend")reported=status;}
            Unit.AnyNegativeStatusGranted+=Negative;
            try {ally.AddStatus(BuffStatus.Create(997907,"qa_extend","검증 연장",source,ally,new QaLifecycleEffect(),duration:2,stackPolicy:BaseEnums.StatusStackPolicy.ExtendDuration,category:BaseEnums.StatusCategory.Negative));}
            finally {Unit.AnyNegativeStatusGranted-=Negative;}
            Check("기간 연장 알림 실제 상태 참조",ReferenceEquals(reported,existing)&&existing.RemainingTurns==4,existing.RemainingTurns.ToString());
            ally.RemoveStatusByKey("qa_extend");
            bool restored=false,caught=false;
            IEnumerator Fault(){yield return null;throw new InvalidOperationException("검증 정리 경로");}
            IEnumerator Parent(){try {yield return Fault();}finally {restored=true;}}
            yield return ExecuteGuarded(Parent(),_=>caught=true);
            Check("검증 자식 실패 시 부모 정리 실행",caught&&restored);
            var chandra=AliveAllies().First(unit=>unit.ID==60);
            chandra.SetCombatResourceMaximum(Codes.Passive.ChandraNishakara.ResourceId,2,true);
            chandra.AddCombatResource(Codes.Passive.ChandraNishakara.ResourceId,1);
            chandra.RemoveShield(chandra.ShieldCurr);chandra.ModifyHp(10);
            int pureHealing=0,overHealing=0;
            void Healed(Unit target,Unit caster,int pure,int over){if(target==chandra){pureHealing+=pure;overHealing+=over;}}
            Unit.AnyHealed+=Healed;
            try {chandra.TakeDamage(new DamageContext(source,500000,BaseEnums.CodeType.Effect,new(){DamageTag.TrueDamage}));}
            finally {Unit.AnyHealed-=Healed;}
            Check("치명상 방지 초과피해의 가짜 순수치유 차단",chandra.isActive&&chandra.HpCurr>0&&pureHealing==chandra.HpCurr&&overHealing==0,pureHealing+"/"+chandra.HpCurr+"/"+overHealing);
            yield return CaptureStill("core_safety_completed");OsFinish(enabled);
        }
    }
}
#endif
