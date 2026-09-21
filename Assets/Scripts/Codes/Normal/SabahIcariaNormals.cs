using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 사바흐 N / 대체행동 — 검격과 번개 베기.
    ///
    /// 평소에는 단일 대상을 베는 <b>평범한 일반행동</b>이고, 일반행동을 두 번 하면
    /// <b>세 번째가 대체행동 번개 베기</b>가 된다 — 우선도가 높은 적부터 최대 3명을 베고
    /// 맞은 대상마다 번개를 부착하는, 예전의 일반행동 그대로다.
    ///
    /// 사바흐를 궁극기 위주의 딜러로 맞추기 위한 개편이다. 매 행동이 셋을 긁던 때보다
    /// 평시 딜이 낮아지는 대신, 광역과 원소 부착이 세 박자마다 오는 대체행동으로 모인다.
    /// 번개가 만드는 반응은 그대로 디버프라 아즈라엘이 차는 길 자체는 남는다.
    ///
    /// 대체행동도 일반행동 한 번으로 세야 하므로 기반 클래스의 흐름을 그대로 타고
    /// 대상 선택·위력·태그·부착만 갈아 끼운다. <c>NotifyActionResolved</c>는 기반 클래스가 부른다.
    /// 박자를 세는 곳은 여기 하나뿐이다.
    /// </summary>
    public sealed class SabahLightningSlash : BaseNormalCode
    {
        /// <summary>몇 번째 일반행동이 대체행동으로 바뀌는가.</summary>
        private const int SubstituteInterval = 3;

        /// <summary>평범한 일반행동 검격 — 단일 대상. 위력은 예전 번개 베기와 같고 대상 수만 하나다.</summary>
        private const int StrikeFlatPower = 110;
        private const float StrikeDexCoefficient = 1.0f;

        /// <summary>대체행동 번개 베기 — 예전의 일반행동을 그대로 옮겼다.</summary>
        private const int SlashFlatPower = 110;
        private const float SlashDexCoefficient = 1.0f;
        private const int SlashTargetCount = 3;

        private int _actionCount;
        private bool _substitute;
        private bool _registered;

        public SabahLightningSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "검격";
            Power = StrikeFlatPower;
            PowerStatCoefficient = StrikeDexCoefficient;
            PowerStat = BaseEnums.PrimaryStat.DEX;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        public override void CastCode()
        {
            RegisterReset();
            _actionCount++;
            _substitute = _actionCount % SubstituteInterval == 0;
            CodeName = _substitute ? "번개 베기" : "검격";
            base.CastCode();
        }

        /// <summary>
        /// 박자는 라운드마다 처음부터 센다. <b>이 리스너는 떼지 않는다</b> —
        /// <see cref="BaseNormalCode.StopCode"/>가 행동이 끝날 때마다 불리므로, 거기서 떼면
        /// 시전 중에만 리스너가 살아 있어 라운드 종료를 영영 듣지 못한다.
        /// </summary>
        private void RegisterReset()
        {
            if (_registered || Caster == null) return;
            Action<EventContext> reset = _ => _actionCount = 0;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, reset);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, reset);
            _registered = true;
        }

        /// <summary>대체행동일 때만 셋을 긁는다. 평소에는 기반 클래스의 단일 대상 선택을 그대로 쓴다.</summary>
        protected override List<Unit> SelectTarget()
            => _substitute
                ? GetAvailableEnemies()
                    .OrderByDescending(unit => unit.Priority)
                    .ThenBy(unit => unit.HpCurr)
                    .Take(SlashTargetCount)
                    .ToList()
                : base.SelectTarget();

        /// <summary>대체행동의 위력. <c>CurrentPower</c>와 같이 기본 스탯을 읽는다.</summary>
        private int SubstitutePower()
            => SlashFlatPower + Mathf.RoundToInt(
                Caster.GetBasePrimaryStat(BaseEnums.PrimaryStat.DEX) * SlashDexCoefficient);

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(_substitute ? SubstitutePower() : CurrentPower,
                    BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            _substitute ? DamageTag.MultiTarget : DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };

        /// <summary>번개를 부착하는 것은 <b>대체행동뿐이다</b>. 평범한 검격은 원소를 남기지 않는다.</summary>
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (!_substitute || target == null || !target.isActive) return;
            target.GrantCombatElement(
                BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
        }
    }

    /// <summary>이카리아 N — 체력을 지불할 수 있으면 INT 계수가 0.6에서 0.9로 오른다.</summary>
    public sealed class IcariaRecklessThrust : BaseNormalCode
    {
        /// <summary>체력을 지불했을 때의 위력 배율.</summary>
        private const float PaidPowerMultiplier = 1.5f;

        private float _spentHpRatio;
        private float _codeDamageMultiplier = 1f;

        public IcariaRecklessThrust(NormalCodeContext context) : base(context)
        {
            CodeName = "무모한 찌르기";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            _spentHpRatio = 0f;
            _codeDamageMultiplier = 1f;

            // 체력을 낼 수 있으면 위력이 1.5배(60 → 90)가 된다. 단계·비례 위력을
            // 잃지 않도록 리터럴이 아니라 CurrentPower에서 출발한다.
            int power = CurrentPower;
            if (Caster.TryConsumeAttackHp(0.10f, false, out float skillCost))
            {
                power = Mathf.RoundToInt(CurrentPower * PaidPowerMultiplier);
                _spentHpRatio += skillCost;
            }

            ApplyPyroAffinityCost();
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.INT) * critMultiplier));
        }

        private void ApplyPyroAffinityCost()
        {
            if (!Caster.HasLearnedPassiveCode(Passive.SabahIcariaCodeIds.IcariaPyroAffinity) ||
                !Caster.HasCombatElement(BaseEnums.UnitElement.Pyro)) return;

            Caster.TryConsumeAttackHp(0.05f, true, out float affinityCost);
            _spentHpRatio += affinityCost;
            _codeDamageMultiplier = 1.2f;
        }

        protected override DamageContext CreateDamageContext(
            int damage, List<int> damageTags, bool isCrit)
        {
            DamageContext context = base.CreateDamageContext(damage, damageTags, isCrit);
            context.SelfHpSpentRatio = _spentHpRatio;
            context.OutgoingDamageMultiplier = _codeDamageMultiplier;
            return context;
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce,
        };
    }
}
