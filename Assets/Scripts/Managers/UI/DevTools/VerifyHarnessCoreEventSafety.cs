#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreEventSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            var game=GameManager.Instance;SaveSystem.SeedEmptyDebugAccount();SaveSystem.MarkBossDefeated(13012);
            foreach(int floor in new[]{5,13})
            {
                game.DebugResetBattle();DebugMode.ForcedThemeId=0;game.DebugLoadStage(floor);
                bool scheduled=game.RoundManager.TryGetScheduledEvent(out _);
                YvCheck($"아문라 기록이 있어도 범용{floor}층 서사 없음","준비·일반·사건없음",game.gameState.ToString(),
                    game.gameState==BaseEnums.GameState.Preparation&&!scheduled&&game.RoundManager.GetStageKind(floor)==StageKind.Normal);
                game.StartRound();
                YvCheck($"범용{floor}층 실제 전투 유지","적 존재·전투",AliveEnemies().Count.ToString(),game.gameState==BaseEnums.GameState.RoundInProgress&&AliveEnemies().Count>0);
            }
            game.DebugResetBattle();DebugMode.ForcedThemeId=8;game.DebugLoadStage(17);DebugMode.ForcedThemeId=0;
            var story=game.dataManager.FetchStageThemeDataList().events.First(item=>item.themeId==8&&item.choices.Any(choice=>choice.battleEnemyId>0));
            var choice=story.choices.First(item=>item.battleEnemyId>0);
            game.inventoryManager.AddItem(5104);
            bool tonicUsed = game.TryUseTonic(5104, out string tonicReason);
            YvCheck("사건 전투 신속 비약 준비", "사용 성공", tonicReason, tonicUsed);
            var scheduler=game.ActionScheduler;
            typeof(ActionScheduler).GetProperty("TurnsTaken").SetValue(scheduler,80);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(GameManager).GetField("_battleKillGold",flags).SetValue(game,12345);
            typeof(GameManager).GetField("_battleGoldBonus",flags).SetValue(game,.75f);
            ((List<int>)typeof(GameManager).GetField("_battleKilledEnemyIds",flags).GetValue(game)).Add(1101);
            game.EnterEvent(story,()=>{});game.SkipEventDialogue();game.SelectEventChoice(choice.id);
            YvCheck("직전80턴 뒤 실제 사건 선택 전투 새 시계","턴0·전투",$"{scheduler.TurnsTaken}/{game.gameState}",scheduler.TurnsTaken==0&&game.gameState==BaseEnums.GameState.RoundInProgress);
            var actionValues = (Dictionary<Entities.Unit, float>)typeof(ActionScheduler).GetField("_actionValues", flags).GetValue(scheduler);
            bool speedReflected = AliveAllies().All(unit => actionValues.TryGetValue(unit, out float actual) &&
                Mathf.Abs(actual - ActionScheduler.FullActionValue(unit)) < .001f);
            YvCheck("사건 첫 AV에 신속 비약 반영", "10000/비약 적용 후 속도", string.Join("/", AliveAllies().Select(unit =>
                actionValues.GetValueOrDefault(unit).ToString("F3") + "=" + ActionScheduler.FullActionValue(unit).ToString("F3"))), speedReflected);
            int killGold=(int)typeof(GameManager).GetField("_battleKillGold",flags).GetValue(game);
            float bonus=(float)typeof(GameManager).GetField("_battleGoldBonus",flags).GetValue(game);
            int kills=((List<int>)typeof(GameManager).GetField("_battleKilledEnemyIds",flags).GetValue(game)).Count;
            YvCheck("사건 전투 직전 골드·보너스·처치기록 정리","0/0/0",$"{killGold}/{bonus}/{kills}",killGold==0&&bonus==0&&kills==0);
            float until=Time.realtimeSinceStartup+15f;
            while(scheduler.ActionCount==0&&Time.realtimeSinceStartup<until){scheduler.Tick(Time.deltaTime);yield return null;}
            YvCheck("사건 전투 실제 첫 행동 진행","행동1이상·턴80미만",$"{scheduler.ActionCount}/{scheduler.TurnsTaken}",scheduler.ActionCount>0&&scheduler.TurnsTaken<80);
            yield return CaptureStill("core_event_safety_fresh_battle");game.DebugResetBattle();OsFinish(enabled);
        }
    }
}
#endif
