#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Ultimate;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using Effects.Negative;
using Effects.Vfx;
using Managers.UI.Core;
using Managers.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private sealed class QaSummonPolicy : ISummonerPolicy
        {
            public SummonStyle Style => SummonStyle.Swarm;
            public int Cap => 1;
            public SummonSpec Spec { get; }
            public QaSummonPolicy(SummonSpec spec) => Spec=spec;
            public void OnOverflow(Unit owner,Unit oldest) { }
        }
        private IEnumerator ScenarioQaLobbyGuide(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return WaitUnscaled(.2f);
            var lobby=FindFirstObjectByType<MainMenuUI>();
            if(lobby==null||GameManager.Instance!=null) { FailCurrent("로비 자료실 검증은 MainMenu·GameManager 없음 상태에서 시작해야 합니다.");yield break; }
            var wikiButton=FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name=="Wiki");wikiButton.onClick.Invoke();
            var wiki=(WikiScreen)typeof(MainMenuUI).GetField("_wiki",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lobby);
            yield return WaitUnscaled(.15f);
            GameTermLinks.OpenArticle("combat");yield return WaitUnscaled(.15f);
            RuiCheck("로비 자료실 문서 링크","게임 매니저 없이 combat 이동",wiki.SelectedKey,GameManager.Instance==null&&wiki.SelectedKey=="guide:combat");
            wiki.GoBack();RuiCheck("로비 자료실 링크 탐색 이력","home 복귀",wiki.SelectedKey,wiki.SelectedKey=="guide:home");
            GameTermLinks.OpenArticle("unit:25");yield return WaitUnscaled(.1f);
            RuiCheck("로비 자료실 캐릭터 링크","오르페우스 이동",wiki.SelectedKey,wiki.SelectedKey=="unit:25");
            RuiCheck("로비 자료실 추천 표 로드","GameManager 없이 역할·편성 사용",SynergyCatalog.UnitOf(25)?.name,GameManager.Instance==null&&SynergyCatalog.IsLoaded&&SynergyCatalog.UnitOf(25)!=null);
            GameTermLinks.OpenArticle("enemy:3202");RuiCheck("로비 자료실 적 카탈로그","48층 고정 보스",wiki.SelectedKey,wiki.SelectedKey=="enemy:3202");
            GuideTermTooltip.Show(wiki,"concentration",new Vector2(960,540),immediatePin:true);yield return WaitUnscaled(.15f);
            var read=GameObject.Find("GuideTooltipCanvas").GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="ReadGuide");read.onClick.Invoke();yield return WaitUnscaled(.1f);
            RuiCheck("로비 용어 카드 자세히 읽기 버튼","집중 이동·툴팁 닫힘",wiki.SelectedKey,wiki.SelectedKey=="term:concentration"&&GuideTermTooltip.Depth==0);
            yield return CaptureStill("qa_lobby_guide_links");ModalScreen.TryEscapeTop();
            RuiCheck("로비 자료실 ESC 닫기","닫힘·배속 복원",wiki.IsVisible.ToString(),!wiki.IsVisible&&WikiScreen.Active==null&&Time.timeScale>0);RuiFinish();
        }

        private IEnumerator ScenarioQaField(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun("party5");yield return PrepareStage(config,7,false,false,false,4);
            var game=GameManager.Instance;var grid=GridManager.Instance;
            var drag=FindFirstObjectByType<DragAndDropManager>();
            var getCell=typeof(GridManager).GetMethod("GetFieldCell",BindingFlags.Instance|BindingFlags.NonPublic);
            Cell EmptyField() => (from x in new[]{-1,-2} from y in Enumerable.Range(1,4) let cell=(Cell)getCell.Invoke(grid,new object[]{x,y}) where !cell.isOccupied select cell).First();
            var handle=typeof(DragAndDropManager).GetMethod("HandleDrop",BindingFlags.Instance|BindingFlags.NonPublic);
            void Drop(Unit unit,Cell cell) { drag.StartDrag(unit.currentCell,Vector2.zero);try { handle.Invoke(drag,new object[]{cell}); } finally { drag.CancelDrag(); } }
            var bench=grid.SpawnUnit(0,0,false,3,true);Cell savedBench=bench.currentCell;
            Drop(bench,EmptyField());
            RuiCheck("아군6번째 출전 실제 드롭 거부","출전5·대기석 보존",grid.ActiveAllyFieldCount.ToString(),grid.ActiveAllyFieldCount==5&&bench.currentCell==savedBench);
            var field=AliveAllies().First();Cell savedField=field.currentCell;int hp=field.HpCurr,level=field.Level;
            Drop(bench,savedField);
            RuiCheck("5인 출전 대기석 교환 허용","객체·체력·레벨·인원 보존",grid.ActiveAllyFieldCount.ToString(),bench.currentCell==savedField&&field.currentCell==savedBench&&field.HpCurr==hp&&field.Level==level&&grid.ActiveAllyFieldCount==5);
            while(grid.HasAvailableBenchSlot())grid.SpawnUnit(0,0,false,7,true);
            bool recruited=(bool)typeof(GameManager).GetMethod("RecruitSupportUnit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{124});
            RuiCheck("가득 찬 대기석 영입이 출전 상한 우회하지 않음","수용불가·영입실패·5인 유지",recruited.ToString(),!grid.HasAvailableAllySlot()&&!recruited&&grid.ActiveAllyFieldCount==5);
            var extra=grid.SpawnUnit(EmptyField().xPos,EmptyField().yPos,false,124);
            extra.isActive=false;Cell inactiveCell=extra.currentCell;
            Drop(field,inactiveCell);
            RuiCheck("비활성 점유 칸 교환으로6인 출전 우회 차단","대기석 보존·출전5",grid.ActiveAllyFieldCount.ToString(),field.currentCell==savedBench&&grid.ActiveAllyFieldCount==5);
            extra.isActive=true;
            var start=typeof(GameManager).GetMethod("StartBattle",BindingFlags.Instance|BindingFlags.NonPublic);
            start.Invoke(game,null);
            RuiCheck("오염된6인 편성 전투 시작 차단","준비 유지",game.gameState.ToString(),game.gameState==GameState.Preparation&&grid.ActiveAllyFieldCount==6);
            grid.RetireUnit(extra);
            foreach(var unit in grid.heroList.Where(u=>u!=null&&u.IsBench).ToList())grid.RetireUnit(unit);
            foreach(var unit in AliveAllies().Take(4).ToList())
                Drop(unit,FindObjectsByType<Cell>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>grid.IsBenchCell(c)&&!c.isOccupied));
            var last=AliveAllies().Single(u=>u.IsOnField);var lastCell=last.currentCell;
            var emptyBench=FindObjectsByType<Cell>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>grid.IsBenchCell(c)&&!c.isOccupied);
            Drop(last,emptyBench);
            RuiCheck("마지막 출전 유닛 대기석 이동 거부","출전1·위치 보존",grid.ActiveAllyFieldCount.ToString(),grid.ActiveAllyFieldCount==1&&last.currentCell==lastCell);
            lastCell.isOccupied=false;try { start.Invoke(game,null);RuiCheck("0인 편성 시작 차단","준비 유지",game.gameState.ToString(),game.gameState==GameState.Preparation); } finally { lastCell.isOccupied=true; }
            yield return CaptureStill("qa_field_limits");
            yield return BuildFreshRun("party5");yield return PrepareStage(config,7,false,false,false,4);
            game=GameManager.Instance;grid=GridManager.Instance;var owner=AliveAllies().First();var enemy=AliveEnemies().First();
            var pet=grid.SpawnSummon(owner,new SummonSpec { Id=998,Kind="qa_field",Name="검증 소환수",Hp=10000,Targetable=true,PortraitOverride=owner.currentCell.portraitRenderer.sprite });
            EhStat(enemy,StatType.SpecialAttack,1000000);
            RuiCheck("소환수 아군·적 질의 진영","아군 목록과 적 목록 구분",Target.GetAllAllies(pet).Count.ToString(),Target.GetAllAllies(pet).All(u=>!u.IsEnemy)&&Target.GetAllAllies(pet).Count==5&&Target.GetAllEnemies(pet).All(u=>u.IsEnemy));
            ElementalReaction.SpreadResult spread=null;
            void Spread(ElementalReaction.SpreadResult result) { if(result.OriginalTarget==pet)spread=result; }
            ElementalReaction.AnySpreadResolved+=Spread;
            try { pet.GrantCombatElement(UnitElement.Pyro,source:enemy,suppressReaction:true,effectHitResolved:true);pet.GrantCombatElement(UnitElement.Anemo,source:enemy,effectHitResolved:true); }
            finally { ElementalReaction.AnySpreadResolved-=Spread; }
            RuiCheck("피격 가능 소환수 실제 확산 진영","아군에만 불 부착",spread?.Delivered.Count.ToString()??"없음",spread!=null&&spread.Delivered.Count==5&&spread.Delivered.All(u=>!u.IsEnemy)&&AliveEnemies().All(u=>!u.HasAttachedElement(UnitElement.Pyro)));
            yield return CaptureStill("qa_summon_spread");game.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaPresentation(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return PrepareStage(config,7,false,false,false,4);
            var game=GameManager.Instance;var ui=game.uiManager;var wiki=ui.GuideForVerification;
            foreach(float speed in new[]{.5f,1f,2f})
            {
                Time.timeScale=speed;yield return WaitUnscaled(.12f);Effects.HitStop.Play(.08f);
                ui.HandleEscape();yield return WaitUnscaled(.12f);ui.HandleEscape();
                RuiCheck("히트스톱 중 메뉴 열고 닫기 "+speed,"선택 배속 복원",Time.timeScale.ToString(),Mathf.Approximately(Time.timeScale,speed));
                yield return WaitUnscaled(.12f);Effects.HitStop.Play(.08f);ui.ShowGuide();yield return WaitUnscaled(.12f);wiki.Hide();
                RuiCheck("히트스톱 중 자료실 열고 닫기 "+speed,"선택 배속 복원",Time.timeScale.ToString(),Mathf.Approximately(Time.timeScale,speed));
            }
            Time.timeScale=0;Effects.HitStop.Play(.08f);ui.ShowGuide();yield return WaitUnscaled(.12f);wiki.Hide();
            RuiCheck("일시정지 중 자료실·타격 연출","정지 유지",Time.timeScale.ToString(),Time.timeScale==0);Time.timeScale=4;
            RuiCheck("오르페우스 초월기 카탈로그 슬롯","U25 존재·특수25 없음",CodeCatalog.Find(CodeCatalog.Slot.Ultimate,25)?.verbalName??"없음",CodeCatalog.Find(CodeCatalog.Slot.Ultimate,25)?.codeName=="OrpheusLament"&&CodeCatalog.Find(CodeCatalog.Slot.Special,25)==null);
            var owner=AliveAllies().First();var target=AliveEnemies().First();var pet=GridManager.Instance.SpawnSummon(owner,new SummonSpec { Id=998,Kind="qa_meter",Name="검증 소환수",Hp=10000,Targetable=true,PortraitOverride=owner.currentCell.portraitRenderer.sprite });
            DamageMeter.Begin(AliveAllies());var damage=new DamageContext(pet,7,CodeType.Normal,new(){DamageTag.TrueDamage,DamageTag.NormalAttack});target.TakeDamage(damage);DamageMeter.End();
            RuiCheck("소환수 원본 공격자 피해 집계","소환자 귀속·총합 일치",DamageMeter.Total().ToString(),damage.ResolvedDamage>0&&DamageMeter.Current.All(e=>!e.Unit.IsSummon)&&DamageMeter.Current.Single(e=>e.Unit==owner).Damage==damage.ResolvedDamage&&DamageMeter.Total()==damage.ResolvedDamage);
            var audio=AudioManager.Instance;var pick=typeof(AudioManager).GetMethod("PickClip",BindingFlags.Instance|BindingFlags.NonPublic);
            var cue=new SfxCueData { clips=new(){"temporary/jade_basic"},mixerGroup="Voice" };
            // 현행 큐 데이터에서 실제 리소스 경로를 읽어 재생 회피가 리소스 누락으로 바뀌지 않는지 검사한다.
            var cues=typeof(AudioManager).GetField("_cues",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio) as System.Collections.IDictionary;
            cue=(SfxCueData)cues["surya.normal.cast"];
            typeof(AudioManager).GetField("_lastLokapalaVoiceClip",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(audio,cue.clips[0]);
            object[] args={"surya.normal.cast",cue,null};var clip=(AudioClip)pick.Invoke(audio,args);
            RuiCheck("로카팔라 동일 단일 음성 재사용","누락 없이 원본 클립 선택",args[2]?.ToString(),clip!=null&&args[2]?.ToString()==cue.clips[0]);
            var dual=(SfxCueData)cues["jean.holy_strike.cast"];
            var previous=(System.Collections.Generic.Dictionary<string,string>)typeof(AudioManager).GetField("_lastClipByCue",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
            var globalField=typeof(AudioManager).GetField("_lastAvoidRepeatClip",BindingFlags.Instance|BindingFlags.NonPublic);
            previous.TryGetValue("jean.holy_strike.cast",out string savedPrevious);var savedGlobal=globalField.GetValue(audio);
            try
            {
                previous["jean.holy_strike.cast"]=dual.clips[0];globalField.SetValue(audio,dual.clips[1]);
                object[] two={"jean.holy_strike.cast",dual,null};var fallback=(AudioClip)pick.Invoke(audio,two);
                RuiCheck("두 음성이 각각 연속 회피 대상일 때 fallback","실제 유효 후보 반환",two[2]?.ToString(),fallback!=null&&dual.clips.Contains(two[2]?.ToString()));
            }
            finally { if(savedPrevious==null)previous.Remove("jean.holy_strike.cast");else previous["jean.holy_strike.cast"]=savedPrevious;globalField.SetValue(audio,savedGlobal); }
            yield return CaptureStill("qa_presentation_fixed");game.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaVfx(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun("allvfx_group_qa_resources");yield return PrepareStage(config,7,false,false,false,4);
            foreach(var unit in AliveAllies()) { unit.Invoke(UnitEventType.OnRoundStart,new EventContext(unit));CardMotion.ViewOf(unit).Bind(unit,true); }
            yield return WaitUnscaled(.15f);yield return EndOfFrame;
            foreach(var unit in AliveAllies())
            {
                var view=CardMotion.ViewOf(unit);view.Tick();yield return EndOfFrame;var hud=view.VerificationResourceHud(true);
                string expected=unit.ID==82 ? "%" : unit.ID==121 ? "영혼 " : "";
                bool valid=unit.ID==67 ? !hud.ammoEnabled&&!hud.strongEnabled : hud.ammoEnabled&&(expected.Length==0 ? !hud.ammoText.Contains("/") : hud.ammoText.Contains(expected));
                RuiCheck(unit.UnitName+" 자원 표시 소유권","현행 고유 단위·표식형 수리야는 자기 탄창 없음",UnityEngine.JsonUtility.ToJson(hud),valid);
            }
            var jean=AliveAllies().First(u=>u.ID==7);jean.TryConsumeCombatResource(JeanIronSanction.ResourceId,jean.GetCombatResource(JeanIronSanction.ResourceId));
            var freya=AliveAllies().First(u=>u.ID==82);freya.TryConsumeCombatResource(FreyaBrisingamen.Resource,freya.GetCombatResource(FreyaBrisingamen.Resource));
            var anubis=AliveAllies().First(u=>u.ID==121);anubis.GainPermanentStacks(AnubisSoulHarvest.SoulKey,.5f);
            var kubera=AliveAllies().First(u=>u.ID==65);kubera.ClearUltimateResource();kubera.AddUltimateResource(2);
            foreach(var item in new[]{(jean,"0"),(freya,"0%"),(anubis,"영혼 0.5"),(kubera,"2")})
            {
                var view=CardMotion.ViewOf(item.Item1);view.Tick();yield return EndOfFrame;var hud=view.VerificationResourceHud(true);
                RuiCheck(item.Item1.UnitName+" 명시적 고유 자원 단위",item.Item2,UnityEngine.JsonUtility.ToJson(hud),hud.ammoEnabled&&hud.ammoText==item.Item2);
            }
            yield return CaptureStill("qa_resource_cards");
            yield return BuildFreshRun("party5");yield return PrepareStage(config,7,false,false,false,4);
            foreach(var unit in AliveAllies()) { unit.Invoke(UnitEventType.OnRoundStart,new EventContext(unit));CardMotion.ViewOf(unit).Bind(unit,true); }
            var shi=AliveAllies().First(u=>u.ID==2);var bell=(ShiFinalBell)shi.ActiveUltimateCode;
            foreach(int value in new[]{0,9})
            {
                bell.DebugSetStack(value);var view=CardMotion.ViewOf(shi);view.Tick();yield return EndOfFrame;var hud=view.VerificationResourceHud(true);
                RuiCheck("만종 "+value+" 경계 배지",value+"/9",UnityEngine.JsonUtility.ToJson(hud),hud.ammoEnabled&&hud.ammoText==value.ToString()&&hud.badgeValue==value&&hud.badgeMaximum==9);
            }
            var sei=AliveAllies().First(u=>u.ID==1);sei.TryConsumeCombatResource(SeiBigBang.ResourceId,sei.GetCombatResource(SeiBigBang.ResourceId));
            CardMotion.ViewOf(sei).Tick();yield return EndOfFrame;var hidden=CardMotion.ViewOf(sei).VerificationResourceHud(true);
            RuiCheck("세이 결정0 배지 숨김","문구 없음",UnityEngine.JsonUtility.ToJson(hidden),!hidden.ammoEnabled);
            sei.AddCombatResource(SeiBigBang.ResourceId,2);CardMotion.ViewOf(sei).Tick();yield return EndOfFrame;var crystal=CardMotion.ViewOf(sei).VerificationResourceHud(true);
            RuiCheck("세이 결정2 배지","2",UnityEngine.JsonUtility.ToJson(crystal),crystal.ammoEnabled&&crystal.ammoText=="2"&&crystal.badgeValue==2);
            var plain=AliveAllies().First(u=>u.ID==20);CardMotion.ViewOf(plain).Tick();yield return EndOfFrame;
            RuiCheck("일반 마나 충전 텍스트 숨김","원형 게이지만",CardMotion.ViewOf(plain).VerificationResourceHud().ammoEnabled.ToString(),!CardMotion.ViewOf(plain).VerificationResourceHud().ammoEnabled);
            var backdrop=UnityEngine.Object.FindFirstObjectByType<Effects.BattlefieldBackdrop>();
            var layerField=typeof(Effects.BattlefieldBackdrop).GetField("_generatedLayers",BindingFlags.Instance|BindingFlags.NonPublic);
            var spriteField=typeof(Effects.BattlefieldBackdrop).GetField("_generatedSprites",BindingFlags.Instance|BindingFlags.NonPublic);
            var layers=((System.Collections.Generic.List<SpriteRenderer>)layerField.GetValue(backdrop)).ToArray();
            var sprites=((System.Collections.Generic.List<Sprite>)spriteField.GetValue(backdrop)).ToArray();var textures=sprites.Select(s=>s.texture).ToArray();
            UnityEngine.Object.Destroy(backdrop);yield return null;yield return null;
            RuiCheck("배경 생성 리소스 정리","레이어·스프라이트·텍스처 해제",layers.Length+"/"+sprites.Length,layers.Length>0&&sprites.Length>0&&layers.All(x=>x==null)&&sprites.All(x=>x==null)&&textures.All(x=>x==null));
            Effects.BattlefieldBackdrop.Ensure();
            foreach(float speed in new[]{.5f,1f,2f})
            {
                Time.timeScale=speed;int frames=0,peaks=0;bool sawTail=false,tailVisible=true,complete=false;
                var fx=FlipbookFx.Play("VfxTex/Handpaint/Smoke3_Purple",3,3,new Vector2(.5f,.5f),VfxSpace.WorldPoint(AliveEnemies().First()),Vector2.one*Cell.CardSize,Color.white,false,false,false,Enumerable.Repeat(1f/45f,9).ToArray(),0,VfxSpace.OverlaySortingOrder+50,frame=>{frames++;if(frame==4)peaks++;},()=>complete=true,new FlipbookCompositeStyle{FadeInFraction=.1f,FadeOutFraction=.26f});
                float deadline=Time.realtimeSinceStartup+4;
                while(fx!=null&&!fx.IsFinished&&Time.realtimeSinceStartup<deadline)
                {
                    yield return EndOfFrame;
                    if(fx==null)break;
                    int frame=(int)typeof(FlipbookFx).GetField("_frame",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fx);
                    if(frame is 6 or 7)
                    {
                        sawTail=true;var body=(SpriteRenderer)typeof(FlipbookFx).GetField("_renderer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fx);
                        var blended=(SpriteRenderer)typeof(FlipbookFx).GetField("_nextRenderer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fx);
                        tailVisible&=body.color.a+(blended!=null ? blended.color.a : 0f)>.01f;
                        Capture("qa_flipbook_tail_timescale_"+speed+"_frame_"+frame);
                    }
                }
                RuiCheck("플립북 45FPS·실제 배속 "+speed,"9프레임·정점1회·완료1회·후반 표시",$"{frames}/{peaks}/{sawTail}/{tailVisible}",frames==9&&peaks==1&&complete&&sawTail&&tailVisible);
            }
            Time.timeScale=4;GameManager.Instance.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaKits(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun("party5");yield return PrepareStage(config,7,false,false,false,4);
            var game=GameManager.Instance;var sei=AliveAllies().First(u=>u.ID==1);var shi=AliveAllies().First(u=>u.ID==2);var pyg=AliveAllies().First(u=>u.ID==20);var enemy=AliveEnemies().First();
            enemy.Stats.Set(StatType.MaxHp,100000);enemy.RefreshAttributes();enemy.Heal(enemy.HpMax,enemy);
            for(int attempt=0;attempt<2;attempt++)
            {
                sei.FillUltimateResource();sei.ActiveUltimateCode.CastCode();float deadline=Time.realtimeSinceStartup+9;
                while(sei.isCasting&&Time.realtimeSinceStartup<deadline)yield return null;
                RuiCheck("세이 집중 중 재시전 "+attempt,"아군5명 새 버프·집중 유지",AliveAllies().Count(u=>u.HasStatusKey(SeiSongOfGenesis.BuffKey)).ToString(),!sei.isCasting&&Concentration.IsActive(sei,SeiSongOfGenesis.ConcentrationKey)&&AliveAllies().All(u=>u.HasStatusKey(SeiSongOfGenesis.BuffKey)));
            }
            int bonus=0;void Bonus(DamageResolvedContext ctx) { if(ctx.Attacker==shi&&ctx.DamageContext.HasTag(DamageTag.BonusDamage))bonus++; }
            Unit.AnyDamageDealt+=Bonus;try { enemy.TakeDamage(new DamageContext(shi,1,CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.TrueDamage})); }finally{Unit.AnyDamageDealt-=Bonus;}
            RuiCheck("세이 재집중 후 실제 덧붙는 피해","1회",bonus.ToString(),bonus==1);Concentration.End(sei,SeiSongOfGenesis.ConcentrationKey,Concentration.EndReason.Expired);
            var bell=(ShiFinalBell)shi.ActiveUltimateCode;shi.TryConsumeCombatResource(ShiFinalBell.ResourceId,shi.GetCombatResource(ShiFinalBell.ResourceId));bell.DebugSetStack(8);
            var command=shi.ActiveNormalCode.BeginCommandedAttack(enemy,sei);yield return command.Prepare();bool completed=false;
            IEnumerator Deliver() { try { yield return command.Deliver(); } finally { completed=true; } }
            VfxRunner.Run(Deliver());yield return null;command.Cancel();float until=Time.realtimeSinceStartup+5;while(!completed&&Time.realtimeSinceStartup<until)yield return null;command.End();
            RuiCheck("9번째 지휘 멸참 취소 후 만종 순환","취소·자원0",shi.GetCombatResource(ShiFinalBell.ResourceId).ToString(),completed&&command.Cancelled&&command.HitsLanded==0&&shi.GetCombatResource(ShiFinalBell.ResourceId)==0);
            int next=bell.RegisterDestructionSlash();RuiCheck("취소 후 다음 멸참이 9타를 반복하지 않음","단계0·자원1",next+"/"+shi.GetCombatResource(ShiFinalBell.ResourceId),next==0&&shi.GetCombatResource(ShiFinalBell.ResourceId)==1);
            yield return CaptureStill("qa_sei_shi_corrected");
            var aura=pyg.ActivePassiveCodes.OfType<PygmalionGalatea>().Single();aura.CastCode();
            Summons.Register(sei,new QaSummonPolicy(new SummonSpec{Id=998,Kind="qa_galatea",Name="검증 소환수",Hp=10000,Targetable=true,PortraitOverride=sei.currentCell.portraitRenderer.sprite}));
            var pet=Summons.Create(sei);Summons.Unregister(sei);
            var hit=new DamageContext(pet,10,CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.SummonAttack,DamageTag.TrueDamage});enemy.TakeDamage(hit);
            RuiCheck("생성된 소환수 실제 갈라테아 피해","소환수+30%·실피해13",hit.ResolvedDamage.ToString(),pet.HasStatusKey("pygmalion_galatea_aura")&&hit.ResolvedDamage==13);
            aura.StopCode();var plain=new DamageContext(pet,10,CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.SummonAttack,DamageTag.TrueDamage});enemy.TakeDamage(plain);
            RuiCheck("갈라테아 정리 후 소환수 피해","버프 제거·실피해10",plain.ResolvedDamage.ToString(),!pet.HasStatusKey("pygmalion_galatea_aura")&&plain.ResolvedDamage==10);
            var blocked=shi.ActiveNormalCode.BeginCommandedAttack(enemy,sei);yield return blocked.Prepare();
            shi.AddStatus(Effects.Buffs.BuffStatus.Create(79998,"qa_command_block","일반행동 봉쇄",sei,shi,new Effects.Neutral.DisableNormalAttackEffect(2001),duration:0,isBeneficial:false));
            int beforeBlocked=enemy.HpCurr;bool blockedActive=blocked.Scope.Active;
            try { yield return blocked.Deliver(); }
            finally { blocked.End();shi.RemoveStatusByKey("qa_command_block"); }
            RuiCheck("지휘 준비 후 일반행동 봉쇄","지휘 비활성·늦은 타격0",blockedActive+"/"+blocked.HitsLanded+"/"+(beforeBlocked-enemy.HpCurr),!blockedActive&&blocked.HitsLanded==0&&enemy.HpCurr==beforeBlocked);
            game.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaInterruption(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            foreach(bool die in new[]{false,true})
            {
                yield return BuildFreshRun("allvfx_group_8");yield return PrepareStage(config,7,false,false,false,4);
                var source=AliveAllies().First(u=>u.ID==63);var attacker=AliveEnemies().First();
                source.FillUltimateResource();source.ActiveUltimateCode.CastCode();
                if(die)source.Die(attacker);else source.ControlStarts(new ControlContext(attacker,1));
                float deadline=Time.realtimeSinceStartup+8;while(source.isCasting&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return WaitUnscaled(.75f);
                bool pending=AliveAllies().Any(SpecialAction.IsPending);
                RuiCheck("소집 중 인드라 "+(die?"사망":"제어"),"로카팔라 예약 없음",pending.ToString(),!source.isCasting&&!pending&&SpecialAction.OpenGate(source)==0);
                GameManager.Instance.DebugResetBattle();
            }
            yield return BuildFreshRun("allvfx_group_8");yield return PrepareStage(config,7,false,false,false,4);
            var varuna=AliveAllies().First(u=>u.ID==66);int hp=AliveEnemies().Sum(u=>u.HpCurr),shield=AliveAllies().Where(u=>u!=varuna).Sum(u=>u.ShieldCurr);
            varuna.FillUltimateResource();varuna.ActiveUltimateCode.CastCode();varuna.Die(AliveEnemies().First());
            float until=Time.realtimeSinceStartup+8;while(varuna.isCasting&&Time.realtimeSinceStartup<until)yield return null;
            yield return WaitUnscaled(.75f);
            RuiCheck("바루나 초월기 시전 중 사망","후속 피해·보호막 없음",AliveEnemies().Sum(u=>u.HpCurr)+"/"+AliveAllies().Sum(u=>u.ShieldCurr),!varuna.isCasting&&AliveEnemies().Sum(u=>u.HpCurr)==hp&&AliveAllies().Sum(u=>u.ShieldCurr)==shield);
            int called=0;var targets=AliveEnemies();yield return VfxComposer.PlayMultiTargetHits("missing.qa",null,targets,_=>called++,0);
            RuiCheck("시전자 없는 순수 다중 연출 완료 계약","대상당1회",called.ToString(),called==targets.Count);
            int damageCalls=0;yield return DirectAttack.HitEach("missing.qa",null,targets,_=>damageCalls++);
            RuiCheck("시전자 없는 실제 공격은 판정 차단","피해 콜백0",damageCalls.ToString(),damageCalls==0);
            GameManager.Instance.DebugResetBattle();yield return BuildFreshRun("allvfx_group_8");yield return PrepareStage(config,7,false,false,false,4);
            varuna=AliveAllies().First(u=>u.ID==66);var enemyAttacker=AliveEnemies().First();int hitsAfterPeak=0;
            void KillOnFirst(DamageResolvedContext hit) { if(hit.Attacker==varuna&&++hitsAfterPeak==1)varuna.Die(enemyAttacker); }
            Unit.AnyDamageDealt+=KillOnFirst;
            try { varuna.FillUltimateResource();varuna.ActiveUltimateCode.CastCode();until=Time.realtimeSinceStartup+8;while(varuna.isCasting&&Time.realtimeSinceStartup<until)yield return null;yield return WaitUnscaled(.75f); }
            finally { Unit.AnyDamageDealt-=KillOnFirst; }
            RuiCheck("바루나 광역 첫 피격 정산 중 시전자 사망","첫 타격1회·후속 타격/보호막0",hitsAfterPeak+"/"+AliveAllies().Sum(u=>u.ShieldCurr),hitsAfterPeak==1&&AliveAllies().Sum(u=>u.ShieldCurr)==0);
            GameManager.Instance.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaAnubisDuration(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            foreach(int souls in new[]{0,150})
            {
                yield return BuildFreshRun("allvfx_group_3");yield return PrepareStage(config,7,false,false,false,4);
                var actor=AliveAllies().First(u=>u.ID==121);var target=AliveEnemies().First();CfNoCrit(actor);
                if(souls>0)actor.GainPermanentStacks(AnubisSoulHarvest.SoulKey,souls);
                target.Stats.Set(StatType.MaxHp,1000000);target.RefreshAttributes();target.Heal(target.HpMax,target);
                actor.FillUltimateResource();actor.ActiveUltimateCode.CastCode();float deadline=Time.realtimeSinceStartup+8;while(actor.isCasting&&Time.realtimeSinceStartup<deadline)yield return null;
                int duration=souls==0?3:4;
                for(int turn=1;turn<=duration+1;turn++)
                {
                    actor.BeginTurn();int expected=Mathf.RoundToInt(actor.StatValue(StatType.MaxHp)*(turn<=duration?.10f:.06f)),raw=-1;
                    void Hit(DamageResolvedContext hit) { if(hit.Attacker==actor&&hit.DamageContext.CodeType==CodeType.Normal)raw=hit.DamageContext.Damage; }
                    Unit.AnyDamageDealt+=Hit;
                    try { actor.ActiveNormalCode.CastCode();deadline=Time.realtimeSinceStartup+8;while(actor.isCasting&&Time.realtimeSinceStartup<deadline)yield return null; }
                    finally { Unit.AnyDamageDealt-=Hit; }
                    RuiCheck("사자의 서 "+duration+"회 지속·일반 "+turn,"정해진 횟수 강화 뒤 원래 계수",raw+"/"+expected,!actor.isCasting&&Mathf.Abs(raw-expected)<=1);
                    actor.EndTurn();
                }
                GameManager.Instance.DebugResetBattle();
            }
            RuiFinish();
        }

        private IEnumerator ScenarioQaIshtarRetarget(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun("party5_treasury");yield return PrepareStage(config,7,false,false,false,4);
            var game=GameManager.Instance;var grid=GridManager.Instance;grid.ClearActiveEnemies();
            int enemyId=game.dataManager.FetchEnemyDataList().enemies.First(e=>e.tier=="normal"&&e.reworkKit==null).id;
            var first=grid.SpawnUnit(1,2,true,enemyId);var second=grid.SpawnUnit(1,3,true,enemyId);var caster=AliveAllies().First(u=>u.ID==220);CfNoCrit(caster);
            foreach(var enemy in new[]{first,second}) { enemy.Stats.Set(StatType.MaxHp,1000000);enemy.RefreshAttributes();enemy.Heal(enemy.HpMax,enemy); }
            foreach(var ally in AliveAllies())ally.Invoke(UnitEventType.OnRoundStart,new EventContext(ally));
            PartyTreasury.BeginBattle();PartyTreasury.Charge(caster,3);bool removed=false;int fired=0;
            Action<EventContext> disappear=context=>{if(!removed&&context.Grantor==caster){removed=true;context.Grantee.Die(caster);}};
            void Hit(DamageResolvedContext hit) { if(hit.Attacker==caster&&hit.DamageContext.IsTreasurySpendAttack)fired++; }
            first.AddListener(UnitEventType.OnTargeted,disappear);second.AddListener(UnitEventType.OnTargeted,disappear);Unit.AnyDamageDealt+=Hit;
            try { caster.FillUltimateResource();caster.ActiveUltimateCode.CastCode();float deadline=Time.realtimeSinceStartup+15;while(caster.isCasting&&Time.realtimeSinceStartup<deadline)yield return null; }
            finally { first.RemoveListener(UnitEventType.OnTargeted,disappear);second.RemoveListener(UnitEventType.OnTargeted,disappear);Unit.AnyDamageDealt-=Hit; }
            RuiCheck("이슈타르 공지 후 표적 소멸 재선정","소모탄3발·반환/예약0",$"{removed}/{fired}/{PartyTreasury.Total}/{PartyTreasury.Reserved}",removed&&!caster.isCasting&&fired==3&&PartyTreasury.Total==0&&PartyTreasury.Reserved==0);
            yield return CaptureStill("ishtar_retarget_technical_only");
            game.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaAgniFallback(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,x=>enabled=x);
            var agni=AliveAllies().First(u=>u.ID==62);var indra=AliveAllies().First(u=>u.ID==63);
            var passive=agni.ActivePassiveCodes.OfType<AgniBurnToTheEnd>().Single();passive.StopCode();passive.CastCode();
            int basic=0,focused=0;
            void Trace(Unit owner,Unit target,AgniBurnToTheEnd.TicketKind kind)
            {
                if(owner!=agni)return;if(kind==AgniBurnToTheEnd.TicketKind.ReleasedBasic)basic++;
                if(kind==AgniBurnToTheEnd.TicketKind.ReleasedFocused)focused++;
            }
            IEnumerator StopLater() { yield return null;agni.ActiveSpecialCode.StopCode(); }
            void StopSpecial(Unit owner,ActionScheduler.ActionKind kind,string label) { if(owner==agni&&kind==ActionScheduler.ActionKind.Special)StartCoroutine(StopLater()); }
            AgniBurnToTheEnd.AnyTicket+=Trace;ActionScheduler.AnyActionStarted+=StopSpecial;
            try
            {
                yield return TrioAction(indra,ActionScheduler.ActionKind.Ultimate,s=>indra.FillUltimateResource(),r=>
                {
                    int fired=r.Finished.Count(x=>x.Unit==agni&&ActionScheduler.IsAdditional(x.Kind));
                    RuiCheck("현행 수리야 편성 아그니 SP 취소 fallback","기본1·집중0·추공1·보류없음",basic+"/"+focused+"/"+fired,basic==1&&focused==0&&fired==1&&!((Codes.Special.AgniFirstToBurn)agni.ActiveSpecialCode).HasHeldTicket);
                },r=>r.Finished.Count(x=>x.Kind==ActionScheduler.ActionKind.Special)>=SpecialAction.Participants(indra).Count);
            }
            finally { AgniBurnToTheEnd.AnyTicket-=Trace;ActionScheduler.AnyActionStarted-=StopSpecial;GameManager.Instance.enabled=enabled; }
            GameManager.Instance.DebugResetBattle();RuiFinish();
        }

        private IEnumerator ScenarioQaIshtarScheduled(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); _yvFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, x=>enabled=x);
            var game = GameManager.Instance;
            var ish = AliveAllies().Single(x=>x.ID==220);
            var ultimate = (IshtarRoyalBarrage)ish.ActiveUltimateCode;
            foreach (float speed in new[]{1f,4f})
            {
                Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(speed);
                foreach (var unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
                PartyTreasury.BeginBattle(); PartyTreasury.Charge(ish,12);
                using var record = new YvRecorder();
                yield return YvRunScheduled(s=>ish.FillUltimateResource(),
                    ()=>record.Finished.Any(x=>x.Unit==ish&&x.Kind==ActionScheduler.ActionKind.Ultimate),60f);
                int shots = record.Incoming.Count(x=>x.Attacker==ish&&x.IsTreasurySpendAttack);
                RuiCheck(speed+"배속 실제 스케줄러12연사", "12피해·정상완료·재보0·예약0",
                    shots+"/"+ultimate.CompletedNormally+"/"+PartyTreasury.Total+"/"+PartyTreasury.Reserved,
                    shots==12&&ultimate.CompletedNormally&&PartyTreasury.Total==0&&PartyTreasury.Reserved==0);
            }
            yield return CaptureStill("ishtar_scheduled_barrage_technical_only");
            game.DebugResetBattle(); game.enabled=enabled;
            RuiCheck("연사 검증 실행 완료", "별도 제한시간 초과0", _yvFailures.Count.ToString(), _yvFailures.Count==0); RuiFinish();
        }

        private IEnumerator ScenarioQaAudioRoutes(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 1, false, false, false, 1);
            foreach (string cue in new[]{"ishtar.normal.projectile", "ishtar.normal.hit", "orpheus.lament.cast"})
            {
                int before = _result.sfxEvents.Count;
                AudioManager.Instance.PlaySfx(cue);
                yield return null;
                var emitted = _result.sfxEvents.Skip(before).FirstOrDefault(x=>x.cue==cue);
                RuiCheck("누락 음향 큐 " + cue, "클립 로드·실제 재생", emitted == null ? "이벤트 없음" : emitted.reason + "/" + emitted.clip,
                    emitted != null && emitted.played && !string.IsNullOrWhiteSpace(emitted.clip));
                yield return WaitUnscaled(.2f);
            }
            yield return CaptureStill("qa_audio_routes_loaded_not_listening_verdict");
            GameManager.Instance.DebugResetBattle(); RuiFinish();
        }

        private IEnumerator ScenarioQaNoviceUi(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun("party5");yield return PrepareStage(config,1,false,false,false,1);
            var game=GameManager.Instance;var ui=game.uiManager;Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(1);
            var placement=GameObject.Find("Prep_배치 변경");
            RuiCheck("초보 준비 화면의 행동 명칭","배치 변경",RuiText(placement?.transform,"Heading"),placement!=null&&RuiText(placement.transform,"Heading")=="배치 변경");
            yield return CaptureStill("root_novice_preparation");
            ui.ShowCodex();yield return WaitUnscaled(.25f);var party=GameObject.Find("CodexScreen");
            var tabs=FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Exclude).Where(t=>t.text=="보상 코드"||t.text=="고유 스킬").ToArray();
            RuiCheck("보상 편집과 고유 스킬 탭 구분","두 명칭·넘침없음",string.Join("/",tabs.Select(t=>t.text)),tabs.Length==2&&tabs.All(t=>!t.isTextOverflowing));
            yield return CaptureStill("root_novice_reward_codes");
            RuiClick(RuiFind(party,"Tab코드"));yield return WaitUnscaled(.2f);yield return CaptureStill("root_novice_unique_skills");
            ui.CodexForVerification.Hide();game.OpenShopFromPreparation();yield return WaitUnscaled(.8f);
            var stage=FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Exclude).FirstOrDefault(t=>t.text.Contains("층 시세"));
            RuiCheck("상점 층수와 테마 표기 구분","1층 시세",stage?.text,stage!=null&&stage.text=="1층 시세 · 기본가 × 1");
            yield return CaptureStill("root_novice_shop");ModalScreen.TryEscapeTop();
            ui.ShowGuide();yield return WaitUnscaled(.2f);yield return CaptureStill("root_novice_guide");
            ui.GuideForVerification.ShowArticle("start"); yield return WaitUnscaled(.2f);
            yield return CaptureStill("root_novice_quick_start_guide");
            GameTermLinks.OpenArticle("unit:1");yield return WaitUnscaled(.2f);yield return CaptureStill("root_novice_character_guide");
            // 열람 위치를 기억하는 실제 UI를 다음 독립 시나리오에 넘기지 않는다.
            ui.GuideForVerification.ShowArticle("home");
            ui.GuideForVerification.Hide();game.DebugResetBattle();RuiFinish();
        }
    }
}
#endif
