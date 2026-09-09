using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 코드 안쪽에서 도는 재사용 대기를 <b>보유자의 턴 수</b>로 잰다.
    ///
    /// 예전에는 <c>Time.time</c>으로 쟀다. 전투는 턴제이고 배속(F4)이 벽시계를 그대로 늘이므로
    /// 배속을 올리면 같은 전투에서 내부 쿨다운이 <b>더 자주</b> 돌아 결과가 달라졌다.
    /// <see cref="Unit.TurnCount"/>는 배속과 무관하게 늘어나므로 재현 가능한 축이 된다.
    /// </summary>
    public sealed class TurnCooldown
    {
        /// <summary>한 번도 쓰지 않은 상태. 첫 발동을 막지 않도록 충분히 과거로 둔다.</summary>
        private const int Never = -100000;

        private readonly int _turns;
        private int _usedAtTurn = Never;

        public TurnCooldown(int turns) => _turns = Mathf.Max(1, turns);

        public int Turns => _turns;

        public bool IsReady(Unit owner) => owner == null || owner.TurnCount - _usedAtTurn >= _turns;

        public void Use(Unit owner) => _usedAtTurn = owner?.TurnCount ?? 0;

        /// <summary>준비됐으면 소비하고 true.</summary>
        public bool TryUse(Unit owner)
        {
            if (!IsReady(owner)) return false;
            Use(owner);
            return true;
        }

        public void Reset() => _usedAtTurn = Never;
    }

    /// <summary>대상마다 따로 도는 내부 재사용 대기. 반격 화상처럼 "공격자별 1회"에 쓴다.</summary>
    public sealed class TargetTurnCooldown
    {
        private readonly int _turns;
        private readonly Dictionary<Unit, int> _usedAtTurn = new();

        public TargetTurnCooldown(int turns) => _turns = Mathf.Max(1, turns);

        public bool IsReady(Unit owner, Unit target)
        {
            if (owner == null || target == null) return true;
            return !_usedAtTurn.TryGetValue(target, out int used) || owner.TurnCount - used >= _turns;
        }

        public bool TryUse(Unit owner, Unit target)
        {
            if (!IsReady(owner, target)) return false;
            if (target != null) _usedAtTurn[target] = owner?.TurnCount ?? 0;
            return true;
        }

        public void Clear() => _usedAtTurn.Clear();
    }
}
