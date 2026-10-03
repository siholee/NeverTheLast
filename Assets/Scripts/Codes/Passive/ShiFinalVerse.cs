using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Combat;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>
    /// 시 — 특성 「멸망의 시」.
    /// 다른 아군의 공격 직후 멸참으로 끝낼 수 있는 적이 있으면 특수행동을 예약한다.
    /// 멸참 처치 재행동도 같은 특수행동 슬롯을 쓰되, 재행동은 깊이 1에서 반드시 끝낸다.
    /// </summary>
    public sealed class ShiFinalVerse : UniquePassiveCode
    {
        private const string ExecuteKey = "shi_song_of_ruin_execute";
        private const string RepriseKey = "shi_destruction_slash_reprise";

        private ShiGhostSlash _destructionSlash;
        private bool _subscribed;
        private bool _executeQueued;
        private bool _repriseQueued;

        public ShiFinalVerse(PassiveCodeContext context) : base(context)
        {
            CodeName = "멸망의 시";
        }

        public override void CastCode()
        {
            if (Caster == null || _subscribed) return;
            _destructionSlash = Caster.ActiveNormalCode as ShiGhostSlash;
            if (_destructionSlash == null) return;

            Unit.AnyDamageDealt += OnAnyDamageDealt;
            _destructionSlash.SlashResolved += OnSlashResolved;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (!_subscribed) return;
            Unit.AnyDamageDealt -= OnAnyDamageDealt;
            if (_destructionSlash != null) _destructionSlash.SlashResolved -= OnSlashResolved;
            _executeQueued = false;
            _repriseQueued = false;
            _subscribed = false;
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || context?.Attacker == null ||
                context.Attacker == Caster || context.Attacker.IsEnemy != Caster.IsEnemy ||
                context.Target == null || context.Target.IsEnemy == Caster.IsEnemy ||
                !IsAttack(context.DamageContext)) return;

            RequestExecute();
        }

        private static bool IsAttack(DamageContext context)
            => context != null && (context.HasTag(DamageTag.NormalAttack) ||
                context.HasTag(DamageTag.UltAttack) || context.HasTag(DamageTag.SpecialAttack) ||
                context.HasTag(DamageTag.AdditionalAttack) || context.HasTag(DamageTag.CounterAttack) ||
                context.HasTag(DamageTag.PassiveAttack));

        private void RequestExecute()
        {
            if (_executeQueued || Caster == null || !Caster.isActive || _destructionSlash == null) return;
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null || FindExecutableTarget() == null) return;

            _executeQueued = scheduler.EnqueueSpecial(Caster, ExecuteKey, "멸참", Execute,
                () => Caster != null && Caster.isActive && FindExecutableTarget() != null);
        }

        private void Execute()
        {
            _executeQueued = false;
            Unit target = FindExecutableTarget();
            if (target == null || _destructionSlash == null) return;

            ActionAnnouncer.AnnouncePassive(Caster, this);
            _destructionSlash.CastSpecial(target, ShiGhostSlash.SpecialSource.Trait);
        }

        private void OnSlashResolved(Unit target, ShiGhostSlash.SpecialSource source, bool killed)
        {
            if (!killed || source == ShiGhostSlash.SpecialSource.Reprise || _repriseQueued ||
                Caster == null || !Caster.isActive) return;

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;
            _repriseQueued = scheduler.EnqueueSpecial(Caster, RepriseKey, "멸참", Reprise,
                () => Caster != null && Caster.isActive && FindRepriseTarget() != null);
        }

        private void Reprise()
        {
            _repriseQueued = false;
            Unit target = FindRepriseTarget();
            if (target == null || _destructionSlash == null) return;
            _destructionSlash.CastSpecial(target, ShiGhostSlash.SpecialSource.Reprise);
        }

        private Unit FindExecutableTarget()
            => Enemies().Where(unit => _destructionSlash.CanExecuteFromStats(unit))
                .OrderBy(unit => unit.HpCurr)
                .ThenBy(unit => unit.StatValue(BaseEnums.StatType.Defense))
                .FirstOrDefault();

        private Unit FindRepriseTarget()
            => Enemies().OrderBy(unit => unit.HpCurr)
                .ThenBy(unit => unit.StatValue(BaseEnums.StatType.Defense))
                .FirstOrDefault();

        private System.Collections.Generic.IEnumerable<Unit> Enemies()
        {
            if (GridManager.Instance == null || Caster == null)
                return System.Array.Empty<Unit>();
            var pool = Caster.IsEnemy ? GridManager.Instance.heroList : GridManager.Instance.enemyList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField &&
                                      !unit.IsUntargetable && unit.HpCurr > 0);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool DebugRequestExecute()
        {
            RequestExecute();
            return _executeQueued;
        }

        internal bool DebugHasExecuteQueued => _executeQueued;
        internal bool DebugHasRepriseQueued => _repriseQueued;
#endif
    }
}
