using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    public enum LegionNormalStyle
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
    /// '로마' 테마 병종의 일반행동. 적과 아군(아그리파·옥타비아·카이사르)이 함께 쓴다.
    ///
    /// 위력은 <b>고정값 + 주스탯 × 계수</b> 형식이다.
    /// 고정값이 저점을 보장하고 계수가 작아 고점이 억제된다.
    ///
    /// 아그리파만 예외로 피해를 주지 않고 아군을 강화한다(서포터).
    /// </summary>
    public sealed class LegionNormal : BaseNormalCode
    {
        private readonly LegionNormalStyle _style;

        public LegionNormal(NormalCodeContext context, LegionNormalStyle style) : base(context)
        {
            _style = style;
            CodeName = "일반행동";
            CastingDelay = 0.4f;
            MaxStage = 1;

            (int flat, float coefficient) = PowerOf(style);
            Power = flat;
            PowerStatCoefficient = coefficient;
            PowerStat = StatOf(style);
        }

        /// <summary>(고정값, 주스탯 계수).</summary>
        private static (int, float) PowerOf(LegionNormalStyle style) => style switch
        {
            LegionNormalStyle.Hastati => (50, 0.2f),
            LegionNormalStyle.Principes => (62, 0.2f),
            LegionNormalStyle.Triarii => (75, 0.2f),
            LegionNormalStyle.Centurion => (100, 0.2f),
            LegionNormalStyle.Velites => (80, 0.4f),
            LegionNormalStyle.Scorpio => (80, 0.4f),
            LegionNormalStyle.Eques => (0, 0.6f),
            LegionNormalStyle.Optio => (0, 0.6f),
            LegionNormalStyle.Tribune => (0, 0.8f),
            LegionNormalStyle.Agrippa => (0, 0f),
            LegionNormalStyle.Octavia => (0, 0.9f),
            LegionNormalStyle.Caesar => (0, 0.6f),
            _ => (50, 0f),
        };

        internal static BaseEnums.PrimaryStat StatOf(LegionNormalStyle style) => style switch
        {
            LegionNormalStyle.Eques => BaseEnums.PrimaryStat.DEX,
            LegionNormalStyle.Optio => BaseEnums.PrimaryStat.DEX,
            LegionNormalStyle.Tribune => BaseEnums.PrimaryStat.DEX,
            LegionNormalStyle.Octavia => BaseEnums.PrimaryStat.INT,
            LegionNormalStyle.Caesar => BaseEnums.PrimaryStat.LUK,
            _ => BaseEnums.PrimaryStat.STR,
        };

        public override void CastCode()
        {
            // 아그리파는 적을 때리지 않는다. 아군 하나를 강화하고 턴을 마친다.
            if (_style == LegionNormalStyle.Agrippa)
            {
                CastAgrippaSupport();
                return;
            }

            base.CastCode();
        }

        public override bool HasValidTarget()
        {
            if (_style != LegionNormalStyle.Agrippa) return base.HasValidTarget();
            return Caster != null && Caster.isActive && AgrippaTarget() != null;
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, StatOf(_style)) * critMultiplier));
        }

        protected override List<int> GetDamageTags()
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack };

            bool contact = _style is LegionNormalStyle.Hastati
                or LegionNormalStyle.Principes
                or LegionNormalStyle.Triarii
                or LegionNormalStyle.Centurion
                or LegionNormalStyle.Optio
                or LegionNormalStyle.Tribune
                or LegionNormalStyle.Caesar;
            bool physical = contact
                || _style is LegionNormalStyle.Velites or LegionNormalStyle.Scorpio;

            tags.Add(contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack);
            tags.Add(physical ? DamageTag.Physical : DamageTag.Special);

            if (_style is LegionNormalStyle.Hastati or LegionNormalStyle.Principes
                or LegionNormalStyle.Triarii or LegionNormalStyle.Centurion)
            {
                tags.Add(DamageTag.Pierce);
            }
            if (_style is LegionNormalStyle.Optio or LegionNormalStyle.Tribune)
            {
                tags.Add(DamageTag.Slash);
            }

            // 심안: 원소가 붙은 적에게는 내구를 무시한다. 원소 삭제는 패시브 쪽이 맡는다.
            if (LegionAttack.UsesMindsEye(Caster, CurrentNormalTarget()))
            {
                tags.Add(DamageTag.DurabilityPenetration);
            }

            return tags;
        }

        private Unit CurrentNormalTarget() => Caster?.currentNormalTarget;

        // ── 아그리파 ─────────────────────────────────────────────────

        /// <summary>
        /// 필드에서 INT가 가장 높은 아군. '최고의 2인자'를 들고 있고 옥타비아가 있으면
        /// INT와 무관하게 옥타비아를 지목한다.
        /// </summary>
        private Unit AgrippaTarget()
        {
            Unit preferred = LegionAttack.SecondInCommandTarget(Caster);
            if (preferred != null) return preferred;

            return global::Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit.isActive)
                .Concat(new[] { Caster })
                .Distinct()
                .OrderByDescending(unit => unit.GetBaseInt())
                .FirstOrDefault();
        }

        private void CastAgrippaSupport()
        {
            Unit target = AgrippaTarget();
            if (target == null) return;

            target.AddStatus(BuffStatus.Create(
                6269, $"legion_agrippa_focus_{Caster.GetEntityId()}", "집중 지휘",
                Caster, target, new CritMultiplierBonusEffect(0.2f),
                duration: 1,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "1턴간 치명타 피해가 20% 증가합니다."));

            // 최고의 2인자 — 지목이 옥타비아였다면 행동 게이지를 100% 당긴다.
            if (LegionAttack.SecondInCommandTarget(Caster) == target)
            {
                SecondInCommandEffect.PushForward(target);
            }

            // 피해가 없으므로 코루틴 없이 즉시 끝난다. OnNormalActivates는 Unit.CastNormalCode가 발행한다.
        }
    }

    /// <summary>레기온 공격 코드가 함께 쓰는 판정.</summary>
    internal static class LegionAttack
    {
        public const int MindsEyeCodeId = 1219;

        /// <summary>심안을 들고 있고 대상에게 원소가 붙어 있으면 내구를 무시한다.</summary>
        public static bool UsesMindsEye(Unit caster, Unit target)
        {
            return caster != null && target != null &&
                   caster.HasLearnedPassiveCode(MindsEyeCodeId) &&
                   target.HasAnyCombatElement;
        }

        /// <summary>'최고의 2인자' 보유 시 필드의 옥타비아. 없으면 null.</summary>
        public static Unit SecondInCommandTarget(Unit caster)
        {
            if (caster == null || !caster.HasLearnedPassiveCode(1212)) return null;

            return global::Target.GetAllAllies(caster)
                .FirstOrDefault(unit => unit != null && unit.isActive &&
                                        unit.ID == SecondInCommandEffect.OctaviaEnemyId);
        }
    }
}
