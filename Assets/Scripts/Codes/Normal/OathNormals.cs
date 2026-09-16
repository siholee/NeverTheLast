using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 시구르드 N — 그람. 단일 접촉 베기에 불을 얹는다.
    ///
    /// 불을 매 타격 부착하는 것이 협공의 전제다. 브륀힐드의 얼음이 같은 대상에서 만나
    /// 융해가 터지려면 두 원소가 <b>같은 자리에</b> 쌓여야 한다.
    /// </summary>
    public sealed class SigurdGram : BaseNormalCode
    {
        private const int NormalPower = 90;

        public SigurdGram(NormalCodeContext context) : base(context)
        {
            CodeName = "그람";
            CastingDelay = 0.4f;
            MaxStage = 1;
            Power = NormalPower;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Slash };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Pyro, Unit.CommonElementAuraDuration, Caster);
        }
    }

    /// <summary>브륀힐드 N — 발키리의 창. 단일 찌르기에 얼음을 얹는다.</summary>
    public sealed class BrynhildValkyrieSpear : BaseNormalCode
    {
        private const int NormalPower = 90;

        public BrynhildValkyrieSpear(NormalCodeContext context) : base(context)
        {
            CodeName = "발키리의 창";
            CastingDelay = 0.4f;
            MaxStage = 1;
            Power = NormalPower;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Pierce };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
