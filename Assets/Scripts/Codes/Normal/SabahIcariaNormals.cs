using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>사바흐 N — 우선도가 높은 적부터 최대 3명을 베고 번개 원소를 부여한다.</summary>
    public sealed class SabahLightningSlash : BaseNormalCode
    {
        public SabahLightningSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "번개 베기";
            Power = 90;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override List<Unit> SelectTarget()
            => GetAvailableEnemies()
                .OrderByDescending(unit => unit.Priority)
                .ThenBy(unit => unit.HpCurr)
                .Take(3)
                .ToList();

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.MultiTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive)
            {
                target.GrantCombatElement(
                    BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
            }
        }
    }

    /// <summary>이카리아 N — 체력을 지불할 수 있으면 INT 계수가 0.6에서 0.9로 오른다.</summary>
    public sealed class IcariaRecklessThrust : BaseNormalCode
    {
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

            int power = 60;
            if (Caster.TryConsumeAttackHp(0.10f, false, out float skillCost))
            {
                power = 90;
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
