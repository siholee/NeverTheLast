using System.Collections.Generic;
using BaseClasses;
using Entities;

namespace Combat
{
    /// <summary>
    /// 한 판 동안 아군이 넣은 피해를 사람별로 센다. 전투 중 딜 그래프와 결과 화면의 딜 그래프가 같은 값을 읽는다.
    ///
    /// 값은 <see cref="Unit.AnyDamageDealt"/>에서 받는다 — 방어막과 체력에서 <b>실제로 깎인 양</b>이라
    /// 넘친 피해나 막힌 타격은 들어가지 않는다. 소환수가 넣은 피해는 소환자의 몫으로 친다.
    /// <see cref="Unit.RoundDamageDealt"/>는 소마 같은 코드가 판정용으로 쓰는 값이라 건드리지 않고 따로 센다.
    /// </summary>
    public static class DamageMeter
    {
        public sealed class Entry
        {
            public Unit Unit;
            public string Name;
            public string Portrait;
            public long Damage;
        }

        private static readonly List<Entry> Entries = new();
        private static bool _subscribed;

        /// <summary>지금 세고 있는가. 전투가 끝나면 값은 남아 있지만 더 늘지 않는다.</summary>
        public static bool Recording { get; private set; }

        public static IReadOnlyList<Entry> Current => Entries;

        /// <summary>전투 시작. 출전한 아군을 편성 순서대로 0으로 올려 둔다(한 번도 못 때린 사람도 그래프에 선다).</summary>
        public static void Begin(IEnumerable<Unit> party)
        {
            if (!_subscribed)
            {
                Unit.AnyDamageDealt += OnDamageDealt;
                _subscribed = true;
            }

            Entries.Clear();
            if (party != null)
            {
                foreach (Unit unit in party) Find(unit, create: true);
            }
            Recording = true;
        }

        public static void End() => Recording = false;

        /// <summary>가장 많이 넣은 사람의 피해. 막대 길이의 기준이다.</summary>
        public static long Max()
        {
            long max = 0;
            foreach (Entry entry in Entries)
                if (entry.Damage > max) max = entry.Damage;
            return max;
        }

        public static long Total()
        {
            long total = 0;
            foreach (Entry entry in Entries) total += entry.Damage;
            return total;
        }

        /// <summary>많이 넣은 순서. 같으면 편성 순서를 지킨다.</summary>
        public static List<Entry> Sorted()
        {
            var sorted = new List<Entry>(Entries);
            for (int i = 1; i < sorted.Count; i++)
            {
                Entry current = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j].Damage < current.Damage)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }
                sorted[j + 1] = current;
            }
            return sorted;
        }

        private static void OnDamageDealt(DamageResolvedContext context)
        {
            if (!Recording || context == null || context.DamageDealt <= 0) return;

            Unit source = context.Attacker;
            if (source != null && source.IsSummon) source = source.SummonOwner;
            if (source == null || source.IsEnemy) return;
            // 아군끼리 때리는 경우(자해 · 동료 소모)는 딜로 치지 않는다.
            if (context.Target != null && !context.Target.IsEnemy) return;

            Entry entry = Find(source, create: true);
            if (entry != null) entry.Damage += context.DamageDealt;
        }

        private static Entry Find(Unit unit, bool create)
        {
            if (unit == null) return null;
            foreach (Entry entry in Entries)
                if (ReferenceEquals(entry.Unit, unit)) return entry;
            if (!create) return null;

            var added = new Entry { Unit = unit, Name = unit.UnitName, Portrait = unit.PortraitPath };
            Entries.Add(added);
            return added;
        }
    }
}
