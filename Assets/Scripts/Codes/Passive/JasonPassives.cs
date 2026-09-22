using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Special;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>이아손 고유 P — 아군 궁극기 하나에 한 번, 아르고의 지원을 추가행동으로 예약한다.</summary>
    public sealed class JasonArgonautResponse : UniquePassiveCode
    {
        private Action<Unit> _ultimateHandler;
        private Action<EventContext> _turnHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _ready;
        private bool _registered;

        public JasonArgonautResponse(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "아르고의 응답";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _ready = true;
            _ultimateHandler = OnAllyUltimate;
            _turnHandler = _ => _ready = true;
            _cleanupHandler = _ => StopCode();
            Unit.AnyActiveUltimateActivated += _ultimateHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnAllyUltimate(Unit user)
        {
            if (!_ready || Caster == null || !Caster.isActive || !Caster.IsOnField ||
                user == null || !user.isActive || !user.IsOnField || user.IsEnemy != Caster.IsEnemy)
                return;

            JasonArgonautSupport support = Caster.ActiveSpecialCode as JasonArgonautSupport;
            if (support == null || !support.HasValidTarget()) return;

            _ready = false;
            bool queued = GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "jason_argonaut_support", support.CodeName, Caster.CastSpecialCode) == true;
            if (!queued) _ready = true;
        }

        public override void StopCode()
        {
            if (Caster != null && _registered)
            {
                Unit.AnyActiveUltimateActivated -= _ultimateHandler;
                Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }

            _registered = false;
            _ready = false;
        }
    }

    /// <summary>가정 신의 가호 — 카피바라의 금색 상위, 우정도 획득 +50%.</summary>
    public sealed class JasonHouseholdBlessing : PassiveCode
    {
        public const float BondBonus = 0.50f;

        public JasonHouseholdBlessing(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "가정 신의 가호";
            IgnoresActivationChance = true;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }
    }

    /// <summary>포수 — 보유자마다 모든 아군의 궁극기 피해를 10%씩 올린다.</summary>
    public sealed class JasonGunner : PassiveCode
    {
        public const float UltimateDamageBonus = 0.10f;

        public JasonGunner(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "포수";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            string sourceKey = Caster.GetEntityId().ToString();
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    5362, $"jason_gunner_{sourceKey}", CodeName, Caster, ally,
                    new JasonGunnerEffect(UltimateDamageBonus),
                    stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                    isBeneficial: true,
                    description: "포수 한 명당 궁극기 피해가 10% 증가합니다."));
            }
        }
    }

    internal sealed class JasonGunnerEffect : BaseEffect
    {
        private readonly float _bonus;
        public JasonGunnerEffect(float bonus) : base(0, bonus) => _bonus = bonus;
        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || Caster == null || !Caster.isActive || !Caster.IsOnField || context == null)
                return 1f;
            bool ultimate = context.CodeType == BaseEnums.CodeType.Ultimate ||
                            context.DamageTags?.Contains(DamageTag.UltAttack) == true;
            return ultimate ? 1f + _bonus : 1f;
        }
    }
}
