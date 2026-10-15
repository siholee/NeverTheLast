#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCheckpoint24Layout(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 17, false, false, false, 4);
            var game = GameManager.Instance;
            var data = game.dataManager.FetchStageThemeDataList();
            var definition = game.dataManager.FetchEnemyDataList().enemies.Single(e => e.id == 3201);
            RuiCheck("24층 고정 보스 등록", "24층·3201·패시브 없음", string.Join(",",data.fixedBossStages.Select(x=>x.stage)),
                data.fixedBossStages.Count(x => x.stage == 24 && x.bossId == 3201) == 1 && definition.CodeId("passive") == 0);
            foreach (var theme in data.stageThemes.Where(t => t.enabled))
            {
                game.DebugResetBattle(); DebugMode.ForcedThemeId = theme.id; game.DebugLoadStage(17);
                var round = game.RoundManager;
                bool mapped = true;
                for (int floor = 17; floor <= 24; floor++)
                {
                    var wanted = floor is 23 or 24 ? StageKind.Boss : floor == 21 ? (theme.isGeneric ? StageKind.Elite : StageKind.MidBoss) : floor == 20 && !theme.isGeneric ? StageKind.Event : StageKind.Normal;
                    mapped &= round.GetStageKind(floor) == wanted;
                }
                RuiCheck(theme.name+" 第3스테이지 예고", "엘리트21·보스23·고정24·사건20", mapped.ToString(), mapped);
                foreach(int floor in new[]{21,23,24})
                {
                    game.DebugResetBattle(); game.DebugLoadStage(floor);
                    int expected = floor == 21 ? theme.midBossId : floor == 23 ? theme.bossId : 3201;
                    RuiCheck(theme.name+" 실제 "+floor+"층", expected.ToString(), string.Join(",",AliveEnemies().Select(u=>u.ID)),
                        game.gameState == GameState.Preparation && AliveEnemies().Any(u=>u.ID==expected) && (floor!=24 || AliveEnemies().Count==1));
                }
                RuiCheck(theme.name+" 나머지 스테이지 보존", "6엘리트·8테마 보스", round.GetStageKind(32).ToString(),
                    round.GetStageKind(30)==(theme.isGeneric?StageKind.Elite:StageKind.MidBoss) && round.GetStageKind(32)==StageKind.Boss);
                yield return null;
            }
            var boss=AliveEnemies().Single();
            RuiCheck("고정 보스 실제 스탯", "7000/456/205/162/72·Lv24", $"{boss.HpMax}/{boss.AtkCurr}/{boss.DefCurr}/{boss.SpDefCurr}/{boss.SpeedCurr}",
                boss.HpMax==7000 && boss.AtkCurr==456 && boss.DefCurr==205 && boss.SpDefCurr==162 && boss.SpeedCurr==72);
            var snapshot=RunManager.Instance.CaptureDebugSnapshot();
            game.DebugLoadStage(25); RunManager.Instance.RestoreDebugSnapshot(snapshot);
            RuiCheck("24층 준비 저장·복원", "동일 테마·고정 보스1기",game.RoundManager.Stage.ToString(),
                game.RoundManager.Stage==24 && AliveEnemies().Count==1 && AliveEnemies()[0].ID==3201);
            yield return WaitUnscaled(1.4f);
            yield return CaptureStill("checkpoint24_preparation");
            DebugMode.ForcedThemeId=0; game.DebugResetBattle(); RuiFinish();
        }

        private IEnumerator ScenarioCheckpoint24Battle(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return BuildFreshRun(ResolveLineup(config));
            yield return PrepareStage(config, 24, false, false, false, 4);
            var game=GameManager.Instance;
            UnityEngine.Random.InitState(config.stage > 0 ? config.stage : 2401);
            var party=AliveAllies().ToList();
            bool weak=ResolveLineup(config)=="duo_shi_sabah";
            bool treasury=ResolveLineup(config)=="party5_treasury";
            foreach(var unit in party)
            {
                if(!weak)
                {
                    int[] codes = treasury ? unit.ID switch
                    {
                        220 => new[]{501,601,607,631,507}, // 주력 탄창: 공격·치명타·재보 공격에 집중.
                        65 => new[]{508,608},             // 쿠베라의 파티 치명타 피해 오라.
                        200 => new[]{502,503,603},        // 만사의 보호막과 DEF 기반 지원.
                        64 => new[]{502,505,605},         // 바유의 원소 취약과 효과 명중.
                        _ => new[]{502,602},              // 프레이아의 실제 치유량.
                    } : new[]{502,501,unit.ID is 1 or 200 ? 603 : unit.ID is 60 or 82 ? 602 : unit.ID is 64 or 63 or 45 ? 605 : 601};
                    RuiCheck(unit.UnitName+" 유효 보상 지급", treasury?"파티15장·T1/T2·주력과 지원 계수에 배분":"T1두 장·T2한 장",string.Join(",",codes),codes.All(id=>unit.TryGrantRewardCode(id)));
                }
                // 온전한 컨디션을 전제로 하지 않는 검증: 최대 체력의75%로 입장한다.
                unit.TakeDamage(new BaseClasses.DamageContext(AliveEnemies()[0],Mathf.RoundToInt(unit.HpMax*.25f),CodeType.Effect,new(){BaseClasses.DamageTag.TrueDamage}));
            }
            var boss=AliveEnemies().Single(); int startingLife=game.life;
            int minimumAllies=party.Count, normals=0, lastBossHp=boss.HpCurr, turns=0; float start=Time.realtimeSinceStartup,deadline=start+180;
            DebugMode.ForcedThemeId=0; DebugMode.SuiteRunning=false; game.StartRound();
            while(game.gameState==GameState.RoundInProgress && Time.realtimeSinceStartup<deadline)
            {
                minimumAllies=Mathf.Min(minimumAllies,AliveAllies().Count); normals=Mathf.Max(normals,boss.TurnCount); lastBossHp=boss.HpCurr; turns=game.ActionScheduler.TurnsTaken;
                yield return null;
            }
            DebugMode.SuiteRunning=true;
            bool won=Combat.CombatLog.Current.LastOrDefault(e=>e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
            string actual=$"won={won},reason={Combat.CombatLog.Current.LastOrDefault(e=>e.Text.StartsWith("전투 종료 — ")).Text},life={startingLife}->{game.life},turns={turns}/80,lastBossHp={lastBossHp},seconds={Time.realtimeSinceStartup-start:F1},bossNormal={normals},survivors={AliveAllies().Count},min={minimumAllies},hp={string.Join(",",AliveAllies().Select(u=>$"{u.ID}:L{u.Level}:{u.HpCurr}/{u.HpMax}"))}";
            bool noHealer=ResolveLineup(config)=="party5_vedic_trio";
            RuiCheck("24층 "+ResolveLineup(config)+(weak?" 불완전 편성 대조":" 덱 구성 시험"),weak?"회복·탱킹 없는2인 편성은 패배":$"Lv{config.partyLevel}·T1/T2만·75%체력·패배0"+(noHealer?"·최소4인 생존":"·전원 생존"),actual,
                weak ? !won && game.gameState!=GameState.RoundInProgress : won && game.life==startingLife && minimumAllies>=(noHealer?4:party.Count));
            yield return CaptureStill("checkpoint24_result_"+ResolveLineup(config));
            if(won)
            {
                float resultDeadline=Time.realtimeSinceStartup+8;
                while(ReleaseResultContinue()==null && Time.realtimeSinceStartup<resultDeadline) yield return null;
                var button=ReleaseResultContinue(); if(button!=null)button.onClick.Invoke();
                yield return WaitForState(GameState.RewardSelection,5);
                RuiCheck("고정 보스 정산", "보상 선택·생명 보존",game.gameState.ToString(),game.gameState==GameState.RewardSelection && game.life==startingLife);
            }
            game.DebugResetBattle(); RuiFinish();
        }

        private IEnumerator ScenarioCheckpoint24Campaign(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return WaitForGameReady(8);
            // 앞선 패배 대조군의 생명·재화·휴식이 새 런에 섞이지 않게 초기 상태를 명시한다.
            GameManager.Instance.life=20;
            GameManager.Instance.KillCount=0;
            GameManager.Instance.inventoryManager.Initialize();
            GameManager.Instance.RestorePreparationState(false,GameManager.MaxShortRestCharges,0);
            yield return BuildFreshRun("party5");
            yield return PrepareStage(config,1,false,false,false,4);
            var game=GameManager.Instance;
            game.DebugResetBattle(); DebugMode.ForcedThemeId=2; game.DebugLoadStage(1);
            // 보상 창을 닫아도 같은 검증 배속으로 복귀하게 HUD의 배속 원천을 함께 맞춘다.
            Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(Mathf.Max(1f, config.timeScale));
            DebugMode.SuiteRunning=false;
            int battles=0,events=0,prior=0; bool switched=false,passed24=false;
            float deadline=Time.realtimeSinceStartup+900;
            while(game.RoundManager.Stage<25 && game.life>0 && game.gameState!=GameState.GameOver && Time.realtimeSinceStartup<deadline)
            {
                if(game.gameState==GameState.Preparation)
                {
                    int floor=game.RoundManager.Stage;
                    if(floor==17 && !switched)
                    {
                        // 두 범용 테마를 실제 완주한 파티를 그대로 노르드에 보낸다. 레벨·HP·골드·코드 유지.
                        game.DebugResetBattle(); DebugMode.ForcedThemeId=16; game.DebugLoadStage(17); DebugMode.ForcedThemeId=0; switched=true;
                    }
                    if(prior!=floor)
                    {
                        if(AliveAllies().Any(u=>u.HpCurr<u.HpMax*.45f)) game.TryShortRest();
                        prior=floor;
                        if(floor is 21 or 23 or 24)
                        {
                            RuiCheck("자연 진행 "+floor+"층 배치","엘리트·테마 보스·고정 보스",string.Join(",",AliveEnemies().Select(u=>u.ID)),AliveEnemies().Any(u=>u.ID==(floor==21?12050:floor==23?13060:3201)));
                            if(floor==24) { yield return WaitUnscaled(1.4f); yield return CaptureStill("checkpoint24_natural_entry"); }
                        }
                    }
                    game.StartRound();
                }
                else if(game.gameState==GameState.RoundEnd)
                {
                    var button=ReleaseResultContinue();
                    if(button!=null)
                    {
                        if(game.RoundManager.Stage==24)
                        {
                            passed24=Combat.CombatLog.Current.LastOrDefault(e=>e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리")==true;
                            RuiCheck("실제 성장·보상으로24층 승리","Lv1 출발·사망0·자연 보상만",$"won={passed24},life={game.life},party={string.Join(",",AliveAllies().Select(u=>$"{u.ID}:Lv{u.Level}:HP{u.HpCurr}/{u.HpMax}:codes{string.Join('/',u.RewardCodeSlots)}"))}",passed24&&game.life==20&&AliveAllies().Count==5);
                        }
                        battles++; button.onClick.Invoke(); yield return WaitUnscaled(.12f);
                    }
                }
                else if(game.gameState==GameState.RewardSelection)
                {
                    var choices=(System.Collections.Generic.List<RewardDef>)typeof(RewardManager).GetField("_selection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(RewardManager.Instance);
                    var reward=choices.FirstOrDefault(r=>r.IsCodeReward && AliveAllies().Any(u=>u.CanReceiveRewardCode(r.codeId,out _,out _,out _)))??choices.FirstOrDefault(r=>!r.RequiresTargetSelection||Managers.UI.Screens.RewardScreen.TargetCandidates(r).Any());
                    if(reward==null)break;
                    Unit target=reward.IsCodeReward?AliveAllies().FirstOrDefault(u=>u.CanReceiveRewardCode(reward.codeId,out _,out _,out _)):reward.RequiresTargetSelection?Managers.UI.Screens.RewardScreen.TargetCandidates(reward).FirstOrDefault():null;
                    if(!RewardManager.Instance.TryClaimReward(reward,target))break;
                    RewardManager.Instance.FinishRewardSelection(); yield return WaitUnscaled(.12f);
                }
                else if(game.gameState==GameState.EventStage)
                {
                    events++;game.SkipEventDialogue();
                    var ev=(StageEventData)typeof(GameManager).GetField("currentStageEvent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
                    if(ev!=null){var choice=ev.choices.FirstOrDefault(c=>c.battleEnemyId==0&&c.grantUnitId==0);if(choice!=null)game.SelectEventChoice(choice.id);game.CompleteEventStage();}
                    yield return WaitUnscaled(.12f);
                }
                yield return null;
            }
            RuiCheck("1→24층 실제 완주·노르드 연작","23전투+사건·생명20·25층 노르드 북부",$"floor={game.RoundManager.Stage},theme={game.RoundManager.CurrentThemeId},battles={battles},events={events},life={game.life}",
                passed24 && game.RoundManager.Stage==25 && game.RoundManager.CurrentThemeId==17 && battles==23 && events>=1 && game.life==20);
            yield return CaptureStill("checkpoint24_natural_finished");
            DebugMode.SuiteRunning=true; DebugMode.ForcedThemeId=0; game.DebugResetBattle(); RuiFinish();
        }
    }
}
#endif
