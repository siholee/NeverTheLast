using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public sealed class SurtrLastEmber : UniquePassiveCode
    {
        public const float ShieldRatio = .12f;
        private bool _working;
        private int _lastAction = int.MinValue;
        private Action<DamageResolvedContext> _hit;
        public SurtrLastEmber(PassiveCodeContext context) : base(context) => CodeName = "재 속에 남긴 온기";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            _lastAction = int.MinValue;
            _hit = Hit;
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _hit);
        }
        private void Hit(DamageResolvedContext context)
        {
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (!_working || Caster == null || !Caster.isActive || !Caster.IsOnField || scheduler?.ExecutingUnit == null ||
                context == null || context.Target == null || context.Target.IsEnemy == Caster.IsEnemy ||
                context.DamageDealt <= 0 || !DirectAttack.IsDirect(context.DamageContext)) return;
            int action = scheduler.CurrentActionId;
            if (_lastAction == action) return;
            _lastAction = action;
            int amount = Mathf.Max(1, Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Defense) * ShieldRatio));
            var grid = GridManager.Instance;
            if (grid == null) return;
            foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == Caster.IsEnemy))
                ally.AddShield(amount, Caster);
        }
        public override void StopCode()
        {
            if (_hit != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _hit);
            _hit = null;
            _working = false;
        }
    }
}
