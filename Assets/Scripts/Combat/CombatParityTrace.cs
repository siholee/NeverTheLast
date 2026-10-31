using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BaseClasses;
using Entities;
using Managers;

namespace NeverTheLast.Simulation
{
    /// <summary>Unity와 콘솔에서 같은 사건·집계·단위 식별 규칙을 쓰는 읽기 전용 관측기.</summary>
    public sealed class CombatParityTrace : IDisposable
    {
        private readonly Dictionary<Unit, UnitResult> _records = new();
        private readonly StringBuilder _events = new();
        private readonly BattleResult _result;
        public readonly List<string> Events = new();
        private readonly bool _captureEvents;
        private bool _finished;

        public CombatParityTrace(BattleResult result, bool captureEvents = false)
        {
            _result = result;
            _captureEvents = captureEvents;
            Unit.AnyDamageDealt += Damage;
            Unit.AnyHealed += Heal;
            Unit.AnyShieldGranted += Shield;
            ActionScheduler.AnyActionStarted += ActionStarted;
        }

        public void Register(Unit unit)
        {
            if (unit == null || unit.IsSummon || _records.ContainsKey(unit)) return;
            _records.Add(unit, new UnitResult { Id = unit.ID, Name = unit.UnitName, Enemy = unit.IsEnemy,
                Column = Math.Abs(unit.currentCell?.xPos ?? 0), Row = unit.currentCell?.yPos ?? 0 });
        }

        public void SyncRoster()
        {
            var grid = GridManager.Instance;
            if (grid == null) return;
            foreach (Unit unit in grid.heroList.Concat(grid.enemyList)) Register(unit);
        }

        private string Key(Unit unit)
        {
            if (unit == null) return "null";
            Register(unit);
            return $"{(_records.TryGetValue(unit, out var r) ? r.Id : unit.ID)}:{unit.IsEnemy}:{unit.currentCell?.xPos}:{unit.currentCell?.yPos}:{unit.SummonKind}";
        }

        private void Stamp(string value)
        {
            if (_finished) return;
            _events.Append(value).Append('\n');
            if (_captureEvents) Events.Add(value + "/rng=" + Combat.CombatRandom.SeedState);
        }

        private void Damage(DamageResolvedContext context)
        {
            Stamp($"damage/{Key(context.Attacker)}/{Key(context.Target)}/{context.DamageDealt}/{context.Target?.HpCurr}/{context.Target?.ShieldCurr}");
            if (context.Attacker != null && _records.TryGetValue(context.Attacker, out var entry)) entry.Damage += context.DamageDealt;
        }
        private void Heal(Unit target, Unit source, int effective, int overflow)
        {
            if (source != null && _records.TryGetValue(source, out var entry)) entry.Healing += effective;
        }
        private void Shield(Unit target, Unit source, int amount)
        {
            if (source != null && _records.TryGetValue(source, out var entry)) entry.Shielding += amount;
        }
        private void ActionStarted(Unit actor, ActionScheduler.ActionKind kind, string key)
        {
            string name = kind.ToString();
            _result.ActionsByKind[name] = _result.ActionsByKind.TryGetValue(name, out int count) ? count + 1 : 1;
            Stamp($"action/{Key(actor)}/{name}/{key}");
        }

        public void Capture()
        {
            SyncRoster();
            var scheduler = GameManager.Instance.ActionScheduler;
            _result.Actions = scheduler.ActionCount;
            _result.Turns = scheduler.TurnsTaken;
            _result.ActionValue = scheduler.CombatActionValueElapsed;
            using (var sha = SHA256.Create())
                _result.EventDigest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(_events.ToString()))).Replace("-", "");
            _result.Units.Clear();
            foreach (var pair in _records)
            {
                Unit unit = pair.Key;
                UnitResult record = pair.Value;
                record.Hp = unit.isActive ? unit.HpCurr : 0;
                record.MaxHp = unit.HpMax;
                record.Turns = unit.TurnCount;
                _result.Units.Add(record);
            }
            _finished = true;
        }

        public void Dispose()
        {
            Unit.AnyDamageDealt -= Damage;
            Unit.AnyHealed -= Heal;
            Unit.AnyShieldGranted -= Shield;
            ActionScheduler.AnyActionStarted -= ActionStarted;
        }
    }
}
