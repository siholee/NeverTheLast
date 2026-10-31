#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat;
using Core;
using Entities;
using NeverTheLast.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioSimulationParity(VerifyScenarioRequest config)
        {
            string input = Path.GetFullPath(config.trigger);
            var experiment = JsonConvert.DeserializeObject<Experiment>(File.ReadAllText(input));
            if (experiment == null) throw new InvalidOperationException("동등성 실험 입력이 없습니다.");
            GameManager game = GameManager.Instance;
            GridManager grid = GridManager.Instance;
            bool previousEnabled = game.enabled;
            float previousCapture = Time.captureDeltaTime, previousScale = Time.timeScale;
            int serial = 0;
            StackTraceLogType previousLogTrace = Application.GetStackTraceLogType(LogType.Log);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            string output = Path.Combine(_requestDirectory, "unity-battles.jsonl");
            Time.timeScale = 1f;
            Time.captureDeltaTime = .02f;
            game.enabled = false; // 자동 Update와 수동 스케줄러가 두 번 진행하지 않게 한다.
            try
            {
                foreach (Team team in experiment.Teams)
                foreach (Encounter encounter in experiment.Encounters)
                for (int attempt = 0; attempt < experiment.Runs; attempt++)
                {
                    game.gameState = BaseClasses.BaseEnums.GameState.Preparation;
                    game.DebugResetBattle();
                    foreach (Unit unit in grid.heroList.Concat(grid.enemyList).ToArray()) grid.RetireUnit(unit);
                    yield return null;
                    foreach (Cell cell in FindObjectsByType<Cell>(FindObjectsSortMode.None)) cell.reservedTime = 0;
                    DebugMode.ResetAll(); DebugMode.BeginSession(); DebugMode.SuiteRunning = true;
                    game.RoundManager.InitializeStage(encounter.Stage);
                    if (!string.Equals(game.RoundManager.CurrentCombatKind.ToString(), encounter.Kind, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Unity 실제 스테이지 종류와 입력 불일치: {encounter.Stage}/{game.RoundManager.CurrentCombatKind}/{encounter.Kind}");
                    game.inventoryManager.AddGold(team.Gold - game.inventoryManager.Gold);
                    game.KillCount = 0;
                    game.gameState = BaseClasses.BaseEnums.GameState.RoundInProgress;
                    var result = new BattleResult { Team = team.Name, Encounter = encounter.Name,
                        Seed = unchecked(experiment.Seed + attempt), TeamDefinition = team,
                        EncounterDefinition = encounter, MaxActions = experiment.MaxActions, MaxCycles = experiment.MaxCycles };
                    using (var random = CombatRandom.BeginSeed(result.Seed))
                    using (var trace = new CombatParityTrace(result, true))
                    {
                        foreach (Member member in team.Members) Spawn(member, false);
                        foreach (Member member in encounter.Enemies) Spawn(member, true);
                        grid.SetBenchVisible(false);
                        yield return grid.WaitForFieldLayoutSettled();
                        trace.SyncRoster();
                        PartyTreasury.BeginBattle();
                        grid.OnRoundStart();
                        game.ActionScheduler.BeginRound();
                        int ticks = 0;
                        while (true)
                        {
                            bool allies = grid.heroList.Any(u => u != null && u.IsOnField);
                            bool enemies = grid.enemyList.Any(u => u != null && u.IsOnField);
                            if (!allies || !enemies)
                            {
                                result.Outcome = allies ? "win" : enemies ? "loss" : "draw";
                                result.Reason = "annihilation"; break;
                            }
                            if (game.ActionScheduler.ActionCount >= experiment.MaxActions ||
                                game.ActionScheduler.ReferenceCyclesElapsed >= experiment.MaxCycles)
                            { result.Outcome = "draw"; result.Reason = "limit"; break; }
                            if (++ticks > experiment.MaxActions * 5000L + 10000)
                                throw new InvalidOperationException("Unity 동등성 전투 진행 상한 초과");
                            trace.SyncRoster();
                            game.ActionScheduler.Tick(.02f);
                            // 카메라는 unscaled time으로 움직인다. 무제한 배치 프레임에서 가상 시간만
                            // 앞서가면 진형 대기 1.8초가 행동 watchdog 8초를 잘못 넘으므로 최대4배속으로 제한한다.
                            System.Threading.Thread.Sleep(5);
                            yield return null; // 실제 Unity 코루틴·VFX 콜백을 진행한다.
                        }
                        trace.Capture();
                        File.AppendAllText(output, JsonConvert.SerializeObject(result, settings) + Environment.NewLine);
                        File.WriteAllLines(Path.Combine(_requestDirectory, (serial++).ToString("D5") + ".trace.txt"), trace.Events);
                    }
                    game.ActionScheduler.EndRound();
                    grid.OnRoundEnd();
                    foreach (Unit unit in grid.heroList.Concat(grid.enemyList).ToArray()) unit.ClearCombatStateForRemoval();
                    yield return null;

                    void Spawn(Member member, bool enemy)
                    {
                        Unit unit = grid.SpawnUnit(member.Column * (enemy ? 1 : -1), member.Row, enemy, member.Id, playEntry: false);
                        if (unit == null) throw new InvalidOperationException($"Unity 유닛 생성 실패: {member.Id}");
                        if (enemy) unit.DebugSetLevel(member.Level ?? encounter.Stage);
                        else
                        {
                            unit.GrantLevels((member.Level ?? 1) - 1);
                            foreach (int id in member.Rewards)
                                if (!unit.TryGrantRewardCode(id)) throw new InvalidOperationException($"Unity 보상 코드 활성 실패: {id}");
                            foreach (var stack in member.Stacks) unit.SetPermanentStacks(stack.key, stack.value, stack.fraction);
                            if (member.Nectar.Count > 0 || member.RewardInstances.Count > 0)
                            {
                                var save = new UnitSaveData(); unit.WriteRunState(save); save.nectarStats = member.Nectar;
                                foreach (var instance in member.RewardInstances)
                                {
                                    int at = save.activeRewardCodeSlots.IndexOf(instance.codeId);
                                    save.activeRewardCodeInstances[at].charge = instance.charge;
                                }
                                unit.RestoreRunState(save);
                            }
                        }
                        unit.AddUltimateResource(member.Resource);
                        unit.ModifyHp(Mathf.Max(1, Mathf.RoundToInt(unit.HpMax * member.HpRatio)));
                    }
                }
                RecordMechanic("Unity 독립 전투 조건 실행", "모든 조건 완료", serial.ToString(), serial == experiment.Teams.Count * experiment.Encounters.Count * experiment.Runs);
            }
            finally
            {
                game.gameState = BaseClasses.BaseEnums.GameState.Preparation;
                game.DebugResetBattle();
                game.enabled = previousEnabled;
                Time.captureDeltaTime = previousCapture; Time.timeScale = previousScale;
                Application.SetStackTraceLogType(LogType.Log, previousLogTrace);
            }
        }
    }
}
#endif
