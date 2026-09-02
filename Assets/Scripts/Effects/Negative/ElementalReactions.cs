using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 원소 반응.
    ///
    /// 두 원소가 한 유닛에게 겹치면 반응이 일어나 **두 원소가 모두 사라지고** 지속피해를 남긴다.
    /// 피해량은 <b>반응을 일으킨 유닛(반응을 유발한 공격자)의 CON</b>에 비례한다.
    ///
    ///   감전 — 물 + 번개    (지속피해)
    ///   화상 — 불 + 풀      (지속피해)
    ///   촉진 — 풀 + 번개    (부여자와 대상의 CON 차이만큼 방어력 감소)
    ///   빙결 — 얼음 + 물    (피해 없음. 행동 불가만)
    ///   공명 — 바위 + 바위  (피해 없음. 행동 불가만. <b>같은 원소끼리</b> 일어나는 유일한 반응)
    ///
    /// 원소 부착이 일어나는 지점(<see cref="Unit.GrantCombatElement"/>)에서 한 번만 검사한다.
    /// </summary>
    public static class ElementalReaction
    {
        // 5900은 HealingReductionStatus가 이미 쓰고 있어 감전과 겹친다.
        // 5910대로 옮겨 HasStatus(감전) 조회가 치유량 감소와 섞이지 않게 했다.
        public const int ShockStatusId = 5910;
        public const int BurnStatusId = 5911;
        public const int CatalyzeStatusId = 5912;
        public const string BurnStatusKey = "burn";
        public const string CatalyzeStatusKey = "reaction_catalyze";

        /// <summary>
        /// 반응 지속피해의 위력. 반응 유발자의 CON에 곱해진다.
        /// 1턴 = 2초 환산이므로 예전 '매초 위력 45'는 턴당 위력 90이 된다.
        /// </summary>
        private const int ReactionPower = 90;

        private const int ShockDuration = 2;   // 4초 → 2턴
        private const int BurnDuration = 2;    // 별도 명시가 없는 원소 반응 화상의 기본 지속시간

        /// <summary>반응 한 쌍의 정의.</summary>
        private readonly struct Pair
        {
            public readonly BaseEnums.UnitElement A;
            public readonly BaseEnums.UnitElement B;
            public readonly string Name;
            public readonly int StatusId;
            public readonly string Key;
            public readonly int Duration;

            /// <summary>지속피해 대신 빙결(행동 불가)을 남기는 반응인지.</summary>
            public readonly bool Freezes;

            /// <summary>지속피해 대신 기절(행동 불가)을 남기는 반응인지.</summary>
            public readonly bool Stuns;

            /// <summary>지속피해 대신 촉진 방어력 감소를 남기는 반응인지.</summary>
            public readonly bool Catalyzes;

            public Pair(BaseEnums.UnitElement a, BaseEnums.UnitElement b, string name,
                int statusId, string key, int duration,
                bool freezes = false, bool stuns = false, bool catalyzes = false)
            {
                A = a; B = b; Name = name; StatusId = statusId; Key = key; Duration = duration;
                Freezes = freezes;
                Stuns = stuns;
                Catalyzes = catalyzes;
            }
        }

        private static readonly Pair[] Pairs =
        {
            new(BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Electro,
                "감전", ShockStatusId, "reaction_shock", ShockDuration),
            new(BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Dendro,
                "화상", BurnStatusId, "reaction_burn", BurnDuration),
            new(BaseEnums.UnitElement.Dendro, BaseEnums.UnitElement.Electro,
                "촉진", CatalyzeStatusId, CatalyzeStatusKey, 2, catalyzes: true),
            new(BaseEnums.UnitElement.Cryo, BaseEnums.UnitElement.Hydro,
                "빙결", ControlStatuses.FrozenStatusId, ControlStatuses.FrozenKey, 0, freezes: true),
            // 바위 + 바위. 같은 원소가 겹쳐야 일어나므로 아래 TryResolve의 alreadyAttached 경로에서만 성립한다.
            new(BaseEnums.UnitElement.Geo, BaseEnums.UnitElement.Geo,
                "공명", ControlStatuses.StunStatusId, ControlStatuses.StunKey, 0, stuns: true),
        };

        /// <summary>
        /// <paramref name="target"/>이 방금 <paramref name="applied"/> 원소를 부착받았을 때 반응을 검사한다.
        /// 반응이 일어나면 true를 반환한다.
        /// </summary>
        /// <param name="source">반응을 일으킨 유닛. 피해량이 이 유닛의 CON에 비례한다.</param>
        /// <param name="alreadyAttached">
        /// 부착 이전에 <b>같은 원소가 이미 붙어 있었는지</b>. 같은 원소끼리 겹치는 공명은
        /// 부착 집합만 봐서는 판별할 수 없으므로 부착 지점이 알려 준다.
        /// </param>
        public static bool TryResolve(
            Unit target, BaseEnums.UnitElement applied, Unit source, bool alreadyAttached = false)
        {
            if (target == null || applied == BaseEnums.UnitElement.None) return false;

            foreach (Pair pair in Pairs)
            {
                // 같은 원소끼리 일어나는 반응(공명)은 덧붙이기 전에 이미 붙어 있었을 때만 성립한다.
                if (pair.A == pair.B)
                {
                    if (applied != pair.A || !alreadyAttached) continue;
                    if (!target.HasAttachedElement(pair.A)) continue;

                    Trigger(target, pair, source ?? target);
                    return true;
                }

                BaseEnums.UnitElement other =
                    applied == pair.A ? pair.B :
                    applied == pair.B ? pair.A :
                    BaseEnums.UnitElement.None;

                if (other == BaseEnums.UnitElement.None) continue;
                // 판정 전용 원소(스사노오 '뇌신')는 반응 재료가 아니다. 실제 부착만 본다.
                if (!target.HasAttachedElement(pair.A) || !target.HasAttachedElement(pair.B)) continue;

                Trigger(target, pair, source ?? target);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 필드가 만들어 내는 원소 반응 피해 배율.
        /// 스카디 `원소술사`(+25%)와 바루나 `대해의 판결`(+40%)은 같은 계열이라
        /// <b>중첩되지 않고 가장 높은 하나만</b> 적용된다.
        /// </summary>
        public static float FieldReactionMultiplier(Unit source)
        {
            if (source == null) return 1f;

            var allies = global::Target.GetAllAllies(source)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            if (!allies.Contains(source)) allies.Add(source);

            float bonus = 0f;
            foreach (Unit ally in allies)
            {
                foreach (var status in ally.ActiveStatuses)
                {
                    foreach (var effect in status.Effects)
                    {
                        if (effect.EffectObject is not IReactionAmplifier amplifier) continue;
                        bonus = Mathf.Max(bonus, amplifier.ReactionDamageBonus);
                    }
                }
            }
            return 1f + Mathf.Max(0f, bonus);
        }

        private static void Trigger(Unit target, Pair pair, Unit source)
        {
            // 반응한 두 원소는 소모되어 사라진다.
            target.RemoveCombatElement(pair.A);
            target.RemoveCombatElement(pair.B);

            // 빙결은 피해가 없다. 유발자의 CON에 비례한 턴 수 동안 행동만 막는다.
            if (pair.Freezes)
            {
                ControlStatuses.ApplyFreeze(target, source, ControlStatuses.FreezeTurns(source));
                Unit.NotifyElementalReaction(source, target, pair.Name);
                Debug.Log($"[원소 반응] 빙결 — {source.UnitName} → {target.UnitName}");
                return;
            }

            // 공명도 피해가 없다. 부착자와 대상의 CON을 견주어 기절 턴 수를 정한다.
            if (pair.Stuns)
            {
                ControlStatuses.ApplyStun(target, source, ResonanceConMultiplier(source));
                Unit.NotifyElementalReaction(source, target, pair.Name);
                Debug.Log($"[원소 반응] 공명 — {source.UnitName} → {target.UnitName}");
                return;
            }

            if (pair.StatusId == BurnStatusId)
            {
                if (!TryApplyBurn(source, target, pair.Duration))
                {
                    Unit.NotifyElementalReaction(source, target, pair.Name);
                    Debug.Log($"[원소 반응] {pair.Name} 저항 — {source.UnitName} → {target.UnitName}");
                    return;
                }

                Unit.NotifyElementalReaction(source, target, pair.Name);
                Debug.Log($"[원소 반응] {pair.Name} — {source.UnitName} → {target.UnitName}, 최대 체력 5%");
                return;
            }

            if (pair.Catalyzes)
            {
                float reduction = Mathf.Clamp01(
                    (source.GetBaseCon() - target.GetBaseCon()) * 0.01f);
                target.AddStatus(BuffStatus.Create(
                    CatalyzeStatusId, CatalyzeStatusKey, pair.Name,
                    source, target, new CatalyzeDefenseEffect(1f - reduction),
                    duration: pair.Duration,
                    stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                    category: BaseEnums.StatusCategory.Negative,
                    isBeneficial: false,
                    description: $"방어력이 {reduction * 100f:0.#}% 감소합니다."));
                Unit.NotifyElementalReaction(source, target, pair.Name);
                Debug.Log($"[원소 반응] 촉진 — {source.UnitName} → {target.UnitName}, 방어력 -{reduction * 100f:0.#}%");
                return;
            }

            int perSecond = Mathf.Max(1, Mathf.RoundToInt(
                source.SkillDamage(ReactionPower, BaseEnums.PrimaryStat.CON) * FieldReactionMultiplier(source)));

            target.AddStatus(BuffStatus.Create(
                pair.StatusId, $"{pair.Key}_{source.GetEntityId()}", pair.Name,
                source, target, new ReactionDotEffect(perSecond),
                duration: pair.Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: $"턴마다 {perSecond}의 {pair.Name} 피해를 받습니다."));

            Unit.NotifyElementalReaction(source, target, pair.Name);
            Debug.Log($"[원소 반응] {pair.Name} — {source.UnitName} → {target.UnitName}, 턴당 {perSecond}");
        }

        /// <summary>
        /// 공통 화상 부여. 별도 명시가 없으면 2턴이며, 부여자와 대상의 CON으로 명중을 판정한다.
        /// 모든 출처가 같은 키를 사용하므로 재부여 시 피해가 중첩되지 않고 지속시간이 연장된다.
        /// </summary>
        public static bool TryApplyBurn(Unit source, Unit target, int duration = BurnDuration)
        {
            if (source == null || target == null || !target.isActive || duration <= 0) return false;
            if (!EffectContest.PassesConCheck(source, target)) return false;

            target.AddStatus(BuffStatus.Create(
                BurnStatusId, BurnStatusKey, "화상",
                source, target, new PercentDamageOverTimeEffect(0, 5f),
                duration: duration,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: $"{duration}턴간 턴마다 최대 체력의 5%에 해당하는 화상 피해를 받습니다."));
            return true;
        }

        /// <summary>공명 계산에만 적용되는 시전자 CON 배율. 여러 효과가 있어도 가장 높은 값 하나만 쓴다.</summary>
        private static float ResonanceConMultiplier(Unit source)
        {
            if (source == null) return 1f;

            float multiplier = 1f;
            foreach (var status in source.ActiveStatuses)
            {
                foreach (var effect in status.Effects)
                {
                    if (effect.EffectObject is IResonanceConMultiplier modifier)
                        multiplier = Mathf.Max(multiplier, modifier.ResonanceConMultiplier);
                }
            }
            return multiplier;
        }
    }

    /// <summary>
    /// 지속피해·제어처럼 CON으로 명중과 저항을 겨루는 효과의 공통 판정.
    /// 동률은 50%, CON 1 차이마다 1%p이며 극단값에서도 10~90%를 보장한다.
    /// </summary>
    public static class EffectContest
    {
        public static float ConHitChance(Unit source, Unit target)
        {
            float sourceCon = source != null ? source.GetBaseCon() : 0f;
            float targetCon = target != null ? target.GetBaseCon() : 0f;
            return Mathf.Clamp(0.5f + (sourceCon - targetCon) * 0.01f, 0.1f, 0.9f);
        }

        public static bool PassesConCheck(Unit source, Unit target)
            => target != null && target.isActive && Random.value <= ConHitChance(source, target);
    }

    /// <summary>원소 반응 피해를 키우는 코드가 구현한다. 가장 큰 보너스 하나만 적용된다.</summary>
    public interface IReactionAmplifier
    {
        /// <summary>가산 비율. 0.25 = +25%.</summary>
        float ReactionDamageBonus { get; }
    }

    /// <summary>공명 기절 턴 계산에서 시전자의 CON만 증폭하는 효과.</summary>
    public interface IResonanceConMultiplier
    {
        float ResonanceConMultiplier { get; }
    }

    /// <summary>반응이 남기는 지속피해. 대상의 턴마다 한 번 터진다.</summary>
    public sealed class ReactionDotEffect : BaseEffect
    {
        private readonly int _damagePerTurn;

        public override bool IsDamageOverTime => true;

        public ReactionDotEffect(int damagePerTurn) : base(0, damagePerTurn)
        {
            _damagePerTurn = damagePerTurn;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            Target.TakeDamage(new DamageContext(
                Caster, _damagePerTurn, BaseEnums.CodeType.Effect,
                new List<int> { DamageTag.SingleTarget, DamageTag.Special }));
        }

        public override int EstimateDamagePerTurn() => _damagePerTurn;
    }

    /// <summary>촉진이 남기는 방어력 배율 감소.</summary>
    public sealed class CatalyzeDefenseEffect : BaseEffect
    {
        private readonly float _multiplier;

        public CatalyzeDefenseEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = Mathf.Clamp01(multiplier);
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? _multiplier : 1f;
    }
}
