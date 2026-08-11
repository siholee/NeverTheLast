using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 수르트 일반공격. 라그나로크 6중첩에서는 최대 3명을 공격하는 강화 일반공격으로 변한다.
    /// </summary>
    public sealed class SurtrScorchingSlash : BaseNormalCode
    {
        private bool IsEnhanced => Caster != null && Caster.ManaMax > 0 && Caster.ManaCurr >= Caster.ManaMax;

        public SurtrScorchingSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Cooldown = 0f;
            CastingDelay = 0.4f;
            ManaAmount = 0;
            MaxStage = 1;
            Power = 80;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Slash };
        }

        protected override List<Unit> SelectTarget()
        {
            if (!IsEnhanced) return base.SelectTarget();
            return GetAvailableEnemies()
                .OrderByDescending(unit => unit.Priority)
                .ThenBy(unit => unit.HpCurr)
                .Take(3)
                .ToList();
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            int power = IsEnhanced ? 100 : 80;
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(Caster.SkillDamage(power) * critMultiplier));
        }

        protected override List<int> GetDamageTags()
        {
            if (IsEnhanced)
            {
                return new List<int>
                {
                    DamageTag.MultiTarget,
                    DamageTag.NormalAttack,
                    DamageTag.Special,
                    DamageTag.NonContactAttack,
                    DamageTag.Slash,
                };
            }

            return new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.Physical,
                DamageTag.ContactAttack,
                DamageTag.Slash,
            };
        }
    }
}
