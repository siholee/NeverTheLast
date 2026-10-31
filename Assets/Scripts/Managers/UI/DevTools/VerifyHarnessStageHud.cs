#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Effects;
using Core;
using Entities;
using Entities.View;
using Managers.Stage;
using Managers.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static Rect ShScreen(RectTransform rect, bool world=false)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 a=RectTransformUtility.WorldToScreenPoint(world?Camera.main:null,corners[0]);
            Vector2 b=RectTransformUtility.WorldToScreenPoint(world?Camera.main:null,corners[2]);
            return Rect.MinMaxRect(a.x,Screen.height-b.y,b.x,Screen.height-a.y);
        }
        private void ShRect(string key,Rect actual,Rect css)
        {
            float scale=Screen.height/1080f;
            Rect expected=new(css.x*scale,css.y*scale,css.width*scale,css.height*scale);
            float error=Mathf.Max(Mathf.Abs(actual.x-expected.x),Mathf.Abs(actual.y-expected.y),Mathf.Abs(actual.width-expected.width),Mathf.Abs(actual.height-expected.height));
            RecordMechanic("sh_rect_"+key,"시안 CSS 위치/크기 오차≤2px",$"actual={actual}; expected={expected}; maxError={error:R}",error<=2f);
        }
        private void ShOrder(string tag)
        {
            var scheduler=GameManager.Instance.ActionScheduler;
            var queue=GameObject.Find("ActionQueue");
            var links=queue.GetComponentsInChildren<StageUnitFocus>().Where(l=>l.name=="OrderSlot"&&l.BoundUnit!=null)
                .OrderBy(l=>ShScreen(l.GetComponent<RectTransform>()).y).ToArray();
            var expected=scheduler.ForecastOrderCached(7).Select(r=>r.Unit).ToList();
            Unit current=scheduler.ActingUnit;
            if(current!=null)
            {
                // 실제 실행자는 forecast에서 이미 빠졌으며, 별도의 현재 행동 슬롯을 차지한다.
                expected.Insert(0,current);expected=expected.Take(7).ToList();
            }
            RecordMechanic("sh_order_"+tag,"실제 현재 행동자 뒤에 scheduler 예측의 동일 순서",string.Join(",",links.Select(l=>l.BoundUnit.ID))+" | "+string.Join(",",expected.Select(u=>u.ID)),
                links.Select(l=>l.BoundUnit).SequenceEqual(expected));
        }
        private IEnumerator ScenarioStageHudVisualSd(VerifyScenarioRequest config)
        {
            using var seed = CombatRandom.BeginSeed(20261029);
            var randomState = Random.state;
            Random.InitState(20261029);
            bool enabled=GameManager.Instance.enabled;
            yield return S8Prepare(config,startRound:true);
            var game=GameManager.Instance;var scheduler=game.ActionScheduler;
            Unit actionUnit=null;string actionLabel=null;ActionScheduler.ActionKind actionKind=ActionScheduler.ActionKind.Normal;
            void Started(Unit u,ActionScheduler.ActionKind k,string label){actionUnit=u;actionKind=k;actionLabel=label;}
            ActionScheduler.AnyActionStarted+=Started;
            try
            {
                yield return WaitUnscaled(.4f);
                float wide=Screen.width/(Screen.height/1080f);
                ShRect("topbar",ShScreen(GameObject.Find("TopStatusBar").GetComponent<RectTransform>()),new Rect(24,20,wide-48,62));
                var queue=GameObject.Find("ActionQueue").GetComponent<RectTransform>();
                var qrect=ShScreen(queue);ShRect("timeline",qrect,new Rect(24,100,94,524));
                var meter=GameObject.Find("DamageMeter").GetComponent<RectTransform>();
                ShRect("tally",ShScreen(meter),new Rect(wide-254,100,230,282));
                var header=GameObject.Find("TopStatusBar").transform;
                var stageText=header.Find("RoundBlock/RoundLabel").GetComponent<TMP_Text>();
                RecordMechanic("sh_stage_font","시안 스테이지26px",stageText.fontSize.ToString(),Mathf.Abs(stageText.fontSize-26)<.01f);
                ShRect("speed",ShScreen(header.Find("Navigation/SpeedButton").GetComponent<RectTransform>()),new Rect(wide-252,31,40,40));
                ShRect("pause",ShScreen(header.Find("Navigation/BattlePauseButton").GetComponent<RectTransform>()),new Rect(wide-194,31,40,40));
                ShRect("settings",ShScreen(header.Find("Navigation/SettingsButton").GetComponent<RectTransform>()),new Rect(wide-78,31,40,40));
                var treasury=GameObject.Find("PartyTreasury");
                RecordMechanic("sh_treasury_visible","실제 개전·재보 편성에서 가운데 재보 표시",treasury?.name??"없음",treasury!=null);
                if(treasury!=null)ShRect("treasury",ShScreen(treasury.GetComponent<RectTransform>()),new Rect(wide*.5f-68,912,136,150));
                if(treasury!=null)
                {
                    var tip=treasury.GetComponentInChildren<StageTreasuryTooltip>();tip.Hide();yield return null;
                    RecordMechanic("sh_treasury_no_hover","호버 없을 때 재보 설명 숨김",tip.Showing.ToString(),!tip.Showing);
                    Rect tr=ShScreen(treasury.GetComponent<RectTransform>());
                    var pointer=new PointerEventData(EventSystem.current){position=new Vector2(tr.center.x,Screen.height-tr.center.y)};
                    tip.OnPointerEnter(pointer);yield return null;
                    Rect hint=ShScreen(tip.Panel);
                    ShRect("treasury_hint",hint,new Rect(wide*.5f-68,834,136,70));
                    bool cardsClear=AliveAllies().Concat(AliveEnemies()).All(u=>
                    {Rect r=u.currentCell.Card.StageHudHitRect(Camera.main);return !hint.Overlaps(new Rect(r.x,Screen.height-r.yMax,r.width,r.height));});
                    RecordMechanic("sh_treasury_hover","호버 때만 작은 설명·재보 바로 위·하단 카드와 비중첩",$"show={tip.Showing}; rect={hint}; cardsClear={cardsClear}",tip.Showing&&cardsClear);
                    int sdOverlaps=AliveAllies().Concat(AliveEnemies()).Count(u=>
                    {
                        var figure=u.currentCell.Card.StageProxy;Bounds bounds=figure.LowerWorldBounds;bounds.Encapsulate(figure.VisibleHeadWorldBounds);
                        Vector2 a=Camera.main.WorldToScreenPoint(bounds.min),b=Camera.main.WorldToScreenPoint(bounds.max);
                        return hint.Overlaps(Rect.MinMaxRect(a.x,Screen.height-b.y,b.x,Screen.height-a.y));
                    });
                    RecordMechanic("sh_treasury_sd_clear","재보 호버 설명과 양 진영 SD 영역 겹침0",sdOverlaps.ToString(),sdOverlaps==0);
                    yield return CaptureStill("treasury_hover",keepTooltip:true);
                    tip.OnPointerExit(pointer);yield return null;
                    RecordMechanic("sh_treasury_hover_exit","호버 종료 즉시 설명 숨김",tip.Showing.ToString(),!tip.Showing);
                    tip.OnPointerEnter(pointer);pointer.position=Vector2.zero;yield return null;
                    RecordMechanic("sh_treasury_lost_exit","포인터가 벗어나면 Exit 누락 때도 숨김",tip.Showing.ToString(),!tip.Showing);
                }
                ShOrder("idle");
                foreach(var u in AliveAllies().Concat(AliveEnemies()))
                {
                    var view=u.currentCell.Card;RecordStageHudCheck(view,"visualsd",Camera.main);
                    var ring=view.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="UltGauge");
                    // 카드 본체는 공개 히트 영역, 원은 독립 월드 사각형으로 실측한다.
                    Rect hit=view.StageHudHitRect(Camera.main);Rect card=new(hit.x,Screen.height-hit.yMax,hit.width,hit.height);
                    var rr=ShScreen(ring,true);float px=Screen.height/1080f;
                    ShRect("ring_"+u.ID,rr,new Rect(card.x/px+(u.IsEnemy?86:-10),card.y/px-12,42,42));
                    foreach(int amount in new[]{0,Mathf.Max(1,u.ManaMax/2),u.ManaMax})
                    {
                        u.ClearUltimateResource();u.AddUltimateResource(amount);yield return null;
                        var fill=view.GetComponentsInChildren<Image>(true).First(i=>i.name=="LiquidFront");
                        float expected=u.UltimateResourceType==BaseEnums.UltimateResourceType.Passive?1:u.ManaMax>0?(float)u.ManaCurr/u.ManaMax:0;
                        RecordMechanic("sh_resource_"+u.ID+"_"+amount,"실제 자원/최대와 같은 호(상시는 점선)",$"{u.UltimateResourceType}:{u.ManaCurr}/{u.ManaMax}, fill={fill.fillAmount:R}",Mathf.Abs(fill.fillAmount-expected)<.001f);
                    }
                    u.ClearUltimateResource();
                }
                // 실제 상태/피해 API를 통해서만 표시를 만든다. 판정·원시 피해 이벤트는 변경하지 않는다.
                var ally=AliveAllies().First();var enemy=AliveEnemies().First();
                enemy.TakeDamage(new DamageContext(ally,100,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage},false));
                enemy.AddShield(80,enemy);enemy.Heal(40,enemy);
                yield return WaitUnscaled(.1f);
                RecordMechanic("sh_no_chrome_overlap","상단·행동·집계·하단 카드 겹침0",$"queue={qrect}; meter={ShScreen(meter)}",
                    !qrect.Overlaps(ShScreen(meter))&&AliveAllies().Concat(AliveEnemies()).All(u=>!qrect.Overlaps(new Rect(u.currentCell.Card.StageHudHitRect(Camera.main).x,Screen.height-u.currentCell.Card.StageHudHitRect(Camera.main).yMax,u.currentCell.Card.StageHudHitRect(Camera.main).width,u.currentCell.Card.StageHudHitRect(Camera.main).height))));
                var links=FindObjectsByType<StageUnitFocus>(FindObjectsSortMode.None);
                var q=links.First(l=>l.BoundUnit!=null&&!l.BoundUnit.IsEnemy&&l.name=="OrderSlot");
                ally=q.BoundUnit;
                var m=links.First(l=>l.BoundUnit==ally&&l.name.StartsWith("Row"));
                q.OnPointerEnter(new PointerEventData(EventSystem.current));yield return null;
                RecordMechanic("sh_link","카드·SD·서열·집계 동일 유닛 강조",ally.UnitName,ally.currentCell.Card.StageHudFocused&&q.Highlighted&&m.Highlighted);
                q.OnPointerExit(new PointerEventData(EventSystem.current));
                yield return ShFeedback(ally,enemy);
                bool normal=false,ultimate=false;float deadline=Time.realtimeSinceStartup+60;
                while((!normal||!ultimate)&&Time.realtimeSinceStartup<deadline)
                {
                    scheduler.Tick(Time.deltaTime);yield return null;
                    if(actionUnit==null||scheduler.ActingUnit!=actionUnit)continue;
                    if(!normal&&actionKind==ActionScheduler.ActionKind.Normal||normal&&!ultimate&&actionKind==ActionScheduler.ActionKind.Ultimate)
                    {
                        yield return null;ShOrder(actionKind.ToString());
                        var details=GameObject.Find("ActionQueue").GetComponentsInChildren<RectTransform>().First(r=>r.name=="InsertedAction");
                        var texts=details.GetComponentsInChildren<TMP_Text>();
                        RecordMechanic("sh_current_"+actionKind,"현재 행동자 이름·종류·실제 기술명·62px 확장",
                            string.Join("|",texts.Select(t=>t.text)),texts.Any(t=>t.text==actionUnit.UnitName)&&texts.Any(t=>t.text==actionLabel)&&Mathf.Abs(ShScreen(details).height-62*Screen.height/1080f)<=2);
                        foreach(var text in texts)
                        {
                            text.ForceMeshUpdate();
                            bool rendered=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.isVisible);
                            RecordMechanic("sh_current_render_"+text.name+"_"+actionKind,"현재 행동 확장 글자 실제 렌더링",$"{text.text}; font={text.fontSize}; bounds={text.textBounds}",rendered);
                        }
                        yield return CaptureStill(actionKind==ActionScheduler.ActionKind.Normal?"normal_action":"ultimate_action");
                        RecordMechanic("sh_capture_no_hint_"+actionKind,"일반/초월 촬영에 재보 설명 잔류 없음","hidden",!FindObjectsByType<StageTreasuryTooltip>(FindObjectsSortMode.None).Any(t=>t.Showing));
                        if(actionKind==ActionScheduler.ActionKind.Normal){normal=true;foreach(var u in AliveAllies())u.FillUltimateResource();}
                        else ultimate=true;
                    }
                }
                RecordMechanic("sh_capture_coverage","실제 일반행동·초월기 관측",$"normal={normal}; ultimate={ultimate}",normal&&ultimate);
            }
            finally{ActionScheduler.AnyActionStarted-=Started;scheduler.EndRound();game.enabled=enabled;Random.state=randomState;}
        }
        private IEnumerator ShFeedback(Unit source,Unit target)
        {
            yield return WaitUnscaled(1.3f);
            int samples=0,overlaps=0;var seen=new HashSet<string>();
            foreach(string kind in new[]{"normal","critical","shield","heal"})
            {
                if(kind=="shield")target.AddShield(500,target);
                if(kind=="heal")target.Heal(40,source);
                else target.TakeDamage(new DamageContext(source,100,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage},kind=="critical"));
                float until=Time.unscaledTime+.35f;
                bool rendered=false;
                while(Time.unscaledTime<until)
                {
                    yield return EndOfFrame;
                    foreach(var popup in FindObjectsByType<DamagePopup>(FindObjectsSortMode.None))
                    {
                        if(popup.GetComponent<CanvasGroup>().alpha<=.1f)continue;
                        samples++;Rect box=popup.OccupiedRect;
                        if(!CombatFeedbackSurface.NoticeHasRoom(box.center,box.size*.5f,popup))
                        {
                            overlaps++;
                            RecordMechanic("sh_number_collision_"+overlaps, "충돌 진단(기존 비중첩 기대값은 별도 유지)",
                                $"seed=20261029; frame={Time.frameCount}; kind={kind}; popup={popup.name}/{popup.GetInstanceID()}; target={popup.Recipient?.ID}; value={popup.DisplayedAmount}; boxLocal={box}; time={Time.time:R}; unscaled={Time.unscaledTime:R}\n"+
                                CombatFeedbackSurface.DescribeNoticeCollision(box,popup), true);
                        }
                        var label=popup.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Number");
                        label.ForceMeshUpdate();rendered|=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.isVisible);
                        float expected=kind=="critical"?60:kind is "shield" or "heal"?32:46;
                        if(Mathf.Abs(label.fontSize-expected)<.01f)seen.Add(kind);
                    }
                }
                RecordMechanic("sh_number_"+kind,"시안 숫자 크기·실제 피해/흡수/회복 글자 렌더",$"rendered={rendered}; fontSeen={seen.Contains(kind)}",rendered&&seen.Contains(kind));
                yield return WaitUnscaled(1.3f);
            }
            RecordMechanic("sh_number_clearance","일반·치명·보호막·회복 숫자가 얼굴·하단 카드/원·행동서열과 비중첩",$"samples={samples}; overlaps={overlaps}",samples>0&&overlaps==0);
            target.GrantCombatElement(BaseEnums.UnitElement.Hydro,2,source,true,true);
            yield return WaitUnscaled(.2f);yield return EndOfFrame;
            var labels=FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.name=="StatusName").ToArray();
            RecordMechanic("sh_status","실제 원소 부착 상태 글자22px",string.Join("|",labels.Select(t=>t.text+":"+t.fontSize)),labels.Any(t=>Mathf.Abs(t.fontSize-22)<.01f));
        }
        private void ConfigureStageHudEnemies(VerifyScenarioRequest config)
        {
            if ((VisualModeState.Current != VisualMode.Stage && config?.trigger != "s6") || config?.vfxEnemyIds == null || config.vfxEnemyIds.Length == 0) return;
            GridManager grid = GridManager.Instance;
            grid.ClearActiveEnemies();
            for (int i = 0; i < config.vfxEnemyIds.Length; i++)
                grid.SpawnUnit(i < 3 ? 1 : 2, i < 3 ? i + 1 : i - 2, true, config.vfxEnemyIds[i], playEntry: false);
            grid.RefreshFieldLayout();
        }

        private void RecordStageHudCheck(UnitCardView view, string tag, Camera camera)
        {
            if (!StageBattleHud.Visible) return;
            Rect rect = view.StageHudHitRect(camera);
            bool focus = view.StageHudFocused;
            float scale = camera.pixelHeight / 1080f;
            var hp = view.GetComponentsInChildren<TextMeshPro>(true).First(t => t.name == "HpValue");
            hp.ForceMeshUpdate();
            var glass = view.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "BottomGlass");
            int icons = glass.GetComponentsInChildren<Image>(true).Count(i => i.name.StartsWith("StatusBack") && i.enabled);
            bool ownerOrder = view.StageHudOrder == 30000;
            bool fit = !hp.isTextTruncated && hp.preferredWidth <= hp.rectTransform.rect.width + .1f;
            var track=view.GetComponentsInChildren<Image>(true).First(i=>i.name=="HpTrack").rectTransform;
            var points=new Vector3[4];track.GetWorldCorners(points);
            Vector2 trackMin=camera.WorldToScreenPoint(points[0]),trackMax=camera.WorldToScreenPoint(points[2]);
            bool trackFit=Vector2.Distance(trackMin,new Vector2(rect.x+7f*scale,rect.y+8f*scale))<.5f&&
                Vector2.Distance(trackMax-trackMin,new Vector2(104f,6f)*scale)<.5f;
            var resource=view.GetComponentsInChildren<TextMeshPro>(true).First(t=>t.name=="ResourceValue");
            float height=150f;
            RecordMechanic($"stage_hud_{tag}_{view.BoundUnit.UnitName}", "118×150px·HP 숫자 컨테이너·상태≤3·전장보다 앞",
                $"rect={rect}, hp={hp.text}, font={hp.fontSize:F2}, preferred={hp.preferredWidth:F2}, textBounds={hp.textBounds.size}, textFits={fit}, icons={icons}, ownerOrder={ownerOrder}, hpTrack={trackMin}/{trackMax}, trackFits={trackFit}",
                Mathf.Abs(rect.width - 118f * scale) < 1f && Mathf.Abs(rect.height - height * scale) < 1f && fit && icons <= 3 && ownerOrder && trackFit);
        }

        private sealed class S2SummonPolicy : ISummonerPolicy
        {
            public SummonStyle Style => SummonStyle.Swarm;
            public int Cap => 3;
            public SummonSpec Spec => new() { Id=999, Kind="s2_hud", Name="검증 소환수", Element="Fire", Hp=100 };
            public void OnOverflow(Unit owner, Unit oldest) { }
        }

        private IEnumerator ScenarioStageCardHudRestore(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config,Stage(config,6),false,false,false);
            yield return WaitUnscaled(.3f);
            VisualModeState.Set(VisualMode.Card);
            yield return WaitUnscaled(.5f);
            RecordMechanic("s2_card_restore_mode","Stage→Card 복원·Stage 표시 제거",VisualModeState.Current.ToString(),
                VisualModeState.Current==VisualMode.Card&&AliveAllies().All(u=>u.currentCell.Card.StageProxy==null));
            yield return ScenarioCardHudContainment(config);
            var surface=FindObjectsByType<Image>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(i=>i.name=="TreasurySurface");
            RecordMechanic("s2_treasury_card_restore","Card 재보 원래 색·452×60 복원",surface.color.ToString(),
                surface.color==Color.white&&surface.transform.parent.GetComponent<RectTransform>().sizeDelta==new Vector2(452,60));
        }

        private IEnumerator ScenarioStageBottomHud(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 6), false, false, false);
            ConfigureStageHudEnemies(config);
            var game=GameManager.Instance; var grid=GridManager.Instance;
            yield return WaitUnscaled(.2f);
            RecordS7StagePixels("preparation");
            RecordMechanic("s2_prep_hidden","준비 하단 카드·재보 숨김",StageBattleHud.Visible.ToString(),
                !StageBattleHud.Visible && AliveAllies().All(u=>u.currentCell.Card.StageHudHitRect(Camera.main).width==0));
            foreach(var label in Camera.main.GetComponent<StageRowBands>().Labels)
            {
                float y=Camera.main.WorldToScreenPoint(label.transform.position).y;
                RecordMechanic("s2_d7_"+label.name,"열 이름은 가까운 끝 아래 바닥(y≥906)",y.ToString(),y<=174f);
            }
            yield return CaptureStill("preparation");
            bool prior=game.enabled; game.enabled=false;
            try
            {
                game.StartRound(); yield return WaitForState(BaseEnums.GameState.RoundInProgress,5f);
                game.uiManager?.HidePreparationPhasePanel();
                DamageMeter.Begin(AliveAllies()); game.ActionScheduler.BeginRound();
                yield return WaitUnscaled(.4f);
                RecordMechanic("s2_five","실제5:5",$"{AliveAllies().Count}:{AliveEnemies().Count}",AliveAllies().Count==5&&AliveEnemies().Count==5);
                RecordS7StagePixels("battle");
                foreach(var u in AliveAllies().Concat(AliveEnemies())) RecordStageHudCheck(u.currentCell.Card,"initial",Camera.main);
                foreach(bool hostile in new[]{false,true})
                {
                    var team=hostile?AliveEnemies():AliveAllies();
                    var ordered=team.OrderBy(u=>hostile?Mathf.Abs(u.currentCell.xPos):-Mathf.Abs(u.currentCell.xPos)).ThenBy(u=>u.currentCell.yPos).ToArray();
                    RecordMechanic("s2_order_"+hostile,"아군 후열→전열·적 전열→후열",string.Join(",",ordered.Select(u=>u.ID)),
                        ordered.SequenceEqual(team.OrderBy(u=>u.currentCell.Card.StageHudHitRect(Camera.main).x)));
                }
                var sample=AliveAllies().First().currentCell.Card;
                var orb=sample.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="UltGauge");
                RecordMechanic("s2_orb_size","초월 원42px",orb.rect.ToString(),Mathf.Abs(orb.rect.width*orb.lossyScale.x*30f-42f)<.1f);
                var treasury=FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r=>r.name=="PartyTreasury");
                Vector3[] corners=new Vector3[4];treasury.GetWorldCorners(corners);
                RecordMechanic("s2_treasury_rect","화면 중앙136×150·하단18px",$"{corners[0]}->{corners[2]}",
                    Mathf.Abs(corners[2].x-corners[0].x-136)<1&&Mathf.Abs(corners[2].y-corners[0].y-150)<1&&Mathf.Abs(corners[0].y-18)<1);
                yield return CaptureStill("battle_5v5");

                var links=FindObjectsByType<StageUnitFocus>(FindObjectsSortMode.None);
                var queue=links.First(l=>l.BoundUnit!=null&&l.BoundUnit.ID==1&&l.name=="OrderSlot");
                Unit ally=queue.BoundUnit; var view=ally.currentCell.Card;
                var meter=links.First(l=>l.BoundUnit==ally&&l.name.StartsWith("Row"));
                foreach(string source in new[]{"sd","card","queue","meter"})
                {
                    var pointer=new PointerEventData(EventSystem.current);
                    if(source is "sd" or "card")view.SetPointerFeedback(true,false);
                    else (source=="queue"?queue:meter).OnPointerEnter(pointer);
                    yield return null;
                    RecordMechanic("s2_focus_"+source,"동일 SD·카드·행동 항목·집계 행 강조",ally.UnitName,view.StageHudFocused&&queue.Highlighted&&meter.Highlighted);
                    if(source is "sd" or "card")view.SetPointerFeedback(false,false);
                    else (source=="queue"?queue:meter).OnPointerExit(pointer);
                }
                Unit enemy=AliveEnemies().First(); var enemyView=enemy.currentCell.Card;
                TextMeshPro Hp(Unit u)=>u.currentCell.Card.GetComponentsInChildren<TextMeshPro>(true).First(t=>t.name=="HpValue");
                Image Img(Unit u,string name)=>u.currentCell.Card.GetComponentsInChildren<Image>(true).First(i=>i.name==name);
                int before=enemy.HpCurr;
                enemy.TakeDamage(new DamageContext(ally,100,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage},false));
                RecordMechanic("s2_damage_immediate","공통 피해 API 뒤 같은 프레임 HP·막대 갱신",$"{before}->{enemy.HpCurr}; hud={Hp(enemy).text}",
                    enemy.HpCurr<before&&Hp(enemy).text==enemy.HpCurr.ToString("N0")&&Mathf.Abs(Img(enemy,"HpFill").rectTransform.anchorMax.x-(float)enemy.HpCurr/enemy.HpMax)<.001f);
                enemy.Heal(40,enemy);
                RecordMechanic("s2_heal_immediate","회복 API 뒤 같은 프레임 HP 갱신",Hp(enemy).text,Hp(enemy).text==enemy.HpCurr.ToString("N0"));
                enemy.AddShield(80,enemy);
                RecordMechanic("s2_shield_immediate","보호막 API 뒤 같은 프레임 금색 선 비율",enemy.ShieldCurr.ToString(),
                    Img(enemy,"ShieldFill").enabled&&Mathf.Abs(Img(enemy,"ShieldFill").rectTransform.anchorMax.x-Mathf.Clamp01((float)enemy.ShieldCurr/enemy.HpMax))<.001f);
                ally.GrantCombatElement(BaseEnums.UnitElement.Cryo,2,ally,true,true);
                RecordMechanic("s2_element_immediate","부착 원소 기존 바인딩을 카드 내부에 표시","얼음",
                    view.GetComponentsInChildren<Image>().Any(i=>i.name.StartsWith("Element")&&i.enabled&&i.sprite==Managers.UI.Theme.StatusIcons.Element(BaseEnums.UnitElement.Cryo)));
                ally.ClearUltimateResource();yield return null;
                float baseY=view.StageHudHitRect(Camera.main).y;
                ally.FillUltimateResource();yield return null;
                RecordMechanic("s2_ready","충전 기존 원형 바인딩·8px 상승",view.StageHudHitRect(Camera.main).ToString(),
                    Img(ally,"LiquidFront").fillAmount>=.999f&&Mathf.Abs(view.StageHudHitRect(Camera.main).y-baseY-8)<.5f);
                ally.isCasting=true;yield return null;
                RecordMechanic("s2_acting","행동 중14px 상승",view.StageHudHitRect(Camera.main).ToString(),Mathf.Abs(view.StageHudHitRect(Camera.main).y-baseY-14)<.5f);
                yield return CaptureStill("values_ready_acting"); ally.isCasting=false;

                var initial=AliveAllies().ToDictionary(u=>u,u=>u.currentCell.Card.StageHudHitRect(Camera.main));
                var free=FindObjectsByType<Cell>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>grid.IsAllyFieldPosition(c.xPos,c.yPos)&&!c.isOccupied);
                Unit extra=grid.SpawnUnit(free.xPos,free.yPos,false,7);extra.CastPassiveCode();
                yield return WaitUnscaled(.2f);
                RecordMechanic("s2_sixth","실제6명·증원 위쪽 줄·기존 카드 자리 유지",extra.currentCell.Card.StageHudHitRect(Camera.main).ToString(),
                    AliveAllies().Count==6&&Mathf.Abs(extra.currentCell.Card.StageHudHitRect(Camera.main).y-192)<1&&initial.All(k=>Mathf.Abs(k.Key.currentCell.Card.StageHudHitRect(Camera.main).x-k.Value.x)<.5f));
                foreach(var u in AliveAllies())
                {
                    foreach(string id in u.CombatResourceIds.ToArray())u.AddCombatResource(id,Mathf.Min(3,u.GetCombatResourceMaximum(id)));
                    u.RefreshView();
                    var label=u.currentCell.Card.GetComponentsInChildren<TextMeshPro>(true).First(t=>t.name=="ResourceValue");
                    bool noResource=u.ID==67&&u.GetCombatResourceMaximum(Codes.Passive.SuryaBeforeSunset.BulletResourceId)==0;
                    RecordMechanic("s2_resource_"+u.ID,noResource?"현행 미생성 탄환 배지는 숨김":"현행 고유 자원 배지 유지·카드 내부",$"{label.enabled}:{label.text}",
                        noResource?!label.enabled:label.enabled&&!string.IsNullOrEmpty(label.text));
                    if(u.ID is 1 or 2 or 7)
                    {
                        string id=u.ID==1?Codes.Passive.SeiBigBang.ResourceId:u.ID==2?Codes.Ultimate.ShiFinalBell.ResourceId:Codes.Passive.JeanIronSanction.ResourceId;
                        RecordMechanic("s2_resource_value_"+u.ID,"자원 API 뒤 실제 값과 같은 숫자",label.text,label.text==u.GetCombatResource(id).ToString());
                    }
                }
                Summons.Register(ally,new S2SummonPolicy());Unit summon=Summons.Create(ally);
                yield return null;
                var badge=view.GetComponentsInChildren<TextMeshPro>(true).First(t=>t.name=="StageSummonBadge");
                RecordMechanic("s2_summon","소환수는 주인 위 배지·독립 카드 없음",badge.text,summon!=null&&badge.text.Contains("검증 소환수")&&AliveAllies().Count==6);
                Summons.Create(ally);Summons.Create(ally);yield return null;
                RecordMechanic("s2_summon_multiple","다수 소환은 한 배지의 기수·카드 추가 없음",badge.text,badge.text=="소환 ×3"&&AliveAllies().Count==6);
                yield return CaptureStill("resources_reinforcement_summon");
                Summons.ClearAll();Summons.Unregister(ally);
                PartyTreasury.Charge(ally,4);yield return null;
                var count=treasury.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="TreasuryCount");
                RecordMechanic("s2_treasury_charge","기존 Charge·가용4 표시",count.text,PartyTreasury.Available==4&&count.text=="4");
                using(var magazine=PartyTreasury.Reserve(ally))
                {
                    yield return null;
                    RecordMechanic("s2_treasury_reserved","기존 예약4·가용0",count.text,PartyTreasury.Reserved==4&&count.text=="0");
                    magazine.SpendOne();yield return null;
                    RecordMechanic("s2_treasury_spent","예약 탄별 실제 소비3",PartyTreasury.Total.ToString(),PartyTreasury.Total==3);
                }
                yield return null;
                RecordMechanic("s2_treasury_refund","미발사3 반환·가용3",count.text,PartyTreasury.Available==3&&PartyTreasury.Reserved==0&&count.text=="3");
                Rect deathRect=enemyView.StageHudHitRect(Camera.main);
                enemy.TakeDamage(new DamageContext(ally,enemy.HpMax*10,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage},false));
                yield return WaitUnscaled(.1f);
                var fallen=grid.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c=>c.name=="StageFallenCard");
                RecordMechanic("s2_death","사망 카드 같은 자리 회색 유지·HP0",$"{fallen?.name}; actual={(fallen!=null?Camera.main.WorldToScreenPoint(fallen.transform.position):Vector3.zero)}; expected={deathRect}",fallen!=null&&fallen.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.name=="HpValue"&&t.text=="0")&&
                    Vector2.Distance(Camera.main.WorldToScreenPoint(fallen.transform.position),new Vector2(deathRect.center.x,deathRect.yMax))<1f);
                yield return CaptureStill("fallen_slot");
            }
            finally { game.ActionScheduler.EndRound();game.enabled=prior; }
        }

        private IEnumerator ScenarioStageHudFocus(VerifyScenarioRequest config)
        {
            if (VisualModeState.Current != VisualMode.Stage) { FailCurrent("Stage 전용 HUD 검사다."); yield break; }
            yield return PrepareStage(config, Stage(config, 6), false, true, true);
            ConfigureStageHudEnemies(config);
            GameManager game = GameManager.Instance;
            game.StartRound();
            yield return WaitForState(BaseEnums.GameState.RoundInProgress, 5f);
            FreezeBattleForManualCapture();
            bool wasEnabled = game.enabled;
            game.enabled = false;
            game.gameState = BaseEnums.GameState.RoundInProgress;
            game.ActionScheduler.BeginRound();
            DamageMeter.Begin(AliveAllies());
            yield return WaitUnscaled(.5f);
            GridManager.Instance.RefreshFieldLayout();
            yield return CaptureStill("combat");

            var links = FindObjectsByType<StageUnitFocus>(FindObjectsSortMode.None);
            var queue = links.FirstOrDefault(l => l.BoundUnit != null && !l.BoundUnit.IsEnemy && l.name == "OrderSlot");
            Unit unit = queue != null ? queue.BoundUnit : null;
            var meter = links.FirstOrDefault(l => l.BoundUnit == unit && l.name.StartsWith("Row"));
            if (unit == null || meter == null) { FailCurrent("강조할 동일 유닛의 행동 순서/피해 행이 없다."); }
            else
            {
                var view = unit.currentCell.Card;
                var pointer = new PointerEventData(EventSystem.current);
                foreach (string source in new[] { "sd", "bundle", "queue", "meter" })
                {
                    if (source is "sd" or "bundle") view.SetPointerFeedback(true, false);
                    else (source == "queue" ? queue : meter).OnPointerEnter(pointer);
                    yield return WaitUnscaled(.15f);
                    bool all = view.StageHudFocused && queue.Highlighted && meter.Highlighted;
                    RecordMechanic("stage_hud_focus_" + source, "SD·묶음·행동 항목·피해 행 동시 강조", $"unit={unit.UnitName}, all={all}", all);
                    RecordStageHudCheck(view, source, Camera.main);
                    if (source == "meter") yield return CaptureStill("hover_linked");
                    if (source is "sd" or "bundle") view.SetPointerFeedback(false, false);
                    else (source == "queue" ? queue : meter).OnPointerExit(pointer);
                }
                yield return WaitUnscaled(.1f);
                RecordMechanic("stage_hud_focus_clear", "hover 종료 시 네 곳 해제", (StageUnitFocus.Focused == null).ToString(),
                    !view.StageHudFocused && !queue.Highlighted && !meter.Highlighted);
                unit.isCasting = true;
                yield return WaitUnscaled(.1f);
                yield return CaptureStill("acting_element_border");
                unit.isCasting = false;
            }
            game.ActionScheduler.EndRound();
            game.enabled = wasEnabled;
        }
    }
}
#endif
