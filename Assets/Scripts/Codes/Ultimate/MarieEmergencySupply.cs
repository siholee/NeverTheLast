using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>마나를 소비하지 않는 상시형 초월기. 예약 시 중첩을 선점하고 취소 시 반환한다.</summary>
    public sealed class MarieEmergencySupply : UltimateCode
    {
        public const string Resource = "marie_emergency";
        public override bool IsAutoCast => false;
        public override bool IsPersistent => true;
        private bool _working;
        private readonly HashSet<Unit> _pending = new();
        private System.Action<EventContext> _normal;
        private int _serial;
        public MarieEmergencySupply(UltimateCodeContext context) : base(context)
        { CodeName = "돌아올 자리는 남겨둘게"; Combat.CombatResourceLabels.Register(Resource, "긴급지원"); }
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            _pending.Clear();
            Caster.SetCombatResourceMaximum(Resource, 3, resetCurrent: true);
            Caster.AddCombatResource(Resource, 1);
            _normal = _ => Caster.AddCombatResource(Resource, 1);
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            Unit.AnyDamageDealt += Damaged;
        }
        private bool Eligible(Unit target) => Caster != null && Caster.isActive && Caster.IsOnField && target != null &&
            target != Caster && target.isActive && target.IsOnField && target.IsEnemy == Caster.IsEnemy &&
            target.HpCurr > 0 && target.HpCurr <= target.HpMax * .30f;
        private void Damaged(DamageResolvedContext context)
        {
            Unit target = context?.Target;
            if (!_working || context == null || context.DamageContext.HpDamage <= 0 || !Eligible(target) || _pending.Contains(target)) return;
            var scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null || !Caster.TryConsumeCombatResource(Resource, 1)) return;
            _pending.Add(target);
            bool available = true;
            void Refund()
            {
                if (!available) return;
                available = false;
                _pending.Remove(target);
                if (_working && Caster != null && Caster.isActive) Caster.AddCombatResource(Resource, 1);
            }
            if (!scheduler.EnqueuePriorityAdditional(Caster, $"marie_support_{++_serial}", CodeName, () =>
            {
                if (!Eligible(target)) { Refund(); return; }
                available = false;
                _pending.Remove(target);
                target.Heal(Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * 2.80f), Caster);
                AudioManager.PlayCue("marie.emergency.cast");
                Effects.Vfx.VfxComposer.PlayAffectedSlot("marie.emergency", 0, Caster, target);
            }, () => { if (Eligible(target) && !Caster.isControlled) return true; Refund(); return false; }, Refund)) Refund();
        }
        public override void StopCode()
        {
            _working = false;
            Unit.AnyDamageDealt -= Damaged;
            if (_normal != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            _pending.Clear();
            if (Caster != null) Caster.AddCombatResource(Resource, -Caster.GetCombatResource(Resource));
        }
    }
}
