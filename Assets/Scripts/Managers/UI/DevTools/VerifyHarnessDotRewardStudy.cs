#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static bool _campaignDotRewardStudy;
        private static bool CampaignHasDotSource()
        {
            var party = GridManager.Instance.heroList.Where(u => u != null && u.ID > 0).ToList();
            if (party.Any(u => SynergyCatalog.UnitOf(u.ID)?.roles.Contains("DOT") == true)) return true;
            var elements = party.Where(u => SynergyCatalog.UnitOf(u.ID)?.roles.Contains("ELEM_APPLY") == true).Select(u => u.Element).ToHashSet();
            return elements.Contains("Hydro") && (elements.Contains("Electro") || elements.Contains("Dendro")) ||
                elements.Contains("Pyro") && elements.Contains("Dendro");
        }
        // 새 보상과 원 지딜 시전자를 평가하는 QA 운영 정책. 구/신 보상 풀에 동일 적용한다.
        private static float? DotStudyCodeValue(Unit unit, int id)
        {
            float dotSource = unit.ID switch { 164 => 92, 165 => 84, 166 => 88, 167 => 68, 66 => 72, 61 => 62, 123 => 82, 45 => 66, _ => 1 };
            if (id is 617 or 811 or 636 or 637)
                return dotSource <= 1 ? 1 : dotSource + (RewardCodeCatalog.TierOf(id) - 2) * 5;
            bool healer = SynergyCatalog.UnitOf(unit.ID)?.roles.Contains("HEAL") == true;
            bool utility = unit.ID is 45 or 162 or 66;
            bool front = unit.currentCell != null && unit.currentCell.xPos == -1;
            if (id == 739) return OpeningShieldCampaignValue(unit);
            if (id == 738)
            {
                if (!CampaignHasDotSource()) return 1;
                if (GridManager.Instance.heroList.Any(u => u != null && u != unit && u.RewardCodeIds.Contains(738))) return 1;
                return utility ? 108 : healer ? 82 : 78;
            }
            if (id is 648 or 650 or 649 or 734 or 736)
                return healer ? (id == 736 ? 110 : id == 650 ? 103 : id == 648 ? 101 : 94) : 1;
            if (id == 735) return healer ? 108 : front ? 80 : 52;
            if (id == 737) return healer ? 86 : front ? 85 : 20;
            if (id == 638) return healer ? 80 : front ? 42 : 38;
            if (id == 639) return healer ? 104 : utility ? 88 : 72;
            if (id is 732 or 824 or 825 or 910 or 733)
            {
                if (!CampaignHasDotSource()) return 1;
                // 같은 오라를 여러 명에게 배분해 활성 슬롯을 낭비하지 않는다.
                if (GridManager.Instance.heroList.Any(u => u != null && u != unit && u.RewardCodeIds.Any(other =>
                    other == id || RewardCodeCatalog.IsUpperOf(other, id) || RewardCodeCatalog.IsUpperOf(id, other)))) return 1;
                return (utility ? 88 : healer ? 69 : 66) + RewardCodeCatalog.TierOf(id) * 3;
            }
            if (id == 651)
            {
                bool shock = GridManager.Instance.heroList.Any(u => u != null && u.ID is 61 or 166);
                if (!shock || GridManager.Instance.heroList.Any(u => u != null && u != unit && u.RewardCodeIds.Contains(651))) return 1;
                return utility ? 102 : healer ? 84 : 66;
            }
            return null;
        }

        private IEnumerator ScenarioDotRewardReplay(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            var game = GameManager.Instance;
            var files = Directory.GetFiles(config.trigger, "snapshot_*_24.json").OrderBy(x => x).ToList();
            YvCheck("신규 보상 24층 스냅샷 수", "5", files.Count.ToString(), files.Count == 5);
            float oldCapture = Time.captureDeltaTime, oldFixed = Time.fixedDeltaTime;
            int oldLimit = Application.targetFrameRate, oldSync = QualitySettings.vSyncCount;
            var oldFilter = Debug.unityLogger.filterLogType;
            Time.captureDeltaTime = 1f / 30f; Time.fixedDeltaTime = .2f;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Debug.unityLogger.filterLogType = LogType.Warning;
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false; DebugMode.SuiteRunning = true;
            var canvases = new HashSet<Canvas>();
            try
            {
                foreach (string file in files)
                {
                    string[] parts = Path.GetFileNameWithoutExtension(file).Split('_');
                    string team = parts[1] + "_" + parts[2]; int attempt = int.Parse(parts[3]); int seed = 6101600 + attempt;
                    string saved = File.ReadAllText(file); string signature = null;
                    var baselines = new Dictionary<string, DotDiagnosisRow>();
                    foreach (string profile in new[] { "off", "on", "on_repeat", "off_repeat" })
                    {
                        game.DebugResetBattle(); game.enabled = true;
                        UnityEngine.Random.InitState(seed + 240000);
                        RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(saved));
                        Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(5);
                        var row = new DotDiagnosisRow { team = team, attempt = attempt, seed = seed, stage = 24, category = "FixedBoss", profile = profile,
                            enemies = string.Join(",", AliveEnemies().Select(u => u.ID + ":" + u.HpMax + ":" + u.currentCell.xPos + ":" + u.currentCell.yPos)),
                            party = string.Join(";", AliveAllies().Select(u => u.ID + ":L" + u.Level + ":HP" + u.HpCurr + "/" + u.HpMax + ":MP" + u.ManaCurr + ":C" + string.Join("/", u.RewardCodeSlots))) };
                        string current = row.enemies + "/" + row.party;
                        signature ??= current; row.matched = signature == current;
                        using (var recorder = new DotDiagnosisRecorder(row))
                        {
                            UnityEngine.Random.InitState(seed + 240001); game.StartRound(); recorder.SealEnemyBudget();
                            if (profile.StartsWith("off"))
                                foreach (Unit unit in AliveAllies())
                                foreach (var code in unit.ActivePassiveCodes.Where(code => code is SecretSupportRewardCode).ToList()) code.StopCode();
                            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (canvas.enabled) { canvases.Add(canvas); canvas.enabled = false; }
                            float start = Time.realtimeSinceStartup;
                            while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start < 180 && game.ActionScheduler.ReferenceCyclesElapsed < 30)
                                yield return null;
                            row.cycles = game.ActionScheduler.ReferenceCyclesElapsed;
                            row.timedOut = game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start >= 180;
                            row.censored = game.gameState == GameState.RoundInProgress && !row.timedOut;
                            row.won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                            row.survivors = recorder.EndSurvivors; row.enemyHpFractionEnd = recorder.EndEnemyFraction;
                        }
                        string key = profile.Split('_')[0];
                        if (profile == key) baselines[key] = row;
                        else
                        {
                            var baseline = baselines[key];
                            row.matched &= row.won == baseline.won && Near(row.cycles, baseline.cycles) && row.damage == baseline.damage &&
                                row.incoming == baseline.incoming && row.ereshEffective == baseline.ereshEffective && row.ereshOverflow == baseline.ereshOverflow &&
                                row.shieldsAbsorbed == baseline.shieldsAbsorbed && row.deaths == baseline.deaths && Near(row.firstDeathCycle, baseline.firstDeathCycle);
                        }
                        CampaignWrite("replays", row);
                        YvCheck($"동일 상태 신규 보상 {team}/{attempt}/{profile}", "동일 준비 상태·반복 일치·측정 완료", JsonUtility.ToJson(row), row.matched && !row.timedOut && !row.censored);
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = oldCapture; Time.fixedDeltaTime = oldFixed;
                Application.targetFrameRate = oldLimit; QualitySettings.vSyncCount = oldSync;
                Debug.unityLogger.filterLogType = oldFilter;
                foreach (Canvas canvas in canvases) if (canvas != null) canvas.enabled = true;
            }
            OsFinish(true);
        }
    }
}
#endif
