using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class TsukuyomiStatusIds
    {
        public const int Fickle = 120;
        public const int Curse = 121;
        public const int PainfulWound = 122;
        public const int Cycle = 123;
        public const int FullMoon = 124;
        public const int WidenWound = 126;
    }

    /// <summary>처치된 적의 지속피해를 1초 정산해 주변 3×3 범위에 폭발시킨다.</summary>
    public sealed class TsukuyomiMoonReckoning : UniquePassiveCode
    {
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiMoonReckoning(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "월식 정산";
            MaxStage = 3;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (_registered) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Caster == null || !Caster.isActive || dead == null || dead.IsEnemy == Caster.IsEnemy) return;
            int dotPerSecond = dead.GetEstimatedDamageOverTimePerTurn();
            if (dotPerSecond <= 0 || dead.currentCell == null) return;

            float[] ratios = { 0.5f, 0.75f, 1f };
            int damage = Mathf.Max(1, Mathf.RoundToInt(dotPerSecond * ratios[Mathf.Clamp(CurrentStage, 1, 3) - 1]));
            int centerX = dead.currentCell.xPos;
            int centerY = dead.currentCell.yPos;
            var tags = new List<int> { DamageTag.MultiTarget, DamageTag.Special, DamageTag.NonContactAttack };

            foreach (Unit target in Target.GetAllEnemies(Caster).Where(unit =>
                         unit != null && unit != dead && unit.isActive && unit.HpCurr > 0 && unit.currentCell != null &&
                         Mathf.Abs(unit.currentCell.xPos - centerX) <= 1 && Mathf.Abs(unit.currentCell.yPos - centerY) <= 1).ToList())
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, tags));
            }
        }
    }

    /// <summary>8초마다 다음 공격 1회를 무효화하고 INT 50% 보호막을 얻는다.</summary>
    public sealed class TsukuyomiSpellShield : PassiveCode
    {
        private float _timer;
        private bool _armed;
        private int _grantedShield;
        private bool _registered;
        private Action<EventContext> _turnHandler;
        private Action<EventContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiSpellShield(PassiveCodeContext context) : base(context)
        {
            CodeName = "주문 보호막";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _timer = 0f;
            _armed = false;
            _grantedShield = 0;
            if (_registered) return;
            _turnHandler = OnOwnerTurnStart;
            _damageHandler = OnBeforeDamageTaken;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
            _armed = false;
        }

        private void OnOwnerTurnStart(EventContext context)
        {
            if (_armed) return;
            _timer += Caster.LastTurnSeconds;
            if (_timer < 8f) return;
            _timer = 0f;
            int shieldBefore = Caster.ShieldCurr;
            // 사양 "INT 50%" = 위력 50. 보호막도 피해와 같은 척도를 쓴다.
            Caster.AddShield(Mathf.Max(1, Caster.SkillDamage(50, BaseEnums.PrimaryStat.INT)), Caster);
            _grantedShield = Mathf.Max(0, Caster.ShieldCurr - shieldBefore);
            _armed = true;
        }

        private void OnBeforeDamageTaken(EventContext context)
        {
            if (!_armed || context.DmgCtx == null || context.DmgCtx.CodeType == BaseEnums.CodeType.Effect) return;
            context.DmgCtx.IsCancelled = true;
            Caster.RemoveShield(Mathf.Min(_grantedShield, Caster.ShieldCurr));
            _armed = false;
            _timer = 0f;
        }
    }

    /// <summary>2초마다 서로 다른 두 기본 스탯에 +10%/-10%를 다시 배정한다.</summary>
    public sealed class TsukuyomiFickle : PassiveCode
    {
        private const string StatusKey = "tsukuyomi_fickle";
        private float _timer;
        private bool _registered;
        private Action<EventContext> _turnHandler;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiFickle(PassiveCodeContext context) : base(context)
        {
            CodeName = "변덕쟁이";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _timer = 0f;
            if (_registered) return;
            _turnHandler = OnOwnerTurnStart;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnOwnerTurnStart(EventContext context)
        {
            _timer += Caster.LastTurnSeconds;
            if (_timer < 2f) return;
            _timer %= 2f;
            RerollStats();
        }

        private void RerollStats()
        {
            Array stats = Enum.GetValues(typeof(BaseEnums.PrimaryStat));
            int raisedIndex = UnityEngine.Random.Range(0, stats.Length);
            int loweredIndex = UnityEngine.Random.Range(0, stats.Length - 1);
            if (loweredIndex >= raisedIndex) loweredIndex++;

            var status = BuffStatus.Create(
                TsukuyomiStatusIds.Fickle, StatusKey, "변덕쟁이", Caster, Caster,
                new PrimaryStatMultiplierEffect((BaseEnums.PrimaryStat)stats.GetValue(raisedIndex), 1.1f),
                description: "무작위 기본 스탯 하나가 10% 증가하고 다른 하나가 10% 감소합니다.");
            status.AddEffect(new PrimaryStatMultiplierEffect((BaseEnums.PrimaryStat)stats.GetValue(loweredIndex), 0.9f));
            Caster.AddStatus(status);
        }
    }

    public sealed class TsukuyomiWidenWound : PassiveCode
    {
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiWidenWound(PassiveCodeContext context) : base(context)
        {
            CodeName = "상처 벌리기";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatus(BuffStatus.Create(
                TsukuyomiStatusIds.WidenWound, "tsukuyomi_widen_wound", CodeName, Caster, Caster,
                new WidenWoundEffect(), description: "지속피해 상태의 대상을 공격할 때 방어력 45%를 무시하고 치유량 감소를 부여합니다."));
            if (_registered) return;
            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            Unit target = context?.Target;
            if (context?.Attacker != Caster || target == null || target.HpCurr <= 0 ||
                !target.HasDamageOverTimeStatus()) return;
            HealingReductionStatus.Apply(target, Caster, 3, CodeName);   // 6초 → 3턴
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }
    }

    public sealed class TsukuyomiPainfulWound : PassiveCode
    {
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiPainfulWound(PassiveCodeContext context) : base(context)
        {
            CodeName = "고통스러운 상처";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _damageHandler = OnDamageDealt;
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

        private void OnDamageDealt(DamageResolvedContext context)
        {
            Unit target = context?.Target;
            if (target == null || target.HpCurr <= 0 || target.HpCurr >= target.HpMax || !target.HasDamageOverTimeStatus()) return;
            HealingReductionStatus.Apply(target, Caster, 3, CodeName);   // 6초 → 3턴
        }
    }

    public sealed class TsukuyomiCycle : PassiveCode
    {
        private bool _registered;
        private int _triggerCount;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiCycle(PassiveCodeContext context) : base(context)
        {
            CodeName = "순환";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _triggerCount = 0;
            if (_registered) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (dead == null || dead.IsEnemy != Caster.IsEnemy || dead == Caster || !Caster.isActive) return;
            List<Unit> survivors = Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit != dead && unit.isActive && unit.HpCurr > 0).ToList();
            if (survivors.Count == 0) return;
            Unit target = survivors[UnityEngine.Random.Range(0, survivors.Count)];
            var status = BuffStatus.Create(
                TsukuyomiStatusIds.Cycle, $"tsukuyomi_cycle_{Caster.GetEntityId()}_{_triggerCount++}", CodeName,
                Caster, target, new OutgoingDamageEffect(1.25f), stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                description: "가하는 피해가 25% 증가하고 받는 피해가 25% 감소합니다.");
            status.AddEffect(new ReceivingDamageEffect(0.75f));
            target.AddStatus(status);
        }
    }

    public sealed class TsukuyomiChainLightning : PassiveCode
    {
        private int _hitCount;
        private bool _registered;
        private Action<EventContext> _hitHandler;
        private Action<EventContext> _cleanupHandler;

        public TsukuyomiChainLightning(PassiveCodeContext context) : base(context)
        {
            CodeName = "체인 라이트닝";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _hitCount = 0;
            if (_registered) return;
            _hitHandler = OnNormalAttackHit;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnNormalAttackHit(EventContext context)
        {
            Unit primary = context?.Grantor;
            if (primary == null || context.DmgCtx == null || ++_hitCount % 4 != 0) return;
            primary.GrantCombatElement(BaseEnums.UnitElement.Electro);
            int damage = Mathf.Max(1, Mathf.RoundToInt(context.DmgCtx.Damage * 0.5f));
            var tags = new List<int> { DamageTag.MultiTarget, DamageTag.Special, DamageTag.NonContactAttack };
            foreach (Unit target in Target.GetAllEnemies(Caster)
                         .Where(unit => unit != null && unit != primary && unit.isActive && unit.HpCurr > 0 && unit.currentCell != null)
                         .OrderBy(unit => GridDistance(primary, unit)).Take(3).ToList())
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, tags));
            }
        }

        private static int GridDistance(Unit a, Unit b)
        {
            if (a?.currentCell == null || b?.currentCell == null) return int.MaxValue;
            return Mathf.Abs(a.currentCell.xPos - b.currentCell.xPos) + Mathf.Abs(a.currentCell.yPos - b.currentCell.yPos);
        }
    }

    public sealed class TsukuyomiFullMoon : PassiveCode
    {
        public TsukuyomiFullMoon(PassiveCodeContext context) : base(context)
        {
            CodeName = "만월";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            Caster.AddStatus(BuffStatus.Create(
                TsukuyomiStatusIds.FullMoon, "tsukuyomi_full_moon", CodeName, Caster, Caster,
                new ThemeDamageEffect(1.5f, 0.5f), description: "일본 테마 또는 밤 필드에서 주는 피해가 50% 증가하고 받는 피해가 50% 감소합니다."));
        }
    }

    internal sealed class PrimaryStatMultiplierEffect : BaseEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly float _multiplier;
        public PrimaryStatMultiplierEffect(BaseEnums.PrimaryStat stat, float multiplier) : base(0, multiplier)
        {
            _stat = stat;
            _multiplier = multiplier;
        }
        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat) => stat == _stat ? _multiplier : 1f;
    }

    /// <summary>
    /// 지속피해 상태의 대상을 때릴 때 방어력의 45%를 무시한다.
    /// 피해 계산에서 유효 방어력에 이 배율이 곱해진다(0.55 = 45% 무시).
    /// </summary>
    internal sealed class WidenWoundEffect : BaseEffect
    {
        public WidenWoundEffect() : base(0) { }
        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
            => target != null && target.HasDamageOverTimeStatus() ? 0.55f : 1f;
    }

    internal sealed class OutgoingDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public OutgoingDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => _multiplier;
    }

    internal sealed class ReceivingDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public ReceivingDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float ReceivingDamageModifier(Unit unit) => _multiplier;
    }

    internal sealed class ThemeDamageEffect : BaseEffect
    {
        private readonly float _outgoing;
        private readonly float _receiving;
        public ThemeDamageEffect(float outgoing, float receiving) : base(0)
        {
            _outgoing = outgoing;
            _receiving = receiving;
        }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => IsActiveTheme() ? _outgoing : 1f;
        public override float ReceivingDamageModifier(Unit unit) => IsActiveTheme() ? _receiving : 1f;
        private static bool IsActiveTheme()
            => GameManager.Instance?.RoundManager != null &&
               (GameManager.Instance.RoundManager.CurrentThemeHasTag("Japan") || GameManager.Instance.RoundManager.CurrentThemeHasTag("Night"));
    }
}
