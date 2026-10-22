#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Codes.Checkpoints;
using Codes.Base;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        [Serializable] private sealed class Boss48Unit
        { public int id, hp, maxHp, shield, mana; public float defense, specialDefense, attack, specialAttack, speed; }
        [Serializable] private sealed class Boss48Event
        {
            public string type, kind, label; public int actor, target, action, raw, damage, hpDamage, shieldDamage, hpBefore, hpAfter, shieldBefore, shieldAfter;
            public float round, bossFraction; public bool frailty;
        }
        [Serializable] private sealed class Boss48Transcript
        { public List<Boss48Event> events = new(); public List<Boss48Event> deaths = new(); public string[] combatLog; }
        [Serializable] private sealed class Boss48Replay
        {
            public string origin, team, profile, file, transcript, damageMeter, loadoutChanges;
            public int attempt, initialAlive, bossId, bossMaxHp, bossHpAtEnd, escortDeaths, bossUltimates;
            public float bossAtFirstDeath = -1, bossAt10 = -1;
            public bool matched, finished, censored;
            public List<Boss48Unit> start = new(); public DotDiagnosisRow telemetry;
            public List<Boss48Event> deaths = new();
            public List<Boss48Recovery> recovery = new();
        }
        [Serializable] private sealed class Boss48Recovery
        { public string type; public int source, target, effective, overflow, amount; public float round; public bool opening; }
        private sealed class Boss48Damage : BaseEffect
        {
            private readonly float _amount;
            public Boss48Damage(float amount) : base(0) => _amount = amount;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => attacker == Target ? _amount : 1;
        }
        // 예전 기본량으로만 되돌린다. 보유자의 보호막 보상/대상의 수신 보정은 기존 공통 경로에 남긴다.
        private sealed class LegacyMoonShield : BaseEffect
        {
            public LegacyMoonShield() : base(0) { }
            public override float OutgoingShieldMultiplierModifier(Unit source, Unit target)
            {
                if (source != Target) return 1;
                bool ultimate = GameManager.Instance?.ActionScheduler?.ExecutingUnit == source &&
                    GameManager.Instance.ActionScheduler.ActingKind == ActionScheduler.ActionKind.Ultimate;
                float defense = source.DefCurr;
                int current = Mathf.RoundToInt(defense * (ultimate ? 1.5f : 1f));
                return current > 0 ? Mathf.RoundToInt(defense * (ultimate ? .5f : .35f)) / (float)current : 1;
            }
        }
        private string Boss48EquipDefenseReward(int id)
        {
            var holders = AliveAllies().Where(u => !u.IsSummon && (id != 739 || ShieldCapability.StrongestBaseAmount(u) > 0))
                .OrderByDescending(u => id == 739 ? (u.ID == 45 ? 10000 : ShieldCapability.StrongestBaseAmount(u)) : u.ID == 162 ? 100 : u.ID == 45 ? 90 : 0).ToList();
            if (holders.Count == 0) return "없음";
            var holder = holders[0]; var slots = holder.RewardCodeSlots.ToList();
            int index = slots.FindIndex(x => x == 0);
            if (index < 0) index = Enumerable.Range(0, slots.Count).Where(i => slots[i] is not (738 or 739))
                .OrderBy(i => CampaignCodeValue(holder, slots[i])).First();
            int removed = slots[index]; slots[index] = id;
            if (!holder.ApplyRewardCodeInventory(slots, holder.StoredRewardCodeIds.ToList()))
                throw new InvalidOperationException("진단 코드 활성 칸 적용 실패");
            return $"{holder.ID}:slot{index}:{removed}→{id}";
        }
        private sealed class Boss48Recorder : IDisposable
        {
            public readonly Boss48Transcript Transcript = new();
            private readonly Boss48Replay _row;
            private readonly Dictionary<Unit, int> _allies;
            private readonly Dictionary<DamageContext, (int hp, int shield, bool frail)> _before = new();
            private readonly Dictionary<Unit, Boss48Event> _last = new();
            private readonly Unit _boss;
            private int _bossHp;
            private readonly Dictionary<int, string> _actions = new();
            public Boss48Recorder(Boss48Replay row)
            {
                _row = row; _allies = AliveAllies().ToDictionary(u => u, u => u.ID); _boss = AliveEnemies().Single(u => u.ID is 3201 or 3202);
                _row.bossId = _boss.ID;
                Unit.AnyDamageIncoming += Incoming; Unit.AnyDamageDealt += Resolved; Unit.AnyUnitDied += Died;
                Unit.AnyHealed += Healed; Unit.AnyShieldGranted += Shielded;
                ActionScheduler.AnyActionStarted += Action;
            }
            private void Healed(Unit target, Unit source, int effective, int overflow)
            {
                if (!_allies.ContainsKey(target)) return;
                _row.recovery.Add(new Boss48Recovery { type = "heal", source = Id(source), target = Id(target),
                    effective = effective, overflow = overflow, amount = effective + overflow,
                    round = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed });
            }
            private void Shielded(Unit target, Unit source, int amount)
            {
                if (!_allies.ContainsKey(target)) return;
                _row.recovery.Add(new Boss48Recovery { type = "shield", source = Id(source), target = Id(target), amount = amount,
                    round = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed });
            }
            private int Id(Unit u) => u == null ? 0 : _allies.TryGetValue(u, out int id) ? id : u.ID > 0 ? u.ID : u.LastActiveId;
            private float Fraction() => _row.bossMaxHp > 0 && _boss != null ? Mathf.Max(0, _boss.HpCurr) / (float)_row.bossMaxHp : 0;
            public void Seal()
            {
                // 시작 보호막은 AV 초기화 전에 지급되므로 이전 전투의 시계를 물려받지 않는다.
                foreach (var entry in _row.recovery) { entry.round = 0; entry.opening = true; }
                _row.bossMaxHp = _boss.HpMax;
                _bossHp = _boss.HpCurr;
                _row.start = AliveAllies().Concat(AliveEnemies()).Select(u => new Boss48Unit { id = Id(u), hp = u.HpCurr, maxHp = u.HpMax,
                    shield = u.ShieldCurr, mana = u.ManaCurr, defense = u.DefCurr, specialDefense = u.SpDefCurr, attack = u.AtkCurr,
                    specialAttack = u.StatValue(StatType.SpecialAttack), speed = u.SpeedCurr }).ToList();
            }
            private void Action(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                var scheduler = GameManager.Instance.ActionScheduler; _actions[scheduler.CurrentActionId] = label;
                Transcript.events.Add(new Boss48Event { type = "action", actor = Id(unit), action = scheduler.CurrentActionId,
                    kind = kind.ToString(), label = label, round = scheduler.ReferenceCyclesElapsed, bossFraction = Fraction() });
                if (_row.bossAt10 < 0 && scheduler.ReferenceCyclesElapsed >= 10) _row.bossAt10 = Fraction();
                if (Id(unit) == _row.bossId && kind == ActionScheduler.ActionKind.Ultimate) _row.bossUltimates++;
            }
            private void Incoming(Unit target, DamageContext context)
            { _before[context] = (target.HpCurr, target.ShieldCurr, target.HasStatusKey(Checkpoint48.FrailtyKey)); }
            private void Resolved(DamageResolvedContext hit)
            {
                var scheduler = GameManager.Instance.ActionScheduler; var c = hit.DamageContext;
                // 전멸 정리에서 풀로 반환된 개체의 체력이 0으로 초기화되기 전에 보존한다.
                if (hit.Target == _boss) _bossHp = Mathf.Max(0, hit.Target.HpCurr);
                var before = _before.GetValueOrDefault(c);
                var item = new Boss48Event { type = "hit", actor = Id(hit.Attacker), target = Id(hit.Target), action = scheduler.CurrentActionId,
                    kind = c.CodeType.ToString(), label = _actions.GetValueOrDefault(scheduler.CurrentActionId, ""), raw = c.Damage,
                    damage = hit.DamageDealt, hpDamage = c.HpDamage, shieldDamage = hit.DamageDealt - c.HpDamage,
                    hpBefore = before.hp, hpAfter = hit.Target.HpCurr, shieldBefore = before.shield, shieldAfter = hit.Target.ShieldCurr,
                    frailty = before.frail, round = scheduler.ReferenceCyclesElapsed, bossFraction = Fraction() };
                Transcript.events.Add(item); _last[hit.Target] = item; _before.Remove(c);
            }
            private void Died(Unit unit, Unit killer)
            {
                if (_allies.ContainsKey(unit))
                {
                    var last = _last.GetValueOrDefault(unit);
                    var item = new Boss48Event { type = "death", actor = Id(killer), target = Id(unit), round = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed,
                        action = last?.action ?? -1, kind = last?.kind, label = last?.label, raw = last?.raw ?? 0, damage = last?.damage ?? 0,
                        hpDamage = last?.hpDamage ?? 0, hpBefore = last?.hpBefore ?? 0, shieldBefore = last?.shieldBefore ?? 0,
                        frailty = last?.frailty ?? false, bossFraction = Fraction() };
                    Transcript.deaths.Add(item);
                    _row.deaths.Add(item);
                    if (_row.bossAtFirstDeath < 0) _row.bossAtFirstDeath = Fraction();
                }
                else if (Id(unit) == 1121) _row.escortDeaths++;
            }
            public void Dispose()
            {
                _row.bossHpAtEnd = _bossHp;
                Transcript.combatLog = CombatLog.Current.Select(e => e.Text).ToArray();
                Unit.AnyDamageIncoming -= Incoming; Unit.AnyDamageDealt -= Resolved; Unit.AnyUnitDied -= Died;
                Unit.AnyHealed -= Healed; Unit.AnyShieldGranted -= Shielded;
                ActionScheduler.AnyActionStarted -= Action;
            }
        }
        private IEnumerator ScenarioBoss48Diagnosis(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); var game = GameManager.Instance;
            bool defenseStudy = config.name == "boss48_defense_rewards";
            bool oldStudy = _campaignDotRewardStudy; _campaignDotRewardStudy = true;
            var files = Directory.GetFiles(config.trigger, "failure_*_48_combat.json", SearchOption.AllDirectories).OrderBy(s => s).ToList();
            YvCheck("48층 원인 분석 고정 상태", "자연12 + 분기1", files.Count.ToString(), files.Count == 13);
            float capture = Time.captureDeltaTime, step = Time.fixedDeltaTime; int sync = QualitySettings.vSyncCount, limit = Application.targetFrameRate;
            var filter = Debug.unityLogger.filterLogType; var canvases = new HashSet<Canvas>();
            Time.captureDeltaTime = 1f / 30f; Time.fixedDeltaTime = .2f; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Debug.unityLogger.filterLogType = LogType.Warning; DebugMode.SuiteRunning = true;
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            try
            {
                foreach (string file in files)
                foreach (string profile in defenseStudy ? new[] { "legacy", "shield_only", "fate", "opening", "both", "both_repeat" } :
                    new[] { "base", "recovered", "heal2", "incoming70", "incoming40", "outgoing150", "ceiling01" })
                {
                    var p = JsonUtility.FromJson<CampaignFailureCheckpoint>(File.ReadAllText(file));
                    string marker = Path.Combine(_requestDirectory, $"boss48_{p.run}_{profile}.done");
                    if (File.Exists(marker)) continue;
                    game.DebugResetBattle(); game.enabled = true;
                    UnityEngine.Random.state = p.preparationRandom; RunManager.Instance.RestoreDebugSnapshot(p.afterPreparation);
                    FailureRestoreEnemies(p);
                    if (!defenseStudy && profile != "base")
                        foreach (Unit ally in GridManager.Instance.heroList.Where(u => u != null && !u.IsEnemy && (u.ID > 0 || u.LastActiveId > 0)).ToList())
                        { if (!ally.IsOnField) ally.ReviveAfterBattle(1f); else ally.RestoreHpOutsideCombat(ally.HpMax); }
                    foreach (Unit ally in AliveAllies())
                    {
                        if (profile == "legacy" && ally.ID == 45) YvAddTestStatus(ally, 99872, "b48_legacy_shield", new LegacyMoonShield(), StatusCategory.Neutral);
                        if (profile == "heal2" && ally.ID == 167) YvAddTestStatus(ally, 99873, "b48_heal2", new DiagnosisHealing(), StatusCategory.Neutral);
                        if (profile is "incoming70" or "incoming40" or "ceiling01") YvAddTestStatus(ally, 99874, "b48_incoming",
                            new ReceivingDamageMultiplierEffect(profile == "incoming70" ? .7f : profile == "incoming40" ? .4f : .01f), StatusCategory.Neutral);
                        if (profile == "outgoing150") YvAddTestStatus(ally, 99875, "b48_outgoing", new Boss48Damage(1.5f), StatusCategory.Neutral);
                    }
                    string changes = "";
                    if (profile is "fate" or "both" or "both_repeat") changes += Boss48EquipDefenseReward(738) + ";";
                    if (profile is "opening" or "both" or "both_repeat") changes += Boss48EquipDefenseReward(739) + ";";
                    UnityEngine.Random.state = p.battleRandom;
                    var row = new DotDiagnosisRow { team = p.team, attempt = p.attempt, seed = p.seed, stage = 48,
                        category = "FixedBoss", profile = profile, party = CauseParty() };
                    var result = new Boss48Replay { team = p.team, attempt = p.attempt, profile = profile, file = file,
                        origin = file.Contains("Fixtures") ? "natural" : "continuation", telemetry = row, initialAlive = AliveAllies().Count, loadoutChanges = changes };
                    var trace = new Boss48Recorder(result);
                    using (trace)
                    using (var meter = new DotDiagnosisRecorder(row))
                    {
                        Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(5); game.StartRound(); meter.SealEnemyBudget(); trace.Seal();
                        float start = Time.realtimeSinceStartup;
                        while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start < 180 && game.ActionScheduler.ReferenceCyclesElapsed < 20)
                        {
                            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (canvas.enabled) { canvases.Add(canvas); canvas.enabled = false; }
                            yield return null;
                        }
                        row.cycles = game.ActionScheduler.ReferenceCyclesElapsed; result.finished = game.gameState != GameState.RoundInProgress;
                        row.timedOut = !result.finished && Time.realtimeSinceStartup - start >= 180;
                        row.censored = result.censored = !result.finished && !row.timedOut;
                        row.won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                        row.survivors = meter.EndSurvivors; row.enemyHpFractionEnd = meter.EndEnemyFraction;
                        result.damageMeter = string.Join(";", DamageMeter.Current.Select(e => (e.Unit.ID > 0 ? e.Unit.ID : e.Unit.LastActiveId) + ":" + e.Damage));
                        result.matched = profile is not ("base" or "legacy") || (row.won == p.original.won && row.survivors == p.original.survivors &&
                            Near(row.cycles, p.original.cycles, .001f) && result.damageMeter == p.original.damage);
                        if (profile is "base" or "legacy") YvCheck($"48층 원 패배 재현 {p.team}/{p.attempt}/{result.origin}", "승패·피해·생존·라운드 동일", row.cycles.ToString(), result.matched);
                        YvCheck($"48층 진단 종료 {p.team}/{p.attempt}/{profile}/{result.origin}", "정상 종료 또는20R 관측 중절", row.cycles.ToString(), !row.timedOut);
                        result.transcript = $"b48_{p.run}_{profile}_trace.json";
                    }
                    // Dispose에서 확정된 보스 잔여 체력과 전체 로그도 함께 보존한다.
                    File.WriteAllText(Path.Combine(_requestDirectory, result.transcript), JsonUtility.ToJson(trace.Transcript));
                    CampaignWrite("boss48_replays", result);
                    if (!row.timedOut && result.matched) File.WriteAllText(marker, JsonUtility.ToJson(result));
                    File.WriteAllText(Path.Combine(_requestDirectory, "progress48.json"), JsonUtility.ToJson(result));
                }
            }
            finally
            {
                foreach (var canvas in canvases) if (canvas != null) canvas.enabled = true;
                Time.captureDeltaTime = capture; Time.fixedDeltaTime = step; QualitySettings.vSyncCount = sync; Application.targetFrameRate = limit;
                Debug.unityLogger.filterLogType = filter;
                _campaignDotRewardStudy = oldStudy;
            }
            OsFinish(true);
        }
    }
}
#endif
