#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Entities;
using Managers.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioBattlePacing(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();bool enabledBefore=true;
            yield return YvPrepareScene(config,x=>enabledBefore=x);
            var game=GameManager.Instance;var scheduler=game.ActionScheduler;var actor=AliveAllies().First();var enemy=AliveEnemies().First();
            float savedCaptureDelta=Time.captureDeltaTime;
            bool savedDeferred=_deferCaptureEncoding;
            try
            {
            foreach(float speed in new[]{1f,2f,4f,4f})
            {
                bool fixedFour=speed==4f&&Time.captureDeltaTime>0f;
                _deferCaptureEncoding=true;
                float PhaseClock()=>fixedFour?Time.time/(speed*.5f):Time.realtimeSinceStartup;
                scheduler.EndRound();scheduler.BeginRound();foreach(var u in AliveAllies().Concat(AliveEnemies()))u.ClearUltimateResource();
                BattleHud.TrySetSpeedMultiplier(speed);
                // 직전 묶음 PNG 저장 시간과 배속 변경 전 delta가 예고 시간을 소진하지 않도록 새 프레임에서 시작한다.
                yield return null;yield return null;
                float announced=-1,started=-1,hit=-1,finished=-1,nextStarted=-1;
                string presentedName=null;
                Action<ActionAnnouncement> announce=x=>{if(x.Unit==actor&&x.FollowsAction&&announced<0){announced=PhaseClock();presentedName=x.ActionName;}};
                Action<Unit,ActionScheduler.ActionKind,string> begin=(u,k,n)=>{if(u==actor&&started<0)started=PhaseClock();};
                Action<Unit,ActionScheduler.ActionKind> end=(u,k)=>{if(u==actor&&finished<0)finished=PhaseClock();};
                Action<DamageResolvedContext> damage=x=>{if(x.Attacker==actor&&hit<0)hit=PhaseClock();};
                ActionAnnouncer.Announced+=announce;ActionScheduler.AnyActionStarted+=begin;ActionScheduler.AnyActionFinished+=end;Unit.AnyDamageDealt+=damage;
                scheduler.EnqueueSpecial(actor,"pacing.first","페이싱 실제 일반공격",actor.CastNormalCode);
                scheduler.EnqueueSpecial(actor,"pacing.next","다음 행동",()=>nextStarted=PhaseClock());
                scheduler.Tick(Time.deltaTime);
                var immediateRow=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                    .FirstOrDefault(x=>x.name=="InsertedAction"&&x.GetComponentsInChildren<TMP_Text>(true).Any(text=>text.text==presentedName));
                bool immediateReadable=immediateRow!=null&&immediateRow.rect.width>439&&immediateRow.GetComponent<CanvasGroup>().alpha>.5f&&
                    immediateRow.Find("InsertedDetails").GetComponent<CanvasGroup>().alpha>.5f;
                RuiCheck(speed+"× 예고 발행 즉시 내용","폭440·면/글자알파0.5이상",immediateReadable.ToString(),immediateReadable);
                yield return null;
                var presentedRow=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                    .FirstOrDefault(x=>x.name=="InsertedAction"&&x.GetComponentsInChildren<TMP_Text>(true).Any(text=>text.text==presentedName));
                bool resultTextReadable=true;int resultTextFrames=0;
                RuiCheck(speed+"× 예고 우선","이름 표시·실행/피해 없음",$"announce={announced},start={started},hit={hit}",announced>=0&&started<0&&hit<0);
                if(speed==1)
                {
                    int hp=enemy.HpCurr,count=scheduler.ActionCount,turns=scheduler.TurnsTaken;
                    GameObject.Find("BattlePauseButton").GetComponent<Button>().onClick.Invoke();
                    for(int i=0;i<4;i++){scheduler.Tick(Time.deltaTime);yield return WaitUnscaled(.1f);}
                    RuiCheck("별도 일시정지","HP·행동·턴 불변·timeScale0",$"paused={BattleHud.IsPaused},time={Time.timeScale}",BattleHud.IsPaused&&Time.timeScale==0&&enemy.HpCurr==hp&&scheduler.ActionCount==count&&scheduler.TurnsTaken==turns&&started<0);
                    yield return CaptureStill("battle_pause_preview");
                    game.uiManager.HandleEscape();yield return WaitUnscaled(.1f);game.uiManager.HandleEscape();
                    RuiCheck("메뉴 닫기 수동정지 보존","정지 유지",Time.timeScale.ToString(),BattleHud.IsPaused&&Time.timeScale==0);
                    GameObject.Find("BattlePauseButton").GetComponent<Button>().onClick.Invoke();
                }
                float deadline=Time.realtimeSinceStartup+20;
                float nextCapture=0f;
                while(nextStarted<0&&Time.realtimeSinceStartup<deadline)
                {
                    scheduler.Tick(Time.deltaTime);
                    yield return EndOfFrame;
                    if(finished>=0&&presentedRow!=null&&presentedRow.GetComponent<CanvasGroup>().alpha>.25f)
                    {
                        resultTextFrames++;
                        resultTextReadable&=presentedRow.Find("InsertedDetails").GetComponent<CanvasGroup>().alpha>.5f;
                    }
                    if(fixedFour||Time.realtimeSinceStartup>=nextCapture)
                    {
                        nextCapture=Time.realtimeSinceStartup+1f/60f;
                        Capture((fixedFour?"pacing_fixed4_":"pacing_speed"+speed+"_live_")+(started<0?"announced":hit<0?"acting":finished<0?"impact":"result"));
                    }
                }
                RuiCheck(speed+"× 단계 분리","예고→실행→실제피해→종료→다음",$"{announced:F2}/{started:F2}/{hit:F2}/{finished:F2}/{nextStarted:F2}",started>announced&&hit>=started&&finished>=hit&&nextStarted>finished);
                RuiCheck(speed+"× 결과 카드 내용 유지","종료 면과 글자 함께 fade",$"readable={resultTextReadable},frames={resultTextFrames}",resultTextReadable&&resultTextFrames>0);
                RuiCheck(speed+"× 예고 확인 시간",$"명목{ActionScheduler.PreviewSeconds/(speed*.5f):F2}초·프레임 오차10% 이내",(started-announced).ToString("F3"),started-announced>=ActionScheduler.PreviewSeconds/(speed*.5f)*.9f);
                RuiCheck(speed+"× 결과 확인 시간",$"최소{ActionScheduler.ResultSeconds/(speed*.5f):F2}초",(nextStarted-finished).ToString("F3"),nextStarted-finished>=ActionScheduler.ResultSeconds/(speed*.5f)*.9f);
                yield return CaptureStill("pacing_speed"+speed);
                FlushDeferredCaptures();_deferCaptureEncoding=savedDeferred;
                ActionAnnouncer.Announced-=announce;ActionScheduler.AnyActionStarted-=begin;ActionScheduler.AnyActionFinished-=end;Unit.AnyDamageDealt-=damage;
                actor.StopAllCoroutines();actor.isCasting=false;
                if(speed==4f)Time.captureDeltaTime=1f/60f;
            }
            Time.captureDeltaTime=savedCaptureDelta;
            // 고정 배너 수명보다 긴 실제 스케줄러 행동에서 사용자에게 보이는 폭을 검사한다.
            scheduler.EndRound();scheduler.BeginRound();
            foreach(var u in AliveAllies().Concat(AliveEnemies()))u.ClearUltimateResource();
            BattleHud.TrySetSpeedMultiplier(2);
            const string holdLabel="확장 유지 확인";
            string holdDisplayName=null;
            Action<ActionAnnouncement> holdAnnouncement=x=>{if(x.Unit==actor&&x.FollowsAction)holdDisplayName=x.ActionName;};
            RectTransform HeldRow() => UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                .FirstOrDefault(x=>x.name=="InsertedAction" && x.GetComponentsInChildren<TMP_Text>(true).Any(text=>text.text==holdDisplayName));
            IEnumerator LongCast()
            {
                actor.isCasting=true;
                yield return new WaitForSeconds(4f);
                actor.isCasting=false;
            }
            ActionAnnouncer.Announced+=holdAnnouncement;
            scheduler.EnqueueSpecial(actor,"pacing.hold",holdLabel,()=>actor.StartCoroutine(LongCast()));
            scheduler.Tick(Time.deltaTime);
            ActionAnnouncer.Announced-=holdAnnouncement;
            float until=Time.time+2.4f;
            while(Time.time<until){scheduler.Tick(Time.deltaTime);yield return null;}
            RectTransform held=HeldRow();
            RuiCheck("확장 슬롯 행동 중 유지","기존 배너 수명 이후에도 폭440",held!=null?held.rect.width.ToString("F1"):"없음",actor.isCasting&&held!=null&&held.rect.width>439);
            for(int i=0;i<4;i++)ActionAnnouncer.Announce(actor,ActionAnnouncementKind.Passive,"검수 중첩 공지"+i);
            yield return null;
            RuiCheck("패시브 공지 중 실행 슬롯 보존","실행 중 슬롯 유지",HeldRow()!=null?"유지":"제거",HeldRow()!=null);
            GameObject.Find("BattlePauseButton").GetComponent<Button>().onClick.Invoke();
            yield return WaitUnscaled(.35f);
            RuiCheck("정지 중 확장 보존","폭440·행동 미완료",held!=null?held.rect.width.ToString("F1"):"없음",actor.isCasting&&held!=null&&held.rect.width>439);
            yield return CaptureStill("action_expansion_held_paused");
            GameObject.Find("BattlePauseButton").GetComponent<Button>().onClick.Invoke();
            until=Time.realtimeSinceStartup+10;
            int doneCount=scheduler.ActionCount;
            while(scheduler.ActionCount==doneCount&&Time.realtimeSinceStartup<until){scheduler.Tick(Time.deltaTime);yield return null;}
            yield return WaitUnscaled(.3f);
            RuiCheck("행동 종료 후 확장 정리","완료한 슬롯 제거",HeldRow()!=null?"잔존":"제거",scheduler.ActionCount>doneCount&&HeldRow()==null);
            // 예고 도중 실행 조건이 사라져도 종료 이벤트를 기다리며 남지 않는다.
            scheduler.EndRound();scheduler.BeginRound();bool canRun=true;
            scheduler.EnqueueSpecial(actor,"pacing.cancel",holdLabel,()=>{},()=>canRun);
            scheduler.Tick(Time.deltaTime);canRun=false;
            until=Time.time+1f;
            while(Time.time<until){scheduler.Tick(Time.deltaTime);yield return null;}
            RuiCheck("취소된 예고 정리","실행하지 않은 슬롯 제거",HeldRow()!=null?"잔존":"제거",HeldRow()==null);
            scheduler.EndRound();game.gameState=BaseEnums.GameState.Preparation;GridManager.Instance.OnRoundEnd();game.enabled=enabledBefore;
            BattleHud.TrySetSpeedMultiplier(1);RuiFinish();
            }
            finally
            {
                FlushDeferredCaptures();_deferCaptureEncoding=savedDeferred;Time.captureDeltaTime=savedCaptureDelta;
            }
        }
    }
}
#endif
