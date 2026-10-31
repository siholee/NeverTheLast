using System;
using System.Collections.Generic;
using System.Linq;
using Entities;

namespace Combat
{
    /// <summary>원화 피팅·카드 이동과 독립적인 수평 광역 판정 순서와 기존 시간 간격.</summary>
    public static class CombatSweepTiming
    {
        public readonly struct Impact
        {
            public readonly Unit Target;
            public readonly float Due;
            public Impact(Unit target, float due) { Target = target; Due = due; }
        }

        public static Impact[] Plan(IReadOnlyList<Unit> targets)
        {
            float Column(Unit unit) => unit.currentCell != null ? unit.currentCell.xPos :
                unit.SummonOwner != null ? Column(unit.SummonOwner) : 0;
            if (targets == null || targets.Count == 0) return Array.Empty<Impact>();
            float center = targets.Average(Column);
            float Distance(Unit unit) => Math.Abs(Column(unit) - center);
            float farthest = targets.Max(Distance);
            return targets.OrderBy(Distance).Select(unit => new Impact(unit,
                .055f + (farthest > .001f ? Distance(unit) / farthest : 0f) * .075f)).ToArray();
        }
    }
}
