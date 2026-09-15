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
    /// 4턴간 아군 전체가 피해를 가할 때마다 세이 INT ×3의 고정 피해를 추가한다.
    /// </summary>
    public sealed class SeiSanctuary : UltimateCode
    {
        private const int StatusId = 5110;
        private const string StatusKey = "sei_genesis";
        private const int Duration = 4;
        private const float IntRatio = 3f;

        private readonly List<(Unit ally, Action<DamageResolvedContext> handler)> _hooks = new();
        private bool _resolving;

        public SeiSanctuary(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "창세";
            CastingDelay = 0.5f;
        }

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

            ClearHooks();

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                // 속성이 바위인 아군에게도 그대로 부착한다. 고유 원소는 판정 축에만 있어
                // 진동(부착된 바위 + 부착된 바위)의 재료가 아니다.
                ally.GrantCombatElement(
                    BaseEnums.UnitElement.Geo, Unit.CommonElementAuraDuration, Caster);

                var status = BuffStatus.Create(
                    StatusId, StatusKey, CodeName,
                    Caster, ally, new MarkerBuffEffect(),
                    duration: Duration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"가하는 피해에 세이 INT ×{IntRatio:0.#}의 고정 피해가 추가됩니다.");
                ally.AddStatus(status);

                Unit bound = ally;
                Action<DamageResolvedContext> handler = ctx => OnAllyDamageDealt(bound, ctx);
                bound.AddListener(BaseEnums.UnitEventType.OnDamageDealt, handler);
                _hooks.Add((bound, handler));
            }

            // 지속시간이 끝나면 추가 피해 훅을 회수한다.
            Caster.StartCoroutine(ExpireAfter(Duration));

            Debug.Log($"[창세] 아군 전체 추가 고정 피해 INT×{IntRatio:0.#}, {Duration}턴");
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
            int bonus = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * IntRatio));
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
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive;
    }
}
