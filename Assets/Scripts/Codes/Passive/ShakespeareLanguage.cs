using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>생성·소멸·실제 행동으로 언어를 벌고, 공격 묶음 하나에 2를 소비한다.</summary>
    public sealed class ShakespeareLanguage : UniquePassiveCode
    {
        public const string Resource = "shakespeare_language";
        public const int Maximum = 12, Cost = 2;
        private static readonly List<ShakespeareLanguage> Sources = new();
        private readonly Dictionary<Unit, (int action, bool empowered, float converted)> _attacks = new();
        private readonly HashSet<Unit> _revived = new();
        private readonly HashSet<Unit> _removed = new();
        private bool _working;
        private System.Action<EventContext> _death;
        public bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public int Language => Caster?.GetCombatResource(Resource) ?? 0;
        public ShakespeareLanguage(PassiveCodeContext context) : base(context)
        {
            CodeName = "언어의 창조자";
            CombatResourceLabels.Register(Resource, "언어");
        }
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            _attacks.Clear(); _revived.Clear(); _removed.Clear();
            Caster.SetCombatResourceMaximum(Resource, Maximum, true);
            Caster.AddCombatResource(Resource, 2);
            Sources.Add(this);
            Summons.AnySummonCreated += Created;
            Unit.AnyUnitDied += Died;
            ActionScheduler.AnyActionFinished += Finished;
            _death = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _death);
        }
        public void Gain(int amount)
        {
            if (!Working || amount <= 0) return;
            int before = Language;
            Caster.AddCombatResource(Resource, amount);
            if (Language > before) ActionAnnouncer.AnnouncePassive(Caster, this);
        }
        private void Created(Unit owner, Unit summon, SummonSource source, object tag)
        { if (Working && owner != null && owner.IsEnemy == Caster.IsEnemy) Gain(1); }
        private void Died(Unit unit, Unit attacker)
        {
            if (Working && unit != null && unit.IsSummon && unit.IsEnemy == Caster.IsEnemy && _removed.Add(unit)) Gain(1);
        }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            // 공격 후 사라진 분신도 실제로 끝낸 행동을 센다.
            if (Working && actor != null && actor.IsEnemy == Caster.IsEnemy &&
                (actor.IsSummon || actor.CountsAsSummon)) Gain(1);
            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1;
            foreach (Unit unit in _attacks.Where(x => x.Value.action == action).Select(x => x.Key).ToArray()) _attacks.Remove(unit);
        }
        /// <summary>같은 행동자·행동ID는 다단·광역이어도 한 번 소비. 지휘 중 각 분신도 별도 공격자로 식별한다.</summary>
        public static bool TryEmpower(Unit statOwner, Unit actor, float chance, out float converted)
        {
            converted = 0;
            if (statOwner == null || actor == null || statOwner.IsEnemy != actor.IsEnemy || !actor.isActive || !actor.IsOnField ||
                (!actor.IsSummon && !actor.CountsAsSummon)) return false;
            var source = Sources.FirstOrDefault(x => x.Working && x.Caster.IsEnemy == actor.IsEnemy);
            if (source == null) return false;
            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1;
            if (!source._attacks.TryGetValue(actor, out var entry) || entry.action != action)
            {
                bool paid = source.Caster.TryConsumeCombatResource(Resource, Cost);
                entry = (action, paid, paid ? Mathf.Clamp01(chance) : 0);
                source._attacks[actor] = entry;
                if (paid) ActionAnnouncer.AnnouncePassive(source.Caster, source);
            }
            converted = entry.converted;
            return entry.empowered;
        }
        public bool TrySave(Unit unit)
            => Working && unit != null && unit.IsSummon && unit.IsEnemy == Caster.IsEnemy && _revived.Add(unit);
        public override void StopCode()
        {
            _working = false;
            Sources.Remove(this);
            Summons.AnySummonCreated -= Created;
            Unit.AnyUnitDied -= Died;
            ActionScheduler.AnyActionFinished -= Finished;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _death);
                Caster.RemoveStatusByKey(Ultimate.ShakespeareQuestion.FieldKey);
                Caster.AddCombatResource(Resource, -Language);
            }
            _attacks.Clear(); _revived.Clear(); _removed.Clear();
        }
    }
}
