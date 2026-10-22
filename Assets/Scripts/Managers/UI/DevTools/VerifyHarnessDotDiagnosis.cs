#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
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
        [Serializable] private sealed class DotDiagnosisRow
        {
            public string team, category, profile, enemies, party, firstDead;
            public int attempt, seed, stage, survivors, naturalTicks, immediateTicks, ereshUltimateHeals;
            public float cycles, firstDeathCycle = -1, enemyHpFractionAtFirstDeath = -1, enemyHpFractionEnd;
            public long damage, dotDamage, incoming, ereshEffective, ereshOverflow, otherHealing;
            public long ereshPassiveEffective, ereshUltimateEffective, shieldsGranted, shieldsAbsorbed, hpDamage;
            public int deaths, frontlineDeathsBeforeFirstNormal;
            public bool won, timedOut, censored, matched;
            public List<DotDiagnosisUnit> units = new();
            public List<LokapalaMechanismRow> mechanisms = new();
        }
        [Serializable] private sealed class DotDiagnosisUnit
        {
            public int id; public long damage, dotDamage, incoming, healsReceived, overflow;
            public bool front, diedBeforeFirstNormal;
            public float firstNormalCycle = -1, firstDeathCycle = -1;
        }
        [Serializable] private sealed class LokapalaMechanismRow
        {
            public int id, normal, ultimate, special, additional, preparationHits, finaleHits, specialTargets, specialUnmarked,
                treasuryEnhanced, treasuryFallback, treasuryOne, treasuryTwo, ticketAccepted, ticketRejected, ticketHeld, ticketFocused, ticketFired;
            public float firstPayloadCycle = -1, goldMultiplierSum;
            public long normalDamage, specialDamage, additionalDamage;
        }
        private sealed class DiagnosisHealing : BaseEffect
        {
            public DiagnosisHealing() : base(0) { Category = EffectCategory.Neutral; }
            public override float OutgoingHealingMultiplierModifier(Unit source, Unit target) => source == Target ? 2f : 1f;
        }
        private sealed class DotDiagnosisRecorder : IDisposable
        {
            private readonly DotDiagnosisRow _row;
            private readonly Dictionary<Unit, int> _ids;
            private readonly List<(Unit unit, Action<EventContext> callback)> _deaths = new();
            private readonly Dictionary<Unit, float> _enemyHp;
            public float EndEnemyFraction { get; private set; }
            public int EndSurvivors { get; private set; }
            private void CaptureLive()
            {
                if (GameManager.Instance.gameState != GameState.RoundInProgress) return;
                EndEnemyFraction = EnemyFraction();
                EndSurvivors = _ids.Keys.Count(u => u != null && u.IsOnField && u.HpCurr > 0);
            }
            public DotDiagnosisRecorder(DotDiagnosisRow row)
            {
                _row = row;
                _ids = AliveAllies().ToDictionary(u => u, u => u.ID);
                _enemyHp = AliveEnemies().ToDictionary(u => u, u => (float)u.HpMax);
                foreach (var pair in _ids)
                {
                    var tracked = new DotDiagnosisUnit { id = pair.Value,
                        front = pair.Key.currentCell != null && pair.Key.currentCell.xPos == GridManager.Instance.GetFrontColumn(pair.Key.IsEnemy) };
                    _row.units.Add(tracked);
                    Action<EventContext> died = _ =>
                    {
                        CaptureLive(); _row.deaths++;
                        if (tracked.firstDeathCycle < 0) tracked.firstDeathCycle = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed;
                        if (tracked.front && tracked.firstNormalCycle < 0 && !tracked.diedBeforeFirstNormal)
                        { tracked.diedBeforeFirstNormal = true; _row.frontlineDeathsBeforeFirstNormal++; }
                        if (_row.firstDeathCycle >= 0) return;
                        _row.firstDeathCycle = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed;
                        _row.firstDead = pair.Value.ToString();
                        _row.enemyHpFractionAtFirstDeath = EnemyFraction();
                    };
                    pair.Key.AddListener(UnitEventType.OnDeath, died); _deaths.Add((pair.Key, died));
                }
                ActionScheduler.AnyActionStarted += MechanismStarted;
                ActionScheduler.AnyActionFinished += MechanismFinished;
                Codes.Passive.AgniBurnToTheEnd.AnyTicket += Ticket;
                Unit.AnyDamageDealt += Damaged; Unit.AnyHealed += Healed; Unit.AnyShieldGranted += Shielded;
                ActionDot.NaturalTickResolved += Natural;
                DamageOverTimeDetonation.AnyDetonationHit += Immediate;
            }
            private LokapalaMechanismRow Mechanism(Unit unit)
            {
                if (unit == null || !_ids.TryGetValue(unit, out int id) || (id != 67 && id != 65 && id != 62)) return null;
                var row = _row.mechanisms.FirstOrDefault(x => x.id == id);
                if (row == null) { row = new LokapalaMechanismRow { id = id }; _row.mechanisms.Add(row); }
                return row;
            }
            private void MechanismStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                var tracked = Entry(unit);
                if (tracked != null && kind == ActionScheduler.ActionKind.Normal && tracked.firstNormalCycle < 0)
                    tracked.firstNormalCycle = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed;
                var row = Mechanism(unit); if (row == null || kind != ActionScheduler.ActionKind.Special) return;
                if (unit.ID == 67)
                {
                    var passive = unit.ActivePassiveCodes.OfType<Codes.Passive.SuryaExecution>().FirstOrDefault();
                    row.specialTargets += AliveEnemies().Count(x => passive?.Marks(x) > 0);
                    row.specialUnmarked += AliveEnemies().Count(x => passive?.Marks(x) == 0);
                }
                if (unit.ID == 65)
                {
                    if (PartyTreasury.Available >= 3) row.treasuryEnhanced++; else row.treasuryFallback++;
                    if (PartyTreasury.Available == 1) row.treasuryOne++;
                    if (PartyTreasury.Available == 2) row.treasuryTwo++;
                    row.goldMultiplierSum += Codes.Special.KuberaGoldRush.GoldMultiplier();
                }
            }
            private void MechanismFinished(Unit unit, ActionScheduler.ActionKind kind)
            {
                var row = Mechanism(unit); if (row == null) return;
                switch (kind)
                {
                    case ActionScheduler.ActionKind.Normal: row.normal++; break;
                    case ActionScheduler.ActionKind.Ultimate: row.ultimate++; break;
                    case ActionScheduler.ActionKind.Special: row.special++; break;
                    case ActionScheduler.ActionKind.Additional: row.additional++; break;
                }
            }
            private void Ticket(Unit unit, Unit other, Codes.Passive.AgniBurnToTheEnd.TicketKind kind)
            {
                var row = Mechanism(unit); if (row == null) return;
                switch (kind)
                {
                    case Codes.Passive.AgniBurnToTheEnd.TicketKind.Accepted: row.ticketAccepted++; break;
                    case Codes.Passive.AgniBurnToTheEnd.TicketKind.Rejected: row.ticketRejected++; break;
                    case Codes.Passive.AgniBurnToTheEnd.TicketKind.Held: row.ticketHeld++; break;
                    case Codes.Passive.AgniBurnToTheEnd.TicketKind.ReleasedFocused: row.ticketFocused++; break;
                    case Codes.Passive.AgniBurnToTheEnd.TicketKind.Fired: row.ticketFired++; break;
                }
            }
            public void SealEnemyBudget()
            { foreach (Unit enemy in _enemyHp.Keys.ToList()) _enemyHp[enemy] = enemy.HpMax; CaptureLive(); }
            public float EnemyFraction() => _enemyHp.Values.Sum() > 0 ? _enemyHp.Sum(p => p.Key != null && p.Key.HpCurr > 0 ?
                p.Value * p.Key.HpCurr / Mathf.Max(1, p.Key.HpMax) : 0) / _enemyHp.Values.Sum() : 0;
            private DotDiagnosisUnit Entry(Unit unit) => unit != null && _ids.TryGetValue(unit, out int id) ? _row.units.First(e => e.id == id) : null;
            private void Damaged(DamageResolvedContext hit)
            {
                CaptureLive();
                if (hit?.DamageContext == null || hit.DamageDealt <= 0) return;
                // 분담은 별도 대상의 실제 손실이며 원 대상의 ResolvedDamage에는 포함되지 않는다.
                var source = Entry(hit.Attacker); var target = Entry(hit.Target);
                if (source != null && hit.Target.IsEnemy)
                {
                    source.damage += hit.DamageDealt; _row.damage += hit.DamageDealt;
                    var mechanism = Mechanism(hit.Attacker);
                    if (mechanism != null && !hit.DamageContext.IsDamageOverTime)
                    {
                        var kind = GameManager.Instance.ActionScheduler.ActingKind;
                        if (kind == ActionScheduler.ActionKind.Normal) mechanism.normalDamage += hit.DamageDealt;
                        if (kind == ActionScheduler.ActionKind.Special) mechanism.specialDamage += hit.DamageDealt;
                        if (kind == ActionScheduler.ActionKind.Additional) mechanism.additionalDamage += hit.DamageDealt;
                        if (mechanism.id == 67 && kind == ActionScheduler.ActionKind.Normal)
                        {
                            if (hit.DamageContext.AppliesElement) mechanism.finaleHits++; else mechanism.preparationHits++;
                        }
                        if (mechanism.firstPayloadCycle < 0 && (kind == ActionScheduler.ActionKind.Additional || kind == ActionScheduler.ActionKind.Special || (mechanism.id == 67 && hit.DamageContext.AppliesElement)))
                            mechanism.firstPayloadCycle = GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed;
                    }
                    if (hit.DamageContext.IsDamageOverTime) { source.dotDamage += hit.DamageDealt; _row.dotDamage += hit.DamageDealt; }
                }
                if (target != null && hit.Attacker != null && hit.Attacker.IsEnemy)
                { target.incoming += hit.DamageDealt; _row.incoming += hit.DamageDealt;
                    _row.hpDamage += hit.DamageContext.HpDamage; _row.shieldsAbsorbed += Mathf.Max(0, hit.DamageDealt - hit.DamageContext.HpDamage); }
            }
            private void Shielded(Unit target, Unit source, int amount)
            { if (Entry(target) != null && source != null && !source.IsEnemy) _row.shieldsGranted += amount; }
            private void Healed(Unit target, Unit source, int effective, int overflow)
            {
                CaptureLive();
                var entry = Entry(target); if (entry == null) return;
                entry.healsReceived += effective; entry.overflow += overflow;
                if (source != null && _ids.GetValueOrDefault(source) == 167)
                {
                    _row.ereshEffective += effective; _row.ereshOverflow += overflow;
                    var scheduler = GameManager.Instance.ActionScheduler;
                    if (scheduler.ExecutingUnit == source && scheduler.ActingKind == ActionScheduler.ActionKind.Ultimate)
                    { _row.ereshUltimateEffective += effective; _row.ereshUltimateHeals++; }
                    else _row.ereshPassiveEffective += effective;
                }
                else _row.otherHealing += effective;
            }
            private void Natural(ActionDot.NaturalDotTickContext tick) { if (tick.Target.IsEnemy) _row.naturalTicks++; }
            private void Immediate(Unit trigger, Unit target, BaseEffect effect, DamageContext damage)
            { if (target.IsEnemy && damage.ResolvedDamage > 0) _row.immediateTicks++; }
            public void Dispose()
            {
                ActionScheduler.AnyActionStarted -= MechanismStarted;
                ActionScheduler.AnyActionFinished -= MechanismFinished;
                Codes.Passive.AgniBurnToTheEnd.AnyTicket -= Ticket;
                Unit.AnyDamageDealt -= Damaged; Unit.AnyHealed -= Healed; Unit.AnyShieldGranted -= Shielded;
                ActionDot.NaturalTickResolved -= Natural; DamageOverTimeDetonation.AnyDetonationHit -= Immediate;
                foreach (var item in _deaths) item.unit?.RemoveListener(UnitEventType.OnDeath, item.callback);
            }
        }
        private IEnumerator CampaignDotDiagnostic(string team, int attempt, int seed, string category)
        {
            var game = GameManager.Instance;
            string saved = JsonUtility.ToJson(RunManager.Instance.CaptureDebugSnapshot());
            File.WriteAllText(Path.Combine(_requestDirectory, $"snapshot_{team}_{attempt}_{category}.json"), saved);
            var spawnRandom = UnityEngine.Random.state;
            string signature = null;
            DotDiagnosisRow baseline = null;
            foreach (string profile in new[] { "base", "dot2", "heal2", "both", "guard", "base_repeat" })
            {
                game.DebugResetBattle(); UnityEngine.Random.state = spawnRandom;
                RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(saved));
                var row = new DotDiagnosisRow { team = team, attempt = attempt, seed = seed, category = category,
                    stage = game.RoundManager.Stage, profile = profile,
                    enemies = string.Join(",", AliveEnemies().Select(u => u.ID + ":" + u.HpMax + ":" + u.currentCell.xPos + ":" + u.currentCell.yPos)),
                    party = string.Join(";", AliveAllies().Select(u => u.ID + ":L" + u.Level + ":HP" + u.HpCurr + "/" + u.HpMax + ":ATK" + u.AtkCurr +
                        ":SP" + u.SpAtkCurr + ":DEF" + u.DefCurr + ":SDEF" + u.SpDefCurr + ":SPD" + u.SpeedCurr + ":MP" + u.ManaCurr +
                        ":CR" + u.StatValue(StatType.CritRate) + ":CD" + u.StatValue(StatType.CritDamage) + ":C" + string.Join("/", u.RewardCodeSlots))) };
                string current = row.enemies + "/" + row.party;
                signature ??= current; row.matched = signature == current;
                using (var recorder = new DotDiagnosisRecorder(row))
                {
                    // 복원 중 생성/연출이 소비한 난수와 전투 추첨을 분리한다.
                    UnityEngine.Random.InitState(seed + row.stage * 10000);
                    game.StartRound();
                    recorder.SealEnemyBudget();
                    foreach (Unit ally in AliveAllies())
                    {
                        if (profile is "dot2" or "both")
                            YvAddTestStatus(ally, 99894, "diagnose_dot2", new ConditionalOutgoingDamageMultiplierEffect(d => d.IsDamageOverTime, 2f), StatusCategory.Neutral);
                        if (ally.ID == 167 && (profile is "heal2" or "both"))
                            YvAddTestStatus(ally, 99895, "diagnose_heal2", new DiagnosisHealing(), StatusCategory.Neutral);
                        if (ally.ID == 167 && profile == "guard")
                            YvAddTestStatus(ally, 99896, "diagnose_guard", new ReceivingDamageMultiplierEffect(.5f), StatusCategory.Neutral);
                    }
                    float start = Time.realtimeSinceStartup;
                    while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start < 120 && game.ActionScheduler.ReferenceCyclesElapsed < 25)
                        yield return null;
                    row.cycles = game.ActionScheduler.ReferenceCyclesElapsed;
                    row.timedOut = game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup - start >= 120;
                    row.censored = game.gameState == GameState.RoundInProgress && !row.timedOut;
                    row.won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                    row.survivors = recorder.EndSurvivors; row.enemyHpFractionEnd = recorder.EndEnemyFraction;
                }
                if (profile == "base") baseline = row;
                if (profile == "base_repeat")
                    row.matched &= baseline != null && row.won == baseline.won && Near(row.cycles, baseline.cycles) &&
                        row.damage == baseline.damage && row.incoming == baseline.incoming && row.ereshEffective == baseline.ereshEffective &&
                        row.ereshOverflow == baseline.ereshOverflow && Near(row.firstDeathCycle, baseline.firstDeathCycle) &&
                        row.survivors == baseline.survivors && Near(row.enemyHpFractionEnd, baseline.enemyHpFractionEnd);
                CampaignWrite("diagnostics", row);
                YvCheck("지딜 원인 대조 " + team + "/" + attempt + "/" + category + "/" + profile,
                    "동일 준비 상태·측정 완료", JsonUtility.ToJson(row), row.matched && !row.timedOut);
            }
            game.DebugResetBattle(); UnityEngine.Random.state = spawnRandom;
            RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(saved));
        }
    }
}
#endif
