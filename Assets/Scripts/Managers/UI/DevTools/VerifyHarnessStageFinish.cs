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
using Entities.View;
using Managers.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator S6FiveFinish(IEnumerator native,VerifyScenarioRequest config)
        {
            yield return native;
            yield return ScenarioS6Combat(config);
        }
        private IEnumerator S6Observe(IEnumerator body, VerifyScenarioRequest config)
        {
            var trace=new List<string>();
            string Key(Unit u)=>u==null?"null":u.IsSummon?$"summon:{u.SummonOwner?.ID}:{u.SummonKind}":$"{(u.IsEnemy?"E":"A")}:{u.ID}:{u.currentCell?.xPos}:{u.currentCell?.yPos}";
            void Start(Unit u,ActionScheduler.ActionKind k,string label)=>trace.Add($"start|{Key(u)}|{k}|{label}");
            void Finish(Unit u,ActionScheduler.ActionKind k)=>trace.Add($"end|{Key(u)}|{k}");
            void Damage(DamageResolvedContext c)=>trace.Add($"damage|{Key(c.Attacker)}|{Key(c.Target)}|{c.DamageContext.CodeType}|{c.DamageDealt}|{c.DamageContext.HpDamage}|{c.DamageContext.IsCrit}|{string.Join(",",c.DamageContext.DamageTags)}");
            float prior=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            using(CombatRandom.BeginSeed(20261029))
            {
                ActionScheduler.AnyActionStarted+=Start;ActionScheduler.AnyActionFinished+=Finish;Unit.AnyDamageDealt+=Damage;
                try{yield return body;}
                finally
                {
                    ActionScheduler.AnyActionStarted-=Start;ActionScheduler.AnyActionFinished-=Finish;Unit.AnyDamageDealt-=Damage;
                    Time.captureDeltaTime=prior;
                    string state=string.Join(";",AliveAllies().Concat(AliveEnemies()).OrderBy(Key).Select(u=>
                        $"{Key(u)}:{u.HpCurr}:{u.ShieldCurr}:{u.ManaCurr}:{u.isActive}:{string.Join(",",u.ActiveStatuses.Select(s=>s.StatusName).OrderBy(s=>s))}"));
                    RecordMechanic("s6_parity_trace","Card/Stage 동일 시드 사건 순서 비교",string.Join("\n",trace),true);
                    RecordMechanic("s6_parity_result","Card/Stage HP·보호막·마나·활성·상태·RNG 비교",state+"|rng="+CombatRandom.SeedState,true);
                }
            }
        }

        private IEnumerator ScenarioS6Combat(VerifyScenarioRequest config)
        {
            bool fast=_currentScenario=="speed8";
            bool five=_currentScenario=="five_party";
            yield return PrepareStage(config,Stage(config,fast||five?6:1),false,false,false,fast?8f:1f);
            ConfigureStageHudEnemies(config);
            var game=GameManager.Instance; bool wasEnabled=game.enabled;game.enabled=true;
            foreach(var u in AliveAllies().Concat(AliveEnemies()))YvBoostEnemy(u);
            game.StartRound();yield return WaitForState(BaseEnums.GameState.RoundInProgress,5f);
            game.enabled=false;
            bool inBattle=game.gameState==BaseEnums.GameState.RoundInProgress;
            RecordMechanic("s6_round_state","실제 RoundInProgress 진입 후만 전투 비교",$"state={game.gameState}, run={RunManager.Instance.RunActive}",inBattle);
            if(!inBattle){game.enabled=wasEnabled;yield break;}
            GridManager.Instance.RefreshFieldLayout();
            int completed=0;
            void Finish(Unit u,ActionScheduler.ActionKind k)=>completed++;
            ActionScheduler.AnyActionFinished+=Finish;
            try
            {
                yield return CaptureStill(fast?"speed8_entry":five?"combat_5v5_entry":"combat_1v1_entry");
                RecordMechanic("s6_actual_lineup","실제 출전 편성·배치",string.Join("/",AliveAllies().Concat(AliveEnemies()).Select(u=>$"{u.ID}:{u.UnitName}({u.currentCell.xPos},{u.currentCell.yPos})")),
                    five?(AliveAllies().Count==5&&AliveEnemies().Count==5):fast||(AliveAllies().Count==1&&AliveEnemies().Count==1));
                float deadline=Time.realtimeSinceStartup+180f;
                while((completed<12||game.ActionScheduler.IsPresenting)&&Time.realtimeSinceStartup<deadline)
                {game.ActionScheduler.Tick(Time.deltaTime);yield return null;}
                RecordMechanic("s6_combat_complete","12행동 완료",completed.ToString(),completed>=12);
                yield return CaptureStill(fast?"speed8_end":"combat_1v1_end");
            }
            finally{ActionScheduler.AnyActionFinished-=Finish;game.ActionScheduler.EndRound();game.enabled=wasEnabled;}
        }

        private void CheckStageResourceRings(UnitCardView[] cards)
        {
            foreach(var card in cards)
            {
                var unit=card.BoundUnit;
                var fill=card.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.name=="LiquidFront");
                int expected=unit.UltimateResourceType==BaseEnums.UltimateResourceType.Passive?22:
                    unit.UltimateResourceType==BaseEnums.UltimateResourceType.Stack?Mathf.Max(1,unit.ManaMax):1;
                int runs=0; bool prior=false;
                if(fill?.sprite!=null)
                    for(int i=-1;i<1440;i++)
                    {
                        float a=(i+.5f)/1440f*Mathf.PI*2;
                        bool on=fill.sprite.texture.GetPixel(Mathf.RoundToInt(63.5f+55*Mathf.Sin(a)),
                            Mathf.RoundToInt(63.5f+55*Mathf.Cos(a))).a>.15f;
                        if(i>=0&&on&&!prior)runs++; prior=on;
                    }
                if(runs==0)runs=1;
                bool radial=fill!=null&&fill.enabled&&fill.type==Image.Type.Filled&&fill.fillMethod==Image.FillMethod.Radial360;
                bool pattern=runs==expected;
                RuiCheck("Stage 초월 호 "+unit.ID,"마나 연속·스택 "+expected+"분절·상시22점선",
                    $"kind={unit.UltimateResourceType}, radial={radial}, runs={runs}, fill={fill?.fillAmount}",radial&&pattern);
            }
        }

        private IEnumerator ScenarioS6CutIn(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config,1,false,false,false);
            ConfigureStageHudEnemies(config);
            GameManager game=GameManager.Instance;bool enabled=game.enabled;game.enabled=false;
            game.gameState=BaseEnums.GameState.RoundInProgress;game.uiManager?.HidePreparationPhasePanel();
            GridManager.Instance.RefreshFieldLayout();DamageMeter.Begin(AliveAllies());
            foreach(var u in AliveAllies().Concat(AliveEnemies())){YvBoostEnemy(u);u.CastPassiveCode();u.ClearUltimateResource();}
            Unit caster=AliveAllies().First(u=>UltimateCutIn.Window(u)>=.5f);
            float prior=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            try
            {
                foreach(float speed in new[]{1f,2f,3f,4f,5f})
                {
                    bool finished=false,crest=false,paused=false;int hits=0,lastPhase=-1;float start=0,first=-1;
                    void Start(Unit u,ActionScheduler.ActionKind k,string l){if(u==caster&&k==ActionScheduler.ActionKind.Ultimate)start=Time.time;}
                    void End(Unit u,ActionScheduler.ActionKind k){if(u==caster&&k==ActionScheduler.ActionKind.Ultimate)finished=true;}
                    void Hit(Unit t,DamageContext c){if(c.Attacker==caster&&c.CodeType==BaseEnums.CodeType.Ultimate){hits++;if(first<0)first=Time.time-start;}}
                    Time.timeScale=speed;
                    foreach(var u in AliveAllies())u.ClearUltimateResource();
                    game.ActionScheduler.BeginRound();caster.FillUltimateResource();
                    ActionScheduler.AnyActionStarted+=Start;ActionScheduler.AnyActionFinished+=End;Unit.AnyDamageIncoming+=Hit;
                    try
                    {
                        float deadline=Time.realtimeSinceStartup+30;
                        while((!finished||game.ActionScheduler.IsPresenting)&&Time.realtimeSinceStartup<deadline)
                        {
                            game.ActionScheduler.Tick(Time.deltaTime);yield return EndOfFrame;
                            var cut=UltimateCutIn.Current;
                            if(cut==null||cut.Progress>=1)continue;
                            int phase=Mathf.FloorToInt(cut.Progress*8);
                            if(speed==1f&&phase!=lastPhase){lastPhase=phase;Capture("cutin_motion_"+caster.ID+"_"+phase);}
                            if(!paused&&cut.Progress>=.22f)
                            {
                                paused=true;float before=cut.Progress;Vector3 face=cut.FaceWorld;
                                Time.timeScale=0;yield return WaitUnscaled(.15f);
                                RecordMechanic("s6_cutin_pause_"+speed,"일시정지 동안 시간·얼굴 고정",$"{before}/{cut.Progress}",Mathf.Abs(before-cut.Progress)<.00001f&&face==cut.FaceWorld);
                                Time.timeScale=speed;
                            }
                            if(!crest&&cut.Progress>=.4f&&cut.Progress<.86f)
                            {
                                crest=true;
                                var data=DataManager.LoadStageFigureDataList();Sprite source=caster.currentCell.Card.StageProxy.Lower.sprite;
                                var p=data.figures.Find(f=>f.sprite==source.name)??data.@default;
                                Rect art=cut.Art.rect;float faceHalf=(p.chin-p.top)*source.rect.height/source.rect.width*.60f;
                                bool contained=true;var points=new List<string>();
                                foreach(float x in new[]{p.cx-faceHalf,p.cx+faceHalf})foreach(float y in new[]{p.top,p.chin})
                                {
                                    Vector3 world=cut.Art.TransformPoint(new Vector3(art.xMin+art.width*x,art.yMax-art.height*y));
                                    Vector3 local=cut.Band.InverseTransformPoint(world);contained&=cut.Band.rect.Contains(local);points.Add(local.ToString());
                                }
                                RecordMechanic("s6_cutin_face_"+speed,"실제 원화 머리~턱 사각형 네 점 모두250px 띠 안",string.Join("/",points),contained);
                                RecordMechanic("s6_cutin_nonblocking_"+speed,"Canvas 입력 비차단·기술명 x860·높이1100·얼굴 pivot",
                                    $"pivot={cut.Art.pivot}, height={art.height}",!cut.GetComponent<CanvasGroup>().blocksRaycasts&&Mathf.Abs(art.height-1100)<.01f&&Mathf.Abs(cut.Art.pivot.x-p.cx)<.001f);
                                var skill=cut.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Skill");skill.ForceMeshUpdate();
                                bool textInside=true;
                                foreach(var ch in skill.textInfo.characterInfo.Take(skill.textInfo.characterCount).Where(c=>c.isVisible))
                                    foreach(var v in new[]{ch.bottomLeft,ch.topLeft,ch.topRight,ch.bottomRight})
                                        textInside&=cut.Band.rect.Contains(cut.Band.InverseTransformPoint(skill.transform.TransformPoint(v)));
                                RecordMechanic("s6_cutin_title_"+speed,"기술명 실제 글자 네 모서리 띠 안·명조78px",
                                    $"font={skill.font.faceInfo.familyName}, size={skill.fontSize}, textInside={textInside}",textInside&&Mathf.Abs(skill.fontSize-78)<.01f);
                                if(speed==1f)Capture("cutin_crest");
                            }
                        }
                        RecordMechanic("s6_cutin_resolve_"+speed,"정상 종료·실제 피해1·기존 창 안 컷인",$"done={finished}, hits={hits}, first={first:F6}, crest={crest}",finished&&hits==1&&crest&&first>=.5f-.02f);
                    }
                    finally{ActionScheduler.AnyActionStarted-=Start;ActionScheduler.AnyActionFinished-=End;Unit.AnyDamageIncoming-=Hit;game.ActionScheduler.EndRound();}
                }
                CheckStageResourceRings(FindObjectsByType<UnitCardView>(FindObjectsSortMode.None).Where(c=>c.BoundUnit!=null&&c.BoundUnit.IsOnField).ToArray());
                UltimateCutIn.Play(caster);
                VisualModeState.Set(VisualMode.Card);
                RecordMechanic("s6_cutin_card_exit","Card 전환 이벤트 안에서 즉시 오버레이 종료",
                    UltimateCutIn.Current.GetComponent<CanvasGroup>().alpha.ToString(),UltimateCutIn.Current.GetComponent<CanvasGroup>().alpha==0f);
                VisualModeState.Set(VisualMode.Stage);
            }
            finally{Time.captureDeltaTime=prior;Time.timeScale=1;game.gameState=BaseEnums.GameState.Preparation;game.enabled=enabled;}
        }

        private IEnumerator ScenarioS6Resources(VerifyScenarioRequest config)
        {
            bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            var game=GameManager.Instance;
            game.gameState=BaseEnums.GameState.RoundInProgress;game.uiManager?.HidePreparationPhasePanel();
            foreach(var u in AliveAllies())
            {
                foreach(string key in u.CombatResourceIds.ToArray())u.AddCombatResource(key,u.GetCombatResourceMaximum(key));
                if(u.ID==121)u.SetPermanentStacks(Codes.Passive.AnubisSoulHarvest.SoulKey,12345,.5f);
            }
            GridManager.Instance.RefreshFieldLayout();yield return WaitUnscaled(.3f);
            try
            {
                foreach(var u in AliveAllies())
                {
                    var view=u.currentCell.Card;var label=view.GetComponentsInChildren<TextMeshPro>(true).First(t=>t.name=="ResourceValue");
                    RecordStageHudCheck(view,"long_resources",Camera.main);
                    label.ForceMeshUpdate();Rect hud=view.HudScreenRect(Camera.main);
                    if(!label.enabled)
                    {
                        bool noAmmo=u.ID==67&&u.GetCombatResourceMaximum(Codes.Passive.SuryaBeforeSunset.BulletResourceId)==0;
                        RecordMechanic("s6_resource_hidden_"+u.ID,"현행 자원 미생성 시 기존 표시 규칙대로 숨김",$"label={label.enabled}, ids={string.Join(",",u.CombatResourceIds)}",noAmmo);
                        continue;
                    }
                    bool fits=true;var points=new List<Vector2>();
                    foreach(var ch in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible))
                        foreach(var v in new[]{ch.bottomLeft,ch.topLeft,ch.topRight,ch.bottomRight})
                        {
                            Vector2 s=Camera.main.WorldToScreenPoint(label.transform.TransformPoint(v));points.Add(s);fits&=hud.Contains(s);
                        }
                    float px=Screen.height/(Camera.main.orthographicSize*2f);
                    float nominal=label.fontSize*label.transform.lossyScale.y*.1f*px;
                    RecordMechanic("s6_resource_mesh_"+u.ID,"16px/.1 기준 그대로·실제 글자 전부118×150 하단 카드 안",
                        $"text={label.text}, nominal={nominal:F3}, preferred={label.preferredWidth:F3}, hud={hud}, fits={fits}, vertices={points.Count}",
                        label.enabled&&nominal>=16&&points.Count>0&&fits&&Mathf.Abs(hud.width-118)<.1f&&Mathf.Abs(hud.height-150)<.1f);
                }
                CheckStageResourceRings(FindObjectsByType<UnitCardView>(FindObjectsSortMode.None).Where(c=>c.BoundUnit!=null&&c.BoundUnit.IsOnField).ToArray());
                yield return CaptureStill("resource_containment");
            }
            finally{game.gameState=BaseEnums.GameState.Preparation;game.enabled=enabled;}
        }

        private void CheckS6DamageMeter()
        {
            var allies=AliveAllies();
            var rows=FindObjectsByType<StageUnitFocus>(FindObjectsSortMode.None)
                .Where(l=>l.name.StartsWith("Row")&&l.BoundUnit!=null).ToArray();
            foreach(var row in rows)
            {
                var portrait=row.transform.Find("Portrait")?.GetComponent<Image>();
                var unit=row.BoundUnit;
                var source=Helpers.SpriteResource.LoadPortrait(unit.PortraitPath);
                RecordMechanic("s6_meter_face_"+unit.ID,"현재 생존 편성·원본 초상 texture·흰 tint(실루엣 아님)",
                    $"unit={unit.UnitName}, sprite={portrait?.sprite?.name}, tint={portrait?.color}",
                    allies.Contains(unit)&&unit.isActive&&portrait!=null&&portrait.enabled&&portrait.sprite!=null&&
                    source!=null&&portrait.sprite.texture==source.texture&&portrait.color==Color.white);
            }
            RecordMechanic("s6_meter_roster","현재 아군 전원과 행 소유자 일치",$"{rows.Length}/{allies.Count}",
                rows.Length==allies.Count&&allies.All(u=>rows.Any(r=>r.BoundUnit==u)));
        }

        private IEnumerator ScenarioS6Timing(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config,6),false,false,false);
            if(config.trigger=="enemies")ConfigureStageHudEnemies(config);
            GameManager game=GameManager.Instance; bool enabled=game.enabled;
            game.enabled=false; game.gameState=BaseEnums.GameState.RoundInProgress;
            game.uiManager?.HidePreparationPhasePanel(); game.ActionScheduler.BeginRound();
            DamageMeter.Begin(AliveAllies());
            float delta=Time.captureDeltaTime; Time.captureDeltaTime=1f/60f; Time.timeScale=1f;
            try
            {
                foreach(var ally in AliveAllies().Concat(AliveEnemies())){YvBoostEnemy(ally);ally.CastPassiveCode();}
                GridManager.Instance.RefreshFieldLayout();
                yield return new WaitForSeconds(.5f);
                foreach(var caster in (config.trigger=="enemies"?AliveEnemies():AliveAllies()).ToArray())
                {
                    var code=caster.ActiveUltimateCode;
                    if(code==null)continue;
                    float start=Time.time, first=-1f;
                    void Hit(Unit target,DamageContext context)
                    {
                        if(first<0f&&context.Attacker==caster&&context.CodeType==BaseEnums.CodeType.Ultimate)
                            first=Time.time-start;
                    }
                    Unit.AnyDamageIncoming+=Hit;
                    try
                    {
                        if(code.IsAutoCast&&!code.IsPersistent)
                        {
                            foreach(var u in AliveAllies().Concat(AliveEnemies()))u.ModifyHp(Mathf.RoundToInt(u.HpMax*.6f));
                            PartyTreasury.BeginBattle();PartyTreasury.Charge(caster,7);
                            if(caster.ID==180){Summons.Create(caster);Summons.Create(caster);}
                            caster.FillUltimateResource(); start=Time.time;
                            caster.CastUltimateCode();
                            float limit=Time.time+12f;
                            while(caster.isCasting&&Time.time<limit)yield return null;
                        }
                        RecordMechanic("s6_timing_"+caster.ID+"_"+code.GetType().Name,"시전 호출→첫 초월 피해 진입(1배60Hz), 무피해/상시는 생략",
                            $"{caster.UnitName}|{code.CodeName}|{first:F6}|{Time.time-start:F6}|auto={code.IsAutoCast}|persistent={code.IsPersistent}",!caster.isCasting);
                    }
                    finally{Unit.AnyDamageIncoming-=Hit;}
                    caster.ActiveUltimateCode.StopCode();
                    yield return new WaitForSeconds(.4f);
                }
            }
            finally
            {
                Time.captureDeltaTime=delta; game.ActionScheduler.EndRound(); DamageMeter.End();
                game.gameState=BaseEnums.GameState.Preparation;game.enabled=enabled;
            }
        }

        private IEnumerator ScenarioS6Prep(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config,Stage(config,6),false,false,false);
            ConfigureStageHudEnemies(config);
            if (config?.vfxEnemyIds?.Length == 5)
                RecordMechanic("s1_prep_five_lineup", "준비 실제 5:5 편성",
                    string.Join("/",AliveAllies().Concat(AliveEnemies()).Select(u=>$"{u.ID}:{u.UnitName}({u.currentCell.xPos},{u.currentCell.yPos})")),
                    AliveAllies().Count==5 && AliveEnemies().Count==5);
            var grid=GridManager.Instance; var drag=DragAndDropManager.Instance;
            grid.RefreshFieldLayout(); yield return null; Physics2D.SyncTransforms();
            var cells=FindObjectsByType<Cell>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(c=>c.xPos is -2 or -1 && c.yPos>=1&&c.yPos<=4).ToList();
            Camera camera=Camera.main;
            foreach(var cell in cells)
            {
                var marker=cell.transform.Find("StagePreparationSlot")?.GetComponent<SpriteRenderer>();
                var collider=cell.GetComponent<BoxCollider2D>();
                float px=Screen.height/(camera.orthographicSize*2f);
                RecordMechanic("s6_prep_slot_"+cell.xPos+"_"+cell.yPos,"표식8칸170×46px·발 중심 콜라이더",
                    $"active={cell.gameObject.activeInHierarchy}, marker={marker?.bounds.size}, collider={collider.bounds}",
                    cell.gameObject.activeInHierarchy&&marker!=null&&marker.enabled&&
                    Mathf.Abs(marker.bounds.size.x*px-170)<1&&Mathf.Abs(marker.bounds.size.y*px-46)<1&&
                    (collider.bounds.center-cell.transform.position).sqrMagnitude<.001f&&
                    Mathf.Abs(collider.bounds.size.x*px-170)<1&&Mathf.Abs(collider.bounds.size.y*px-46)<1);
            }
            RecordS7StagePixels("preparation");
            yield return null;
            yield return CaptureStill("preparation");
            Unit actor=AliveAllies().First(); Cell from=actor.currentCell;
            Cell empty=cells.First(c=>!c.isOccupied);
            Vector2 ScreenPoint(Cell c)=>camera.WorldToScreenPoint(c.transform.position);
            drag.StartDrag(from,ScreenPoint(from));yield return null;
            drag.UpdateDragPointer(ScreenPoint(empty));yield return null;
            var target=empty.transform.Find("StagePreparationSlot")?.GetComponent<SpriteRenderer>();
            RecordMechanic("s6_prep_target","목표 칸 진한 테두리",target?.sprite?.name??"없음",target!=null&&target.enabled);
            drag.EndDrag(ScreenPoint(empty)); yield return WaitUnscaled(.35f);
            RecordMechanic("s6_prep_move","외부/모바일 화면 좌표 드롭 빈칸 이동",$"{actor.currentCell?.xPos}/{actor.currentCell?.yPos}",actor.currentCell==empty&&!from.isOccupied);
            Unit other=AliveAllies().First(u=>u!=actor);Cell otherCell=other.currentCell;
            drag.StartDrag(actor.currentCell,ScreenPoint(actor.currentCell));yield return null;
            drag.EndDrag(ScreenPoint(otherCell));yield return WaitUnscaled(.35f);
            RecordMechanic("s6_prep_swap","점유 칸 교환·같은 객체 유지",$"{actor.currentCell==otherCell}/{other.currentCell==empty}",actor.currentCell==otherCell&&other.currentCell==empty);
            var selection=CharacterSelectionManager.Instance;
            var saved=selection.Lineup.Select(e=>(e.UnitId,e.XPos,e.YPos)).ToArray();
            selection.ClearLineup(); bool first=selection.AddHero(actor.ID), duplicate=selection.AddHero(actor.ID);
            RecordMechanic("s6_prep_duplicate","기존 편성 API 중복 거절·드래그 후 객체/ID 중복0",
                $"first={first}, duplicate={duplicate}, count={AliveAllies().Count}",first&&!duplicate&&
                AliveAllies().Select(u=>u.ID).Distinct().Count()==AliveAllies().Count&&
                cells.Where(c=>c.isOccupied).Select(c=>c.unit).Distinct().Count()==AliveAllies().Count);
            selection.ClearLineup();foreach(var entry in saved){selection.AddHero(entry.UnitId);
                var restore=selection.Lineup.Last();restore.XPos=entry.XPos;restore.YPos=entry.YPos;}
            // 토글 뒤 원래 Card 콜라이더와 화면도 되돌아가야 한다.
            VisualModeState.Set(VisualMode.Card);yield return null;
            RecordMechanic("s6_prep_card_restore","Card 콜라이더 원본10.16",from.GetComponent<BoxCollider2D>().size.ToString(),
                Mathf.Abs(from.GetComponent<BoxCollider2D>().size.x-10.16f)<.01f);
            VisualModeState.Set(VisualMode.Stage);grid.RefreshFieldLayout();yield return null;
        }
    }
}
#endif
