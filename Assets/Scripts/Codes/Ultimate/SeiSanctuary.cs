using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 세이 궁극기 — 창세.
    ///
    /// 6초간 아군 전체에게:
    ///   · DEX +세이의 INT 만큼
    ///   · 가하는 피해에 세이 INT의 y배만큼 고정 피해 추가 (아군 평균 피해의 30~60%)
    /// </summary>
    public sealed class SeiSanctuary : UltimateCode
    {
        private const int StatusId = 5110;
        private const string StatusKey = "sei_genesis";
        private const float Duration = 6f;

        /// <summary>단계별 INT 계수. 아군 평균 피해 대비 약 30% / 45% / 60%.</summary>
        private static readonly float[] IntRatios = { 3.0f, 4.5f, 6.0f };

        private readonly List<(Unit ally, Action<DamageResolvedContext> handler)> _hooks = new();
        private bool _resolving;

        public SeiSanctuary(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "창세";
            MaxStage = 3;
            Cooldown = 8f;
            CastingDelay = 0.5f;
        }

        private float Ratio => IntRatios[Mathf.Clamp(CurrentStage, 1, MaxStage) - 1];

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
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

            int dexBonus = Mathf.Max(1, Caster.GetBaseInt());
            ClearHooks();

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                var status = BuffStatus.Create(
                    StatusId, StatusKey, CodeName,
                    Caster, ally, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, dexBonus),
                    duration: Duration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"DEX +{dexBonus}, 가하는 피해에 고정 피해가 추가됩니다.");
                ally.AddStatus(status);

                Unit bound = ally;
                Action<DamageResolvedContext> handler = ctx => OnAllyDamageDealt(bound, ctx);
                bound.AddListener(BaseEnums.UnitEventType.OnDamageDealt, handler);
                _hooks.Add((bound, handler));
            }

            // 지속시간이 끝나면 추가 피해 훅을 회수한다.
            Caster.StartCoroutine(ExpireAfter(Duration));

            Debug.Log($"[창세] 아군 전체 DEX +{dexBonus}, 추가 고정 피해 INT×{Ratio:0.#}, {Duration}초");
            StopCode();
        }

        private IEnumerator ExpireAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ClearHooks();
        }

        private void ClearHooks()
        {
            foreach ((Unit ally, Action<DamageResolvedContext> handler) in _hooks)
            {
                if (ally == null) continue;
                ally.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, handler);
            }
            _hooks.Clear();
        }

        private void OnAllyDamageDealt(Unit ally, DamageResolvedContext context)
        {
            if (_resolving || Caster == null || !Caster.isActive) return;

            Unit target = context?.Target;
            if (ally == null || target == null || !target.isActive || context.DamageDealt <= 0) return;
            if (context.DamageContext?.DamageTags != null &&
                context.DamageContext.DamageTags.Contains(DamageTag.TrueDamage)) return;

            int bonus = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * Ratio));
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage };

            _resolving = true;
            try
            {
                target.TakeDamage(new DamageContext(ally, bonus, BaseEnums.CodeType.Ultimate, tags));
            }
            finally
            {
                _resolving = false;
            }
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive;
    }
}
