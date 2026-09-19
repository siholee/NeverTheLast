using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 프레이아 일반행동 — 유일하게 <b>적을 때리지 않는 일반행동</b>이다.
    /// 아군 전체를 고정 300 + CON 기반 위력 50만큼 치유한다.
    ///
    /// 치유가 주스탯을 타므로 최대 체력과 치유량이 같은 축에서 자란다.
    /// 체력을 태우는 아군에게 얼마를 돌려주는지가 레벨이 올라도 흔들리지 않는다.
    ///
    /// 대상이 아군이므로 <see cref="BaseNormalCode"/>의 적 타겟팅 흐름을 그대로 쓸 수 없다.
    /// 시전 지연과 마나 회복만 공유하고 해결부는 새로 짠다.
    /// </summary>
    public sealed class FreyaNormalAttack : BaseNormalCode
    {
        private const int HealPower = 50;

        /// <summary>
        /// 치유의 고정 몫. 예전에는 CON 비례(위력 60)뿐이라 1레벨 치유가 약 320 — 수르트 최대 체력의 11%로,
        /// 수르트가 행동마다 태우는 15%도 메우지 못했다. 고정 200을 깔고 계수를 조금 줄여
        /// 초반을 받치고, 후반 증가는 계수가 맡는다. 추가 QA에서 저점 보강이 더 필요해
        /// 고정 몫을 300으로 올렸다.
        /// </summary>
        private const int HealFlat = 300;

        public FreyaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
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

            int heal = Mathf.Max(1, HealFlat + Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON));
            foreach (Unit ally in Allies())
            {
                ally.ModifyHp(ally.HpCurr + heal, Caster);
            }

            Debug.Log($"[프레이아] 아군 전체를 {heal}만큼 치유했습니다.");
            NotifyActionResolved();
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

    /// <summary>로키 일반행동 — STR 기반 위력 80. 비접촉·물리.</summary>
    public sealed class LokiNormalAttack : BaseNormalCode
    {
        public LokiNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 90;
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

    /// <summary>
    /// 스카디 N / N+.
    /// 방어막이 있으면 단일 적에게 고정 위력 80 + STR×0.5의 접촉 물리 피해를 입힌다.
    /// 방어막이 없으면 공격 대신 자신에게 방어막을 부여한다. 전열 우선도는 공용 진형 규칙이
    /// 맡으며, 도발은 육성으로 별도 패시브를 배웠을 때만 생긴다.
    /// </summary>
    public sealed class SkadiNormalAttack : BaseNormalCode
    {
        private const int NormalFlatPower = 80;
        private const float NormalStrCoefficient = 0.5f;
        private const float ShieldFlat = 100f;
        private const float ShieldStrCoefficient = 1.2f;

        private bool _substitute;

        public SkadiNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = NormalFlatPower;
            PowerStatCoefficient = NormalStrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        public override void CastCode()
        {
            _substitute = Caster != null && Caster.ShieldCurr <= 0;
            CodeName = _substitute ? "대체행동" : "일반행동";
            base.CastCode();
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (!_substitute)
            {
                yield return base.SkillCoroutine();
                yield break;
            }

            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            int shield = Mathf.Max(1, Mathf.RoundToInt(
                ShieldFlat + Caster.GetBaseStr() * ShieldStrCoefficient));
            Caster.AddShield(shield, Caster);

            NotifyActionResolved();
            StopCode();
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical,
        };

        // 방어막이 없으면 적이 없어도 방어행동은 성립한다.
        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && (Caster.ShieldCurr <= 0 || base.HasValidTarget());
    }

    /// <summary>
    /// 펜리르 N(501) — <b>체력이 가장 낮은</b> 적을 문다. STR 위력 40. 접촉·물리·소환수.
    /// 우선도가 아니라 현재 체력으로 고르는 것이 펜리르의 정체성이다(마무리를 물어뜯는다).
    /// </summary>
    public sealed class FenrirBite : BaseNormalCode
    {
        public FenrirBite(NormalCodeContext context) : base(context)
        {
            CodeName = "물어뜯기";
            Power = 40;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override List<Unit> SelectTarget()
        {
            Unit prey = GetAvailableEnemies().OrderBy(unit => unit.HpCurr).FirstOrDefault();
            return prey != null ? new List<Unit> { prey } : new List<Unit>();
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR)
                * critMultiplier * Combat.Summons.DamageMultiplier(Caster.SummonOwner)));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.SummonAttack, DamageTag.ContactAttack, DamageTag.Physical,
        };
    }

    /// <summary>쿠베라 일반행동 — STR 기반 위력 70. 접촉·물리.</summary>
    public sealed class KuberaNormalAttack : BaseNormalCode
    {
        public KuberaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
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

    /// <summary>바루나 일반행동 — CON 기반 위력 60. 비접촉·특수.</summary>
    public sealed class VarunaNormalAttack : BaseNormalCode
    {
        public VarunaNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
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

    /// <summary>오르페우스 일반행동 — LUK 기반 위력 50. 비접촉·특수.</summary>
    public sealed class OrpheusNormalAttack : BaseNormalCode
    {
        public OrpheusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
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
    /// 스사노오 일반행동 — DEX 기반 위력 60. 접촉·물리·베기.
    /// 원안에 있던 `#추가행동`은 붙이지 않는다. 추가행동은 정의상 기본 행동과 별개로 나가는 공격이라
    /// 기본 행동 그 자체인 일반행동과는 상호배타다.
    /// </summary>
    public sealed class SusanooNormalAttack : BaseNormalCode
    {
        public SusanooNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 90;
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
