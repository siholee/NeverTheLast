#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Restored;
using Combat;
using Core;
using Entities;
using Helpers;
using UnityEngine;
using UnityEngine.UI;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioOriginalThemes(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); yield return BuildFreshRun(ResolveLineup(config)); yield return PrepareStage(config, 17, false, false, false);
            var game = GameManager.Instance; var data = game.dataManager.FetchStageThemeDataList();
            var themes = data.stageThemes.Where(t=>t.enabled && !t.isGeneric).ToList();
            var enemies = game.dataManager.FetchEnemyDataList().enemies;
            void Check(string name, bool valid, string info="") => RuiCheck(name,"사양 일치",info,valid);
            Check("오리지널18·범용4·시험장비활성", themes.Count == 18 && data.stageThemes.Count(t=>t.enabled&&t.isGeneric)==4 && !data.stageThemes.First(t=>t.id==1).enabled);
            Check("테마·적 ID 충돌 없음", data.stageThemes.Select(t=>t.id).Distinct().Count()==data.stageThemes.Count && enemies.Select(e=>e.id).Distinct().Count()==enemies.Count);
            foreach(var enemy in enemies.Where(e=>e.reworkKit != null))
            {
                // 인스턴스 초기화는 실제 스폰 루프에서 한다. 원화/데이터/설명은 모든154종을 검사한다.
                Check(enemy.id+" 원화·8스탯·코드 설명", SpriteResource.LoadPortrait(enemy.portrait)!=null && (string.IsNullOrEmpty(enemy.standing)||SpriteResource.LoadStanding(enemy.standing)!=null) && enemy.hpBase>0 && enemy.atkBase>0 && enemy.speedBase>0 &&
                    new[]{CodeCatalog.Slot.Passive,CodeCatalog.Slot.Normal,CodeCatalog.Slot.Ultimate}.All(s=>CodeCatalog.Find(s,enemy.CodeId(s.ToString().ToLowerInvariant()))?.description?.Length>0),enemy.name);
            }
            Check("오리지널154종·범용25종·고정 보스2종·48층 호위1종", enemies.Count(e=>e.reworkKit!=null)==154 && enemies.Count(e=>e.reworkKit==null)==28);
            foreach(var theme in themes)
            {
                Check(theme.name+" 8라운드 구성", theme.eventStageInRound==5 && theme.midBossStageInRound==6 && theme.stagePatterns.Select(p=>p.stageInRound).OrderBy(n=>n).SequenceEqual(new[]{1,2,3,4,6,7,8}));
                foreach(var pattern in theme.stagePatterns.SelectMany(s=>s.patterns))
                    Check(theme.name+" 명시 편성", (pattern.frontIds?.Count??0)<=4 && (pattern.rearIds?.Count??0)<=4 &&
                        (pattern.frontIds??new()).Concat(pattern.rearIds??new()).All(id=>enemies.Any(e=>e.id==id)));
                for(int slot=1;slot<=8;slot++)
                {
                    game.DebugResetBattle(); DebugMode.ForcedThemeId=theme.id; game.DebugLoadStage(32+slot);
                    if(slot==5)
                    {
                        Check(theme.name+" 실제 사건 진입", game.gameState==GameState.EventStage && game.RoundManager.GetStageKind(32+slot)==StageKind.Event,game.gameState.ToString());
                        game.SkipEventDialogue();
                        if(theme.id is 22 or 23 or 7 or 19 or 20 or 21) { yield return WaitUnscaled(.15f); yield return CaptureStill("original_event_"+theme.id); }
                        var ev = data.events.First(e=>e.themeId==theme.id);
                        var choice=ev.choices.First(c=>c.battleEnemyId==0 && c.grantUnitId==0);
                        game.SelectEventChoice(choice.id); game.CompleteEventStage();
                        Check(theme.name+" 사건 뒤6라운드", game.RoundManager.Stage==38 && game.RoundManager.CurrentThemeId==theme.id && game.gameState==GameState.Preparation,game.RoundManager.Stage.ToString());
                    }
                    else
                    {
                        var present=AliveEnemies();
                        var pattern=theme.stagePatterns.First(p=>p.stageInRound==slot).patterns[0];
                        var expected=(pattern.frontIds??new()).Concat(pattern.rearIds??new()).OrderBy(n=>n);
                        Check(theme.name+" 실제 편성 "+slot, game.gameState==GameState.Preparation && present.Select(u=>u.ID).OrderBy(n=>n).SequenceEqual(expected) &&
                            present.All(u=>u.currentCell.portraitRenderer.sprite!=null && u.ActiveNormalCode is RestoredEnemyNormal && u.ActiveUltimateCode is RestoredEnemyUltimate),string.Join(",",present.Select(u=>u.ID)));
                        if(slot==1 || slot==8) { yield return WaitUnscaled(.22f); yield return CaptureStill("original_"+theme.id+"_slot_"+slot); }
                    }
                }
            }
            DebugMode.ForcedThemeId=0;
            var chained=new RoundManager(game.dataManager);chained.InitializeStage(17);chained.RestoreTheme(16);chained.InitializeStage(25);
            Check("노르드1→2 연작",chained.CurrentThemeId==17);chained.InitializeStage(33);Check("노르드2→3 연작",chained.CurrentThemeId==18);
            var restored=new RoundManager(game.dataManager);restored.InitializeStage(18);restored.RestoreTheme(23);restored.LoadRound(19);Check("저장 테마 복원·동일테마 유지",restored.CurrentThemeId==23);
            Check("사건의 영입·아이템·전투 참조",data.events.SelectMany(e=>e.choices??new()).All(c=>(c.grantUnitId==0||game.unitDataList.units.Any(u=>u.id==c.grantUnitId)) && (c.battleEnemyId==0||enemies.Any(e=>e.id==c.battleEnemyId))));
            game.DebugResetBattle();RuiFinish();
        }
        private IEnumerator ScenarioOriginalKits(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return PrepareStage(config,17,false,false,false,4);
            var game=GameManager.Instance;var enemies=game.dataManager.FetchEnemyDataList().enemies.Where(e=>e.reworkKit!=null).ToList();
            foreach(var e in enemies)
            {
                game.DebugResetBattle(); GridManager.Instance.ClearActiveEnemies();
                var enemy=GridManager.Instance.SpawnUnit(1,2,true,e.id);
                foreach(var ally in AliveAllies()) { ally.Heal(ally.HpMax, ally); foreach(var status in ally.GetAllStatuses()) ally.RemoveStatusByKey(status.Key); }
                enemy.Invoke(UnitEventType.OnRoundStart,new EventContext(enemy));
                enemy.ActiveNormalCode.CastCode();float deadline=Time.realtimeSinceStartup+7;
                while(enemy.isCasting && Time.realtimeSinceStartup<deadline)yield return null;
                RuiCheck(e.name+" 일반행동","실제 시전 종료",enemy.isCasting.ToString(),!enemy.isCasting);
                if(enemy.isControlled)enemy.ControlEnds();
                var ultimateHits=new List<DamageContext>();
                void UltimateDamage(DamageResolvedContext hit) { if(hit.DamageContext.CodeType==CodeType.Ultimate)ultimateHits.Add(hit.DamageContext); }
                enemy.AddListener(UnitEventType.OnDamageDealt,(Action<DamageResolvedContext>)UltimateDamage);
                try { enemy.ActiveUltimateCode.CastCode();deadline=Time.realtimeSinceStartup+9;while(enemy.isCasting && Time.realtimeSinceStartup<deadline)yield return null; }
                finally { enemy.RemoveListener(UnitEventType.OnDamageDealt,(Action<DamageResolvedContext>)UltimateDamage); }
                RuiCheck(e.name+" 초월기","실제 완료 증거",((BaseUltimateCode)enemy.ActiveUltimateCode).CompletedNormally.ToString(),!enemy.isCasting&&((BaseUltimateCode)enemy.ActiveUltimateCode).CompletedNormally);
                if(e.reworkKit.ultimateMode=="Area")RuiCheck(e.name+" 광역 초월기 태그","대상 수와 무관하게 전체 판정",ultimateHits.Count.ToString(),ultimateHits.Count>0&&ultimateHits.All(h=>h.HasTag(DamageTag.AllTarget)));
                if(e.reworkKit.ultimateMode=="Charge")
                {
                    bool prepared=((RestoredEnemyNormal)enemy.ActiveNormalCode).Charged;
                    enemy.ControlStarts(new ControlContext(AliveAllies().First(),1));
                    RuiCheck(e.name+" 제어로 집중 취소","준비됨→취소",prepared.ToString(),prepared&&!((RestoredEnemyNormal)enemy.ActiveNormalCode).Charged);
                }
                if(e.reworkKit.wardHits>0)
                {
                    var attacker=AliveAllies().First();int hp=enemy.HpCurr,shield=enemy.ShieldCurr;
                    for(int j=0;j<e.reworkKit.wardHits;j++)enemy.TakeDamage(new DamageContext(attacker,1,CodeType.Normal,new(){DamageTag.NormalAttack}));
                    bool blocked=enemy.HpCurr==hp&&enemy.ShieldCurr==shield;
                    enemy.TakeDamage(new DamageContext(attacker,1,CodeType.Normal,new(){DamageTag.NormalAttack}));
                    RuiCheck(e.name+" 유한 타수방호","정확한 횟수 이후 피해",blocked.ToString(),blocked&&(enemy.HpCurr<hp||enemy.ShieldCurr<shield));
                }
                if(e.reworkKit.passiveMode=="Armor")
                {
                    // 앞선 실제 시전 중 반격으로 갑주가 소진될 수 있으므로 새 전투의 갑주로 검증한다.
                    enemy.Invoke(UnitEventType.OnRoundEnd,new EventContext(enemy));
                    enemy.Invoke(UnitEventType.OnRoundStart,new EventContext(enemy));
                    int before=enemy.ShieldCurr;
                    enemy.AddShield(100,enemy);int extra=enemy.ShieldCurr-before;
                    enemy.TakeDamage(new DamageContext(AliveAllies().First(),1,CodeType.Normal,new(){DamageTag.NormalAttack}));
                    enemy.ControlStarts(new ControlContext(AliveAllies().First(),1));
                    RuiCheck(e.name+" 갑주 해제·동료 보호막 보존","추가 보호막 유지",enemy.ShieldCurr.ToString(),enemy.ShieldCurr==extra);
                    enemy.ControlEnds();
                }
                if(e.reworkKit.passiveMode=="Rage")
                {
                    enemy.TakeDamage(new DamageContext(AliveAllies().First(),Mathf.Max(1,enemy.HpCurr/3),CodeType.Effect,new(){DamageTag.TrueDamage}));
                    float expected=e.speedBase*(1+.25f*(1-enemy.HpCurr/(float)enemy.HpMax));
                    RuiCheck(e.name+" 격노 피격 즉시 속도","체력 비율 반영",enemy.SpeedCurr.ToString(),Mathf.Abs(enemy.SpeedCurr-expected)<.1f);
                    enemy.Heal(enemy.HpMax,enemy);
                    RuiCheck(e.name+" 격노 치유 후 속도 복원","기초 속도",enemy.SpeedCurr.ToString(),Mathf.Abs(enemy.SpeedCurr-e.speedBase)<.1f);
                }
                enemy.Invoke(UnitEventType.OnRoundEnd,new EventContext(enemy));
                RuiCheck(e.name+" 상태·집중 정리","잔여0",enemy.GetAllStatuses().Count.ToString(),enemy.GetAllStatuses().Count==0&&!Concentration.IsActive(enemy,"restored_charge"));
                yield return null;
            }
            yield return CaptureStill("original_kits_complete"); game.DebugResetBattle();RuiFinish();
        }
        private IEnumerator ScenarioOriginalEdges(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun("party5");yield return PrepareStage(config,17,false,false,false,4);
            var game=GameManager.Instance;game.DebugResetBattle();DebugMode.ForcedThemeId=23;game.DebugLoadStage(17);DebugMode.ForcedThemeId=0;
            var ally=AliveAllies().First();ally.TakeDamage(new DamageContext(AliveEnemies().First(),100,CodeType.Effect,new(){DamageTag.TrueDamage}));
            int hp=ally.HpCurr,allyId=ally.ID;var snapshot=RunManager.Instance.CaptureDebugSnapshot();
            game.DebugLoadStage(18);RunManager.Instance.RestoreDebugSnapshot(snapshot);
            RuiCheck("복구 군단 실제 런 저장 복원","테마23·17라운드·체력 보존",$"{game.RoundManager.CurrentThemeId}:{game.RoundManager.Stage}",game.RoundManager.CurrentThemeId==23&&game.RoundManager.Stage==17&&Mathf.Abs(AliveAllies().First(u=>u.ID==allyId).HpCurr-hp)<=1);
            var entries=game.dataManager.FetchEnemyDataList().enemies.Where(e=>e.reworkKit!=null).ToList();
            foreach(var e in entries.Where(e=>e.reworkKit.passiveMode=="Rebirth"||e.reworkKit.wardHits>0))
            {
                game.DebugResetBattle();GridManager.Instance.ClearActiveEnemies();var enemy=GridManager.Instance.SpawnUnit(1,2,true,e.id);enemy.Invoke(UnitEventType.OnRoundStart,new EventContext(enemy));
                var source=AliveAllies().First();int before=enemy.HpCurr;
                enemy.TakeDamage(new DamageContext(source,5,CodeType.Effect,new(){DamageTag.TrueDamage}));
                RuiCheck(e.name+" 지속피해가 타수 방호 우회","체력 감소",enemy.HpCurr.ToString(),enemy.HpCurr<before);
                if(e.reworkKit.wardHits>0)
                {
                    var secondWard=GridManager.Instance.SpawnUnit(1,1,true,e.id);
                    var unguarded=GridManager.Instance.SpawnUnit(2,2,true,entries.First(x=>x.reworkKit.wardHits==0&&x.reworkKit.passiveMode=="None").id);
                    secondWard.Invoke(UnitEventType.OnRoundStart,new EventContext(secondWard));
                    unguarded.Invoke(UnitEventType.OnRoundStart,new EventContext(unguarded));
                    var shared=new DamageContext(source,1,CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.AllTarget,DamageTag.TrueDamage});
                    int firstHp=enemy.HpCurr,secondHp=secondWard.HpCurr,plainHp=unguarded.HpCurr;
                    enemy.TakeDamage(shared); bool firstBlocked=shared.ResolvedDamage==0&&!shared.IsCancelled;
                    secondWard.TakeDamage(shared); bool secondBlocked=shared.ResolvedDamage==0&&!shared.IsCancelled;
                    unguarded.TakeDamage(shared);
                    RuiCheck(e.name+" 광역 공유 컨텍스트 방호 격리","두 방호만 취소·일반 대상 피해",shared.ResolvedDamage.ToString(),firstBlocked&&secondBlocked&&enemy.HpCurr==firstHp&&secondWard.HpCurr==secondHp&&unguarded.HpCurr<plainHp);
                    bool remainingBlocked=true;
                    for(int j=1;j<e.reworkKit.wardHits;j++) { secondWard.TakeDamage(shared); remainingBlocked&=shared.ResolvedDamage==0; }
                    secondWard.TakeDamage(shared);
                    RuiCheck(e.name+" 뒤쪽 방호도 정확히1회 소비","정해진 타수 이후 체력 또는 보호막 피해",shared.ResolvedDamage.ToString(),remainingBlocked&&shared.ResolvedDamage>0&&!shared.IsCancelled);
                }
                if(e.reworkKit.passiveMode=="Rebirth")
                {
                    int kills=game.KillCount,gold=game.inventoryManager.Gold,deaths=0;Action<Unit,Unit> died=(target,killer)=>{if(target==enemy)deaths++;};Unit.AnyUnitDied+=died;
                    try { enemy.TakeDamage(new DamageContext(source,enemy.HpMax*3,CodeType.Effect,new(){DamageTag.TrueDamage})); }
                    finally { Unit.AnyUnitDied-=died; }
                    RuiCheck(e.name+" 부활의 경제·사망 분리","생존·35% 체력·처치 보상0",enemy.HpCurr.ToString(),enemy.isActive&&Mathf.Abs(enemy.HpCurr-enemy.HpMax*.35f)<=1&&game.KillCount==kills&&game.inventoryManager.Gold==gold&&deaths==0);
                }
                enemy.Invoke(UnitEventType.OnRoundEnd,new EventContext(enemy));
            }
            foreach(var e in entries.Where(e=>e.reworkKit.ultimateMode=="Charge"))
            {
                game.DebugResetBattle();GridManager.Instance.ClearActiveEnemies();
                var enemy=GridManager.Instance.SpawnUnit(1,2,true,e.id);
                foreach(var target in AliveAllies()) { target.Heal(target.HpMax,target); foreach(var status in target.GetAllStatuses())target.RemoveStatusByKey(status.Key); }
                enemy.Invoke(UnitEventType.OnRoundStart,new EventContext(enemy));
                for(int attempt=0;attempt<2;attempt++)
                {
                    enemy.ActiveUltimateCode.CastCode();float deadline=Time.realtimeSinceStartup+9;
                    while(enemy.isCasting&&Time.realtimeSinceStartup<deadline)yield return null;
                    RuiCheck(e.name+" 연속 집중 초월기 "+attempt,"실제 완료",enemy.isCasting.ToString(),!enemy.isCasting&&((BaseUltimateCode)enemy.ActiveUltimateCode).CompletedNormally);
                }
                var normal=(RestoredEnemyNormal)enemy.ActiveNormalCode;
                RuiCheck(e.name+" 재충전 상태 유지","충전·상태·집중 일치",normal.Charged.ToString(),normal.Charged&&enemy.GetAllStatuses().Any(s=>s.Key=="restored_charge")&&Concentration.IsActive(enemy,"restored_charge"));
                int expected=AliveAllies().Count,hits=0;bool allTarget=true,strong=true;
                Action<DamageResolvedContext> hit=ctx=>{ if(ctx.DamageContext.CodeType!=CodeType.Normal)return; hits++;allTarget&=ctx.DamageContext.HasTag(DamageTag.AllTarget);strong&=ctx.DamageContext.Damage>=Mathf.RoundToInt(enemy.StatValue(StatType.Attack)*1.65f); };
                enemy.AddListener(UnitEventType.OnDamageDealt,hit);
                try { normal.CastCode();float deadline=Time.realtimeSinceStartup+9;while(enemy.isCasting&&Time.realtimeSinceStartup<deadline)yield return null; }
                finally { enemy.RemoveListener(UnitEventType.OnDamageDealt,hit); }
                RuiCheck(e.name+" 재충전 뒤 실제 광역 강타","모든 대상·165% 이상 원시피해·집중 해제",hits+"/"+expected,!enemy.isCasting&&hits==expected&&allTarget&&strong&&!normal.Charged&&!Concentration.IsActive(enemy,"restored_charge"));
                enemy.Invoke(UnitEventType.OnRoundEnd,new EventContext(enemy));
            }
            yield return CaptureStill("original_edges_complete");game.DebugResetBattle();RuiFinish();
        }
        private IEnumerator ScenarioOriginalAudio(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return PrepareStage(config,17,false,false,false,4);
            var game=GameManager.Instance;var entries=game.dataManager.FetchEnemyDataList().enemies.Where(e=>e.reworkKit!=null).ToList();
            foreach(string mode in new[]{"Single","Area","Shield"})
            {
                game.DebugResetBattle();GridManager.Instance.ClearActiveEnemies();var e=entries.First(x=>x.reworkKit.ultimateMode==mode);var enemy=GridManager.Instance.SpawnUnit(1,2,true,e.id);
                foreach(var ally in AliveAllies())ally.Heal(ally.HpMax,ally);
                string cue=SfxCueRoutes.Resolve(enemy,SfxActionSlot.Ultimate,SfxCuePhase.Cast,enemy.ActiveUltimateCode.CodeName);int calls=0;
                Action<SfxPlaybackEvent> record=evt=>{if(evt.cue==cue)calls++;};AudioManager.SfxEventRecorded+=record;
                try { enemy.ActiveUltimateCode.CastCode();float deadline=Time.realtimeSinceStartup+9;while(enemy.isCasting&&Time.realtimeSinceStartup<deadline)yield return null; }
                finally { AudioManager.SfxEventRecorded-=record; }
                RuiCheck(e.name+" 초월기 시전음","정확히1회",calls.ToString(),calls==1&&!enemy.isCasting);
            }
            yield return CaptureStill("original_audio_complete");game.DebugResetBattle();RuiFinish();
        }
        private IEnumerator ScenarioOriginalCampaign(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return BuildFreshRun(ResolveLineup(config));yield return PrepareStage(config,17,false,false,false,4);
            var game=GameManager.Instance;int themeId=config.stage;game.DebugResetBattle();DebugMode.ForcedThemeId=themeId;game.DebugLoadStage(17);DebugMode.ForcedThemeId=0;
            DebugMode.SuiteRunning=false;int battles=0,events=0,prior=0;float deadline=Time.realtimeSinceStartup+480;
            while(game.RoundManager.Stage<25&&game.life>0&&game.gameState!=GameState.GameOver&&Time.realtimeSinceStartup<deadline)
            {
                if(game.gameState==GameState.Preparation)
                {
                    if(prior!=game.RoundManager.Stage) { if(AliveAllies().Any(u=>u.HpCurr<u.HpMax*.45f))game.TryShortRest();prior=game.RoundManager.Stage; }
                    game.StartRound();
                }
                else if(game.gameState==GameState.RoundEnd)
                { var button=ReleaseResultContinue();if(button!=null){battles++;button.onClick.Invoke();yield return WaitUnscaled(.1f);} }
                else if(game.gameState==GameState.RewardSelection)
                {
                    var choices=(List<RewardDef>)typeof(RewardManager).GetField("_selection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(RewardManager.Instance);
                    var reward=choices.FirstOrDefault(r=>r.IsCodeReward&&AliveAllies().Any(u=>u.CanReceiveRewardCode(r.codeId,out _,out _,out _)))??choices.FirstOrDefault(r=>!r.RequiresTargetSelection||Managers.UI.Screens.RewardScreen.TargetCandidates(r).Any());
                    if(reward==null)break;
                    Unit target=reward.IsCodeReward?AliveAllies().FirstOrDefault(u=>u.CanReceiveRewardCode(reward.codeId,out _,out _,out _)):reward.RequiresTargetSelection?Managers.UI.Screens.RewardScreen.TargetCandidates(reward).FirstOrDefault():null;
                    if(!RewardManager.Instance.TryClaimReward(reward,target))break;RewardManager.Instance.FinishRewardSelection();yield return WaitUnscaled(.1f);
                }
                else if(game.gameState==GameState.EventStage)
                {
                    events++;game.SkipEventDialogue();
                    var ev=(StageEventData)typeof(GameManager).GetField("currentStageEvent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
                    if(ev!=null){var choice=ev.choices.FirstOrDefault(c=>c.battleEnemyId==0&&c.grantUnitId==0);if(choice!=null)game.SelectEventChoice(choice.id);game.CompleteEventStage();}
                    yield return WaitUnscaled(.1f);
                }
                yield return null;
            }
            RuiCheck("테마"+themeId+" 8라운드 자연 완주","7전투+사건·패배0",$"stage={game.RoundManager.Stage},battle={battles},event={events},life={game.life}",game.RoundManager.Stage>=25&&battles==7&&events>=1&&game.life==20);
            if(themeId is 16 or 17) RuiCheck("노르드 실제 완주 후 다음 편","테마"+(themeId+1),game.RoundManager.CurrentThemeId.ToString(),game.RoundManager.CurrentThemeId==themeId+1);
            yield return CaptureStill("original_campaign_"+themeId);game.DebugResetBattle();DebugMode.SuiteRunning=true;DebugMode.ForcedThemeId=0;RuiFinish();
        }
    }
}
#endif
