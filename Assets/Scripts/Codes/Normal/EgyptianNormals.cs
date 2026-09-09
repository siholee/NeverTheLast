using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>호루스 N — STR×1.0 화살. 네 번째 사격은 우제트로 강화된다.</summary>
    public sealed class HorusNormalAttack : BaseNormalCode
    {
        private bool _empowered;

        public HorusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 100;
        }

        protected override List<int> GetDamageTags()
        {
            _empowered = Caster.ManaCurr >= 3;
            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.NormalAttack,
                DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
            };
            if (_empowered) tags.Add(DamageTag.UltAttack);
            return tags;
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            int power = _empowered ? 160 : 100;
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) * critMultiplier));
        }

        protected override DamageContext CreateDamageContext(int damage, List<int> damageTags, bool isCrit)
        {
            var context = base.CreateDamageContext(damage, damageTags, isCrit);
            if (_empowered) context.DefenseStatMultiplier = 0.8f;
            return context;
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            Caster.AddUltimateResource(1);
            if (_empowered) Caster.StartCoroutine(ClearWadjetGauge());
        }

        private IEnumerator ClearWadjetGauge()
        {
            // 4타 적중 순간 링이 4/4까지 찬 모습을 잠깐 보여 준 뒤 다음 연사를 위해 비운다.
            yield return new WaitForSeconds(0.22f);
            if (Caster != null) Caster.AddUltimateResource(-Caster.ManaCurr);
        }
    }

    /// <summary>아누비스 N — CON×0.8 물리·접촉·찌르기.</summary>
    public sealed class AnubisNormalAttack : BaseNormalCode
    {
        public AnubisNormalAttack(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 80; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce,
        };
    }

    /// <summary>바스테트 N — DEX×0.8 물리·접촉·베기.</summary>
    public sealed class BastetNormalAttack : BaseNormalCode
    {
        public BastetNormalAttack(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 80; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };
    }

    /// <summary>세트 N — STR×0.8 물리·접촉·베기.</summary>
    public sealed class SetNormalAttack : BaseNormalCode
    {
        public SetNormalAttack(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 80; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };
    }

    /// <summary>토트 N — INT×0.8 특수·비접촉.</summary>
    public sealed class ThothNormalAttack : BaseNormalCode
    {
        public ThothNormalAttack(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 80; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };
    }

    /// <summary>이시스 N — INT×0.6. 세 번째 적중마다 바위 원소를 부여한다.</summary>
    public sealed class IsisNormalAttack : BaseNormalCode
    {
        private int _hitCount;

        public IsisNormalAttack(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 60; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(60, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            _hitCount++;
            if (_hitCount < 3) return;
            _hitCount = 0;
            target.GrantCombatElement(BaseEnums.UnitElement.Geo, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
