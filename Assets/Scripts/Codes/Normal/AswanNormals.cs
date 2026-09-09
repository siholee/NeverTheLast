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
    /// <summary>
    /// 파멸·종말의 성기사 N — 단일 적에게 STR 기반 참격.
    /// 두손검을 든 전열 병종이라 접촉·물리·베기로 나간다. 화형 심판(360·361)의 방어 무시가 여기에 얹힌다.
    /// </summary>
    public sealed class AswanPaladinSlash : BaseNormalCode
    {
        public AswanPaladinSlash(NormalCodeContext context, int power) : base(context)
        {
            CodeName = "일반행동";
            Power = power;
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };
    }

    /// <summary>
    /// 이단심문관 계열 N — 적을 때리지 않고 <b>아군 전체를 CON 기반으로 치유한다.</b>
    /// 프레이아와 같은 이유로 <see cref="BaseNormalCode"/>의 적 타겟팅 흐름을 쓰지 않고 해결부만 새로 짠다.
    /// </summary>
    public sealed class AswanInquisitorPrayer : BaseNormalCode
    {
        public AswanInquisitorPrayer(NormalCodeContext context, int power) : base(context)
        {
            CodeName = "일반행동";
            Power = power;
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

            int heal = Mathf.Max(1, Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON));
            foreach (Unit ally in AswanCombat.AlliesIncludingSelf(Caster))
            {
                ally.ModifyHp(ally.HpCurr + heal, Caster);
            }

            Debug.Log($"[{Caster.UnitName}] 아군 전체를 {heal}만큼 치유했습니다.");
            StopCode();
        }

        /// <summary>치유는 적이 없어도 유효하다. 아군이 하나라도 살아 있으면 시전한다.</summary>
        public override bool HasValidTarget() =>
            Caster != null && Caster.isActive && AswanCombat.AlliesIncludingSelf(Caster).Count > 0;
    }

    /// <summary>명계의 사령 N — 단일 적에게 LUK×0.9의 피해를 입히고 출혈을 부여한다.</summary>
    public sealed class AswanWraithClaw : BaseNormalCode
    {
        private const int BleedTurns = 5;

        public AswanWraithClaw(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 90; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(90, BaseEnums.PrimaryStat.LUK) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            if (!AswanCombat.TryApplyBleed(Caster, target, BleedTurns))
            {
                Debug.Log($"[명계의 사령] {target.UnitName}이(가) 출혈에 저항했습니다.");
            }
        }
    }

    /// <summary>타락한 사제 N — 단일 적에게 INT×0.8의 피해를 입히고 불 원소를 부착한다.</summary>
    public sealed class AswanFallenPriestRite : BaseNormalCode
    {
        public AswanFallenPriestRite(NormalCodeContext context) : base(context)
        { CodeName = "일반행동"; Power = 80; }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
            => AswanCombat.GrantPyro(Caster, target);
    }

    /// <summary>오시리스 N — 적 전체에게 INT×1.2의 피해를 입히고 바위 원소를 부여한다.</summary>
    public sealed class OsirisEarthRequiem : BaseNormalCode
    {
        public OsirisEarthRequiem(NormalCodeContext context) : base(context)
        { CodeName = "대지의 진혼"; Power = 120; }

        protected override List<Unit> SelectTarget() => AswanCombat.Enemies(Caster);

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(120, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.AllTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Geo, Unit.CommonElementAuraDuration, Caster);
        }
    }

    /// <summary>
    /// 아문·라 N / N+ — 칼날 세례.
    ///
    /// 기본형은 칼날 셋을 던져 서로 다른 적 3명을 때린다. 그 공격이 치명타로 터지면
    /// <b>대체행동(N+)</b>을 곧바로 추가행동으로 예약한다. 강화형은 단일 대상이며,
    /// '지옥불'(370)을 배웠다면 강화형이 끝난 뒤 '승천' 스택 수만큼 칼날을 더 날린다.
    ///
    /// N+를 따로 등록할 수 있도록 코드 ID 308도 이 클래스를 상시 강화형으로 만들어 준다.
    /// 예약으로 들어오는 강화형은 같은 인스턴스가 한 번만 강화 모드로 도는 방식이라
    /// 큐에 코드가 두 벌 올라가지 않는다.
    /// </summary>
    public sealed class AmunRaBladeVolley : BaseNormalCode
    {
        private const int BladeCount = 3;
        private const int BladePower = 180;

        private readonly bool _alwaysSubstitute;
        private bool _queuedSubstitute;
        private bool _substituteNow;

        public AmunRaBladeVolley(NormalCodeContext context, bool alwaysSubstitute = false) : base(context)
        {
            _alwaysSubstitute = alwaysSubstitute;
            CodeName = alwaysSubstitute ? "대체행동" : "일반행동";
            Power = BladePower;
        }

        public override void CastCode()
        {
            _substituteNow = _alwaysSubstitute || _queuedSubstitute;
            _queuedSubstitute = false;
            CodeName = _substituteNow ? "대체행동" : "일반행동";
            base.CastCode();
        }

        protected override List<Unit> SelectTarget()
        {
            List<Unit> enemies = AswanCombat.Enemies(Caster);
            if (enemies.Count == 0) return new List<Unit>();
            if (_substituteNow) return base.SelectTarget();

            // 칼날 셋은 서로 다른 적에게 간다. 적이 셋보다 적으면 있는 만큼만 날아간다.
            return enemies
                .OrderByDescending(unit => unit.Priority)
                .Take(BladeCount)
                .ToList();
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(BladePower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            _substituteNow ? DamageTag.SingleTarget : DamageTag.MultiTarget,
            DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack, DamageTag.Slash,
        };

        protected override IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            if (Caster == null || context == null) yield break;

            if (_substituteNow)
            {
                yield return FireHellfire();
                yield break;
            }

            // 치명타가 하나라도 터지면 대체행동을 즉시 큐에 예약한다.
            if (!context.IsCrit) yield break;

            _queuedSubstitute = true;
            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                Caster,
                $"amunra_substitute_{Caster.GetEntityId()}",
                "대체행동",
                Caster.CastNormalCode);
        }

        /// <summary>370 지옥불 — 대체행동 뒤 '승천' 스택만큼 무작위 단일 적에게 칼날을 더 날린다.</summary>
        private IEnumerator FireHellfire()
        {
            if (Caster == null || !Caster.HasStatus(AswanStatusIds.Hellfire)) yield break;

            int stacks = Caster.GetCombatResource(AswanCombat.AscensionResource);
            if (stacks <= 0) yield break;

            for (int shot = 0; shot < stacks; shot++)
            {
                List<Unit> enemies = AswanCombat.Enemies(Caster);
                if (enemies.Count == 0) yield break;

                Unit target = enemies[Random.Range(0, enemies.Count)];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(BladePower, BaseEnums.PrimaryStat.INT) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Normal,
                    new List<int>
                    {
                        DamageTag.SingleTarget, DamageTag.AdditionalAttack,
                        DamageTag.Special, DamageTag.NonContactAttack, DamageTag.Slash,
                    }, isCrit));

                // 연출 간격은 짧게 잡는다. 스택이 많이 쌓인 상태에서도
                // 스케줄러의 행동 감시 시간(8초)을 넘기지 않아야 한다.
                yield return new WaitForSeconds(0.05f);
            }

            Debug.Log($"[지옥불] {Caster.UnitName}이(가) 칼날 {stacks}발을 더 날렸습니다.");
        }
    }
}
