using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 라이트 N — 플라이어가 없으면 불러내고, 이미 있으면 아군 전체를 300 + CON×0.5 치유.
    ///
    /// 플라이어는 <b>칸을 차지하지 않는 실제 유닛</b>이다. 자기 DEX로 행동 순서를 얻고
    /// INT로 궁극기를 채우며 적에게 맞아 쓰러질 수도 있다 — 자세한 규칙은 <c>Combat.SummonSpec</c>.
    /// </summary>
    public sealed class LightNormalAttack : BaseNormalCode
    {
        private const int HealFlat = 300;
        private const float HealConCoefficient = 0.5f;

        private bool _summoning;

        public LightNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 0;
        }

        /// <summary>이 라이트가 지금 플라이어를 데리고 있는가.</summary>
        private bool HasFlyer
            => Caster != null &&
               Caster.ActiveSummons.Any(summon => summon != null && summon.isActive);

        public override void CastCode()
        {
            _summoning = !HasFlyer;
            CodeName = _summoning ? "플라이어 소환" : "전체 치유";
            base.CastCode();
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            if (_summoning)
            {
                GridManager.Instance?.SpawnSummon(Caster, SummonCatalog.Flyer(Caster));
            }
            else
            {
                int healing = Mathf.Max(1,
                    HealFlat + Mathf.RoundToInt(Caster.GetBaseCon() * HealConCoefficient));
                foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
                {
                    ally.ModifyHp(ally.HpCurr + healing, Caster);
                }
            }

            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>플라이어 N(500) — 단일 적에게 INT 위력 60. 소환수 공격이다.</summary>
    public sealed class FlyerNormalAttack : BaseNormalCode
    {
        public FlyerNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 60;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT)
                * critMultiplier * Summons.DamageMultiplier(Caster.SummonOwner)));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.SummonAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };
    }

    /// <summary>
    /// 가우디 N / N+.
    /// 기본은 단일 INT×0.6, 6스택에서는 우선도 기반으로 서로 다른 대상 최대 3인을 뽑아
    /// 각각 INT×0.8 피해 후 풀 원소를 부착한다.
    /// </summary>
    public sealed class GaudiNormalAttack : BaseNormalCode
    {
        private const int NormalPower = 10;
        private const float NormalIntCoefficient = 2.5f;
        private const int SubstitutePower = 20;
        private const float SubstituteIntCoefficient = 3.3f;
        private const int SubstituteTargetCount = 3;

        private GaudiSagradaFamilia _passive;
        private bool _substitute;

        public bool IsSubstituteAttack => _substitute;

        public GaudiNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = NormalPower;
            PowerStat = BaseEnums.PrimaryStat.INT;
            PowerStatCoefficient = NormalIntCoefficient;
            CodeTags = new List<int> { DamageTag.Special };
        }

        public override void CastCode()
        {
            _passive = Caster.ActivePassiveCodes.OfType<GaudiSagradaFamilia>().FirstOrDefault();
            SetSubstitute(_passive?.IsSubstitute == true);
            base.CastCode();
        }

        protected override List<Unit> SelectTarget()
        {
            if (!_substitute) return base.SelectTarget();

            List<Unit> remaining = GetAvailableEnemies();
            if (remaining.Count == 0) return new List<Unit>();
            if (_passive?.TryConsumeEmpowerment() != true)
            {
                SetSubstitute(false);
                return base.SelectTarget();
            }

            return CombatTargets.PickByPriority(remaining, SubstituteTargetCount);
        }

        /// <summary>강화 여부에 따라 이름과 위력을 함께 갈아 끼운다. 위력은 CurrentPower로만 읽는다.</summary>
        private void SetSubstitute(bool substitute)
        {
            _substitute = substitute;
            CodeName = substitute ? "대체행동" : "일반행동";
            Power = substitute ? SubstitutePower : NormalPower;
            PowerStatCoefficient = substitute ? SubstituteIntCoefficient : NormalIntCoefficient;
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        // 강화형은 서로 다른 적 최대 3인을 때린다. 대상 범위 태그를 단일로 두면
        // '단일 대상 피해'를 조건으로 삼는 코드(부관의 신호 등)가 잘못 발동한다.
        protected override List<int> GetDamageTags() => new()
        {
            _substitute ? DamageTag.MultiTarget : DamageTag.SingleTarget,
            DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (_substitute && target != null && target.isActive)
                target.GrantCombatElement(BaseEnums.UnitElement.Dendro, Unit.CommonElementAuraDuration, Caster);
        }
    }

    public sealed class AsclepiusNormalAttack : BaseNormalCode
    {
        public AsclepiusNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 48;
            PowerStatCoefficient = 0.3f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };
    }

    public sealed class AmaterasuNormalAttack : BaseNormalCode
    {
        public AmaterasuNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            Power = 43;
            PowerStatCoefficient = 0.4f;
            PowerStat = BaseEnums.PrimaryStat.DEX;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Physical, DamageTag.NonContactAttack,
        };
    }
}
