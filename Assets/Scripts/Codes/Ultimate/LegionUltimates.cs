using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    public enum LegionUltimateStyle
    {
        Hastati,
        Principes,
        Triarii,
        Centurion,
        Velites,
        Scorpio,
        Eques,
        Optio,
        Tribune,
        Agrippa,
        Octavia,
        Caesar,
    }

    /// <summary>
    /// '로마' 테마 병종의 궁극기. 전열은 단일 대상 강타, 후열은 지원과 광역을 맡는다.
    /// 콜로세움과 달리 <b>보호막·마나·행동 게이지</b>를 다루는 지휘형 궁극기가 섞여 있다.
    ///
    /// 위력은 <b>고정값 + 주스탯 × 계수</b> 형식이다.
    /// 적과 아군(아그리파·옥타비아·카이사르)이 같은 구현을 공유한다.
    /// </summary>
    public sealed class LegionUltimate : UltimateCode
    {
        /// <summary>주사위는 던져졌다 — 아군이 치명타를 낼 때 고정 피해를 더하는 표식.</summary>
        public const string AleaIactaEstKey = "legion_alea_iacta_est";

        private readonly LegionUltimateStyle _style;

        public LegionUltimate(UltimateCodeContext context, LegionUltimateStyle style) : base(context)
        {
            _style = style;
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = style switch
            {
                LegionUltimateStyle.Hastati => "필룸 투척",
                LegionUltimateStyle.Principes => "전열 돌파",
                LegionUltimateStyle.Triarii => "최후의 창",
                LegionUltimateStyle.Centurion => "백인대 돌격",
                LegionUltimateStyle.Velites => "일제 투창",
                LegionUltimateStyle.Scorpio => "관통 사격",
                LegionUltimateStyle.Eques => "쌍기창",
                LegionUltimateStyle.Optio => "방벽 지시",
                LegionUltimateStyle.Tribune => "천부장의 방벽",
                LegionUltimateStyle.Agrippa => "제 2의 건국자",
                LegionUltimateStyle.Octavia => "존엄한 자",
                LegionUltimateStyle.Caesar => "주사위는 던져졌다",
                _ => "군단의 비기",
            };
            CastingDelay = style == LegionUltimateStyle.Octavia ? 0.9f : 0.65f;
            MaxStage = 1;

            (int flat, float coefficient) = PowerOf(style);
            Power = flat;
            PowerStatCoefficient = coefficient;
            PowerStat = StatOf(style);
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        /// <summary>(고정값, 주스탯 계수).</summary>
        private static (int, float) PowerOf(LegionUltimateStyle style) => style switch
        {
            LegionUltimateStyle.Hastati => (80, 0.4f),
            LegionUltimateStyle.Principes => (90, 0.4f),
            LegionUltimateStyle.Triarii => (100, 0.4f),
            LegionUltimateStyle.Centurion => (120, 0.4f),
            LegionUltimateStyle.Velites => (120, 0.6f),
            LegionUltimateStyle.Scorpio => (120, 0.6f),
            LegionUltimateStyle.Eques => (0, 1.2f),
            LegionUltimateStyle.Optio => (0, 1.2f),
            LegionUltimateStyle.Tribune => (0, 1.6f),
            LegionUltimateStyle.Agrippa => (0, 0f),
            // 가상 마나 리워크 뒤에도 단독 딜러 평탄화 허용 범위(+20%) 안에서
            // 실전 저점을 보완한다. 기존 61+INT×0.51 대비 약 1.5배다.
            LegionUltimateStyle.Octavia => (92, 0.77f),
            LegionUltimateStyle.Caesar => (0, 0.1f),
            _ => (0, 0f),
        };

        private static BaseEnums.PrimaryStat StatOf(LegionUltimateStyle style) => style switch
        {
            LegionUltimateStyle.Eques => BaseEnums.PrimaryStat.DEX,
            LegionUltimateStyle.Optio => BaseEnums.PrimaryStat.INT,
            LegionUltimateStyle.Tribune => BaseEnums.PrimaryStat.INT,
            LegionUltimateStyle.Octavia => BaseEnums.PrimaryStat.INT,
            LegionUltimateStyle.Caesar => BaseEnums.PrimaryStat.LUK,
            _ => BaseEnums.PrimaryStat.STR,
        };

        public override void CastCode()
        {
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
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

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;

            switch (_style)
            {
                case LegionUltimateStyle.Hastati:
                case LegionUltimateStyle.Principes:
                case LegionUltimateStyle.Velites:
                    StrikeSingle(isCrit, critMultiplier, BaseEnums.UnitElement.None);
                    break;
                case LegionUltimateStyle.Triarii:
                case LegionUltimateStyle.Centurion:
                    // 바위 원소를 부여한다. 이미 바위가 붙어 있으면 진동이 터진다.
                    StrikeSingle(isCrit, critMultiplier, BaseEnums.UnitElement.Geo);
                    break;
                case LegionUltimateStyle.Scorpio:
                    StrikeSingle(isCrit, critMultiplier, BaseEnums.UnitElement.None, defenseIgnore: 0.8f);
                    break;
                case LegionUltimateStyle.Eques:
                    ResolveEques(isCrit, critMultiplier);
                    break;
                case LegionUltimateStyle.Optio:
                case LegionUltimateStyle.Tribune:
                    ResolvePartyShield();
                    break;
                case LegionUltimateStyle.Agrippa:
                    ResolveAgrippa();
                    break;
                case LegionUltimateStyle.Octavia:
                    ResolveOctavia(isCrit, critMultiplier);
                    break;
                case LegionUltimateStyle.Caesar:
                    ResolveCaesar();
                    break;
            }

            StopCode();
        }

        // ── 단일 강타 ────────────────────────────────────────────────

        private void StrikeSingle(
            bool isCrit, float critMultiplier, BaseEnums.UnitElement element, float defenseIgnore = 1f)
        {
            Unit target = HighestPriorityEnemy();
            if (target == null) return;

            DealDamage(target, isCrit, critMultiplier, defenseIgnore: defenseIgnore);
            if (element != BaseEnums.UnitElement.None && target.isActive)
            {
                target.GrantCombatElement(element, 5, Caster);
            }
        }

        private void ResolveEques(bool isCrit, float critMultiplier)
        {
            // 서로 다른 적 둘에게 하나씩 꽂는다. 하나뿐이면 그 하나만 맞는다.
            foreach (Unit target in ColosseumCombat.Enemies(Caster)
                         .OrderByDescending(unit => unit.Priority)
                         .Take(2)
                         .ToList())
            {
                DealDamage(target, isCrit, critMultiplier, multiTarget: true);
            }
        }

        // ── 지휘형 ───────────────────────────────────────────────────

        private void ResolvePartyShield()
        {
            int shield = Mathf.Max(1, Caster.SkillDamage(CurrentPower, StatOf(_style)));
            foreach (Unit ally in ColosseumCombat.Allies(Caster))
            {
                ally.AddShield(shield, Caster);
            }
        }

        /// <summary>
        /// 제 2의 건국자 — 아군 전체의 궁극기 자원을 최대치의 20% 채운다.
        /// '화합' 표식을 들고 있는 아군은 두 배(40%)를 받는다.
        /// </summary>
        private void ResolveAgrippa()
        {
            foreach (Unit ally in ColosseumCombat.Allies(Caster))
            {
                if (ally.UltimateResourceType != BaseEnums.UltimateResourceType.Mana) continue;

                float ratio = ally.HasStatusKey(LegionCombat.ConcordiaKey) ? 0.4f : 0.2f;
                ally.AddUltimateResource(Mathf.Max(1, Mathf.RoundToInt(ally.ManaMax * ratio)));
            }
        }

        private void ResolveOctavia(bool isCrit, float critMultiplier)
        {
            foreach (Unit target in ColosseumCombat.Enemies(Caster))
            {
                DealDamage(target, isCrit, critMultiplier, multiTarget: true);
            }
        }

        /// <summary>
        /// 주사위는 던져졌다 — 아군 전체의 행동 게이지를 100% 당기고,
        /// 카이사르 기준 2턴(독재관 보유 시 3턴) 동안 아군의 치명타에 고정 피해를 더한다.
        /// </summary>
        private void ResolveCaesar()
        {
            int duration = ColosseumCombat.HasPassive(Caster, 317) ? 3 : 2;
            int bonusDamage = Mathf.Max(1, Caster.SkillDamage(CurrentPower, StatOf(_style)));

            foreach (Unit ally in ColosseumCombat.Allies(Caster))
            {
                Managers.GameManager.Instance?.ActionScheduler.AdvanceAction(ally, 1f);
                ColosseumCombat.AddStatus(
                    Caster, ally, 6371, AleaIactaEstKey, CodeName,
                    new AleaIactaEstEffect(bonusDamage), duration,
                    beneficial: true,
                    description: $"치명타를 낼 때마다 {bonusDamage}의 고정 피해가 추가됩니다.");
            }
        }

        // ── 공통 ─────────────────────────────────────────────────────

        private Unit HighestPriorityEnemy()
        {
            return ColosseumCombat.Enemies(Caster)
                .OrderByDescending(unit => unit.Priority)
                .FirstOrDefault();
        }

        private void DealDamage(
            Unit target,
            bool isCrit,
            float critMultiplier,
            bool multiTarget = false,
            float defenseIgnore = 1f)
        {
            if (target == null || !target.isActive) return;

            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, StatOf(_style)) * critMultiplier));

            bool contact = _style is LegionUltimateStyle.Hastati or LegionUltimateStyle.Principes
                or LegionUltimateStyle.Triarii or LegionUltimateStyle.Centurion;
            bool physical = contact || _style is LegionUltimateStyle.Velites or LegionUltimateStyle.Scorpio;

            var tags = new List<int>
            {
                multiTarget ? DamageTag.MultiTarget : DamageTag.SingleTarget,
                DamageTag.UltAttack,
                contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
                physical ? DamageTag.Physical : DamageTag.Special,
            };
            if (contact) tags.Add(DamageTag.Pierce);
            if (LegionAttack.UsesMindsEye(Caster, target)) tags.Add(DamageTag.DurabilityPenetration);

            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit);

            // 관통 사격은 방어력 일부를 무시한다. 상태로 태우지 않고 이 한 방에만 배율을 건다.
            if (defenseIgnore < 1f)
            {
                context.DefenseStatMultiplier = defenseIgnore;
            }

            target.TakeDamage(context);
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            if (Caster == null || !Caster.isActive) return false;

            // 지휘형 궁극기는 적이 없어도 아군만 있으면 쓸 수 있다.
            if (_style is LegionUltimateStyle.Optio or LegionUltimateStyle.Tribune
                or LegionUltimateStyle.Agrippa or LegionUltimateStyle.Caesar)
            {
                return ColosseumCombat.Allies(Caster).Count > 0;
            }

            return ColosseumCombat.Enemies(Caster).Count > 0;
        }
    }

    /// <summary>치명타 적중마다 고정 피해를 얹는다. 카이사르의 궁극기가 부여한다.</summary>
    internal sealed class AleaIactaEstEffect : BaseEffect
    {
        private readonly int _bonusDamage;
        private System.Action<DamageResolvedContext> _handler;

        public override bool IsBeneficial => true;

        public AleaIactaEstEffect(int bonusDamage) : base(0, bonusDamage) => _bonusDamage = bonusDamage;

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker != Target || context.Target == null) return;
                if (context.DamageContext == null || !context.DamageContext.IsCrit) return;
                // 추가 고정 피해가 다시 자기 자신을 부르지 않도록 막는다.
                if (context.DamageContext.DamageTags != null &&
                    context.DamageContext.DamageTags.Contains(DamageTag.TrueDamage)) return;

                context.Target.TakeDamage(new DamageContext(
                    Target, _bonusDamage, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage, DamageTag.Physical }));
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }
}
