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
    /// 수르트의 체력 연소. 일반행동과 궁극기 라그나로크가 <b>각자</b> 행동을 열 때 한 번 치른다.
    ///
    /// 최대 체력의 15%를 태우고 그 행동이 주는 피해를 40% 올린다. 태운 뒤 체력이 최대 체력의 30%
    /// 아래로 내려가면 태우지 않고 증가도 없다. 예전에는 고유 패시브 '황혼'이 이 일을 했는데,
    /// 연소는 두 공격 코드의 성질로 옮기고 '황혼'은 보호막 패시브로 바꿨다(초보자용 안정성).
    /// </summary>
    public static class SurtrBurn
    {
        public const float HpCostRatio = 0.15f;
        public const float HpFloorRatio = 0.30f;
        public const float DamageMultiplier = 1.4f;

        /// <summary>값을 치렀으면 피해 배율(1.4)을, 못 치렀으면 1을 돌려준다.</summary>
        public static float Pay(Unit caster)
        {
            if (caster == null || caster.HpMax <= 0) return 1f;
            bool aboveFloor = caster.HpCurr - caster.HpMax * HpCostRatio >= caster.HpMax * HpFloorRatio;
            return aboveFloor && caster.TryConsumeAttackHp(HpCostRatio, false, out _) ? DamageMultiplier : 1f;
        }
    }

    /// <summary>
    /// 수르트 고유 P — 황혼. 붕괴: 스타레일의 화염 개척자(보존)를 참고했다.
    ///
    /// 일반행동이나 궁극기로 적에게 피해를 주면 <b>맞힌 적 하나마다</b> STR×5의 보호막을 얻는다.
    /// 한 행동에서 같은 적을 두 번 때려도(라그나로크의 단일 + 광역) 한 번만 센다.
    /// 보호막은 최대 체력의 40%까지 쌓인다.
    ///
    /// 연소(<see cref="SurtrBurn"/>)가 빼 가는 체력을 보호막이 되돌려 주는 짝이다. 적이 많을수록
    /// 라그나로크 한 번이 크게 두르므로, 초보자가 광역 궁극기로 전열을 버티는 흐름이 저절로 생긴다.
    /// </summary>
    public sealed class SurtrTwilight : UniquePassiveCode
    {
        public const float ShieldStrCoefficient = 5f;
        public const float ShieldCapRatio = 0.40f;

        private readonly HashSet<Unit> _hitThisAction = new();
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _actionHandler;
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
            _actionHandler = _ => _hitThisAction.Clear();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActivates, _actionHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _actionHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || context?.Attacker != Caster || context.Target == null) return;
            BaseEnums.CodeType type = context.DamageContext?.CodeType ?? BaseEnums.CodeType.Passive;
            if (type != BaseEnums.CodeType.Normal && type != BaseEnums.CodeType.Ultimate) return;
            if (!_hitThisAction.Add(context.Target)) return;

            int cap = Mathf.RoundToInt(Caster.HpMax * ShieldCapRatio);
            int room = cap - Caster.ShieldCurr;
            if (room <= 0) return;

            int shield = Mathf.Min(room, Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseStr() * ShieldStrCoefficient)));
            Caster.AddShield(shield, Caster);
        }

        public override void StopCode()
        {
            if (Caster == null || !_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActivates, _actionHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _actionHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _hitThisAction.Clear();
            _registered = false;
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
