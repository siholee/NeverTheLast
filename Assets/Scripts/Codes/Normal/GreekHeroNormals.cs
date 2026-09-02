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
        public OrionNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 80;
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
        private bool _isEnhanced;

        public TheseusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 80;
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
                Caster.SkillDamage(60, BaseEnums.PrimaryStat.DEX) * critMultiplier));
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
            }
        }

        public override void StopCode()
        {
            _isEnhanced = false;
            base.StopCode();
        }
    }
}
