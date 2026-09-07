using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 공허의 늑대 N — 단일 적에게 80 + STR×0.9 위력의 접촉 물리 피해.
    /// 적중하면 현재 LUK% 확률로 2턴 출혈을 남긴다(괴조의 '날카로운 부리'와 같은 판정).
    /// </summary>
    public sealed class VoidWolfNormal : BaseNormalCode
    {
        /// <summary>공허 계열이 공유하는 출혈 강도. 턴당 최대 체력의 3%다.</summary>
        public const float BleedMaxHpPercent = 3f;
        private const int BleedTurns = 2;

        public VoidWolfNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "늑대의 송곳니";
            Power = 80;
            PowerStatCoefficient = 0.9f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.35f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;

            float chance = Mathf.Clamp01(Caster.GetBaseLuk() * 0.01f);
            if (Random.value >= chance) return;

            BleedStatus.Apply(target, Caster, BleedTurns, BleedMaxHpPercent, CodeName);
        }
    }
}
