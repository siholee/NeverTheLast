using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 바유 — 특성 「바람이 남긴 길」(Detail_08 §14.2). 바유가 <b>자기 확산</b>으로 불 · 물 · 풀 · 번개 · 얼음 · 바위를 퍼뜨리면
    /// 원래 대상과 실제로 전달받은 적에게 그 원소의 취약(+15%, 대상 2턴)을 남긴다.
    ///
    /// 취약은 <b>공격자의 고유 원소</b>로 판정한다 — 불 취약이면 불 속성 유닛의 직접 공격이 세진다. 공격이 실제로 불을 붙일 필요는 없다.
    /// 확산 판정 · 부착 · 반응 우선순위는 건드리지 않고 <see cref="ElementalReaction.AnySpreadResolved"/>의 결과만 읽는다.
    /// </summary>
    public sealed class VayuTailwind : UniquePassiveCode
    {
        private bool _subscribed;

        public VayuTailwind(PassiveCodeContext context) : base(context) => CodeName = "바람이 남긴 길";

        public override void CastCode()
        {
            if (Caster == null || _subscribed) return;
            ElementalReaction.AnySpreadResolved += OnSpreadResolved;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (!_subscribed) return;
            ElementalReaction.AnySpreadResolved -= OnSpreadResolved;
            _subscribed = false;
        }

        private void OnSpreadResolved(ElementalReaction.SpreadResult result)
        {
            if (Caster == null || result == null || result.Source != Caster) return;
            if (!VayuVulnerability.IsSpreadElement(result.Element)) return;

            // 원래 대상은 확산으로 두 원소를 잃었다가 다시 받는다. 실제 전달 목록에 없어도 취약은 남긴다.
            var targets = new List<Unit> { result.OriginalTarget };
            targets.AddRange(result.Delivered);
            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive && unit.IsEnemy != Caster.IsEnemy).Distinct())
                VayuVulnerability.Apply(Caster, target, result.Element, gale: false, effectHitResolved: true);
        }
    }

    /// <summary>
    /// 바유의 원소 취약(Detail_08 §14.1). 원소마다 <b>기본(특성 15%)</b>과 <b>태풍의 눈(25%)</b> 두 키를 따로 두고,
    /// 같은 원소에서는 높은 하나만 적용한다(15 × 25로 겹치지 않는다). 다른 원소의 취약은 공존한다.
    /// 다시 걸면 같은 키가 지속을 새로 고친다. 해로운 상태라 효과 명중 · 상태 저항 공통 규칙을 그대로 따른다.
    /// </summary>
    public static class VayuVulnerability
    {
        public const int BaseStatusId = 70642;
        public const int GaleStatusId = 70643;
        public const float BaseBonus = 0.15f;
        public const float GaleBonus = 0.25f;
        public const int Duration = 2;

        /// <summary>태풍의 눈이 거는 일곱 원소. None은 원소가 아니다.</summary>
        public static readonly BaseEnums.UnitElement[] AllElements =
        {
            BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Dendro,
            BaseEnums.UnitElement.Electro, BaseEnums.UnitElement.Cryo, BaseEnums.UnitElement.Geo,
            BaseEnums.UnitElement.Anemo,
        };

        /// <summary>확산이 실제로 퍼뜨릴 수 있는 여섯 원소. 바람 자신과 None은 아니다.</summary>
        public static bool IsSpreadElement(BaseEnums.UnitElement element)
            => element != BaseEnums.UnitElement.None && element != BaseEnums.UnitElement.Anemo;

        public static string BaseKey(BaseEnums.UnitElement element) => $"vayu_vuln_{element}";
        public const string GaleAllKey = "vayu_gale_all";
        public static string GaleKey(BaseEnums.UnitElement element) => GaleAllKey;

        /// <summary>전 속성 취약은 대상당 상태·명중을 한 번만 만든다. 재부여는 같은 상태의 지속을 갱신한다.</summary>
        public static bool ApplyAll(Unit caster, Unit target)
        {
            if (caster == null || target == null || !target.isActive) return false;
            UnitStatus status = BuffStatus.Create(GaleStatusId, GaleAllKey, "전 속성 취약", caster, target,
                new ElementalVulnerabilityEffect(BaseEnums.UnitElement.None, GaleBonus, allElements: true),
                duration: Duration, stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "원소 속성 유닛의 직접 공격에 받는 피해 +25%. 무원소·지속피해·반응은 제외. 같은 원소 취약은 높은 하나만 적용.");
            target.AddStatus(status);
            return target.GetAllStatuses().Contains(status);
        }

        /// <returns>상태가 실제로 붙었는가(명중 · 저항에 막히면 false).</returns>
        public static bool Apply(Unit caster, Unit target, BaseEnums.UnitElement element, bool gale, bool effectHitResolved = false)
        {
            if (caster == null || target == null || !target.isActive || element == BaseEnums.UnitElement.None) return false;
            if (gale) return ApplyAll(caster, target);
            float bonus = BaseBonus;
            string key = gale ? GaleKey(element) : BaseKey(element);
            string name = $"{ElementName(element)} 취약";
            UnitStatus status = BuffStatus.Create(gale ? GaleStatusId : BaseStatusId, key, name, caster, target,
                new ElementalVulnerabilityEffect(element, bonus), duration: Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace, category: BaseEnums.StatusCategory.Negative,
                description: $"{ElementName(element)} 속성 유닛의 직접 공격에 받는 피해 +{Mathf.RoundToInt(bonus * 100f)}%. " +
                             "같은 원소의 취약은 가장 높은 하나만 적용한다.");
            status.SkipEffectHitRoll = effectHitResolved;
            return target.TryAddStatus(status);
        }

        /// <summary>이 원소로 지금 적용되는 취약(가장 높은 하나). 없으면 0.</summary>
        public static float BonusFor(Unit target, BaseEnums.UnitElement element)
            => Strongest(target, element)?.Bonus ?? 0f;

        internal static ElementalVulnerabilityEffect Strongest(Unit target, BaseEnums.UnitElement element)
        {
            if (target == null || element == BaseEnums.UnitElement.None) return null;
            ElementalVulnerabilityEffect best = null;
            foreach (BaseEffect effect in target.CombatEffects())
            {
                if (effect is not ElementalVulnerabilityEffect vulnerability || !vulnerability.Matches(element)) continue;
                if (best == null || vulnerability.Bonus > best.Bonus) best = vulnerability;
            }
            return best;
        }

        public static string ElementName(BaseEnums.UnitElement element) => element switch
        {
            BaseEnums.UnitElement.Pyro => "불",
            BaseEnums.UnitElement.Hydro => "물",
            BaseEnums.UnitElement.Dendro => "풀",
            BaseEnums.UnitElement.Electro => "번개",
            BaseEnums.UnitElement.Cryo => "얼음",
            BaseEnums.UnitElement.Geo => "바위",
            BaseEnums.UnitElement.Anemo => "바람",
            _ => element.ToString(),
        };
    }

    /// <summary>
    /// 고유 원소가 같은 공격자의 <b>직접 공격</b>에만 받는 피해를 늘린다. 지속피해 · 반응 · 덧붙는 피해는 받지 않는다.
    /// 같은 원소의 취약이 여럿이면 가장 높은 효과 하나만 배율을 돌려준다.
    /// </summary>
    public sealed class ElementalVulnerabilityEffect : BaseEffect
    {
        public readonly BaseEnums.UnitElement Element;
        public readonly float Bonus;
        public readonly bool AllElements;
        public bool Matches(BaseEnums.UnitElement element)
            => element != BaseEnums.UnitElement.None && (AllElements || Element == element);

        public ElementalVulnerabilityEffect(BaseEnums.UnitElement element, float bonus, bool allElements = false) : base(0, bonus)
        {
            Element = element;
            AllElements = allElements;
            Bonus = bonus;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || !DirectAttack.IsDirect(context)) return 1f;
            Unit attacker = context.Attacker;
            BaseEnums.UnitElement element = DirectAttack.InnateElement(attacker);
            if (attacker.IsEnemy == unit.IsEnemy || !Matches(element)) return 1f;
            return ReferenceEquals(VayuVulnerability.Strongest(unit, element), this) ? 1f + Bonus : 1f;
        }
    }

    /// <summary>
    /// 바유의 방깍 상태 「바람의 침식」(Detail_08 §14.1). <b>방어력만</b> 깎는다 — 특수방어력이 막는 지속피해 · 반응에는 걸리지 않는다.
    /// 바유가 거는 방깍은 한 상태(같은 키)이고, 새로 걸면 더 큰 수치와 더 긴 남은 지속을 남긴다. 다른 유닛의 방깍(원소 침식 712 등)과는 따로 곱해진다.
    /// </summary>
    public static class VayuErosion
    {
        public const int StatusId = 70641;
        public const string Key = "vayu_erosion";

        /// <param name="defenseCut">방어력 감소 비율(0.2 = -20%).</param>
        public static void Apply(Unit caster, Unit target, float defenseCut, int duration)
        {
            if (caster == null || target == null || !target.isActive) return;

            float cut = Mathf.Clamp01(defenseCut);
            int turns = Mathf.Max(1, duration);
            UnitStatus existing = target.GetAllStatuses().FirstOrDefault(status => status.Key == Key);
            if (existing != null)
            {
                float existingCut = existing.Effects.Select(instance => instance.EffectObject)
                    .OfType<DefenseOnlyShredEffect>().Select(effect => effect.Cut).DefaultIfEmpty(0f).Max();
                cut = Mathf.Max(cut, existingCut);
                turns = Mathf.Max(turns, existing.RemainingTurns);
            }

            // 명중은 공통 규칙(효과 명중 · 상태 저항)에 맡긴다. 같은 키 교체는 명중한 뒤에만 일어나 기존 침식을 잃지 않는다.
            target.AddStatus(BuffStatus.Create(StatusId, Key, "바람의 침식", caster, target, new DefenseOnlyShredEffect(cut),
                duration: turns, stackPolicy: BaseEnums.StatusStackPolicy.Replace, category: BaseEnums.StatusCategory.Negative,
                description: $"방어력 -{Mathf.RoundToInt(cut * 100f)}%."));
        }

        /// <summary>방어력으로 경감하는 피해에서만 방어 스탯을 줄인다(특수방어력은 그대로).</summary>
        public sealed class DefenseOnlyShredEffect : BaseEffect
        {
            public readonly float Cut;

            public DefenseOnlyShredEffect(float cut) : base(0, cut)
            {
                Cut = Mathf.Clamp01(cut);
                Category = BaseEnums.EffectCategory.Negative;
            }

            public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
                => unit == Target && DamageMitigation.TryGetDefenseStat(context, out BaseEnums.StatType stat) &&
                   stat == BaseEnums.StatType.Defense
                    ? 1f - Cut
                    : 1f;
        }
    }
}
