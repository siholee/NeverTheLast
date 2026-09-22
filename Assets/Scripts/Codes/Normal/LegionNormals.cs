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
            LegionNormalStyle.Octavia => (37, 0.31f),
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

        /// <summary>
        /// 병종이 두르는 원소를 부착한다.
        ///
        /// 바위는 겹치면 <b>진동</b>(기절), 바람은 <b>확산</b>으로 진영 전체에 원소를 퍼뜨린다.
        /// 군단의 전열이 바위를 쌓고 경보병이 그것을 퍼뜨리는 그림이다.
        ///
        /// <b>불 병종(에퀴테스·트리뷴)은 일부러 부착하지 않는다.</b> 불과 바위가 만나면
        /// 단조(DEX 증가)가 되는데, 버프 반응은 부착된 유닛이 가져가므로 아군을 이롭게 한다.
        /// </summary>
        private static BaseEnums.UnitElement AttachOf(LegionNormalStyle style) => style switch
        {
            LegionNormalStyle.Principes or LegionNormalStyle.Triarii or
            LegionNormalStyle.Centurion or LegionNormalStyle.Scorpio => BaseEnums.UnitElement.Geo,

            LegionNormalStyle.Hastati or LegionNormalStyle.Velites or
            LegionNormalStyle.Optio or LegionNormalStyle.Octavia or
            LegionNormalStyle.Caesar => BaseEnums.UnitElement.Anemo,

            _ => BaseEnums.UnitElement.None,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;

            BaseEnums.UnitElement element = AttachOf(_style);
            if (element == BaseEnums.UnitElement.None) return;
            target.GrantCombatElement(element, Unit.CommonElementAuraDuration, Caster);
        }

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
        /// 방출 아군을 먼저 고르고, 같은 조건에서는 마나 최대치와 INT 순으로 고른다.
        /// '최고의 2인자'를 들고 있고 옥타비아가 있으면 이 규칙보다 먼저 옥타비아를 지목한다.
        /// </summary>
        private Unit AgrippaTarget()
            => SelectAgrippaSupportTarget(Caster);

        internal static Unit SelectAgrippaSupportTarget(Unit caster)
        {
            Unit preferred = LegionAttack.SecondInCommandTarget(caster);
            if (preferred != null) return preferred;

            return global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive)
                .Concat(new[] { caster })
                .Distinct()
                .OrderByDescending(IsBurstUnit)
                .ThenByDescending(unit => unit.UltimateResourceType == BaseEnums.UltimateResourceType.Mana
                    ? unit.ManaMax
                    : 0)
                .ThenByDescending(unit => unit.GetBaseInt())
                .FirstOrDefault();
        }

        private static bool IsBurstUnit(Unit unit)
        {
            if (unit == null || Managers.GameManager.Instance?.unitDataList?.units == null) return false;
            Managers.UnitData definition = Managers.GameManager.Instance.unitDataList.units
                .FirstOrDefault(data => data.id == unit.ID);
            return definition?.archetypeTags?.Any(tag =>
                string.Equals(tag, "Burst", System.StringComparison.OrdinalIgnoreCase)) == true;
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
            NotifyActionResolved();
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
