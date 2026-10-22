#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private bool _causeTrace;
        private string _causeKit, _causeTeam;
        private int _causeAttempt, _causeBattleStage;
        private CauseDeathRecorder _causeDeaths;
        [Serializable] private sealed class CauseDeath
        { public int unit, actor, action; public float round; public string remaining; }
        [Serializable] private sealed class CauseReplay
        {
            public string kit, state, team, profile, enemySignature;
            public int attempt, seed, foodBefore, foodAfter, shortRestBefore, shortRestAfter;
            public bool interventionApplied;
            public DotDiagnosisRow row;
            public List<CauseDeath> deaths;
        }
        [Serializable] private sealed class CausePhase
        { public string kit, team, phase, snapshot; public int attempt, stage, food, gold, shortRests; }
        private static string CauseParty() => string.Join(";", GridManager.Instance.heroList
            .Where(u => u != null && !u.IsEnemy && (u.ID > 0 || u.LastActiveId > 0))
            .Select(u => (u.ID > 0 ? u.ID : u.LastActiveId) + ":L" + u.Level + ":HP" + u.HpCurr + "/" + u.HpMax + ":MP" + u.ManaCurr + ":C" + string.Join("/", u.RewardCodeSlots)));
        private sealed class CauseDeathRecorder : IDisposable
        {
            public readonly List<CauseDeath> Events = new();
            private readonly List<(Unit unit, Action<EventContext> callback)> _hooks = new();
            public CauseDeathRecorder()
            {
                foreach (var unit in AliveAllies())
                {
                    int id = unit.ID;
                    Action<EventContext> callback = _ =>
                    {
                        var game = GameManager.Instance;
                        if (game.gameState != GameState.RoundInProgress) return;
                        var scheduler = game.ActionScheduler;
                        Events.Add(new CauseDeath { unit = id, actor = scheduler.ExecutingUnit?.ID ?? 0,
                            action = scheduler.CurrentActionId, round = scheduler.ReferenceCyclesElapsed, remaining = CauseParty() });
                    };
                    unit.AddListener(UnitEventType.OnDeath, callback); _hooks.Add((unit, callback));
                }
            }
            public void Dispose()
            { foreach (var h in _hooks) if (h.unit != null) h.unit.RemoveListener(UnitEventType.OnDeath, h.callback); }
        }
        private void CauseRunStart(string team, int attempt)
        { if (!_causeTrace) return; CauseEndFight(); _causeTeam = team; _causeAttempt = attempt; }
        private void CauseCapture(string phase)
        {
            if (!_causeTrace) return;
            var game = GameManager.Instance;
            var saved = RunManager.Instance.CaptureDebugSnapshot();
            string relative = $"trace_{_causeKit}_{_causeTeam}_{_causeAttempt}_{game.RoundManager.Stage}_{phase}.json";
            File.WriteAllText(Path.Combine(_requestDirectory, relative), JsonUtility.ToJson(saved));
            CampaignWrite("phases", new CausePhase { kit = _causeKit, team = _causeTeam, attempt = _causeAttempt,
                stage = game.RoundManager.Stage, phase = phase, snapshot = relative,
                food = game.inventoryManager.FoodUnits, gold = game.inventoryManager.Gold, shortRests = game.ShortRestCharges });
        }
        private void CauseBeginFight()
        { if (!_causeTrace) return; CauseEndFight(); CauseCapture("battle_start"); _causeBattleStage = GameManager.Instance.RoundManager.Stage; _causeDeaths = new CauseDeathRecorder(); }
        private void CauseEndFight()
        {
            if (_causeDeaths == null) return;
            CampaignWrite("trace_deaths", new CauseReplay { kit = _causeKit, team = _causeTeam, attempt = _causeAttempt,
                profile = _causeBattleStage.ToString(), deaths = _causeDeaths.Events });
            _causeDeaths.Dispose(); _causeDeaths = null;
        }
        private IEnumerator ScenarioAtalanteCauseCampaign(VerifyScenarioRequest config, string kit)
        {
            _causeTrace = true; _causeKit = kit;
            try { yield return ScenarioCompositionCampaign(config); }
            finally { CauseEndFight(); _causeTrace = false; }
        }
        private IEnumerator ScenarioAtalanteCauseReplay(VerifyScenarioRequest config, string kit, bool operations = false)
        {
            _yvFailures.Clear(); var game = GameManager.Instance;
            float capture = Time.captureDeltaTime, fixedStep = Time.fixedDeltaTime;
            int limit = Application.targetFrameRate, sync = QualitySettings.vSyncCount;
            var filter = Debug.unityLogger.filterLogType;
            Time.captureDeltaTime = 1f / 30f; Time.fixedDeltaTime = .2f;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; Debug.unityLogger.filterLogType = LogType.Warning;
            var canvases = new HashSet<Canvas>();
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false; DebugMode.SuiteRunning = true;
            try
            {
                var files = Directory.GetFiles(config.trigger, "snapshot_*_24.json", SearchOption.AllDirectories).OrderBy(x => x).ToList();
                YvCheck("아탈 원인 비교 고정 준비 상태", "20", files.Count.ToString(), files.Count == 20);
                foreach (string file in files)
                {
                    string state = new DirectoryInfo(Path.GetDirectoryName(file)).Name;
                    if (operations && state != "new") continue;
                    string[] parts = Path.GetFileNameWithoutExtension(file).Split('_');
                    string team = parts[1] + "_" + parts[2]; int attempt = int.Parse(parts[3]); int seed = 7301700 + attempt;
                    string saved = File.ReadAllText(file); CauseReplay baseline = null;
                    var profiles = operations ? new List<string> { "heal2" } : new List<string> { "base" };
                    if (operations && team == "campaign_01" && attempt == 5) { profiles.Add("short_rest"); profiles.Add("food_rest"); }
                    if (!operations && kit == "new") { profiles.Add("base_repeat"); if (state == "new") profiles.Add("recovered"); }
                    foreach (string profile in profiles)
                    {
                        game.DebugResetBattle(); game.enabled = true; UnityEngine.Random.InitState(seed);
                        RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(saved));
                        int foodBefore = game.inventoryManager.FoodUnits, shortBefore = game.ShortRestCharges; bool applied = false;
                        if (profile == "short_rest") applied = game.TryShortRest();
                        if (profile == "food_rest" && game.CanRest) { game.inventoryManager.AddItem(5002); applied = game.TryLongRest(); }
                        if (profile == "recovered")
                            foreach (Unit u in GridManager.Instance.heroList.Where(u => u != null && !u.IsEnemy && (u.ID > 0 || u.LastActiveId > 0)).ToList())
                            { if (!u.IsOnField) u.ReviveAfterBattle(1f); else u.RestoreHpOutsideCombat(u.HpMax); }
                        Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(5);
                        var row = new DotDiagnosisRow { team = team, attempt = attempt, seed = seed, stage = game.RoundManager.Stage,
                            profile = profile, category = "FixedBoss", party = CauseParty() };
                        var result = new CauseReplay { kit = kit, state = state, team = team, attempt = attempt, profile = profile, seed = seed, row = row, interventionApplied = applied, foodBefore = foodBefore, foodAfter = game.inventoryManager.FoodUnits, shortRestBefore = shortBefore, shortRestAfter = game.ShortRestCharges };
                        using (var deaths = new CauseDeathRecorder())
                        using (var recorder = new DotDiagnosisRecorder(row))
                        {
                            UnityEngine.Random.InitState(seed + 1); game.StartRound(); recorder.SealEnemyBudget();
                            if (profile == "heal2")
                                foreach (Unit u in AliveAllies().Where(u => u.ID == 167))
                                { YvAddTestStatus(u, 99887, "cause_heal2", new DiagnosisHealing(), StatusCategory.Neutral); result.interventionApplied = true; }
                            result.enemySignature = string.Join(";", AliveEnemies().Select(u => u.ID + ":HP" + u.HpMax + ":L" + u.Level + ":DEF" + u.DefCurr + ":SPDEF" + u.SpDefCurr + ":SPD" + u.SpeedCurr));
                            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (canvas.enabled) { canvases.Add(canvas); canvas.enabled = false; }
                            float start = Time.realtimeSinceStartup;
                            while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start < 180 && game.ActionScheduler.ReferenceCyclesElapsed < 40) yield return null;
                            row.cycles = game.ActionScheduler.ReferenceCyclesElapsed;
                            row.timedOut = game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start >= 180;
                            row.censored = game.gameState == GameState.RoundInProgress && !row.timedOut;
                            row.won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                            row.survivors = recorder.EndSurvivors; row.enemyHpFractionEnd = recorder.EndEnemyFraction;
                            result.deaths = deaths.Events;
                        }
                        YvCheck("사망 순서 누락 없음 " + kit + "/" + state + "/" + team + "/" + attempt + "/" + profile, "전체 사망 수 일치", result.deaths.Count + "/" + row.deaths, result.deaths.Count == row.deaths);
                        if (profile == "base") baseline = result;
                        if (profile == "base_repeat") YvCheck("고정 상태·난수 재현 " + state + "/" + team + "/" + attempt, "동일", row.cycles.ToString(),
                            baseline != null && baseline.enemySignature == result.enemySignature && baseline.row.party == row.party && baseline.row.won == row.won &&
                            Near(baseline.row.cycles, row.cycles) && baseline.row.damage == row.damage && baseline.row.incoming == row.incoming && baseline.row.ereshEffective == row.ereshEffective &&
                            string.Join(",", baseline.deaths.Select(d => d.unit)) == string.Join(",", result.deaths.Select(d => d.unit)));
                        YvCheck("원인 비교 정상 종료 " + kit + "/" + state + "/" + team + "/" + attempt + "/" + profile, "완료", row.cycles.ToString(), !row.timedOut && (!row.censored || operations));
                        if (profile is "short_rest" or "food_rest" or "heal2") YvCheck("준비·치유 개입 적용 " + profile + "/" + team + "/" + attempt, "적용", result.interventionApplied.ToString(), result.interventionApplied);
                        CampaignWrite("replays", result);
                    }
                }
            }
            finally
            {
                foreach (Canvas c in canvases) if (c != null) c.enabled = true;
                Time.captureDeltaTime = capture; Time.fixedDeltaTime = fixedStep; Application.targetFrameRate = limit;
                QualitySettings.vSyncCount = sync; Debug.unityLogger.filterLogType = filter; DebugMode.SuiteRunning = false;
            }
            OsFinish(true);
        }
    }
}
#endif
