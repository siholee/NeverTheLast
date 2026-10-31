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
using Managers.Stage;
using TMPro;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static string S8Key(Unit u) => u == null ? "null" :
            u.IsSummon ? $"S:{u.SummonOwner?.ID}:{u.SummonKind}" : $"{(u.IsEnemy ? "E" : "A")}:{u.ID}:{u.currentCell?.xPos}:{u.currentCell?.yPos}";
        private static string S8State(IEnumerable<Unit> units) => string.Join(";", units.OrderBy(S8Key).Select(u =>
            $"{S8Key(u)}:{u.HpCurr}:{u.ShieldCurr}:{u.ManaCurr}:{u.isActive}:{string.Join(",",u.ActiveStatuses.Select(s=>s.StatusName).OrderBy(s=>s))}"));

        private IEnumerator S8Prepare(VerifyScenarioRequest config, bool startRound = false)
        {
            yield return PrepareStage(config, Stage(config, 6), false, false, false);
            RecordMechanic("s8_environment","현재 프로젝트의 열린 Unity IPC",Application.dataPath+"|"+Application.unityVersion,true);
            GameManager game = GameManager.Instance;
            game.enabled = false;
            // Card도 동일한 명시적 적 편성을 쓴다.
            if (config.vfxEnemyIds != null && config.vfxEnemyIds.Length > 0)
            {
                GridManager.Instance.ClearActiveEnemies();
                for (int i=0;i<config.vfxEnemyIds.Length;i++)
                    GridManager.Instance.SpawnUnit(i<3?1:2,i<3?i+1:i-2,true,config.vfxEnemyIds[i],playEntry:false);
            }
            if(startRound)
            {
                // 촬영도 실제 개전 수명·패시브·AV 초기화를 통과한다. GameManager의 자동 진행만 멈춘다.
                game.StartRound();
                yield return WaitForState(BaseEnums.GameState.RoundInProgress,5f);
                foreach(Unit u in AliveAllies().Concat(AliveEnemies())){YvBoostEnemy(u);u.ClearUltimateResource();}
            }
            else
            {
                game.gameState = BaseEnums.GameState.RoundInProgress;
                game.uiManager?.HidePreparationPhasePanel();
                foreach (Unit u in AliveAllies().Concat(AliveEnemies())) { YvBoostEnemy(u); u.CastPassiveCode(); u.ClearUltimateResource(); }
            }
            DamageMeter.Begin(AliveAllies());
            GridManager.Instance.RefreshFieldLayout();
            yield return GridManager.Instance.WaitForFieldLayoutSettled();
        }

        private IEnumerator ScenarioS8Parity(VerifyScenarioRequest config)
        {
            bool enabled=GameManager.Instance.enabled;
            float prior=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/60f;
            using (CombatRandom.BeginSeed(20261029))
            {
                yield return S8Prepare(config);
                var game=GameManager.Instance;
                var trace=new List<string>();int finished=0,ults=0;
                void Start(Unit u,ActionScheduler.ActionKind k,string l){trace.Add($"start|{S8Key(u)}|{k}|{l}");if(k==ActionScheduler.ActionKind.Ultimate)ults++;}
                void End(Unit u,ActionScheduler.ActionKind k){trace.Add($"end|{S8Key(u)}|{k}");finished++;}
                void Damage(DamageResolvedContext c)=>trace.Add($"damage|{S8Key(c.Attacker)}|{S8Key(c.Target)}|{c.DamageContext.CodeType}|{c.DamageDealt}|{c.DamageContext.HpDamage}|{c.DamageContext.IsCrit}|{string.Join(",",c.DamageContext.DamageTags)}");
                void Created(Unit owner, Unit summon, SummonSource source, object tag) => trace.Add($"summon_created|{S8Key(owner)}|{S8Key(summon)}|{source}");
                void Died(Unit unit, Unit attacker) => trace.Add($"died|{S8Key(unit)}|{S8Key(attacker)}");
                ActionScheduler.AnyActionStarted+=Start;ActionScheduler.AnyActionFinished+=End;Unit.AnyDamageDealt+=Damage;
                Summons.AnySummonCreated += Created; Unit.AnyUnitDied += Died;
                try
                {
                    Time.timeScale=config.timeScale>0?config.timeScale:1;
                    game.ActionScheduler.BeginRound();
                    foreach(var u in AliveAllies().Concat(AliveEnemies()))u.FillUltimateResource();
                    float deadline=Time.realtimeSinceStartup+180;
                    while((finished<24||game.ActionScheduler.IsPresenting)&&Time.realtimeSinceStartup<deadline)
                    {game.ActionScheduler.Tick(Time.deltaTime);yield return null;}
                    RecordMechanic("s8_parity_coverage","24행동 이상·초월기 포함",$"finished={finished}, ultimate={ults}",finished>=24&&ults>0);
                    RecordMechanic("s8_parity_trace","변경 전후·Card/Stage 사건 순서·수·값",string.Join("\n",trace),true);
                    RecordMechanic("s8_parity_state","HP·보호막·마나·상태·소환수·RNG",S8State(AliveAllies().Concat(AliveEnemies()).Concat(GridManager.Instance.summonList))+"|rng="+CombatRandom.SeedState,true);
                    yield return CaptureStill("parity_end");
                }
                finally
                {
                    ActionScheduler.AnyActionStarted-=Start;ActionScheduler.AnyActionFinished-=End;Unit.AnyDamageDealt-=Damage;
                    Summons.AnySummonCreated -= Created; Unit.AnyUnitDied -= Died;
                    game.ActionScheduler.EndRound();game.gameState=BaseEnums.GameState.Preparation;game.enabled=enabled;
                    Time.captureDeltaTime=prior;Time.timeScale=1;
                }
            }
        }

        private IEnumerator ScenarioS8Sample(VerifyScenarioRequest config)
        {
            bool enabled=GameManager.Instance.enabled;
            float prior=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            yield return S8Prepare(config);
            var game=GameManager.Instance;
            try
            {
                foreach(Unit caster in AliveAllies().Concat(AliveEnemies()).ToArray())
                {
                    if(caster.ActiveUltimateCode?.IsAutoCast!=true||caster.ActiveUltimateCode.IsPersistent)continue;
                    foreach(float speed in new[]{1f,5f})
                        yield return S8Cast(caster,speed,config.trigger=="after",true);
                }
            }
            finally
            {
                game.ActionScheduler.EndRound();game.gameState=BaseEnums.GameState.Preparation;game.enabled=enabled;
                Time.captureDeltaTime=prior;Time.timeScale=1;
            }
        }

        private IEnumerator S8Cast(Unit caster,float speed,bool after,bool capture)
        {
            var game=GameManager.Instance;var scheduler=game.ActionScheduler;
            foreach(var u in AliveAllies().Concat(AliveEnemies())){u.ClearUltimateResource();u.ModifyHp(Mathf.RoundToInt(u.HpMax*.6f));}
            PartyTreasury.BeginBattle();PartyTreasury.Charge(caster,7);
            if(caster.ID==180){Summons.Create(caster);Summons.Create(caster);}
            bool done=false,paused=false,crest=false;int hits=0,starts=0,ends=0,phase=-1;
            float announce=-1,start=-1,first=-1,finish=-1;string previewState=null;float previewTicks=0;
            int startFrame=-1,firstFrame=-1;
            void Announce(ActionAnnouncement a){if(a.Unit==caster&&a.Kind==ActionAnnouncementKind.Ultimate&&a.FollowsAction)announce=Time.time;}
            void Start(Unit u,ActionScheduler.ActionKind k,string l){if(u==caster&&k==ActionScheduler.ActionKind.Ultimate){start=Time.time;startFrame=Time.frameCount;starts++;}}
            void End(Unit u,ActionScheduler.ActionKind k){if(u==caster&&k==ActionScheduler.ActionKind.Ultimate){done=true;finish=Time.time;ends++;}}
            void Hit(Unit t,DamageContext c){if(c.Attacker==caster&&c.CodeType==BaseEnums.CodeType.Ultimate){hits++;if(first<0){first=Time.time;firstFrame=Time.frameCount;}}}
            ActionAnnouncer.Announced+=Announce;ActionScheduler.AnyActionStarted+=Start;ActionScheduler.AnyActionFinished+=End;Unit.AnyDamageIncoming+=Hit;
            Time.timeScale=speed;scheduler.BeginRound();caster.FillUltimateResource();
            try
            {
                bool can=caster.ActiveUltimateCode.HasValidTarget()&&caster.CanCastUltimateCode();
                if(can)
                {
                    float deadline=Time.realtimeSinceStartup+45;
                    while((!done||scheduler.IsPresenting)&&Time.realtimeSinceStartup<deadline)
                    {
                        if(scheduler.PreviewUnit==caster&&start<0)previewTicks+=Time.deltaTime;
                        scheduler.Tick(Time.deltaTime);yield return EndOfFrame;
                        var cut=UltimateCutIn.Current;
                        if(after&&VisualModeState.Current==VisualMode.Stage&&scheduler.PreviewUnit==caster&&start<0)
                        {
                            string state=S8State(AliveAllies().Concat(AliveEnemies()))+"|"+scheduler.ReferenceCyclesElapsed+"|"+scheduler.ActionCount;
                            if(previewState==null)previewState=state;
                            else if(state!=previewState)RecordMechanic("s8_preview_state_"+caster.ID,"예고 대기 중 AV·상태·HP·행동 불변",state,false);
                        }
                        if(cut==null||cut.Progress>=1||cut.GetComponent<CanvasGroup>().alpha==0)continue;
                        if(capture&&speed==1&&phase!=Mathf.FloorToInt(cut.Progress*8))
                        {phase=Mathf.FloorToInt(cut.Progress*8);Capture("motion_"+caster.ID+"_"+phase);}
                        if(after&&capture&&!paused&&cut.Progress>=.22f)
                        {
                            paused=true;float p=cut.Progress;Vector3 face=cut.FaceWorld;
                            Time.timeScale=0;yield return WaitUnscaled(.3f);scheduler.Tick(Time.deltaTime);
                            RecordMechanic("s8_pause_"+caster.ID+"_"+speed,"0.3초 정지: 컷인·스케줄러 시간 불변",$"{p}/{cut.Progress}",Mathf.Abs(p-cut.Progress)<.00001f&&face==cut.FaceWorld&&start<0);
                            Time.timeScale=speed;
                        }
                        if(!crest&&cut.Progress>=.4f&&cut.Progress<.86f)
                        {crest=true;if(capture)S8Face(caster,cut,speed);if(capture&&speed==1)Capture("face_"+caster.ID);}
                    }
                }
                RecordMechanic("s8_cast_"+caster.ID+"_"+speed,"예고→시전/시전→첫 피해·콜백 관측",
                    $"{caster.UnitName}|{caster.ActiveUltimateCode.GetType().Name}|can={can}|announce={announce:F6}|preview={(start<0?-1:start-announce):F6}|first={(first<0?-1:first-start):F6}|complete={(finish<0?-1:finish-start):F6}|firstFrames={(firstFrame<0?-1:firstFrame-startFrame)}|start={starts}|end={ends}|hits={hits}|crest={crest}|pause={paused}",!can||(done&&starts==1&&ends==1));
                if(after&&can&&VisualModeState.Current==VisualMode.Stage)
                    RecordMechanic("s8_full_"+caster.ID+"_"+speed,"Stage 초월기 1.15초 전체판·예고 Tick 누계1.51초·대기 정지",$"wallScaled={start-announce}, tickSum={previewTicks}, crest={crest}, paused={paused}",crest&&(!capture||paused)&&previewTicks>=1.15f+ActionScheduler.PreviewSeconds-.0001f&&previewTicks<1.15f+ActionScheduler.PreviewSeconds+Mathf.Max(5,speed)/60f+.02f&&previewState!=null);
            }
            finally
            {
                ActionAnnouncer.Announced-=Announce;ActionScheduler.AnyActionStarted-=Start;ActionScheduler.AnyActionFinished-=End;Unit.AnyDamageIncoming-=Hit;
                scheduler.EndRound();caster.ActiveUltimateCode.StopCode();
            }
            yield return new WaitForSeconds(.4f);
        }

        private void S8Face(Unit caster,UltimateCutIn cut,float speed)
        {
            var data=DataManager.LoadStageFigureDataList();var source=caster.currentCell.Card.StageProxy.Lower.sprite;
            var p=data.figures.Find(f=>f.sprite==source.name)??data.@default;Rect art=cut.Art.rect;
            float half=(p.chin-p.top)*source.rect.height/source.rect.width*.60f;bool inside=true;
            foreach(float x in new[]{p.cx-half,p.cx+half})foreach(float y in new[]{p.top,p.chin})
                inside&=cut.Band.rect.Contains(cut.Band.InverseTransformPoint(cut.Art.TransformPoint(new Vector3(art.xMin+art.width*x,art.yMax-art.height*y))));
            var skill=cut.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Skill");skill.ForceMeshUpdate();bool text=true;
            foreach(var ch in skill.textInfo.characterInfo.Take(skill.textInfo.characterCount).Where(c=>c.isVisible))
                foreach(var v in new[]{ch.bottomLeft,ch.topLeft,ch.topRight,ch.bottomRight})
                    text&=cut.Band.rect.Contains(cut.Band.InverseTransformPoint(skill.transform.TransformPoint(v)));
            RecordMechanic("s8_face_"+caster.ID+"_"+speed,"얼굴 네 모서리·기술명 전체가 띠 안·입력 비차단",$"face={inside}, title={text}, sprite={source.name}",inside&&text&&!cut.GetComponent<CanvasGroup>().blocksRaycasts);
        }

        private IEnumerator S8Cancel(Unit caster)
        {
            var scheduler=GameManager.Instance.ActionScheduler;Time.timeScale=1;
            foreach(var u in AliveAllies().Concat(AliveEnemies()))u.ClearUltimateResource();
            int starts=0,hits=0;void Start(Unit u,ActionScheduler.ActionKind k,string l){if(u==caster)starts++;}
            void Hit(Unit t,DamageContext c){if(c.Attacker==caster)hits++;}
            scheduler.BeginRound();caster.FillUltimateResource();
            ActionScheduler.AnyActionStarted+=Start;Unit.AnyDamageIncoming+=Hit;
            try
            {
                float beginLimit=Time.realtimeSinceStartup+10;
                while(scheduler.PreviewUnit!=caster&&Time.realtimeSinceStartup<beginLimit){scheduler.Tick(Time.deltaTime);yield return null;}
                RecordMechanic("s8_death_preview","사망 전에 컷인·실제 초월 예고 시작",$"preview={scheduler.PreviewUnit==caster}",scheduler.PreviewUnit==caster&&UltimateCutIn.Current?.GetComponent<CanvasGroup>().alpha>0);
                caster.TakeDamage(new DamageContext(AliveEnemies().First(),caster.HpMax*10,BaseEnums.CodeType.Normal,new List<int>()){DefenseStatMultiplier=0});
                yield return EndOfFrame;
                RecordMechanic("s8_death_close","대기 중 시전자 실제 사망 즉시 컷인 닫힘",$"active={caster.isActive}, alpha={UltimateCutIn.Current?.GetComponent<CanvasGroup>().alpha}",!caster.isActive&&UltimateCutIn.Current?.GetComponent<CanvasGroup>().alpha==0);
                float deadline=Time.realtimeSinceStartup+10;
                while(scheduler.PreviewUnit!=null&&Time.realtimeSinceStartup<deadline){scheduler.Tick(Time.deltaTime);yield return null;}
                RecordMechanic("s8_death_cancel","기존 대기 끝 재확인에서 실행·피해0",$"start={starts}, hits={hits}, preview={scheduler.PreviewUnit}",starts==0&&hits==0&&scheduler.PreviewUnit==null);
            }
            finally{scheduler.EndRound();ActionScheduler.AnyActionStarted-=Start;Unit.AnyDamageIncoming-=Hit;}
        }

        private IEnumerator ScenarioS8Timing(VerifyScenarioRequest config)
        {
            // 같은 5인 fixture를 매 그룹 새로 만든다. 각 코드의 실제 예약 가능 여부도 구분한다.
            bool enabled=GameManager.Instance.enabled;float prior=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            var roster=DataManager.LoadUnitDataList().units.Select(u=>(u.id,false))
                .Concat(DataManager.LoadEnemyDataList().enemies.Select(e=>(e.id,true))).ToArray();
            var game=GameManager.Instance;var grid=GridManager.Instance;
            try
            {
                foreach(bool enemy in new[]{false,true})
                {
                    var ids=roster.Where(x=>x.Item2==enemy).Select(x=>x.Item1).ToArray();
                    for(int offset=0;offset<ids.Length;offset+=5)
                    {
                        game.DebugResetBattle();game.gameState=BaseEnums.GameState.Preparation;
                        foreach(Unit u in grid.heroList.Concat(grid.enemyList).ToArray())grid.RetireUnit(u);
                        yield return null;
                        game.RoundManager.InitializeStage(6);game.enabled=false;
                        int[] opponents={1,2,3,7,81};
                        for(int i=0;i<5;i++)grid.SpawnUnit(i<3?(enemy?-1:1):(enemy?-2:2),i<3?i+1:i-2,!enemy,enemy?opponents[i]:1001,playEntry:false);
                        var casters=new List<Unit>();
                        for(int i=0;i<5&&offset+i<ids.Length;i++)casters.Add(grid.SpawnUnit((enemy?1:-1)*(i<3?1:2),i<3?i+1:i-2,enemy,ids[offset+i],playEntry:false));
                        game.gameState=BaseEnums.GameState.RoundInProgress;game.uiManager?.HidePreparationPhasePanel();DamageMeter.Begin(AliveAllies());
                        foreach(Unit u in AliveAllies().Concat(AliveEnemies())){YvBoostEnemy(u);u.CastPassiveCode();u.ClearUltimateResource();}
                        grid.RefreshFieldLayout();yield return grid.WaitForFieldLayoutSettled();
                        foreach(Unit caster in casters)
                        {
                            var code=caster.ActiveUltimateCode;
                            RecordMechanic("s8_timing_code_"+caster.ID,"현행 로스터의 초월 코드 분류",$"{(enemy?"E":"A")}|{caster.UnitName}|{code?.CodeName}|{code?.GetType().Name}|auto={code?.IsAutoCast}|persistent={code?.IsPersistent}|resource={caster.UltimateResourceType}",true);
                            if(code==null||!code.IsAutoCast||code.IsPersistent)
                                RecordMechanic("s8_timing_skip_"+caster.ID,"코드 없음/상시형은 실제 Ultimate 예약 아님",$"{caster.UnitName}|{code?.GetType().Name}|auto={code?.IsAutoCast}|persistent={code?.IsPersistent}",true);
                            else yield return S8Cast(caster,1,config.trigger=="after",false);
                        }
                    }
                }
                yield return CaptureStill("timing_complete");
            }
            finally{game.ActionScheduler.EndRound();game.gameState=BaseEnums.GameState.Preparation;game.enabled=enabled;Time.captureDeltaTime=prior;Time.timeScale=1;}
        }

        private IEnumerator ScenarioS8Guards(VerifyScenarioRequest config)
        {
            bool enabled=GameManager.Instance.enabled;
            yield return S8Prepare(config);
            var scheduler=GameManager.Instance.ActionScheduler;var caster=AliveAllies().First(u=>u.ID==81);
            try
            {
                foreach(VisualMode mode in new[]{VisualMode.Card,VisualMode.Stage})
                {
                    VisualModeState.Set(mode);
                    using(CombatRandom.BeginSeed(73))
                        foreach(ActionScheduler.ActionKind kind in Enum.GetValues(typeof(ActionScheduler.ActionKind)))
                            RecordMechanic("s8_seed_zero_"+mode+"_"+kind,"시드 세션 추가 시간0",ActionPresentation.ExtraPreviewSeconds(caster,kind).ToString(),ActionPresentation.ExtraPreviewSeconds(caster,kind)==0);
                }
                VisualModeState.Set(VisualMode.Card);
                RecordMechanic("s8_card_zero","Card 초월기 추가 시간0",ActionPresentation.ExtraPreviewSeconds(caster,ActionScheduler.ActionKind.Ultimate).ToString(),ActionPresentation.ExtraPreviewSeconds(caster,ActionScheduler.ActionKind.Ultimate)==0);
                VisualModeState.Set(VisualMode.Stage);
                foreach(ActionScheduler.ActionKind kind in Enum.GetValues(typeof(ActionScheduler.ActionKind)))
                    if(kind!=ActionScheduler.ActionKind.Ultimate)
                        RecordMechanic("s8_kind_zero_"+kind,"실제 Ultimate 종류만 추가 시간",ActionPresentation.ExtraPreviewSeconds(caster,kind).ToString(),ActionPresentation.ExtraPreviewSeconds(caster,kind)==0);
                foreach(bool controlled in new[]{true,false})
                {
                    foreach(var u in AliveAllies().Concat(AliveEnemies()))u.ClearUltimateResource();
                    scheduler.BeginRound();bool can=true;int ran=0,cancelled=0;
                    // 검증 fixture에서만 private 예약 API를 호출한다. 런타임 접근 제한자는 유지한다.
                    var enqueue=typeof(ActionScheduler).GetMethod("Enqueue",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                    enqueue.Invoke(scheduler,new object[]{caster,ActionScheduler.ActionKind.Ultimate,"s8-cancel","취소 경계",(Action)(()=>ran++),(Func<bool>)(()=>can),(Action)(()=>cancelled++)});
                    scheduler.Tick(1f/60f);yield return null;
                    if(controlled)caster.ControlStarts(new ControlContext(AliveEnemies().First(),2));else can=false;
                    float deadline=Time.realtimeSinceStartup+10;
                    while(scheduler.PreviewUnit!=null&&Time.realtimeSinceStartup<deadline){scheduler.Tick(Time.deltaTime);yield return null;}
                    yield return EndOfFrame;
                    RecordMechanic("s8_cancel_"+(controlled?"control":"CanRun"),"기존 재확인 실패: 실행0·취소1·컷인 닫힘",$"run={ran}, cancel={cancelled}, alpha={UltimateCutIn.Current?.GetComponent<CanvasGroup>().alpha}",ran==0&&cancelled==1&&UltimateCutIn.Current?.GetComponent<CanvasGroup>().alpha==0);
                    if(controlled)caster.ControlEnds();scheduler.EndRound();
                }
                yield return S8Cancel(caster);
                yield return CaptureStill("guards_complete");
            }
            finally{scheduler.EndRound();GameManager.Instance.gameState=BaseEnums.GameState.Preparation;GameManager.Instance.enabled=enabled;}
        }
    }
}
#endif
