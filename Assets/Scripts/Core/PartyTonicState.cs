using System.Collections.Generic;
using BaseClasses;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 런 범위 상태: 강화제. 포켓로그의 플러스 파워 계열을 옮겨 온 것이다.
    ///
    /// 한 병이 <b>출전한 아군 전원</b>의 5스탯 하나를 <b>5전투 동안</b> 올린다.
    /// 전투 중에 걸리는 물건이 아니므로 <c>UnitStatus</c>가 아니다 — 상태는 라운드가 끝날 때
    /// 전부 지워지고, 아군 유닛 자체도 라운드마다 스냅샷에서 다시 세워지기 때문이다.
    /// 그래서 유닛이 아니라 런이 들고 있고, 유닛은 스탯을 계산할 때 여기에 물어본다.
    ///
    /// 남은 전투 수는 <see cref="Managers.GameManager"/>가 라운드를 끝낼 때 한 번 깎는다.
    /// </summary>
    public class PartyTonicState
    {
        /// <summary>한 병이 버티는 전투 수. 이 숫자는 여기에만 있다.</summary>
        public const int BattleDuration = 5;

        /// <summary>
        /// 걸려 있는 강화제 하나.
        /// 합연산(<see cref="Flat"/>)과 곱연산(<see cref="Multiplier"/>)은 서로 다른 칸을 쓴다.
        /// </summary>
        public class Entry
        {
            public BaseEnums.PrimaryStat Stat;
            public int Flat;
            public float Multiplier;
            public int RemainingBattles;
            public string SourceName;

            public bool IsMultiplicative => Multiplier > 1f;
        }

        private readonly List<Entry> _entries = new();

        public IReadOnlyList<Entry> Active => _entries;

        public bool HasAny => _entries.Count > 0;

        /// <summary>
        /// 새 강화제를 건다.
        ///
        /// 같은 스탯·같은 방식(합/곱)은 <b>한 병만</b> 남는다. 더 강한 쪽이 남고 지속은 5전투로
        /// 되돌아간다. 무한 누적을 허용하면 100스테이지 런에서 강화제만 모아도 스탯이 터진다.
        /// 방식이 다르면(합 + 곱) 함께 걸린다.
        /// </summary>
        public void Apply(BaseEnums.PrimaryStat stat, int flat, float multiplier, string sourceName)
        {
            bool multiplicative = multiplier > 1f;
            if (!multiplicative && flat <= 0) return;

            Entry existing = _entries.Find(entry => entry.Stat == stat && entry.IsMultiplicative == multiplicative);
            if (existing == null)
            {
                _entries.Add(new Entry
                {
                    Stat = stat,
                    Flat = multiplicative ? 0 : flat,
                    Multiplier = multiplicative ? multiplier : 1f,
                    RemainingBattles = BattleDuration,
                    SourceName = sourceName,
                });
                return;
            }

            bool stronger = multiplicative ? multiplier > existing.Multiplier : flat > existing.Flat;
            if (stronger)
            {
                existing.Flat = multiplicative ? 0 : flat;
                existing.Multiplier = multiplicative ? multiplier : 1f;
                existing.SourceName = sourceName;
            }
            existing.RemainingBattles = BattleDuration;
        }

        /// <summary>해당 스탯에 걸린 합연산 보정의 합계.</summary>
        public int FlatBonus(BaseEnums.PrimaryStat stat)
        {
            int total = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Stat == stat) total += _entries[i].Flat;
            }

            return total;
        }

        /// <summary>해당 스탯에 걸린 곱연산 보정의 곱.</summary>
        public float Multiplier(BaseEnums.PrimaryStat stat)
        {
            float multiplier = 1f;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Stat == stat) multiplier *= Mathf.Max(0f, _entries[i].Multiplier);
            }

            return multiplier;
        }

        /// <summary>전투 하나를 소비한다. 만료된 병이 있으면 true를 돌려준다(스탯 재계산 신호).</summary>
        public bool ConsumeBattle()
        {
            bool expired = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                _entries[i].RemainingBattles -= 1;
                if (_entries[i].RemainingBattles > 0) continue;

                Debug.Log($"[강화제] {_entries[i].SourceName} 효과가 끝났습니다.");
                _entries.RemoveAt(i);
                expired = true;
            }

            return expired;
        }

        public void Clear() => _entries.Clear();

        /// <summary>HUD 한 줄 요약. 걸린 게 없으면 빈 문자열이다.</summary>
        public string DescribeShort()
        {
            if (_entries.Count == 0) return "";

            var parts = new List<string>();
            foreach (Entry entry in _entries)
            {
                string amount = entry.IsMultiplicative
                    ? $"×{entry.Multiplier:0.00}"
                    : $"+{entry.Flat}";
                parts.Add($"{entry.Stat} {amount} ({entry.RemainingBattles}전투)");
            }

            return string.Join(" · ", parts);
        }

        public List<PartyTonicSaveData> BuildSaveData()
        {
            var result = new List<PartyTonicSaveData>();
            foreach (Entry entry in _entries)
            {
                result.Add(new PartyTonicSaveData
                {
                    stat = (int)entry.Stat,
                    flat = entry.Flat,
                    multiplier = entry.Multiplier,
                    remainingBattles = entry.RemainingBattles,
                    sourceName = entry.SourceName,
                });
            }

            return result;
        }

        public void Restore(List<PartyTonicSaveData> saved)
        {
            _entries.Clear();
            if (saved == null) return;

            foreach (PartyTonicSaveData data in saved)
            {
                if (data == null || data.remainingBattles <= 0) continue;
                _entries.Add(new Entry
                {
                    Stat = (BaseEnums.PrimaryStat)data.stat,
                    Flat = data.flat,
                    Multiplier = data.multiplier <= 0f ? 1f : data.multiplier,
                    RemainingBattles = data.remainingBattles,
                    SourceName = data.sourceName,
                });
            }
        }
    }
}
