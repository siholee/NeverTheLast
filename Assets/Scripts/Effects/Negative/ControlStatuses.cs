using System;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 행동만 막는 제어 상태들.
    ///
    /// 수르트의 <b>경직</b>과는 다르다. 경직은 행동 불가에 더해 무적이고 대상 지정도 되지 않지만,
    /// 여기 있는 <b>빙결</b>과 <b>에어본</b>은 <b>행동만</b> 막는다. 피격과 대상 지정은 정상이다.
    /// 적을 얼리거나 띄운 순간 그 적을 때릴 수 없게 되면 얼음·에어본 파티가 스스로를 방해하기 때문이다.
    /// </summary>
    public static class ControlStatuses
    {
        /// <summary>에어본 연출이 최고점에 도달했을 때. (source, target)</summary>
        public static event Action<Unit, Unit> AirborneApexReached;
        public const int FrozenStatusId = 5920;
        public const int AirborneStatusId = 5921;
        public const int StunStatusId = 5922;

        public const string FrozenKey = "cc_frozen";
        public const string AirborneKey = "cc_airborne";
        public const string StunKey = "cc_stun";

        // ── 빙결 ──────────────────────────────────────────────────

        /// <summary>빙결 지속 <b>턴</b> 수. 유발자의 CON에 비례한다(1~3턴).</summary>
        public static int FreezeTurns(Unit source)
        {
            int con = source != null ? Mathf.Max(0, source.GetBaseCon()) : 0;
            return Mathf.Clamp(Mathf.RoundToInt(0.75f + con * 0.01f), 1, 3);
        }

        /// <summary>
        /// 빙결을 건다. 면역이면 무시하고, 이미 얼어 있으면 남은 시간과 새 지속시간 중 긴 쪽을 남긴다.
        /// </summary>
        public static bool ApplyFreeze(Unit target, Unit source, int turns)
        {
            return ApplyControl(target, source, turns, FrozenStatusId, FrozenKey, "빙결",
                "행동할 수 없습니다. 피격과 대상 지정은 정상입니다.",
                target != null && target.IsFreezeImmune);
        }

        // ── 에어본 ────────────────────────────────────────────────

        /// <summary>
        /// 에어본 지속 <b>턴</b> 수 — <c>1.5턴 × 시전자 CON ÷ 대상 CON</c>, 1~2턴으로 제한한다.
        /// 탱커끼리는 짧게 뜨고 후열은 오래 뜬다.
        /// </summary>
        public static int AirborneTurns(Unit source, Unit target)
        {
            float casterCon = source != null ? Mathf.Max(1, source.GetBaseCon()) : 1f;
            float targetCon = target != null ? Mathf.Max(1, target.GetBaseCon()) : 1f;
            return Mathf.Clamp(Mathf.RoundToInt(1.5f * casterCon / targetCon), 1, 2);
        }

        /// <summary>에어본을 건다. 면역이면 무시하고, 겹치면 긴 쪽을 남긴다.</summary>
        public static bool ApplyAirborne(Unit target, Unit source)
        {
            int turns = AirborneTurns(source, target);
            bool applied = ApplyControl(target, source, turns, AirborneStatusId, AirborneKey, "에어본",
                "공중에 떠 행동할 수 없습니다. 피격과 대상 지정은 정상입니다.",
                target != null && target.IsAirborneImmune);
            if (applied)
            {
                Effects.AirborneEffect.Attach(target,
                    () => AirborneApexReached?.Invoke(source, target));
            }
            return applied;
        }

        // ── 기절 ──────────────────────────────────────────────────

        /// <summary>
        /// 기절 지속 <b>턴</b> 수 — <c>1턴 × 시전자 CON ÷ 대상 CON</c>, 1~2턴으로 제한한다.
        /// 에어본과 같은 CON 비교식이지만 기준이 1턴이라 한 턴을 넘기기 어렵다.
        /// </summary>
        public static int StunTurns(Unit source, Unit target, float sourceConMultiplier = 1f)
        {
            float casterCon = source != null ? Mathf.Max(1, source.GetBaseCon()) : 1f;
            casterCon *= Mathf.Max(0f, sourceConMultiplier);
            float targetCon = target != null ? Mathf.Max(1, target.GetBaseCon()) : 1f;
            return Mathf.Clamp(Mathf.RoundToInt(casterCon / targetCon), 1, 2);
        }

        /// <summary>기절을 건다. 빙결·에어본과 같은 계열이라 행동만 막는다.</summary>
        public static bool ApplyStun(Unit target, Unit source, float sourceConMultiplier = 1f)
        {
            int turns = StunTurns(source, target, sourceConMultiplier);
            return ApplyControl(target, source, turns, StunStatusId, StunKey, "기절",
                "행동할 수 없습니다. 피격과 대상 지정은 정상입니다.",
                false);
        }

        /// <summary>CON 비교 없이 명시한 턴 수만큼 기절시킨다.</summary>
        public static bool ApplyFixedStun(Unit target, Unit source, int turns)
        {
            return ApplyControl(target, source, Mathf.Max(1, turns), StunStatusId, StunKey, "기절",
                "행동할 수 없습니다. 피격과 대상 지정은 정상입니다.",
                false);
        }

        // ── 제어 분쇄 ─────────────────────────────────────────────

        /// <summary>
        /// 회차별 지속시간 배율. 배열을 넘어서면 <b>면역</b>이다(4회차부터).
        ///
        /// 자동 전투라 플레이어가 제어를 끊을 수단이 없다. 바위 파티가 진동을 연달아 터뜨리거나
        /// 폭풍(1542)이 궁극기마다 전체 기절을 거는 식으로 <b>손 쓸 수 없는 제어 루프</b>가
        /// 만들어지는 것을 막는다. 모든 제어가 <see cref="ApplyControl"/>을 지나므로
        /// 원소 반응이든 패시브든 궁극기든 같은 규칙을 받는다.
        /// </summary>
        private static readonly float[] DiminishMultipliers = { 1f, 0.6f, 0.3f };

        /// <summary>제어에 걸리지 않은 채 자기 턴을 이만큼 보내면 회차가 초기화된다.</summary>
        public const int DiminishResetTurns = 3;

        /// <summary>회차가 초기화될 만큼 제어 없이 버텼는지.</summary>
        private static bool HasRecovered(Unit target)
            => target.TurnCount - target.LastControlledTurn >= DiminishResetTurns;

        /// <summary>분쇄를 적용한 지속 턴 수. 0이면 면역이다.</summary>
        public static int DiminishedTurns(Unit target, int turns)
        {
            if (target == null) return turns;

            int count = HasRecovered(target) ? 0 : target.ControlAppliedCount;
            if (count >= DiminishMultipliers.Length) return 0;

            // 반올림이라 1턴짜리 제어는 2회차까지만 통한다(1 → 1 → 0).
            return Mathf.RoundToInt(turns * DiminishMultipliers[count]);
        }

        /// <summary>제어가 실제로 걸렸을 때만 회차를 올린다. 갱신에 실패한 재부여는 세지 않는다.</summary>
        private static void RegisterControl(Unit target)
        {
            if (target == null) return;
            if (HasRecovered(target)) target.ControlAppliedCount = 0;
            target.ControlAppliedCount++;
            target.LastControlledTurn = target.TurnCount;
        }

        // ── 공통 ──────────────────────────────────────────────────

        private static bool ApplyControl(
            Unit target, Unit source, int turns,
            int statusId, string key, string name, string description, bool immune)
        {
            if (target == null || !target.isActive || turns <= 0) return false;
            if (immune)
            {
                Debug.Log($"[{name}] {target.UnitName}은(는) 면역입니다.");
                return false;
            }

            int requested = turns;
            turns = DiminishedTurns(target, turns);
            if (turns <= 0)
            {
                Debug.Log($"[{name}] {target.UnitName} — 제어 분쇄로 무효 ({target.ControlAppliedCount}회차)");
                return false;
            }

            UnitStatus existing = target.GetAllStatuses().FirstOrDefault(status => status.Key == key);
            if (existing != null)
            {
                if (existing.RemainingTurns >= turns) return false;
                target.RemoveStatusByKey(key);
            }

            var definition = new StatusDefinition
            {
                Id = statusId,
                Key = key,
                Name = name,
                Description = description,
                Category = BaseEnums.StatusCategory.Negative,
                StackPolicy = BaseEnums.StatusStackPolicy.Replace,
                Duration = turns,
                IsBeneficial = false,
            };
            var status = new UnitStatus(definition, source, target);
            status.AddEffect(new ActionLockEffect(turns, source));
            target.AddStatus(status);
            RegisterControl(target);

            Debug.Log(turns == requested
                ? $"[{name}] {target.UnitName} — {turns}턴"
                : $"[{name}] {target.UnitName} — {turns}턴 (분쇄 {target.ControlAppliedCount}회차, 원래 {requested}턴)");
            return true;
        }
    }

    /// <summary>
    /// 상태가 살아 있는 동안 행동 불가를 유지시키는 효과.
    ///
    /// <see cref="Unit.isControlled"/>의 타이머는 <see cref="Unit"/>의 Update가 직접 흘려보내므로
    /// 평소에는 손대지 않는다. 다른 효과가 제어를 먼저 풀어 버렸을 때만 남은 시간으로 다시 건다.
    /// </summary>
    internal sealed class ActionLockEffect : BaseEffect
    {
        private readonly Unit _source;
        private int _remainingTurns;

        public ActionLockEffect(int turns, Unit source) : base(0, turns)
        {
            _remainingTurns = turns;
            _source = source;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnApply()
        {
            Target?.ApplyControlAtLeast(_source, _remainingTurns);
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            _remainingTurns--;
            if (_remainingTurns <= 0) return;

            // 평소에는 Unit이 자기 턴마다 제어 턴을 깎는다.
            // 다른 효과가 제어를 먼저 끊어 버린 경우에만 다시 건다.
            if (!Target.isControlled) Target.ApplyControlAtLeast(_source, _remainingTurns);
        }
    }
}
