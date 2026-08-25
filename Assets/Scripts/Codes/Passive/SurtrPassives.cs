using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class SurtrIds
    {
        public const int Unit = 24;
        public const int TwilightControllerStatus = 130;
        public const int TwilightStatus = 131;
        public const string TwilightControllerKey = "surtr_twilight_controller";
        public const string TwilightStatusKey = "surtr_twilight";
    }

    /// <summary>
    /// 수르트의 고유 패시브 황혼과 궁극기 라그나로크의 스택을 함께 관리한다.
    /// 치명 피해를 전투당 한 번 막고 2초 경직 후 완전 회복·황혼 상태에 진입한다.
    /// </summary>
    public sealed class SurtrTwilight : UniquePassiveCode
    {
        private const float RagnarokStackDuration = 5f;

        private bool _registered;
        private float _stackRemaining;
        private DamageContext _lastStackContext;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _updateHandler;
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
            if (_registered) return;

            Caster.AddUltimateResource(-Caster.ManaCurr);
            Caster.AddStatus(BuffStatus.Create(
                SurtrIds.TwilightControllerStatus,
                SurtrIds.TwilightControllerKey,
                "황혼",
                Caster,
                Caster,
                new SurtrTwilightControllerEffect(),
                category: BaseEnums.StatusCategory.Neutral,
                description: "치명 피해를 한 번 막고 2초 경직 후 완전히 회복하여 황혼에 진입합니다. 라그나로크 스택마다 방어력 20%를 무시합니다."));

            _damageHandler = OnDamageDealt;
            _updateHandler = OnUpdate;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _updateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUpdate, _updateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey(SurtrIds.TwilightControllerKey);
            Caster.RemoveStatusByKey(SurtrIds.TwilightStatusKey);
            _stackRemaining = 0f;
            _lastStackContext = null;
            _registered = false;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (context?.Attacker != Caster || context.DamageDealt <= 0 || context.Target == null ||
                context.DamageContext == null || context.DamageContext.CodeType == BaseEnums.CodeType.Effect ||
                !context.Target.HasCombatElement(BaseEnums.UnitElement.Pyro) ||
                ReferenceEquals(_lastStackContext, context.DamageContext))
            {
                return;
            }

            _lastStackContext = context.DamageContext;
            Caster.AddUltimateResource(1);
            _stackRemaining = RagnarokStackDuration;
        }

        private void OnUpdate(EventContext context)
        {
            if (Caster.HasStatusKey(SurtrIds.TwilightStatusKey))
            {
                Caster.FillUltimateResource(false);
                return;
            }

            if (Caster.ManaCurr <= 0) return;
            _stackRemaining -= context.FloatParam;
            if (_stackRemaining > 0f) return;
            Caster.AddUltimateResource(-Caster.ManaCurr);
            _stackRemaining = 0f;
            _lastStackContext = null;
        }
    }

    internal sealed class SurtrTwilightControllerEffect : BaseEffect
    {
        private const int StaggerDuration = 1;   // 2초 → 1턴
        private bool _used;
        private bool _staggering;
        private float _staggerRemaining;

        public SurtrTwilightControllerEffect() : base(0) { }

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target) return 1f;
            return Mathf.Max(0f, 1f - attacker.ManaCurr * 0.2f);
        }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit == null || unit != Target) return false;
            _used = true;
            _staggering = true;
            _staggerRemaining = StaggerDuration;
            unit.AddUntargetableSource();
            unit.ControlStarts(new ControlContext(attacker, StaggerDuration));
            return true;
        }

        public override void OnOwnerTurn()
        {
            if (!_staggering || Target == null || !Target.isActive) return;
            _staggerRemaining -= 1f;
            if (_staggerRemaining > 0f) return;

            _staggering = false;
            Target.RemoveUntargetableSource();
            Target.ControlEnds();
            Target.ModifyHp(Target.HpMax);
            Target.AddStatus(BuffStatus.Create(
                SurtrIds.TwilightStatus,
                SurtrIds.TwilightStatusKey,
                "황혼",
                Target,
                Target,
                new SurtrBurningTwilightEffect(),
                category: BaseEnums.StatusCategory.Neutral,
                description: "라그나로크가 항상 최대 중첩이며 턴마다 증가하는 비율로 최대 체력을 잃습니다."));
        }

        public override void OnRemove()
        {
            if (!_staggering || Target == null) return;
            _staggering = false;
            Target.RemoveUntargetableSource();
            if (Target.isControlled) Target.ControlEnds();
        }
    }

    /// <summary>
    /// 황혼의 자기 소진.
    ///
    /// 이 효과만은 턴이 아니라 <b>전투 시계(초)</b>를 쓴다.
    /// 소진율이 시간에 따라 점점 커지는 구조라, 행동 속도가 빠른 수르트일수록
    /// 턴 기준으로는 더 빨리 타 죽는 역설이 생기기 때문이다.
    /// 전투 시계는 AV에서 환산하므로 누군가 행동하는 동안에는 멈춘다.
    /// </summary>
    internal sealed class SurtrBurningTwilightEffect : BaseEffect
    {
        private int _drainTick;
        private float _secondsCarry;

        public SurtrBurningTwilightEffect() : base(0) { }

        public override void OnApply()
        {
            _drainTick = 0;
            _secondsCarry = 0f;
            Target?.FillUltimateResource(false);
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;
            Target.FillUltimateResource(false);

            // 직전 턴 이후 흐른 전투 시간만큼 1초 단위로 정산한다.
            _secondsCarry += Target.LastTurnSeconds;
            while (_secondsCarry >= 1f && Target.isActive)
            {
                _secondsCarry -= 1f;

                // 매초 최대 체력의 2%부터 시작해 1%p씩 증가한다.
                float drainRatio = (2f + _drainTick) * 0.01f;
                _drainTick++;

                int drain = Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * drainRatio));
                if (Target.HpCurr <= drain)
                {
                    Target.Die(Target);
                    return;
                }
                Target.ModifyHp(Target.HpCurr - drain);
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

    public sealed class SurtrGiant : SurtrStatusPassive
    {
        public SurtrGiant(PassiveCodeContext context) : base(context, 133, "surtr_giant", "거인", "STR +4") { }
        protected override BaseEffect CreateEffect() => new SurtrGiantEffect();
    }

    internal sealed class SurtrGiantEffect : BaseEffect
    {
        public SurtrGiantEffect() : base(0) { }
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Caster && stat == BaseEnums.PrimaryStat.STR ? 4 : 0;
    }

    public sealed class SurtrFireMastery : SurtrStatusPassive
    {
        public SurtrFireMastery(PassiveCodeContext context) : base(
            context, 134, "surtr_fire_mastery", "원소 숙련 - 불", "불 원소를 보유한 적에게 주는 피해가 10% 증가합니다.") { }
        protected override BaseEffect CreateEffect() => new SurtrFireMasteryEffect();
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

    public sealed class SurtrFighter : PassiveCode
    {
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public SurtrFighter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "투사";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _damageHandler = context =>
            {
                if (context?.Attacker == Caster && context.DamageDealt > 0)
                {
                    Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(context.DamageDealt * 0.08f), Caster);
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
