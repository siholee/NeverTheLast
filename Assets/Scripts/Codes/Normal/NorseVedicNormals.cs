using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 프레이아 일반공격 — 유일하게 <b>적을 때리지 않는 일반공격</b>이다.
    /// 아군 전체를 INT 기반 위력 40만큼 치유한다.
    ///
    /// 대상이 아군이므로 <see cref="BaseNormalCode"/>의 적 타겟팅 흐름을 그대로 쓸 수 없다.
    /// 시전 지연과 마나 회복만 공유하고 해결부는 새로 짠다.
    /// </summary>
    public sealed class FreyaNormalAttack : BaseNormalCode
    {
        private const int HealPower = 40;

        public FreyaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = HealPower;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override IEnumerator SkillCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < CastingDelay)
            {
                if (Caster == null || !Caster.isActive || Caster.isControlled)
                {
                    StopCode();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            int heal = Mathf.Max(1, Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT));
            foreach (Unit ally in Allies())
            {
                ally.ModifyHp(ally.HpCurr + heal, Caster);
            }

            Debug.Log($"[프레이아] 아군 전체를 {heal}만큼 치유했습니다.");
            StopCode();
        }

        /// <summary>치유는 적이 없어도 유효하다. 아군이 하나라도 살아 있으면 시전한다.</summary>
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Allies().Count > 0;

        private List<Unit> Allies()
        {
            List<Unit> allies = global::Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            if (Caster != null && Caster.isActive && !allies.Contains(Caster)) allies.Add(Caster);
            return allies;
        }
    }

    /// <summary>로키 일반공격 — STR 기반 위력 80. 비접촉·물리.</summary>
    public sealed class LokiNormalAttack : BaseNormalCode
    {
        public LokiNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 80;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Physical,
        };
    }

    /// <summary>스카디 일반공격 — INT 기반 위력 40 + 얼음 원소 부착. 비접촉·특수.</summary>
    public sealed class SkadiNormalAttack : BaseNormalCode
    {
        public SkadiNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 40;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Special,
        };

        protected override IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            // 투사체 도착 시점에 맞춰 부착한다. FireProjectile의 비행 시간과 같은 값을 쓴다.
            yield return new WaitForSeconds(0.5f);
            if (target == null || !target.isActive) yield break;
            target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
        }
    }

    /// <summary>쿠베라 일반공격 — STR 기반 위력 70. 접촉·물리.</summary>
    public sealed class KuberaNormalAttack : BaseNormalCode
    {
        public KuberaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 70;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical,
        };
    }

    /// <summary>바루나 일반공격 — CON 기반 위력 60. 비접촉·특수.</summary>
    public sealed class VarunaNormalAttack : BaseNormalCode
    {
        public VarunaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Special,
        };
    }

    /// <summary>오르페우스 일반공격 — LUK 기반 위력 50. 비접촉·특수.</summary>
    public sealed class OrpheusNormalAttack : BaseNormalCode
    {
        public OrpheusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 50;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.LUK) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.NonContactAttack, DamageTag.Special,
        };
    }

    /// <summary>
    /// 스사노오 일반공격 — DEX 기반 위력 60. 접촉·물리·베기.
    /// 원안에 있던 `#추가공격`은 붙이지 않는다. 추가공격은 정의상 기본 행동과 별개로 나가는 공격이라
    /// 기본 행동 그 자체인 일반공격과는 상호배타다.
    /// </summary>
    public sealed class SusanooNormalAttack : BaseNormalCode
    {
        public SusanooNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical, DamageTag.Slash,
        };
    }
}
