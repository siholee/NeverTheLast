using System;
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
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 로키 — 초월기 「신들의 황혼」(Detail_08 §22.4). 그리고 <b>사냥감 표식의 소유자</b>.
    ///
    /// 표식(사냥감):
    ///   · 전투 시작에 특성이 <see cref="InstallMark"/>로 연다. 적 하나에게만 — 우선도가 가장 높은 적(같으면 앞 열 → 위 칸 → 개체 순, 난수 없음).
    ///     해로운 효과라 공통 효과 명중을 굴리고, 실패하거나 적이 아직 없으면 다음 실제 행동 시작마다 한 번씩 다시 시도한다(매 프레임이 아니다).
    ///   · 표식이 쓰러지면 그 자리에서 다음 적에게 옮긴다. 키는 로키 개체별이라 다른 로키의 표식을 지우지 않는다.
    ///   · 표식 대상은 아군 직접 추가행동 피해를 +15% 더 받는다(로키가 필드에 있을 때).
    /// 초월기: 표식 대상(없으면 우선도 대상)에게 공격력 160% 베기, 받는 치유 -30%(대상 일반행동 2회, 효과 명중),
    /// 펜리르 자유 추격 2(최대 2, 갱신 — 넘치게 주지 않는다). 피해가 실제로 들어가야 완료로 친다.
    /// </summary>
    public sealed class LokiTwilight : BaseUltimateCode
    {
        public const int MarkStatusId = 70800;
        public const int WoundStatusId = 70801;
        public const float AttackRatio = 1.60f;
        public const float HealingReduction = 0.30f;
        public const int WoundNormals = 2;
        public const float MarkVulnerability = 0.15f;

        /// <summary>표식 시도(로키, 대상, 성공). 검증 · 연출이 읽는다.</summary>
        public static event Action<Unit, Unit, bool> AnyMarkAttempt;

        private Unit _marked;
        private bool _installed;
        private int _lastAttemptActionId = int.MinValue;

        public override int AttackForm => DamageTag.Slash;
        protected override bool RequiresCompletionEvidence => true;

        public LokiTwilight(UltimateCodeContext context) : base(context) => CodeName = "신들의 황혼";

        public string MarkKey => $"loki_prey_{Caster.GetEntityId()}";
        public string WoundKey => $"loki_wound_{Caster.GetEntityId()}";

        // 사망 이벤트는 DeactivateUnit보다 먼저 나간다. 그 순간의 대상은 아직 isActive이므로 체력으로도 거른다.
        private static bool Alive(Unit unit) => unit != null && unit.isActive && unit.IsOnField && unit.HpCurr > 0;

        /// <summary>지금 유효한 사냥감. 쓰러졌거나 정화 등으로 표식이 사라졌으면 null.</summary>
        public Unit MarkedTarget => Alive(_marked) && _marked.HasStatusKey(MarkKey) ? _marked : null;

        /// <summary>표식이 열려 있지만 아직 붙은 적이 없다 — 다음 실제 행동 시작에 다시 시도한다.</summary>
        public bool NeedsMark => _installed && MarkedTarget == null;

        private LokiUnboundWolf Passive => Caster?.ActivePassiveCodes.OfType<LokiUnboundWolf>().FirstOrDefault();

        // ── 표식 수명 ─────────────────────────────────────────────

        public void InstallMark()
        {
            if (_installed || Caster == null) return;
            _installed = true;
            _lastAttemptActionId = int.MinValue;
            Unit.AnyUnitDied += OnAnyUnitDied;
            TryMark();
        }

        public void RemoveMark()
        {
            if (_marked != null) _marked.RemoveStatusByKey(MarkKey);
            _marked = null;
            if (!_installed) return;
            _installed = false;
            Unit.AnyUnitDied -= OnAnyUnitDied;
        }

        /// <summary>실제 행동 하나가 시작될 때 특성이 부른다. 표식이 없을 때만, 행동마다 한 번 시도한다.</summary>
        internal void OnActionStarted(int actionId)
        {
            if (!NeedsMark || actionId == _lastAttemptActionId) return;
            _lastAttemptActionId = actionId;
            TryMark();
        }

        private bool TryMark(Unit exclude = null)
        {
            if (!_installed || Caster == null || !Caster.isActive || !Caster.IsOnField) return false;
            if (MarkedTarget != null) return true;
            Unit candidate = PickMarkCandidate(exclude);
            if (candidate == null) return false;
            if (_marked != null && _marked != candidate) _marked.RemoveStatusByKey(MarkKey);
            UnitStatus mark = BuffStatus.Create(MarkStatusId, MarkKey, "늑대의 사냥감", Caster, candidate,
                new PreyEffect(Caster), duration: 0, stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "로키의 아군이 가하는 직접 추가행동 피해를 +15% 더 받는다. 펜리르가 이 적을 쫓는다.");
            bool success = candidate.TryAddStatus(mark);
            AnyMarkAttempt?.Invoke(Caster, candidate, success);
            if (!success) return false;
            _marked = candidate;
            return true;
        }

        /// <summary>우선도가 가장 높은 적 하나. 체력은 보지 않는다. 동률은 배치로 결정적으로 고른다.</summary>
        public Unit PickMarkCandidate(Unit exclude = null)
            => CombatTargets.AliveEnemies(Caster)
                .Where(unit => unit != exclude && Alive(unit))
                .OrderByDescending(unit => unit.Priority)
                .ThenBy(unit => unit.currentCell != null ? Mathf.Abs(unit.currentCell.xPos) : 99)
                .ThenBy(unit => unit.currentCell != null ? unit.currentCell.yPos : 99)
                .ThenBy(unit => unit.GetEntityId())
                .FirstOrDefault();

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (dead == null || dead != _marked) return;
            dead.RemoveStatusByKey(MarkKey);
            _marked = null;
            TryMark(dead);
        }

        // ── 초월기 ───────────────────────────────────────────────

        protected override IEnumerator Perform()
        {
            Unit target = MarkedTarget ?? PickPriorityEnemy();
            var tags = new List<int> { DamageTag.UltAttack, DamageTag.SingleTarget, DamageTag.NonContactAttack, DamageTag.Slash };
            AttackTargetResolution routing = PrepareTargets(target != null ? new[] { target } : Array.Empty<Unit>(), tags);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            target = routing.Targets.FirstOrDefault(unit => unit != null && unit.isActive);
            if (target == null) yield break;

            DamageContext context = BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), tags, target);
            yield return DirectAttack.HitOne("loki.ultimate", Caster, target, () =>
            {
                target.TakeDamage(context);
                if (context.IsCancelled) return;
                MarkEffectApplied();
                if (target.isActive) ApplyWound(target);
                Passive?.GrantFlex();
            }, presentation: impact => Effects.Vfx.VfxComposer.PlaySingleTarget("loki.twilight", Caster, target, impact, 1.12f));
            AudioManager.PlayCue("loki.ultimate.resolve");
        }

        private void ApplyWound(Unit target)
        {
            UnitStatus wound = BuffStatus.Create(WoundStatusId, WoundKey, "찢긴 상처", Caster, target,
                new WoundEffect(), duration: WoundNormals, stackPolicy: BaseEnums.StatusStackPolicy.RefreshDuration,
                category: BaseEnums.StatusCategory.Negative,
                description: $"받는 치유 -30%. 대상의 일반행동 {WoundNormals}회 동안 유지.");
            wound.NormalActionDuration = true;
            target.TryAddStatus(wound);
        }

        /// <summary>사냥감 — 로키 아군의 직접 추가행동 피해 +15%. 로키가 필드에 없으면 효력 0.</summary>
        private sealed class PreyEffect : BaseEffect
        {
            private readonly Unit _loki;

            public PreyEffect(Unit loki) : base(0, MarkVulnerability)
            {
                _loki = loki;
                Category = BaseEnums.EffectCategory.Negative;
            }

            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            {
                if (unit != Target || !DirectAttack.IsDirect(context) || !context.HasTag(DamageTag.AdditionalAttack)) return 1f;
                if (context.Attacker == null || context.Attacker.IsEnemy == unit.IsEnemy) return 1f;
                return _loki != null && _loki.isActive && _loki.IsOnField ? 1f + MarkVulnerability : 1f;
            }
        }

        private sealed class WoundEffect : BaseEffect
        {
            public WoundEffect() : base(0, HealingReduction) => Category = BaseEnums.EffectCategory.Negative;

            public override float HealingReceivedMultiplierModifier(Unit unit) => 1f - HealingReduction;
        }
    }
}
