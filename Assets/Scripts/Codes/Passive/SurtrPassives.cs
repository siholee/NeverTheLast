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
    /// 수르트 고유 P — 황혼.
    ///
    /// 공격 행동을 시작할 때 최대 체력의 20%를 태우고 그 행동이 주는 피해를 40% 올린다.
    /// 체력이 모자라 태우지 못하면 증가도 없다 — 치유가 곧 화력 재장전이 되게 하는 장치다.
    ///
    /// 값은 <b>행동마다 한 번</b>만 치른다. 피해 판정마다 태우면 라그나로크처럼
    /// 적 전체를 때리는 행동이 인원수만큼 비싸져 적이 많을수록 못 쓰게 된다.
    /// </summary>
    public sealed class SurtrTwilight : UniquePassiveCode
    {
        /// <summary>행동 한 번에 태우는 최대 체력 비율.</summary>
        private const float HpCostRatio = 0.2f;

        private const int StatusId = 136;
        private const string StatusKey = "surtr_twilight";

        private SurtrTwilightEffect _effect;
        private bool _registered;
        private Action<EventContext> _normalHandler;
        private Action<EventContext> _ultimateHandler;
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

            _effect = new SurtrTwilightEffect();
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster, _effect,
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "공격할 때 최대 체력의 20%를 소모하고 그 행동이 주는 피해가 40% 증가합니다."));

            _normalHandler = _ => Pay(BaseEnums.CodeType.Normal);
            _ultimateHandler = _ => Pay(BaseEnums.CodeType.Ultimate);
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActivates, _normalHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        /// <summary>
        /// 행동이 열리는 순간 값을 치른다. 체력이 최대 체력의 20%를 넘지 않으면
        /// <see cref="Unit.TryConsumeAttackHp"/>가 false를 돌려주고 그 행동은 맨몸으로 나간다.
        /// </summary>
        private void Pay(BaseEnums.CodeType codeType)
        {
            if (Caster == null || _effect == null) return;
            _effect.EmpoweredCodeType =
                Caster.TryConsumeAttackHp(HpCostRatio, false, out _) ? codeType : (BaseEnums.CodeType?)null;
        }

        public override void StopCode()
        {
            if (Caster == null || !_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActivates, _normalHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey(StatusKey);
            _effect = null;
            _registered = false;
        }
    }

    /// <summary>
    /// 황혼의 피해 증가. 값을 치른 행동에만 붙어야 하므로 <b>어떤 종류의 행동이 냈는가</b>를 들고 있는다.
    /// 반격·지속피해처럼 행동이 아닌 피해는 종류가 달라 저절로 걸러진다.
    /// </summary>
    internal sealed class SurtrTwilightEffect : BaseEffect
    {
        private const float Multiplier = 1.4f;

        public BaseEnums.CodeType? EmpoweredCodeType;

        public SurtrTwilightEffect() : base(0, Multiplier) { }

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Caster && context != null && EmpoweredCodeType == context.CodeType ? Multiplier : 1f;
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
        protected override BaseEffect CreateEffect() => new ReceivedSupportMultiplierEffect(1.25f);
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
            context, 137, "surtr_clutch_player", "클러치 플레이어", "잃은 체력 비율의 50%만큼 주는 피해가 증가합니다.")
            => SupersededByCodeId = NordEnemyCodeIds.LastGasp;
        protected override BaseEffect CreateEffect() => new MissingHpDamageEffect(0.5f);
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
