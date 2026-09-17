using System.Collections.Generic;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    /// <summary>야마 일반행동 — DEX 기반 위력 60. 접촉·물리·베기.</summary>
    public sealed class YamaNormalAttack : BaseNormalCode
    {
        public YamaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 90;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical, DamageTag.Slash,
        };
    }

    /// <summary>아그니 일반행동 — INT 기반 위력 60. 비접촉·특수.</summary>
    public sealed class AgniNormalAttack : BaseNormalCode
    {
        public AgniNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Special,
        };
    }

    /// <summary>인드라 일반행동 — INT 기반 위력 50. 비접촉·특수.</summary>
    public sealed class IndraNormalAttack : BaseNormalCode
    {
        public IndraNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 50;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Special,
        };
    }

    /// <summary>바유 일반행동 — CON 기반 위력 60. 비접촉·물리.</summary>
    public sealed class VayuNormalAttack : BaseNormalCode
    {
        public VayuNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Physical,
        };
    }
}
