#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Effects.Vfx;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static IEnumerator QaParentAttack(Unit attacker, Unit jean, JeanAttackProbe probe)
        {
            try { yield return DebugJeanDirectAttack(attacker,new[]{jean},false,"qa_scheduler_counter",probe); }
            finally { if(attacker!=null)attacker.isCasting=false; }
        }

        private IEnumerator ScenarioCoreScheduler(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            var jean=AliveAllies().First(unit=>unit.ID==JeanId);var attacker=AliveEnemies().First();
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            attacker.RemoveShield(attacker.ShieldCurr);attacker.ModifyHp(1);
            CfNoCrit(jean);jean.RemoveShield(jean.ShieldCurr);
            jean.RestoreHpOutsideCombat(jean.HpMax);
            ActionDot.Apply(attacker,jean,ActionDotKind.Bleed,inheritedHit:true);
            var bleed=jean.GetAllStatuses().First(status=>status.Key==ActionDot.Key(ActionDotKind.Bleed));
            var scheduler=GameManager.Instance.ActionScheduler;var probe=new JeanAttackProbe();
            int parentStart=-1,parentEnd=-2,childStart=-3,childEnd=-4,bleedTicks=0;
            bool childFinished=false,overlap=false,parentEarly=false;
            void Started(Unit actor,ActionScheduler.ActionKind kind,string label)
            {
                if(actor==attacker)parentStart=scheduler.CurrentActionId;
                else if(actor==jean&&kind==ActionScheduler.ActionKind.PriorityAdditional)childStart=scheduler.CurrentActionId;
                else if(childStart>=0&&!childFinished)overlap=true;
            }
            void Finished(Unit actor,ActionScheduler.ActionKind kind)
            {
                if(actor==attacker){parentEnd=scheduler.CurrentActionId;parentEarly=!childFinished;}
                if(actor==jean&&kind==ActionScheduler.ActionKind.PriorityAdditional){childEnd=scheduler.CurrentActionId;childFinished=true;}
            }
            void Damage(DamageResolvedContext hit)
            {if(hit.Target==jean&&hit.DamageContext.IsDamageOverTime&&hit.DamageContext.StatusVfxKey=="bleed")bleedTicks++;}
            ActionScheduler.AnyActionStarted+=Started;ActionScheduler.AnyActionFinished+=Finished;Unit.AnyDamageDealt+=Damage;
            try
            {
                yield return CfRun(s=>s.EnqueueAdditional(attacker,"qa_parent","검증 공격",()=>
                { attacker.isCasting=true;VfxRunner.Run(QaParentAttack(attacker,jean,probe)); }),()=>parentEnd>=0,25f);
                YvCheck("선제 처치 실제 원래 공격 취소","반격 처치·원공격 없음",probe.CanceledBeforeOriginal.ToString(),probe.CanceledBeforeOriginal&&!probe.OriginalApplied&&!attacker.isActive);
                YvCheck("선제 개입 부모·자식 종료ID 보존","시작ID=종료ID·서로 다름",$"{parentStart}/{parentEnd};{childStart}/{childEnd}",parentStart>=0&&parentStart==parentEnd&&childStart==childEnd&&childStart!=parentStart);
                YvCheck("선제 개입 끝까지 부모 종료·다음 행동 대기","겹침·조기종료 없음",$"overlap={overlap},early={parentEarly}",!overlap&&!parentEarly&&childFinished);
                YvCheck("선제 반격 출혈 완료 틱·기간 보존","틱1회·대상 일반행동 기간2",$"{bleedTicks}/{bleed.RemainingTurns}",bleedTicks==1&&bleed.RemainingTurns==2);
                YvCheck("선제 개입 실행 문맥 정리","ExecutingUnit null",scheduler.ExecutingUnit?.UnitName??"null",scheduler.ExecutingUnit==null);
                var hud=GameManager.Instance.uiManager.GetType().GetField("_hud",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(GameManager.Instance.uiManager);
                var rows=hud?.GetType().GetField("_queue",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(hud);
                var announcements=rows==null?null:typeof(Managers.UI.HUD.ActionQueuePanel).GetField("_announcements",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(rows) as IEnumerable;
                int holding=announcements?.Cast<object>().Count(row=>(bool)row.GetType().GetProperty("HoldingAction").GetValue(row))??-1;
                YvCheck("선제 개입 행동서열 확장 닫힘","행동 종료 후 유지슬롯0",holding.ToString(),holding==0);
                yield return CaptureStill("core_scheduler_counter_completed");
            }
            finally
            {
                ActionScheduler.AnyActionStarted-=Started;ActionScheduler.AnyActionFinished-=Finished;Unit.AnyDamageDealt-=Damage;
                OsFinish(enabled);
            }
        }

        private IEnumerator ScenarioCoreSchedulerSabah(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            var sabah=AliveAllies().First(unit=>unit.ID==SabahId);var notices=new List<ActionAnnouncement>();
            void Announced(ActionAnnouncement notice){if(notice.Unit==sabah)notices.Add(notice);}
            ActionAnnouncer.Announced+=Announced;
            try
            {
                for(int i=0;i<3;i++)
                {
                    bool completed=false;
                    void Finished(Unit actor,ActionScheduler.ActionKind kind){if(actor==sabah&&kind==ActionScheduler.ActionKind.Normal)completed=true;}
                    ActionScheduler.AnyActionFinished+=Finished;
                    try {yield return CfRun(s=>s.AdvanceAction(sabah,1f),()=>completed);}
                    finally {ActionScheduler.AnyActionFinished-=Finished;}
                }
                var third=notices.LastOrDefault(notice=>notice.Kind==ActionAnnouncementKind.Substitute);
                YvCheck("사바흐 세 번째 대체행동 실제 공지명","번개 베기",third.ActionName??"없음",third.ActionName=="번개 베기");
                yield return CaptureStill("core_scheduler_sabah_substitute_completed");
            }
            finally {ActionAnnouncer.Announced-=Announced;OsFinish(enabled);}
        }

        private IEnumerator ScenarioCoreSchedulerAbort(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            var parent=AliveEnemies().First();var child=AliveAllies().First(unit=>unit.ID==JeanId);
            var next=AliveAllies().First(unit=>unit!=child);var scheduler=GameManager.Instance.ActionScheduler;
            IEnumerator WaitInterrupted(){yield return new WaitForSeconds(99f);}
            IEnumerator Parent()
            {
                yield return scheduler.RunPreAttackInterrupt(child,"검증 개입 중단",WaitInterrupted);
                parent.isCasting=false;
            }
            foreach(bool externalStop in new[]{false,true})
            {
                bool entered=false,followed=false,ended=false;int warnings=0;
                Coroutine coroutine=null;
                void Started(Unit actor,ActionScheduler.ActionKind kind,string name){if(actor==child)entered=true;}
                void Finished(Unit actor,ActionScheduler.ActionKind kind){if(actor==parent)ended=true;}
                void Warning(string text,string stack,LogType type){if(type==LogType.Warning&&text.Contains("[ActionScheduler] 선제 개입"))warnings++;}
                ActionScheduler.AnyActionStarted+=Started;ActionScheduler.AnyActionFinished+=Finished;Application.logMessageReceived+=Warning;
                try
                {
                    scheduler.BeginRound();
                    scheduler.EnqueueAdditional(parent,"qa_stuck","검증 중단",()=>{parent.isCasting=true;coroutine=parent.StartCoroutine(Parent());});
                    scheduler.EnqueueAdditional(next,"qa_after_stuck","검증 다음 행동",()=>followed=true);
                    bool stopped=false;float deadline=Time.realtimeSinceStartup+25f;
                    while(!followed&&Time.realtimeSinceStartup<deadline)
                    {
                        scheduler.Tick(Time.deltaTime);
                        if(externalStop&&entered&&!stopped&&coroutine!=null){parent.StopCoroutine(coroutine);stopped=true;}
                        yield return null;
                    }
                    YvCheck(externalStop?"외부 중단 개입 복구":"장시간 개입 복구","경고1·부모종료·다음행동",$"{warnings}/{ended}/{followed}",warnings==1&&ended&&followed);
                    YvCheck("중단 개입 후 실행 문맥 복구","ExecutingUnit null",scheduler.ExecutingUnit?.UnitName??"null",scheduler.ExecutingUnit==null);
                    scheduler.EndRound();
                }
                finally {ActionScheduler.AnyActionStarted-=Started;ActionScheduler.AnyActionFinished-=Finished;Application.logMessageReceived-=Warning;}
            }
            yield return CaptureStill("core_scheduler_abort_recovered");OsFinish(enabled);
        }
    }
}
#endif
