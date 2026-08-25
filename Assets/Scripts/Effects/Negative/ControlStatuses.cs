using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;
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
            return ApplyControl(target, source, turns, AirborneStatusId, AirborneKey, "에어본",
                "공중에 떠 행동할 수 없습니다. 피격과 대상 지정은 정상입니다.",
                target != null && target.IsAirborneImmune);
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

            Debug.Log($"[{name}] {target.UnitName} — {turns}턴");
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
