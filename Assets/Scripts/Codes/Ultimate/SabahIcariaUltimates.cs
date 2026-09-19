using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>사바흐 U — 아즈라엘 7스택을 모두 소모해 적 전체를 고정 위력 150 + DEX×1.0으로 벤다.</summary>
    public sealed class SabahAzrael : SimpleUltimate
    {
        private const int AzraelFlatPower = 150;
        private const float DexCoefficient = 1.0f;

        public SabahAzrael(UltimateCodeContext context)
            : base(context, "아즈라엘", 0.4f)
        {
            Power = AzraelFlatPower;
            PowerStatCoefficient = DexCoefficient;
            PowerStat = BaseEnums.PrimaryStat.DEX;
        }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));
            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
            };

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
            }
        }
    }

    /// <summary>이카리아 U — 체력을 지불할 수 있으면 INT 계수가 1.1에서 1.4로 오른다.</summary>
    public sealed class IcariaChallengeTheSky : SimpleUltimate
    {
        public IcariaChallengeTheSky(UltimateCodeContext context)
            : base(context, "창공에 대한 도전", 0.5f) { }

        protected override void Resolve()
        {
            float spentHpRatio = 0f;
            float codeDamageMultiplier = 1f;
            int power = 110;

            if (Caster.TryConsumeAttackHp(0.20f, false, out float skillCost))
            {
                power = 140;
                spentHpRatio += skillCost;
            }

            if (Caster.HasLearnedPassiveCode(Passive.SabahIcariaCodeIds.IcariaPyroAffinity) &&
                Caster.HasCombatElement(BaseEnums.UnitElement.Pyro))
            {
                Caster.TryConsumeAttackHp(0.05f, true, out float affinityCost);
                spentHpRatio += affinityCost;
                codeDamageMultiplier = 1.2f;
            }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.INT) * critMultiplier));
            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.ContactAttack, DamageTag.Pierce,
            };

            foreach (Unit target in Enemies())
            {
                var context = new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit)
                {
                    SelfHpSpentRatio = spentHpRatio,
                    OutgoingDamageMultiplier = codeDamageMultiplier,
                };
                target.TakeDamage(context);
            }
        }
    }
}
