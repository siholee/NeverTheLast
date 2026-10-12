using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>
    /// 사바흐 — 공격 대상의 해로운 상태만큼 피해가 증가하고, 아군이 만든 빈틈으로 아즈라엘을 채운다.
    /// 옛 고유 특성의 두 충전 경로(부여·갱신 / 지속피해 틱)를 그대로 유지한다.
    /// </summary>
    public sealed class SabahDebuffHunter : UniquePassiveCode
    {
        public const float DamagePerDebuff = 0.04f;
        private const int StatusId = 70030;
        private const string StatusKey = "sabah_debuff_hunter";

        private Action<Unit, Unit, Entities.Status.UnitStatus> _negativeStatusHandler;
        private Action<DamageResolvedContext> _damageOverTimeHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public SabahDebuffHunter(PassiveCodeContext context) : base(context) => CodeName = "빈틈 포착";

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster, new SabahDebuffDamageEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"공격 대상의 디버프 1개당 가하는 피해가 {DamagePerDebuff:P0} 증가합니다."));
            ActionAnnouncer.AnnouncePassive(Caster, this);

            if (_registered) return;
            _negativeStatusHandler = OnNegativeStatusGranted;
            _damageOverTimeHandler = OnDamageOverTimeTick;
            _cleanupHandler = _ => StopCode();
            Unit.AnyNegativeStatusGranted += _negativeStatusHandler;
            Unit.AnyDamageDealt += _damageOverTimeHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnNegativeStatusGranted(Unit source, Unit target, Entities.Status.UnitStatus status)
        {
            if (Caster == null || !Caster.isActive || source == null || target == null) return;
            if (source.IsEnemy != Caster.IsEnemy || target.IsEnemy == Caster.IsEnemy) return;
            GainAzrael();
        }

        private void OnDamageOverTimeTick(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || context?.DamageContext == null) return;
            if (context.DamageDealt <= 0 || !context.DamageContext.IsDamageOverTime) return;
            Unit source = context.Attacker;
            Unit target = context.Target;
            if (source == null || target == null) return;
            if (source.IsEnemy != Caster.IsEnemy || target.IsEnemy == Caster.IsEnemy) return;
            if (!target.HasDamageOverTimeStatus()) return;
            GainAzrael();
        }

        private void GainAzrael()
        {
            if (Caster == null) return;
            int before = Caster.ManaCurr;
            Caster.AddUltimateResource(1);
            if (Caster.ManaCurr > before)
            {
                AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Resolve, CodeName);
                ActionAnnouncer.AnnouncePassive(Caster, this);
            }
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyNegativeStatusGranted -= _negativeStatusHandler;
            Unit.AnyDamageDealt -= _damageOverTimeHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _registered = false;
        }
    }

    internal sealed class SabahDebuffDamageEffect : BaseEffect
    {
        public SabahDebuffDamageEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            int debuffs = target.GetAllStatuses().Count(status => status.CountsAsDebuff);
            return 1f + debuffs * SabahDebuffHunter.DamagePerDebuff;
        }
    }
}
