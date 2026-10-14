#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Codes.Base;
using Core;
using Effects.Vfx;
using Entities;
using Managers.UI.Core;
using Managers.UI.HUD;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator PresentationFrames(string tag, float seconds)
        {
            float until=Time.time+seconds;
            do { yield return EndOfFrame; Capture(tag); } while(Time.time<until);
        }
        private IEnumerator ScenarioPresentationPolish(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled=true;
            yield return YvPrepareScene(config,x=>enabled=x);
            var actor=AliveAllies().First(); var target=AliveEnemies().First();
            var view=CardMotion.ViewOf(actor); var victim=CardMotion.ViewOf(target);
                            var rng=UnityEngine.Random.state;
                // 모든 음성 선택·피치·볼륨 변주는 배속과 무관하게 전투 난수와 격리된다.
                foreach(float speed in new[]{1f,4f})
                {
                    float restore=Time.timeScale;Time.timeScale=speed;
                    AudioManager.PlayCue("common.crit");AudioManager.PlayCue("surtr.ultimate.resolve");
                    YvCheck("음성 전투 난수 격리 "+speed,"전투 난수 불변",UnityEngine.Random.state.Equals(rng).ToString(),UnityEngine.Random.state.Equals(rng));
                    Time.timeScale=restore;
                }
            float previousDelta=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/60f;
            try
            {
                victim.PlayHitReaction(1f,true,true);
                YvCheck("피격 연출 전투 난수 격리","전투 난수 불변",UnityEngine.Random.state.Equals(rng).ToString(),UnityEngine.Random.state.Equals(rng));
                yield return PresentationFrames("polish_hit",.35f);
                int hits=0; bool done=false;
                IEnumerator Charge() { yield return CardMotion.Charge(actor,target,(p,d)=> {hits++;victim.PlayHitReaction(1f,true,false);ImpactFx.Slam(p,d,new Color(.55f,.72f,.91f));}); done=true; }
                StartCoroutine(Charge()); yield return PresentationFrames("polish_charge",.14f);
                var portrait=actor.currentCell.portraitRenderer;
                int restingLayer=portrait.sortingLayerID;
                YvCheck("돌진 원화 앞 정렬","상대 HUD보다 앞인 CombatHud 레이어",SortingLayer.IDToName(restingLayer),restingLayer==SortingLayer.NameToID("CombatHud"));
                var pause=GameObject.Find("BattlePauseButton").GetComponent<Button>();
                pause.onClick.Invoke(); yield return null;
                Vector3 position=view.transform.position;
                var body=view.transform.Find("Body");
                Vector3 bodyPosition=body.localPosition,bodyScale=body.localScale;var bodyRotation=body.localRotation;
                yield return WaitUnscaled(.35f);
                bool frozen=body.localPosition==bodyPosition&&body.localScale==bodyScale&&body.localRotation==bodyRotation;
                // HUD의 상시 자원 장식은 UI이므로 움직일 수 있다. 전투 몸체/위치는 멈춰야 한다.
                YvCheck("공격 모션 일시정지","위치 불변·피해 없음·동작 미완료",position+"/"+hits,frozen&&view.transform.position==position&&hits==0&&!done);
                yield return CaptureStill("polish_pause"); pause.onClick.Invoke();
                yield return PresentationFrames("polish_charge",.85f);
                YvCheck("돌진 판정 및 복귀","콜백1회·동작 종료·원점",hits+"/"+done+"/"+view.transform.localPosition,hits==1&&done&&!view.IsPerformingMotion&&view.transform.localPosition.sqrMagnitude<.00001f);
                YvCheck("돌진 정렬 복원","원래 원화 레이어",SortingLayer.IDToName(portrait.sortingLayerID),portrait.sortingLayerID==view.LayerId);
                view.PlayEntry(.06f); yield return PresentationFrames("polish_entry",.95f);
                YvCheck("등장 후 복원","활성·원점·유한 좌표",view.transform.localPosition.ToString(),view.gameObject.activeInHierarchy&&view.transform.localPosition.sqrMagnitude<.00001f);
                yield return new WaitForSeconds(.8f);
                var priorMouse=Mouse.current;var pointer=InputSystem.AddDevice<Mouse>("WorldCardMotionVerification");
                bool wasEnabled=priorMouse!=null&&priorMouse.enabled;
                if(wasEnabled)InputSystem.DisableDevice(priorMouse);
                var inputField=FindFirstObjectByType<FieldPointerInput>();
                bool fieldWasEnabled=inputField.enabled;
                inputField.enabled=true;
                Vector3 baseBodyScale=view.Body.localScale;
                try
                {
                    Vector2 point=view.PortraitFocusScreenRect(Camera.main).center;
                    InputSystem.QueueStateEvent(pointer,new MouseState{position=point});yield return WaitUnscaled(.2f);
                    var field=FindFirstObjectByType<FieldPointerInput>();
                    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
                    var picked=typeof(FieldPointerInput).GetMethod("CellWithUnitAt",flags)?.Invoke(null,new object[]{point}) as Cell;
                    bool blocked=(bool)typeof(FieldPointerInput).GetMethod("IsOverUi",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(field,new object[]{point});
                    YvCheck("전장 입력 경로 진단","원화가 해당 유닛으로 판정",$"point={point} current={Mouse.current?.position.ReadValue()} picked={picked?.name} UI={blocked} field={field.enabled}",picked==actor.currentCell&&!blocked);
                    YvCheck("전장 카드 실제 호버","몸체 소폭 확대",view.Body.localScale.ToString(),view.Body.localScale.x>baseBodyScale.x*1.003f);
                    InputSystem.QueueStateEvent(pointer,new MouseState{position=point,buttons=1});yield return WaitUnscaled(.1f);
                    YvCheck("전장 카드 실제 누름","몸체 눌림",view.Body.localScale.ToString(),view.Body.localScale.x<baseBodyScale.x*.995f);
                    YvCheck("전장 카드 누름 명도","실제 원화 RGB 0.86",portrait.color.ToString(),portrait.color.r<=.87f&&portrait.color.g<=.87f);
                    yield return CaptureStill("polish_world_card_press");
                    InputSystem.QueueStateEvent(pointer,new MouseState{position=new Vector2(Screen.width-2,Screen.height-2)});yield return WaitUnscaled(.5f);
                    YvCheck("전장 카드 밖 해제","비율 복원",view.Body.localScale.ToString(),Vector3.Distance(view.Body.localScale,baseBodyScale)<.0001f);
                    var originalCell=actor.currentCell;
                    GameManager.Instance.gameState=BaseClasses.BaseEnums.GameState.Preparation;
                    point=view.PortraitFocusScreenRect(Camera.main).center;
                    InputSystem.QueueStateEvent(pointer,new MouseState{position=point,buttons=1});yield return WaitUnscaled(.1f);
                    for(int step=1;step<=12;step++)
                    {
                        InputSystem.QueueStateEvent(pointer,new MouseState{position=point+Vector2.right*(35f*step/12f),buttons=1});
                        yield return EndOfFrame;Capture("polish_world_drag_live_move");
                    }
                    YvCheck("원화 영역에서 실제 배치 드래그","끌기 시작",DragAndDropManager.Instance.IsDragging().ToString(),DragAndDropManager.Instance.IsDragging());
                    InputSystem.QueueStateEvent(pointer,new MouseState{position=new Vector2(400,Screen.height-60)});
                    float recoverUntil=Time.realtimeSinceStartup+.3f;
                    while(Time.realtimeSinceStartup<recoverUntil){yield return EndOfFrame;Capture("polish_world_drag_live_cancel");}
                    YvCheck("UI 위 배치 드래그 취소","원래 칸·입력 복원",actor.currentCell.name,!DragAndDropManager.Instance.IsDragging()&&actor.currentCell==originalCell);
                    float dragDelta=Time.captureDeltaTime;Time.captureDeltaTime=0f;_deferCaptureEncoding=true;
                    try
                    {
                        foreach(bool swap in new[]{false,true})
                        {
                            var fromCell=actor.currentCell;
                            point=CardMotion.ViewOf(actor).PortraitFocusScreenRect(Camera.main).center;
                            InputSystem.QueueStateEvent(pointer,new MouseState{position=point,buttons=1});yield return WaitUnscaled(.08f);
                            InputSystem.QueueStateEvent(pointer,new MouseState{position=point+Vector2.right*35f,buttons=1});
                            float setup=Time.realtimeSinceStartup+.42f;
                            while(Time.realtimeSinceStartup<setup){yield return EndOfFrame;Capture("polish_world_drag_pickup_live");}
                            var destination=FindObjectsByType<Cell>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                                .First(c=>c.xPos<0&&c.yPos>0&&c!=fromCell&&c.isOccupied==swap);
                            Unit swapped=destination.unit!=null?destination.unit.GetComponent<Unit>():null;
                            Vector2 to=swap?destination.Card.PortraitFocusScreenRect(Camera.main).center:(Vector2)Camera.main.WorldToScreenPoint(destination.transform.position);
                            string tag=swap?"polish_world_drag_swap_live":"polish_world_drag_empty_live";
                            for(int step=1;step<=12;step++)
                            {InputSystem.QueueStateEvent(pointer,new MouseState{position=Vector2.Lerp(point,to,step/12f),buttons=1});yield return EndOfFrame;Capture(tag+"_move");}
                            float hoverUntil=Time.realtimeSinceStartup+.30f;
                            while(Time.realtimeSinceStartup<hoverUntil){yield return EndOfFrame;Capture(tag+"_hover");}
                            YvCheck("전장 "+(swap?"교환":"빈 칸")+" 목적지 표시","목적지 면/배치·교환 표식",
                                destination.name+"/"+destination.DropTargetFeedbackVisible,destination.DropTargetFeedbackVisible);
                            InputSystem.QueueStateEvent(pointer,new MouseState{position=to});
                            float settle=Time.realtimeSinceStartup+.42f;
                            while(Time.realtimeSinceStartup<settle){yield return EndOfFrame;Capture(tag+"_drop");}
                            YvCheck("전장 실제 "+(swap?"교환":"빈 칸 이동"),"드롭 대상 점유·드래그 종료",actor.currentCell.name,actor.currentCell==destination&&!DragAndDropManager.Instance.IsDragging()&&(!swap||swapped.currentCell==fromCell));
                            YvCheck("전장 드롭 뒤 툴팁 보류","포인터 정지 중 숨김",UITooltip.IsShowing(actor.currentCell).ToString(),!UITooltip.IsShowing(actor.currentCell));
                            // 배치판이 접힌 뒤의 실제 카드 위치로 호버한다. 펼친 배치판의 옛 좌표는 교환 뒤 빈 공간이다.
                            Vector2 restoredHover=CardMotion.ViewOf(actor).PortraitFocusScreenRect(Camera.main).center+Vector2.right*12f;
                            InputSystem.QueueStateEvent(pointer,new MouseState{position=restoredHover});
                            yield return WaitUnscaled(.15f);
                            // 화면 합성이 끝난 뒤 읽는다. Update 중 즉시 캡처하면 에디터 뷰포트가 섞인다.
                            yield return EndOfFrame;
                            Capture(tag+"_hover_restored",keepTooltip:true);
                            YvCheck("전장 포인터 이동 뒤 툴팁 복원","8px 이상 이동 뒤 정상 호버",UITooltip.IsShowing(actor.currentCell).ToString(),UITooltip.IsShowing(actor.currentCell));
                            GridManager.Instance.RelocateUnit(actor,fromCell);
                            if(swapped!=null)GridManager.Instance.RelocateUnit(swapped,destination);
                            yield return WaitUnscaled(.4f);
                        }
                    }
                    finally{FlushDeferredCaptures();Time.captureDeltaTime=dragDelta;}
                    GameManager.Instance.gameState=BaseClasses.BaseEnums.GameState.RoundInProgress;
                }
                finally {inputField.enabled=fieldWasEnabled;InputSystem.RemoveDevice(pointer);if(priorMouse!=null&&priorMouse.added){if(wasEnabled)InputSystem.EnableDevice(priorMouse);priorMouse.MakeCurrent();}}
                var game=GameManager.Instance; game.gameState=BaseClasses.BaseEnums.GameState.Preparation;
                yield return null;
                // UI는 실시간 시계다. PNG 압축은 리빌이 끝난 뒤 수행하여 중간 렌더를 누락하지 않는다.
                float uiCaptureDelta=Time.captureDeltaTime;Time.captureDeltaTime=0f;
                _deferCaptureEncoding=true;
                game.uiManager.ShowRewardPanel(new List<RewardDef>{RewardManager.BuildCodeReward(RewardCodeCatalog.Find(808)),RewardManager.BuildCodeReward(RewardCodeCatalog.Find(723)),RewardManager.BuildCodeReward(RewardCodeCatalog.Find(618))});
                var initialReward=RuiFind(GameObject.Find("RewardScreen"),"Reward0");
                var revealGroup=initialReward.GetComponent<CanvasGroup>();
                YvCheck("보상 리빌 시작","alpha 0·보이지 않는 입력 차단",revealGroup.alpha+"/"+revealGroup.blocksRaycasts,revealGroup.alpha==0f&&!revealGroup.blocksRaycasts);
                float minimumPartial=1f;
                float revealUntil=Time.realtimeSinceStartup+.42f;
                while(Time.realtimeSinceStartup<revealUntil)
                {
                    yield return EndOfFrame;
                    Capture("polish_reward_reveal_live");
                    if(revealGroup.alpha>0f)minimumPartial=Mathf.Min(minimumPartial,revealGroup.alpha);
                }
                FlushDeferredCaptures();Time.captureDeltaTime=uiCaptureDelta;
                YvCheck("보상 실시간 리빌","0과 1 사이를 실제 경과",minimumPartial.ToString("F3"),minimumPartial>0f&&minimumPartial<1f);
                yield return PresentationFrames("polish_reward_open",.25f);
                var root=GameObject.Find("RewardScreen"); var card=RuiFind(root,"Reward0");
                var hover=card.GetComponent<UIHoverRing>(); var ev=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                Vector3 cardBase=card.localScale; hover.OnPointerEnter(ev); yield return WaitUnscaled(.15f);
                hover.OnPointerDown(ev); yield return PresentationFrames("polish_card_press",.08f);
                YvCheck("보상 카드 누름 반응","기본 배율보다 작음",card.localScale.ToString(),card.localScale.x<cardBase.x*.995f);
                hover.OnPointerExit(ev); yield return WaitUnscaled(.5f);
                YvCheck("카드 밖 취소 복귀","기본 배율 복원",card.localScale.ToString(),Vector3.Distance(card.localScale,cardBase)<.0001f);
                var button=RuiFind(root,"ConfirmReward").GetComponent<Button>(); button.interactable=true;
                var style=button.GetComponent<UIButtonStyle>(); button.transform.localScale=new Vector3(.9f,.9f,1f);
                style.OnPointerDown(ev); yield return PresentationFrames("polish_button_press",.08f);
                YvCheck("버튼 기본 배율 보존","0.9 기준 누름",button.transform.localScale.ToString(),button.transform.localScale.x<.895f&&button.transform.localScale.z<1f);
                style.OnPointerUp(ev); yield return WaitUnscaled(.5f);
                YvCheck("버튼 누름 해제 복원","설정된 배율 복원",button.transform.localScale.ToString(),Mathf.Abs(button.transform.localScale.x-.9f)<.0001f);
                style.OnPointerDown(ev); yield return WaitUnscaled(.1f); button.gameObject.SetActive(false);
                YvCheck("비활성화 누름 정리","설정된 배율 복원",button.transform.localScale.ToString(),Mathf.Abs(button.transform.localScale.x-.9f)<.0001f);
                button.transform.localScale=Vector3.one; button.gameObject.SetActive(true);button.interactable=false;
                game.uiManager.HideRewardPanel();
                // 정적 상품 화면만으로 리빌 애니메이션을 검수할 수 없다. 실제 시간의 중간 렌더를 남긴다.
                game.uiManager.ShowShopPanel();
                var shopRoot = GameObject.Find("ShopScreen");
                var shopCard = RuiFind(shopRoot,"CodeGood_0");
                var shopReveal = shopCard != null ? shopCard.GetComponent<CanvasGroup>() : null;
                YvCheck("상점 리빌 시작","alpha 0·입력 차단",shopReveal != null ? shopReveal.alpha+"/"+shopReveal.blocksRaycasts : "카드 없음",
                    shopReveal != null && shopReveal.alpha == 0f && !shopReveal.blocksRaycasts);
                float shopDelta=Time.captureDeltaTime;
                Time.captureDeltaTime=0f; _deferCaptureEncoding=true;
                float shopPartial=1f;
                float shopUntil=Time.realtimeSinceStartup+.75f;
                while(Time.realtimeSinceStartup<shopUntil)
                {
                    yield return EndOfFrame; Capture("polish_shop_reveal_live");
                    if(shopReveal != null && shopReveal.alpha>0f)shopPartial=Mathf.Min(shopPartial,shopReveal.alpha);
                }
                FlushDeferredCaptures(); Time.captureDeltaTime=shopDelta;
                YvCheck("상점 실시간 리빌","0과 1 사이를 실제 경과",shopPartial.ToString("F3"),shopPartial>0f&&shopPartial<1f);
                game.uiManager.HideShopPanel();
            }
            finally
            {
                FlushDeferredCaptures();Time.captureDeltaTime=previousDelta;
                if(BattleHud.IsPaused)GameObject.Find("BattlePauseButton")?.GetComponent<Button>()?.onClick.Invoke();
                GameManager.Instance.enabled=enabled;
            }
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }
    }
}
#endif
