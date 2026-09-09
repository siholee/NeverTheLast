using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 수르트 고유 P — 적을 공격해 피해를 해결할 때마다 공격 대상 하나당
    /// 아군 전체에게 50 + STR×0.5의 방어막을 부여한다.
    /// </summary>
    public sealed class SurtrTwilight : UniquePassiveCode
    {
        private const float ShieldFlat = 50f;
        private const float ShieldStrCoefficient = 0.5f;

        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public SurtrTwilight(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "황혼";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (Caster == null || !_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (Caster == null || context?.Attacker != Caster || context.DamageDealt <= 0 ||
                context.Target == null || context.DamageContext == null ||
                context.DamageContext.CodeType == BaseEnums.CodeType.Effect) return;

            int shield = Mathf.Max(1, Mathf.RoundToInt(ShieldFlat + Caster.GetBaseStr() * ShieldStrCoefficient));
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddShield(shield, Caster);
            }
        }
    }

    public abstract class SurtrStatusPassive : PassiveCode
    {
        private readonly int _statusId;
        private readonly string _key;
        private readonly string _description;
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        protected SurtrStatusPassive(PassiveCodeContext context, int statusId, string key, string name, string description)
            : base(context)
        {
            _statusId = statusId;
            _key = key;
            _description = description;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
        }

        protected abstract BaseEffect CreateEffect();

        public override void CastCode()
        {
            if (_registered) return;
            Caster.AddStatus(BuffStatus.Create(_statusId, _key, CodeName, Caster, Caster, CreateEffect(), description: _description));
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey(_key);
            _registered = false;
        }
    }

    internal sealed class SurtrFireMasteryEffect : BaseEffect
    {
        public SurtrFireMasteryEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Caster && target != null && target.HasCombatElement(BaseEnums.UnitElement.Pyro) ? 1.1f : 1f;
    }

    public sealed class SurtrCelestialBody : SurtrStatusPassive
    {
        public SurtrCelestialBody(PassiveCodeContext context) : base(
            context, 135, "surtr_celestial_body", "천상의 신체", "받는 치유량과 보호막량이 25% 증가합니다.") { }
        protected override BaseEffect CreateEffect() => new SurtrCelestialBodyEffect();
    }

    internal sealed class SurtrCelestialBodyEffect : BaseEffect
    {
        public SurtrCelestialBodyEffect() : base(0) { }
        public override float HealingReceivedMultiplierModifier(Unit unit) => unit == Target ? 1.25f : 1f;
        public override float ShieldReceivedMultiplierModifier(Unit unit) => unit == Target ? 1.25f : 1f;
    }

    /// <summary>
    /// 투사(41)와 그 금색 상위 코드 '하루종일도 할 수 있어'(1524) — 가한 피해의 일부를 회복한다.
    ///
    /// 회복 비율만 다른 같은 코드라 한 클래스로 묶었다. 둘을 같이 배우면
    /// <see cref="PassiveCode.SupersededByCodeId"/>가 은색 쪽을 재워 비율이 겹쳐 쌓이지 않는다.
    /// </summary>
    /// <summary>
    /// 투사(41) 계열 — 가한 피해의 일정 비율을 회복한다. 조건이 없는 순수 흡혈이다.
    ///
    /// 수르트 전용으로 만들었다가 테세우스·세트·공허의 멧돼지가 같은 것을 필요로 해
    /// <b>회복 비율만 다른 범용 코드</b>가 됐다. 예전에는 테세우스만 쓰는
    /// '물리·접촉 25% 회복'짜리 별도 클래스가 같은 '투사' 이름을 달고 따로 있었다.
    /// </summary>
    public sealed class LifestealFighter : PassiveCode
    {
        private readonly float _healRatio;
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public LifestealFighter(PassiveCodeContext context, float healRatio, string name,
            int supersededByCodeId, BaseEnums.CodeGrade grade) : base(context)
        {
            _healRatio = healRatio;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
            SupersededByCodeId = supersededByCodeId;
            Grade = grade;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _damageHandler = context =>
            {
                if (context?.Attacker == Caster && context.DamageDealt > 0)
                {
                    Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(context.DamageDealt * _healRatio), Caster);
                }
            };
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }
    }

    public sealed class SurtrClutchPlayer : SurtrStatusPassive
    {
        public SurtrClutchPlayer(PassiveCodeContext context) : base(
            context, 137, "surtr_clutch_player", "클러치 플레이어", "잃은 체력 비율의 50%만큼 주는 피해가 증가합니다.") { }
        protected override BaseEffect CreateEffect() => new SurtrClutchPlayerEffect();
    }

    internal sealed class SurtrClutchPlayerEffect : BaseEffect
    {
        public SurtrClutchPlayerEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Caster || attacker.HpMax <= 0) return 1f;
            float missingRatio = 1f - Mathf.Clamp01((float)attacker.HpCurr / attacker.HpMax);
            return 1f + missingRatio * 0.5f;
        }
    }

    public sealed class SurtrGodslayer : SurtrStatusPassive
    {
        public SurtrGodslayer(PassiveCodeContext context) : base(
            context, 138, "surtr_godslayer", "신살자", "최대 체력이 자신의 150% 이상인 적에게 주는 피해가 25% 증가합니다.") { }
        protected override BaseEffect CreateEffect() => new SurtrGodslayerEffect();
    }

    internal sealed class SurtrGodslayerEffect : BaseEffect
    {
        public SurtrGodslayerEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Caster && target != null && target.HpMax >= attacker.HpMax * 1.5f ? 1.25f : 1f;
    }
}
