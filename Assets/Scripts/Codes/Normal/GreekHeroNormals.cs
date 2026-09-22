using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    public sealed class OrionNormalAttack : BaseNormalCode
    {
        private const int FlatDamagePower = 40;
        private const float StrCoefficient = 0.9f;

        public OrionNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = FlatDamagePower;
            PowerStatCoefficient = StrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget,
            DamageTag.NormalAttack,
            DamageTag.Physical,
            DamageTag.NonContactAttack,
            DamageTag.Arrow,
        };
    }

    public sealed class TheseusNormalAttack : BaseNormalCode
    {
        /// <summary>강화 일반행동이 뒤에 붙이는 충격파(적 전체). 주 타격과 별개의 부가 위력이다.</summary>
        private const int ShockwavePower = 60;

        private bool _isEnhanced;

        public TheseusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 90;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        public override void CastCode()
        {
            // CastNormalCode가 직후 OnNormalActivates를 발행하므로, 기존 스택 소비 여부를 먼저 확정한다.
            _isEnhanced = Caster != null && Caster.TryConsumeCombatResource(
                GreekHeroCombat.TheseusCodeWeaveResource,
                GreekHeroCombat.TheseusEnhancedAttackCost);
            base.CastCode();
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget,
            DamageTag.NormalAttack,
            DamageTag.Physical,
            DamageTag.ContactAttack,
        };

        protected override IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            if (!_isEnhanced || target == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int shockwaveDamage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(ShockwavePower, BaseEnums.PrimaryStat.DEX) * critMultiplier));
            List<int> tags = new()
            {
                DamageTag.AllTarget,
                DamageTag.NormalAttack,
                DamageTag.Physical,
                DamageTag.ContactAttack,
            };

            foreach (Unit enemy in global::Target.GetAllEnemies(Caster)
                         .Where(unit => unit != null && unit.isActive).ToList())
            {
                enemy.TakeDamage(new DamageContext(Caster, shockwaveDamage, BaseEnums.CodeType.Normal, tags, isCrit));

                // 충격파가 적 전체에 물을 남긴다. 강화 일반행동(코드 직조 스택)에만 붙으므로
                // 빈도가 저절로 묶이고, 불·전기·얼음 딜러가 증발·감전·빙결을 이어받는다.
                if (enemy.isActive)
                    enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override void StopCode()
        {
            _isEnhanced = false;
            base.StopCode();
        }
    }
}
