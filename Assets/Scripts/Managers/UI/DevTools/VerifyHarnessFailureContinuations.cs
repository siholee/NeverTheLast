#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        [Serializable] private sealed class FailureContinuation
        {
            public string id, parentRun, team, checkpoint, rescuePolicy, laterPolicy = "original_natural";
            public int attempt, seed, startStage, originalReached, cap;
            public bool rescueMatched;
            public float expectedRescueCycles, actualRescueCycles;
            public CampaignRun result;
        }
        // 보상·사건은 자연 진행의 동일 선택 정책을 쓴다. 재시드·무상 회복·새 초기 자원은 주입하지 않는다.
        private bool ContinuationRewards(string team, int attempt)
        {
            var manager = RewardManager.Instance;
            var choices = (List<RewardDef>)typeof(RewardManager).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            bool claimed = false;
            foreach (var reward in (choices ?? new List<RewardDef>()).Where(manager.CanClaimReward).OrderByDescending(CampaignRewardScore))
            {
                Unit target = reward.RequiresTargetSelection ? CampaignTarget(reward) : null;
                int replace = reward.IsCodeReward && target != null ? CampaignCodeChoice(reward.codeId).replace : 0;
                if (reward.IsCodeReward && target == null)
                {
                    var fallback = CampaignCodeChoice(reward.codeId, true); target = fallback.target; replace = fallback.replace;
                    CampaignWrite("reward_fallbacks", new CampaignRewardRejection { team = team, attempt = attempt,
                        stage = GameManager.Instance.RoundManager.Stage, reward = reward.displayName, code = reward.codeId, target = target?.ID ?? 0 });
                }
                if (manager.TryClaimReward(reward, target, replace)) { claimed = true; break; }
                CampaignWrite("reward_rejections", new CampaignRewardRejection { team = team, attempt = attempt,
                    stage = GameManager.Instance.RoundManager.Stage, reward = reward.displayName, code = reward.codeId, target = target?.ID ?? 0 });
            }
            if (!claimed) return false;
            foreach (var extra in choices.Where(manager.CanClaimReward).OrderByDescending(CampaignRewardScore).ToList())
            {
                if (CampaignRewardScore(extra) < 35) continue;
                Unit target = extra.RequiresTargetSelection ? CampaignTarget(extra) : null;
                manager.TryClaimReward(extra, target, extra.IsCodeReward ? CampaignCodeChoice(extra.codeId).replace : 0);
            }
            manager.FinishRewardSelection(); return true;
        }
        private CampaignBattle ContinuationBattle(FailureContinuation item, HashSet<int> fixedFloors)
        {
            var game = GameManager.Instance; var kind = game.RoundManager.CurrentCombatKind; int stage = game.RoundManager.Stage;
            return new CampaignBattle { run = item.id, team = item.team, attempt = item.attempt, seed = item.seed, stage = stage,
                theme = game.RoundManager.CurrentThemeId, stageKind = kind.ToString(),
                category = fixedFloors.Contains(stage) ? "FixedBoss" : kind is StageKind.Elite or StageKind.MidBoss ? "Elite" : kind == StageKind.Boss ? "Boss" : kind == StageKind.Event ? "Event" : "Normal",
                formation = string.Join(";", AliveAllies().Select(u => u.ID + ":" + u.currentCell.xPos + ":" + u.currentCell.yPos)),
                enemies = string.Join(",", AliveEnemies().Select(u => u.ID)),
                meanLevel = AliveAllies().Select(u => (float)u.Level).DefaultIfEmpty().Average(),
                party = string.Join(";", GridManager.Instance.heroList.Where(u => u != null && (u.ID > 0 || u.LastActiveId > 0)).Select(u =>
                    (u.ID > 0 ? u.ID : u.LastActiveId) + ":L" + u.Level + ":HP" + u.HpCurr + "/" + u.HpMax + ":C" + string.Join("/", u.RewardCodeSlots))) };
        }
        private IEnumerator ScenarioFailureContinuations(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); var game = GameManager.Instance;
            var prior = File.ReadAllLines(config.trigger).Where(s => !string.IsNullOrWhiteSpace(s)).Select(JsonUtility.FromJson<CampaignFailureBranch>)
                .Where(b => b.won && b.executed && b.team != "operation_fixture")
                .GroupBy(b => b.parentRun + "/" + b.stage).Select(g => g.OrderBy(b => b.policy == "reserve_food" ? 0 : 1).First()).OrderBy(b => b.team).ThenBy(b => b.attempt).ToList();
            YvCheck("구제 분기 이어달리기 대상", "중복 없는 실제 구제 3건", prior.Count.ToString(), prior.Count == 3);
            float capture = Time.captureDeltaTime, step = Time.fixedDeltaTime; int sync = QualitySettings.vSyncCount, limit = Application.targetFrameRate;
            var filter = Debug.unityLogger.filterLogType; var canvases = new HashSet<Canvas>();
            Time.captureDeltaTime = 1f / 30f; Time.fixedDeltaTime = .2f; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Debug.unityLogger.filterLogType = LogType.Warning; _campaignInventoryAware = _campaignDotRewardStudy = true;
            DebugMode.SuiteRunning = true; DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            var fixedFloors = new HashSet<int>(game.dataManager.FetchStageThemeDataList().fixedBossStages.Select(f => f.stage));
            try
            {
                foreach (var expected in prior)
                {
                    string marker = Path.Combine(_requestDirectory, $"continuation_{expected.team}_{expected.attempt}_{expected.stage}.done");
                    if (File.Exists(marker)) continue;
                    var point = JsonUtility.FromJson<CampaignFailureCheckpoint>(File.ReadAllText(expected.checkpoint));
                    var sourceRuns = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(expected.checkpoint), "runs.jsonl"))
                        .Where(s => !string.IsNullOrWhiteSpace(s)).Select(JsonUtility.FromJson<CampaignRun>);
                    var original = sourceRuns.Single(r => r.id == expected.parentRun && r.valid);
                    var item = new FailureContinuation { id = Guid.NewGuid().ToString("N"), parentRun = expected.parentRun, team = expected.team,
                        checkpoint = expected.checkpoint, rescuePolicy = expected.policy, attempt = expected.attempt, seed = point.seed,
                        startStage = expected.stage, originalReached = original.reached, cap = config.stage > 0 ? config.stage : 96,
                        expectedRescueCycles = expected.cycles };
                    item.result = new CampaignRun { id = item.id, team = item.team, attempt = item.attempt, seed = item.seed, valid = true };
                    game.DebugResetBattle(); game.enabled = true; _failurePreparationPolicy = null; _failureChoices.Clear();
                    UnityEngine.Random.state = point.preparationRandom; RunManager.Instance.RestoreDebugSnapshot(point.beforePreparation);
                    FailureRestoreEnemies(point); UnityEngine.Random.state = point.preparationRandom;
                    _failurePreparationPolicy = expected.policy; CampaignPreparation();
                    bool preparationMatched = game.inventoryManager.Gold == expected.goldAfter && game.inventoryManager.FoodUnits == expected.foodAfter &&
                        game.ShortRestCharges == expected.shortRestsAfter && AliveAllies().Count == expected.aliveAfterPreparation &&
                        string.Join(";", _failureChoices) == string.Join(";", expected.choices);
                    // 이 한 번의 선택을 재현한 뒤에는 기존 자연 진행 운영으로 복귀한다.
                    _failurePreparationPolicy = null;
                    Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(5);
                    var battle = ContinuationBattle(item, fixedFloors);
                    game.StartRound();
                    float start = Time.realtimeSinceStartup, changed = start; GameState lastState = game.gameState; int lastStage = game.RoundManager.Stage;
                    bool first = true, eventChosen = false;
                    CampaignFailureCheckpoint checkpoint48 = null;
                    while (true)
                    {
                        int floor = game.RoundManager.Stage;
                        if (game.gameState != lastState || floor != lastStage) { changed = Time.realtimeSinceStartup; lastState = game.gameState; lastStage = floor; }
                        if (battle != null && game.gameState != GameState.RoundInProgress)
                        {
                            battle.av = game.ActionScheduler.CombatActionValueElapsed; battle.cycles = game.ActionScheduler.ReferenceCyclesElapsed;
                            battle.turns = game.ActionScheduler.TurnsTaken;
                            battle.reason = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text ?? "";
                            battle.won = battle.reason.Contains("전투 종료 — 승리"); battle.survivors = AliveAllies().Count;
                            battle.damage = string.Join(";", DamageMeter.Current.Select(e => (e.Unit.ID > 0 ? e.Unit.ID : e.Unit.LastActiveId) + ":" + e.Damage));
                            item.result.reached = Mathf.Max(item.result.reached, battle.stage); item.result.battles++;
                            if (battle.won) item.result.cleared = Mathf.Max(item.result.cleared, battle.stage);
                            else if (item.result.firstLoss == 0) item.result.firstLoss = battle.stage;
                            CampaignWrite("battles", battle);
                            if (checkpoint48 != null && battle.stage == 48 && !battle.won) CampaignSaveFailure(checkpoint48, battle, false);
                            if (first)
                            {
                                item.actualRescueCycles = battle.cycles;
                                item.rescueMatched = preparationMatched && battle.won && expected.enemies == battle.enemies && expected.formation == battle.formation &&
                                    expected.survivors == battle.survivors && expected.damage == battle.damage && Near(expected.cycles, battle.cycles, .001f);
                                CampaignWrite("rescue_reproduction", item);
                                YvCheck($"구제 승리 재현 {item.team}/{item.attempt}/{item.startStage}", "자산·선택·적·배치·피해·라운드·생존 동일", battle.cycles.ToString(), item.rescueMatched);
                                if (!item.rescueMatched) { item.result.valid = false; item.result.stop = "rescue_mismatch"; break; }
                                first = false;
                            }
                            battle = null;
                        }
                        if (game.gameState == GameState.GameOver) { item.result.stop = "game_over"; break; }
                        if (floor > item.cap && game.gameState != GameState.RoundInProgress) { item.result.capped = true; item.result.stop = "observation_cap"; break; }
                        if (Time.realtimeSinceStartup - changed > 180 || Time.realtimeSinceStartup - start > 3600)
                        { item.result.valid = false; item.result.stop = "harness_watchdog"; break; }
                        if (game.gameState == GameState.Preparation)
                        {
                            if (floor == 48) checkpoint48 = CampaignFailureCapture(item.id, item.team, item.attempt, item.seed);
                            CampaignPreparation();
                            if (AliveAllies().Count == 0) { item.result.stop = "party_exhausted"; break; }
                            if (floor == 48 && checkpoint48 != null)
                            { checkpoint48.afterPreparation = RunManager.Instance.CaptureDebugSnapshot(); checkpoint48.battleRandom = UnityEngine.Random.state; }
                            battle = ContinuationBattle(item, fixedFloors); game.StartRound();
                        }
                        else if (game.gameState == GameState.RewardSelection)
                        {
                            if (!ContinuationRewards(item.team, item.attempt)) { item.result.valid = false; item.result.stop = "reward_rejected"; break; }
                            eventChosen = false;
                        }
                        else if (game.gameState == GameState.EventStage)
                        {
                            game.SkipEventDialogue();
                            var ev = (StageEventData)typeof(GameManager).GetField("currentStageEvent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                            if (!eventChosen && ev != null)
                            {
                                var choice = ev.choices?.FirstOrDefault(c => c.battleEnemyId == 0 && c.grantUnitId == 0 && c.goldCostPerStage * floor <= game.inventoryManager.Gold);
                                if (choice != null) game.SelectEventChoice(choice.id);
                                eventChosen = true; item.result.events++;
                            }
                            game.CompleteEventStage(); if (game.gameState != GameState.EventStage) eventChosen = false;
                        }
                        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (canvas.enabled) { canvases.Add(canvas); canvas.enabled = false; }
                        yield return null;
                    }
                    item.result.wallSeconds = Time.realtimeSinceStartup - start;
                    CampaignWrite("continuations", item); File.WriteAllText(Path.Combine(_requestDirectory, "progress.json"), JsonUtility.ToJson(item));
                    if (item.result.valid && item.rescueMatched) File.WriteAllText(marker, JsonUtility.ToJson(item));
                    YvCheck($"구제 분기 완주 {item.team}/{item.attempt}", "종료 또는96층 관찰 상한", item.result.stop, item.result.valid && item.rescueMatched);
                    game.DebugResetBattle(); foreach (var canvas in canvases) if (canvas != null) canvas.enabled = true; canvases.Clear();
                }
            }
            finally
            {
                _failurePreparationPolicy = null; _campaignDotRewardStudy = false;
                foreach (var canvas in canvases) if (canvas != null) canvas.enabled = true;
                Time.captureDeltaTime = capture; Time.fixedDeltaTime = step; QualitySettings.vSyncCount = sync; Application.targetFrameRate = limit;
                Debug.unityLogger.filterLogType = filter;
            }
            OsFinish(true);
        }
    }
}
#endif
