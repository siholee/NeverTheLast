#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // 자연 런은 공유 난수를 그대로 쓴다. 분기는 완료된 실패 표본을 별도 시나리오에서만 재생한다.
        [Serializable] private sealed class CampaignFailureCheckpoint
        {
            public string run, team;
            public int attempt, seed, stage;
            public bool partyExhausted;
            public int[] enemyIds, enemyXs, enemyYs;
            public RunSaveData beforePreparation, afterPreparation;
            public UnityEngine.Random.State preparationRandom, battleRandom;
            public CampaignBattle original;
        }
        [Serializable] private sealed class CampaignFailureBranch
        {
            public string parentRun, team, checkpoint, policy, stop, formation, enemies, enemyRestoreMode, damage, combatKey;
            public int attempt, stage, goldBefore, goldAfter, foodBefore, foodAfter, survivors, shortRestsBefore, shortRestsAfter, aliveBeforePreparation, aliveAfterPreparation;
            public bool restedBefore, restedAfter;
            public bool won, finished, changed, controlMatched, executed;
            public string reusedPolicy;
            public float cycles;
            public List<string> choices = new();
            public DotDiagnosisRow telemetry;
        }
        private string _failurePreparationPolicy;
        private readonly List<string> _failureChoices = new();
        private CampaignFailureCheckpoint CampaignFailureCapture(string run, string team, int attempt, int seed)
            => new() { run = run, team = team, attempt = attempt, seed = seed, stage = GameManager.Instance.RoundManager.Stage,
                beforePreparation = RunManager.Instance.CaptureDebugSnapshot(), preparationRandom = UnityEngine.Random.state,
                enemyIds = AliveEnemies().Select(u => u.ID).ToArray(), enemyXs = AliveEnemies().Select(u => u.currentCell.xPos).ToArray(), enemyYs = AliveEnemies().Select(u => u.currentCell.yPos).ToArray() };
        private void CampaignSaveFailure(CampaignFailureCheckpoint point, CampaignBattle battle, bool exhausted)
        {
            point.original = battle; point.partyExhausted = exhausted;
            string file = $"failure_{point.team}_{point.attempt}_{point.stage}_{(exhausted ? "preparation" : "combat")}.json";
            File.WriteAllText(Path.Combine(_requestDirectory, file), JsonUtility.ToJson(point));
        }
        private void CampaignFailurePreparation()
        {
            if (string.IsNullOrEmpty(_failurePreparationPolicy)) return;
            var game = GameManager.Instance;
            // 자동 재배치로 보관 카드를 활성 칸에 넣기 전에 판매·식량 확보를 결정한다.
            game.inventoryManager.SellAllValuables();
            if (game.inventoryManager.FoodUnits >= GameManager.LongRestFoodCost || game.ShopPurchasesLeft <= 0) return;
            // 현행 보급은 4종·표시 6칸. 실제 보급 후보의 식량만 실제 가격으로 구매한다.
            var supplyRandom = new System.Random(game.RoundManager.Stage * 7919);
            int display = (int)typeof(Managers.UI.Screens.ShopScreen).GetField("SupplyOnDisplay", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
            var food = RewardManager.Instance.BuildShopItems().OrderBy(_ => supplyRandom.Next()).Take(display).FirstOrDefault(r => r.itemId == 5002);
            if (food == null) { _failureChoices.Add("식량40 매대 없음"); return; }
            int price = RewardManager.ShopPrice(food, game.RoundManager.Stage);
            if (_failurePreparationPolicy == "sell_for_rest")
            {
                // 활성 칸은 유지하고 평가가 낮은 보관 코드부터 최대 3장만 판매한다.
                var holdings = AttackForms.PartyUnits().SelectMany(u => u.StoredRewardCodeInstances.Select(h => (owner: u, holding: h)))
                    .OrderBy(x => CampaignCodeValue(x.owner, x.holding.codeId)).ThenBy(x => RewardCodeInventory.SalePrice(x.holding)).Take(3).ToList();
                foreach (var x in holdings)
                {
                    if (game.inventoryManager.Gold >= price) break;
                    if (RewardCodeInventory.TrySell(x.owner, x.holding.instanceId, out int gold))
                        _failureChoices.Add($"보관 코드 판매 {(x.owner.ID > 0 ? x.owner.ID : x.owner.LastActiveId)}/{x.holding.codeId}: +{gold}G");
                    else _failureChoices.Add($"보관 코드 판매 거절 {x.holding.codeId}");
                }
            }
            if (price <= 0 || price > game.inventoryManager.Gold || !game.inventoryManager.TrySpendGold(price)) return;
            if (!RewardManager.Instance.ApplyRewardEffect(food, null, 0)) { game.inventoryManager.AddGold(price); return; }
            game.NotifyShopPurchase(); _failureChoices.Add($"식량40 구매: -{price}G");
        }
        private static string FailureCombatKey(RunSaveData save, UnityEngine.Random.State random)
        {
            // 새 구매의 인스턴스 GUID만 다른 동일 선택은 재전투하지 않는다. 보유 코드·축전·취득층은 유지한다.
            string state = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(save), "\"instanceId\":\"[^\"]*\"", "\"instanceId\":\"\"");
            return state + JsonUtility.ToJson(random);
        }
        private static void FailureRestoreEnemies(CampaignFailureCheckpoint p)
        {
            var game = GameManager.Instance; var grid = GridManager.Instance;
            int[] ids = p.enemyIds, xs = p.enemyXs, ys = p.enemyYs;
            // 이 필드 도입 전의 체크포인트도 원 패배의 적 ID와 현행 편성 규칙으로 복원한다.
            if ((ids == null || ids.Length == 0) && !p.partyExhausted && !string.IsNullOrWhiteSpace(p.original?.enemies))
            {
                ids = p.original.enemies.Split(',').Select(int.Parse).ToArray(); xs = new int[ids.Length]; ys = new int[ids.Length];
                var data = game.dataManager.FetchStageThemeDataList();
                var theme = data.stageThemes.FirstOrDefault(t => t.id == p.beforePreparation.currentThemeId);
                var patterns = theme?.stagePatterns?.Where(t => t.stageInRound == game.RoundManager.ContentSlotInRound).SelectMany(t => t.patterns ?? new List<RoundPattern>());
                var pinned = patterns?.FirstOrDefault(t => ((t.frontIds?.Count ?? 0) + (t.rearIds?.Count ?? 0)) > 0 &&
                    (t.frontIds ?? new List<int>()).Concat(t.rearIds ?? new List<int>()).SequenceEqual(ids));
                int front = 0, rear = 0;
                var enemies = game.dataManager.FetchEnemyDataList().enemies;
                for (int i = 0; i < ids.Length; i++)
                {
                    int archetype = enemies.First(e => e.id == ids[i]).archetype;
                    xs[i] = pinned != null ? i < (pinned.frontIds?.Count ?? 0) ? 1 : 2 : archetype is 7 or 8 or 9 ? 1 : 2;
                    ys[i] = xs[i] == 1 ? ++front : ++rear;
                }
            }
            if (ids == null || ids.Length == 0) return;
            grid.ClearActiveEnemies();
            for (int i = 0; i < ids.Length; i++) grid.SpawnUnit(xs[i], ys[i], true, ids[i]);
        }
        private IEnumerator ScenarioFailureBranches(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); var game = GameManager.Instance;
            float capture = Time.captureDeltaTime, step = Time.fixedDeltaTime;
            int sync = QualitySettings.vSyncCount, limit = Application.targetFrameRate;
            var filter = Debug.unityLogger.filterLogType;
            Time.captureDeltaTime = 1f / 30f; Time.fixedDeltaTime = .2f; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Debug.unityLogger.filterLogType = LogType.Warning;
            _campaignInventoryAware = _campaignDotRewardStudy = true;
            DebugMode.SuiteRunning = true; DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            try
            {
                // 자연 런별 마지막 전투 패배와 준비 실패에서 최대 3개 선택만 재생한다. 자연 평균에 합산하지 않는다.
                var files = Directory.GetFiles(config.trigger, "failure_*.json").OrderBy(x => x)
                    .Select(f => (file: f, point: JsonUtility.FromJson<CampaignFailureCheckpoint>(File.ReadAllText(f))))
                    .GroupBy(x => x.point.run).SelectMany(g => g.GroupBy(x => x.point.partyExhausted).Select(h => h.OrderByDescending(x => x.point.stage).First()))
                    .OrderBy(x => x.point.team).ThenBy(x => x.point.attempt).ThenBy(x => x.point.stage).ToList();
                if (config.partyLevel > 0) files = files.Take(config.partyLevel).ToList();
                YvCheck("실패 분기 체크포인트", "1개 이상", files.Count.ToString(), files.Count > 0);
                string output = Path.Combine(_requestDirectory, "failure_branches.jsonl");
                var completed = (File.Exists(output) ? File.ReadAllLines(output).Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(JsonUtility.FromJson<CampaignFailureBranch>) : Enumerable.Empty<CampaignFailureBranch>())
                    .Where(r => r.finished).GroupBy(r => r.parentRun + "/" + r.stage + "/" + r.policy).ToDictionary(g => g.Key, g => g.Last());
                var reused = completed.Values.Where(r => !string.IsNullOrEmpty(r.combatKey))
                    .GroupBy(r => r.combatKey).ToDictionary(g => g.Key, g => g.First());
                foreach (var item in files)
                foreach (string policy in new[] { "control", "reserve_food", "sell_for_rest" })
                {
                    var p = item.point;
                    if (policy == "control" && p.partyExhausted) continue;
                    if (completed.TryGetValue(p.run + "/" + p.stage + "/" + policy, out var done))
                    {
                        YvCheck($"완료 분기 재개 {p.team}/{p.attempt}/{p.stage}/{policy}", "완료 표본 재사용·원 실패 일치", done.stop,
                            done.finished && (policy != "control" || done.controlMatched));
                        continue;
                    }
                    game.DebugResetBattle(); game.enabled = true;
                    _failurePreparationPolicy = null; _failureChoices.Clear();
                    var snapshot = policy == "control" ? p.afterPreparation : p.beforePreparation;
                    UnityEngine.Random.state = p.preparationRandom;
                    RunManager.Instance.RestoreDebugSnapshot(snapshot);
                    FailureRestoreEnemies(p);
                    UnityEngine.Random.state = policy == "control" ? p.battleRandom : p.preparationRandom;
                    _failurePreparationPolicy = policy == "control" ? null : policy;
                    int goldBefore = game.inventoryManager.Gold, foodBefore = game.inventoryManager.FoodUnits, shortBefore = game.ShortRestCharges, aliveBefore = AliveAllies().Count;
                    bool restedBefore = !game.CanRest;
                    if (policy != "control") CampaignPreparation();
                    var result = new CampaignFailureBranch { parentRun = p.run, team = p.team, attempt = p.attempt, stage = p.stage,
                        checkpoint = item.file, policy = policy, enemyRestoreMode = p.enemyIds?.Length > 0 ? "captured" : !p.partyExhausted && !string.IsNullOrWhiteSpace(p.original?.enemies) ? "original_combat_roster" : "regenerated_preparation", goldBefore = goldBefore, foodBefore = foodBefore,
                        goldAfter = game.inventoryManager.Gold, foodAfter = game.inventoryManager.FoodUnits, choices = _failureChoices.ToList(),
                        shortRestsBefore = shortBefore, shortRestsAfter = game.ShortRestCharges, aliveBeforePreparation = aliveBefore, aliveAfterPreparation = AliveAllies().Count, restedBefore = restedBefore, restedAfter = !game.CanRest,
                        changed = JsonUtility.ToJson(RunManager.Instance.CaptureDebugSnapshot()) != JsonUtility.ToJson(p.afterPreparation),
                        formation = string.Join(";", AliveAllies().Select(u => u.ID + ":" + u.currentCell.xPos + ":" + u.currentCell.yPos)) };
                    string combatKey = p.run + "/" + p.stage + "/" + Hash128.Compute(FailureCombatKey(RunManager.Instance.CaptureDebugSnapshot(), UnityEngine.Random.state));
                    result.combatKey = combatKey;
                    if (policy != "control" && reused.TryGetValue(combatKey, out var prior))
                    {
                        result.reusedPolicy = prior.policy; result.stop = prior.stop; result.finished = prior.finished;
                        result.cycles = prior.cycles; result.won = prior.won; result.survivors = prior.survivors;
                        result.telemetry = prior.telemetry == null ? null : JsonUtility.FromJson<DotDiagnosisRow>(JsonUtility.ToJson(prior.telemetry));
                        if (result.telemetry != null) result.telemetry.profile = policy;
                        result.enemies = prior.enemies; result.damage = prior.damage;
                    }
                    else if (AliveAllies().Count == 0) { result.stop = "party_exhausted"; result.finished = true; }
                    else
                    {
                        Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(5);
                        var row = new DotDiagnosisRow { team = p.team, attempt = p.attempt, stage = p.stage, seed = p.seed, profile = policy, party = CauseParty() };
                        result.telemetry = row;
                        using (var recorder = new DotDiagnosisRecorder(row))
                        {
                            result.executed = true; game.StartRound(); recorder.SealEnemyBudget(); result.enemies = string.Join(",", AliveEnemies().Select(u => u.ID));
                            float start = Time.realtimeSinceStartup;
                            while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start < 180) yield return null;
                            result.finished = game.gameState != GameState.RoundInProgress;
                            result.cycles = row.cycles = game.ActionScheduler.ReferenceCyclesElapsed;
                            result.won = row.won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                            result.survivors = row.survivors = recorder.EndSurvivors;
                            row.enemyHpFractionEnd = recorder.EndEnemyFraction;
                            result.damage = string.Join(";", DamageMeter.Current.Select(e => (e.Unit.ID > 0 ? e.Unit.ID : e.Unit.LastActiveId) + ":" + e.Damage));
                            result.stop = result.finished ? result.won ? "win" : "loss" : "watchdog";
                        }
                    }
                    result.controlMatched = policy != "control" || (p.original != null && result.won == p.original.won &&
                        result.survivors == p.original.survivors && result.enemies == p.original.enemies && result.damage == p.original.damage && Near(result.cycles, p.original.cycles, .001f));
                    if (result.finished && result.reusedPolicy == null) reused[combatKey] = result;
                    CampaignWrite("failure_branches", result);
                    YvCheck($"실패 분기 종료 {p.team}/{p.attempt}/{p.stage}/{policy}", "정상 종료·원 실패 별도 보존", result.stop, result.finished);
                    if (policy == "control") YvCheck($"원 실패 재현 {p.team}/{p.attempt}/{p.stage}", "승패·라운드·생존·적 일치", result.cycles.ToString(), result.controlMatched);
                }
            }
            finally
            {
                _failurePreparationPolicy = null; _campaignDotRewardStudy = false;
                Time.captureDeltaTime = capture; Time.fixedDeltaTime = step; QualitySettings.vSyncCount = sync; Application.targetFrameRate = limit;
                Debug.unityLogger.filterLogType = filter;
            }
            OsFinish(true);
        }
    }
}
#endif
