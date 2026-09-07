using System.Collections.Generic;
using System.Linq;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 코드가 반복해서 쓰는 대상 질의 묶음.
    ///
    /// <see cref="global::Target"/>은 이미 필드 여부와 대상 지정 가능 여부를 걸러 주지만,
    /// 코드마다 <c>isActive</c> 재확인과 "우선도가 가장 높은 하나를 무작위로" 같은 선택 규칙을
    /// 따로 적어 왔다. 규칙이 갈라지지 않도록 여기에 모은다.
    /// </summary>
    public static class CombatTargets
    {
        /// <summary>공격할 수 있는 적. 필드 밖·대상 지정 불가·전투 불능은 빠진다.</summary>
        public static List<Unit> AliveEnemies(Unit caster)
            => caster == null
                ? new List<Unit>()
                : global::Target.GetAllEnemies(caster).Where(unit => unit != null && unit.isActive).ToList();

        /// <summary>자신을 포함한 살아 있는 아군.</summary>
        public static List<Unit> AliveAlliesIncludingSelf(Unit caster)
        {
            if (caster == null) return new List<Unit>();

            List<Unit> allies = global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            // 대상 지정 불가 상태의 자신은 GetAllAllies에서 빠지므로 여기서 되돌린다.
            if (caster.isActive && !allies.Contains(caster)) allies.Add(caster);
            return allies;
        }

        /// <summary>우선도가 가장 높은 대상 하나. 동률이면 무작위로 고른다.</summary>
        public static Unit PickByPriority(IReadOnlyList<Unit> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            int highest = candidates.Max(unit => unit.Priority);
            List<Unit> tied = candidates.Where(unit => unit.Priority == highest).ToList();
            return tied[Random.Range(0, tied.Count)];
        }

        /// <summary>우선도가 높은 순서로 서로 다른 대상을 최대 <paramref name="count"/>명 고른다.</summary>
        public static List<Unit> PickByPriority(IEnumerable<Unit> candidates, int count)
        {
            var remaining = candidates?.ToList() ?? new List<Unit>();
            var selected = new List<Unit>();
            while (selected.Count < count && remaining.Count > 0)
            {
                Unit picked = PickByPriority(remaining);
                if (picked == null) break;
                selected.Add(picked);
                remaining.Remove(picked);
            }
            return selected;
        }
    }
}
