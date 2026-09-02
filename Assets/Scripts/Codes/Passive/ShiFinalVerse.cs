using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public class ShiFinalVerse : UniquePassiveCode
    {
        // 종언 스택 도달 시 터지는 세 단계의 위력.
        private const int FinisherPower = 60;
        private const int SplitPower1 = 100;
        private const int SplitPower2 = 150;

        private int _verseStack;
        private bool _isRegistered;
        private Action<EventContext> _normalAttackHitHandler;

        public ShiFinalVerse(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "시의 종언";
            Caster = context.Caster;
            MaxStage = 3;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            _verseStack = 0;
            if (_isRegistered) return;

            _normalAttackHitHandler = OnNormalAttackHit;
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalAttackHitHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered || _normalAttackHitHandler == null) return;

            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalAttackHitHandler);
            _normalAttackHitHandler = null;
            _isRegistered = false;
        }

        public override bool HasValidTarget()
        {
            return Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive);
        }

        private void OnNormalAttackHit(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            int stage = Mathf.Clamp(CurrentStage, 1, MaxStage);
            int maxStack = stage * 3;
            int stackGain = Caster.HasStatusKey(Codes.Ultimate.a002_U_FinalBell.StatusKey) ? 3 : 1;
            int previousStack = Mathf.Min(_verseStack, maxStack);
            _verseStack = Mathf.Min(previousStack + stackGain, maxStack);

            // 추가공격은 그 자리에서 터뜨리지 않고 스케줄러에 예약한다.
            // 한 번에 하나만 행동해야 하고, 같은 키라 한 턴에 두 번 겹쳐 들어가지 않는다.
            if (previousStack < 3 && _verseStack >= 3)
            {
                EnqueueVerse("shi_verse_3", () => DealLowestHpDamage(FinisherPower));
            }
            else if (stage >= 2 && previousStack < 6 && _verseStack >= 6)
            {
                EnqueueVerse("shi_verse_6", () => DealSplitDamage(SplitPower1));
            }
            else if (stage >= 3 && previousStack < 9 && _verseStack >= 9)
            {
                EnqueueVerse("shi_verse_9", () => DealSplitDamage(SplitPower2));
            }

            if (_verseStack >= maxStack)
            {
                _verseStack = 0;
            }
        }

        private void EnqueueVerse(string key, System.Action run)
        {
            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                Caster, key, CodeName, run);
        }

        private void DealLowestHpDamage(int power)
        {
            // 방어력이 폐지되어 '가장 무른 적' 기준을 현재 체력으로 대체한다.
            Unit target = GetAvailableEnemies()
                .OrderBy(unit => unit.HpCurr)
                .FirstOrDefault();
            if (target == null) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(power) * critMultiplier));
            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.ContactAttack,
                DamageTag.Slash,
            };
            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, tags, isCrit));
        }

        private void DealSplitDamage(int power)
        {
            List<Unit> targets = GetAvailableEnemies();
            if (targets.Count == 0) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int totalDamage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(power) * critMultiplier));
            int splitDamage = Mathf.Max(1, totalDamage / targets.Count);
            var tags = new List<int>
            {
                DamageTag.AllTarget,
                DamageTag.SplitDamage,
                DamageTag.ContactAttack,
                DamageTag.Slash,
            };

            foreach (Unit target in targets)
            {
                target.TakeDamage(new DamageContext(Caster, splitDamage, BaseEnums.CodeType.Passive, tags, isCrit));
            }
        }

        private List<Unit> GetAvailableEnemies()
        {
            return Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && unit.currentCell != null && unit.currentCell.isOccupied)
                .ToList();
        }
    }

    public abstract class ShiStatusPassive : PassiveCode
    {
        private readonly int _statusId;
        private readonly string _statusKey;
        private readonly string _description;
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        protected ShiStatusPassive(
            PassiveCodeContext context,
            int statusId,
            string statusKey,
            string codeName,
            string description) : base(context)
        {
            _statusId = statusId;
            _statusKey = statusKey;
            _description = description;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = codeName;
            IgnoresActivationChance = true;
        }

        protected abstract Effects.Base.BaseEffect CreateEffect();

        public override void CastCode()
        {
            if (_registered) return;
            Caster.AddStatus(Effects.Buffs.BuffStatus.Create(
                _statusId,
                _statusKey,
                CodeName,
                Caster,
                Caster,
                CreateEffect(),
                description: _description));
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
            Caster.RemoveStatusByKey(_statusKey);
            _registered = false;
        }
    }

    /// <summary>Lv.10: 베기 태그 공격의 피해가 25% 증가한다.</summary>
    public sealed class ShiSwordMaster : ShiStatusPassive
    {
        public ShiSwordMaster(PassiveCodeContext context) : base(
            context, 154, "shi_sword_master", "검의 달인", "베기류 공격의 피해가 25% 증가합니다.") { }

        protected override Effects.Base.BaseEffect CreateEffect() => new ShiSwordMasterEffect();
    }

    internal sealed class ShiSwordMasterEffect : Effects.Base.BaseEffect
    {
        public ShiSwordMasterEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            return attacker == Caster && context?.DamageTags?.Contains(DamageTag.Slash) == true ? 1.25f : 1f;
        }
    }

    /// <summary>Lv.28: 바람 원소가 부착된 동안 DEX +10.</summary>
    public sealed class ShiSwiftness : ShiStatusPassive
    {
        public ShiSwiftness(PassiveCodeContext context) : base(
            context, 156, "shi_swiftness", "신속", "바람 원소가 부착된 동안 DEX가 10 증가합니다.") { }

        protected override Effects.Base.BaseEffect CreateEffect() => new ShiSwiftnessEffect();
    }

    internal sealed class ShiSwiftnessEffect : Effects.Base.BaseEffect
    {
        public ShiSwiftnessEffect() : base(0) { }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            return unit == Caster && stat == BaseEnums.PrimaryStat.DEX &&
                   unit.HasCombatElement(BaseEnums.UnitElement.Anemo) ? 10 : 0;
        }
    }

    /// <summary>Lv.44 / 수르트 Lv.66 공용: 공격 후 생명력 8% 미만의 적을 처형한다.</summary>
    public sealed class HeavenlyKiller : ExecuteThresholdPassive
    {
        public const int CodeId = 11;

        public HeavenlyKiller(PassiveCodeContext context) : base(context, "천살성", 0.08f)
        {
            // 일반 등급. 당연한 운명(13)을 배웠으면 발동 자체가 막힌다.
            SupersededByCodeId = ShiCertainDestiny.CodeId;
        }
    }

    /// <summary>Lv.78: 체력 10% 미만 대상 공격은 확정 치명타이며 치명타 확률×2를 치명타 피해에 더한다.</summary>
    public sealed class ShiTenDaysNoFlower : ShiStatusPassive
    {
        public const int CodeId = 12;

        public ShiTenDaysNoFlower(PassiveCodeContext context) : base(
            context, 159, "shi_ten_days_no_flower", "화무십일홍",
            "체력 10% 미만의 적을 공격하면 확정 치명타가 발생하고 치명타 확률의 2배가 치명타 피해에 더해집니다.")
        {
        }

        protected override Effects.Base.BaseEffect CreateEffect() => new ShiTenDaysNoFlowerEffect();
    }

    internal sealed class ShiTenDaysNoFlowerEffect : Effects.Base.BaseEffect
    {
        /// <summary>확정 치명타가 걸리는 체력선. 전수 가능해진 대신 창을 좁혔다.</summary>
        private const float TenDaysThreshold = 0.10f;

        public ShiTenDaysNoFlowerEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Caster || target == null || target.HpMax <= 0 || context == null ||
                context.CodeType == BaseEnums.CodeType.Effect || (float)target.HpCurr / target.HpMax >= TenDaysThreshold)
            {
                return 1f;
            }

            float normalCritMultiplier = Mathf.Max(1f, attacker.CritMultiplierCurr);
            float desiredCritMultiplier = normalCritMultiplier + attacker.CritChanceCurr * 2f;
            float alreadyAppliedMultiplier = context.IsCrit ? normalCritMultiplier : 1f;
            context.IsCrit = true;
            return desiredCritMultiplier / alreadyAppliedMultiplier;
        }
    }

    /// <summary>Lv.82: 천살성을 보유한 경우 처형선을 12%로 대체한다.</summary>
    public sealed class ShiCertainDestiny : ExecuteThresholdPassive
    {
        public const int CodeId = 13;

        public ShiCertainDestiny(PassiveCodeContext context) : base(context, "당연한 운명", 0.12f)
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        /// <summary>천살성을 발판으로 삼는 강화 등급이라, 그 코드를 배우지 않았으면 처형선이 서지 않는다.</summary>
        protected override bool CanExecute() => Caster.HasLearnedPassiveCode(HeavenlyKiller.CodeId);
    }

    public abstract class ExecuteThresholdPassive : PassiveCode
    {
        private readonly float _threshold;
        private bool _registered;
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        protected ExecuteThresholdPassive(PassiveCodeContext context, string codeName, float threshold) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = codeName;
            IgnoresActivationChance = true;
            _threshold = threshold;
        }

        protected virtual bool CanExecute() => true;

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
            if (!CanExecute() || context?.Attacker != Caster || context.DamageDealt <= 0 ||
                context.DamageContext?.CodeType == BaseEnums.CodeType.Effect || target == null ||
                !target.isActive || target.HpCurr <= 0 || target.HpMax <= 0)
            {
                return;
            }

            // 처형은 일반 등급의 적에게만 통한다. 엘리트·보스·아군은 대상이 아니다.
            if (!target.IsExecutable) return;

            if ((float)target.HpCurr / target.HpMax < _threshold)
            {
                target.Die(Caster);
            }
        }
    }
}
