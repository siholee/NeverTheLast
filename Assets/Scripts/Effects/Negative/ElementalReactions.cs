using System.Collections.Generic;
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
    ///   감전 — 물 + 번개
    ///   화상 — 불 + 풀
    ///
    /// 원소 부착이 일어나는 지점(<see cref="Unit.GrantCombatElement"/>)에서 한 번만 검사한다.
    /// </summary>
    public static class ElementalReaction
    {
        public const int ShockStatusId = 5900;
        public const int BurnStatusId = 5901;

        /// <summary>반응 지속피해의 위력. 반응 유발자의 CON에 곱해진다.</summary>
        private const int ReactionPower = 45;

        private const float ShockDuration = 4f;
        private const float BurnDuration = 6f;

        /// <summary>반응 한 쌍의 정의.</summary>
        private readonly struct Pair
        {
            public readonly BaseEnums.UnitElement A;
            public readonly BaseEnums.UnitElement B;
            public readonly string Name;
            public readonly int StatusId;
            public readonly string Key;
            public readonly float Duration;

            public Pair(BaseEnums.UnitElement a, BaseEnums.UnitElement b, string name,
                int statusId, string key, float duration)
            {
                A = a; B = b; Name = name; StatusId = statusId; Key = key; Duration = duration;
            }
        }

        private static readonly Pair[] Pairs =
        {
            new(BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Electro,
                "감전", ShockStatusId, "reaction_shock", ShockDuration),
            new(BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Dendro,
                "화상", BurnStatusId, "reaction_burn", BurnDuration),
        };

        /// <summary>
        /// <paramref name="target"/>이 방금 <paramref name="applied"/> 원소를 부착받았을 때 반응을 검사한다.
        /// 반응이 일어나면 true를 반환한다.
        /// </summary>
        /// <param name="source">반응을 일으킨 유닛. 피해량이 이 유닛의 CON에 비례한다.</param>
        public static bool TryResolve(Unit target, BaseEnums.UnitElement applied, Unit source)
        {
            if (target == null || applied == BaseEnums.UnitElement.None) return false;

            foreach (Pair pair in Pairs)
            {
                BaseEnums.UnitElement other =
                    applied == pair.A ? pair.B :
                    applied == pair.B ? pair.A :
                    BaseEnums.UnitElement.None;

                if (other == BaseEnums.UnitElement.None) continue;
                if (!target.HasCombatElement(pair.A) || !target.HasCombatElement(pair.B)) continue;

                Trigger(target, pair, source ?? target);
                return true;
            }
            return false;
        }

        private static void Trigger(Unit target, Pair pair, Unit source)
        {
            // 반응한 두 원소는 소모되어 사라진다.
            target.RemoveCombatElement(pair.A);
            target.RemoveCombatElement(pair.B);

            int perSecond = Mathf.Max(1, source.SkillDamage(ReactionPower, BaseEnums.PrimaryStat.CON));

            target.AddStatus(BuffStatus.Create(
                pair.StatusId, $"{pair.Key}_{source.GetEntityId()}", pair.Name,
                source, target, new ReactionDotEffect(perSecond),
                duration: pair.Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: $"매초 {perSecond}의 {pair.Name} 피해를 받습니다."));

            Unit.NotifyElementalReaction(source, target, pair.Name);
            Debug.Log($"[원소 반응] {pair.Name} — {source.UnitName} → {target.UnitName}, 초당 {perSecond}");
        }
    }

    /// <summary>반응이 남기는 지속피해. 1초 간격으로 고정량을 준다.</summary>
    public sealed class ReactionDotEffect : BaseEffect
    {
        private const float Interval = 1f;

        private readonly int _damagePerSecond;
        private float _elapsed;

        public override bool IsDamageOverTime => true;

        public ReactionDotEffect(int damagePerSecond) : base(0, damagePerSecond)
        {
            _damagePerSecond = damagePerSecond;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Target == null || !Target.isActive) return;

            _elapsed += deltaTime;
            while (_elapsed >= Interval)
            {
                _elapsed -= Interval;
                Target.TakeDamage(new DamageContext(
                    Caster, _damagePerSecond, BaseEnums.CodeType.Effect,
                    new List<int> { DamageTag.SingleTarget, DamageTag.Special }));
            }
        }

        public override int EstimateDamagePerSecond() => _damagePerSecond;
    }
}
