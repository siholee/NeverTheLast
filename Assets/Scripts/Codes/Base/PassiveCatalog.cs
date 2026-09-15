using System.Collections.Generic;
using BaseClasses;
using Entities;

namespace Codes.Base
{
    /// <summary>
    /// 해금 패시브의 <b>메타데이터 색인</b>. 이름 · 등급 · 전수 가능 여부 · 은금 사다리를 모아 둔다.
    ///
    /// 이 정보는 전부 코드 클래스 안에 이미 있다. 문제는 <c>ID → 그 사실</c>로 가는 길이
    /// 없었다는 것이다. 스킬 힌트 화면은 "이 금 코드의 선행 은 코드가 무엇인가"를 물어야 하는데,
    /// 사다리는 <see cref="PassiveCode.SupersededByCodeId"/>로 <b>은 쪽에만</b> 적혀 있어
    /// 역방향으로는 읽을 수 없다. 그래서 한 번 훑어 역색인을 만들어 둔다.
    ///
    /// 코드를 하나 만들어 읽고 버리는 방식이다. 패시브 생성자는 값만 채우고
    /// 이벤트 구독은 <c>CastCode()</c>에서 하므로, 만들기만 하는 것은 부작용이 없다.
    /// 사다리를 새로 이어도 여기 손댈 것이 없다 — 은 코드의 선언 한 줄이 곧 색인이다.
    /// </summary>
    public static class PassiveCatalog
    {
        /// <summary>진영 공용 해금 패시브의 ID 구간. 힌트는 이 구간에서만 나온다.</summary>
        public const int MinSharedId = 1;
        public const int MaxSharedId = 179;

        public readonly struct Entry
        {
            public readonly int CodeId;
            public readonly string Name;
            public readonly BaseEnums.CodeGrade Grade;
            public readonly bool Transferable;
            public readonly bool IsUnique;
            public readonly int SupersededByCodeId;

            public Entry(int codeId, string name, BaseEnums.CodeGrade grade, bool transferable,
                bool isUnique, int supersededByCodeId)
            {
                CodeId = codeId;
                Name = name;
                Grade = grade;
                Transferable = transferable;
                IsUnique = isUnique;
                SupersededByCodeId = supersededByCodeId;
            }

            public bool Exists => CodeId > 0;

            /// <summary>힌트로 흘릴 수 있는 코드인가. 고유 패시브와 전수 불가 코드는 제외된다.</summary>
            public bool CanBeHinted => Exists && Transferable && !IsUnique;
        }

        private static readonly Dictionary<int, Entry> Entries = new();

        /// <summary>금 코드 ID → 선행 은 코드 ID.</summary>
        private static readonly Dictionary<int, int> Prerequisites = new();

        private static bool _ladderBuilt;

        /// <summary>
        /// 코드 하나의 메타데이터. 처음 물어본 ID만 그때 만들어 캐시한다.
        /// <paramref name="probeOwner"/>는 생성자에 넘길 시전자다 — 메타데이터는 시전자에
        /// 의존하지 않으므로 아무 아군이나 된다.
        /// </summary>
        public static Entry Get(int codeId, Unit probeOwner)
        {
            if (codeId <= 0) return default;
            if (Entries.TryGetValue(codeId, out Entry cached)) return cached;

            Entry entry = Probe(codeId, probeOwner);
            Entries[codeId] = entry;
            return entry;
        }

        /// <summary>
        /// 이 금 코드를 배우기 전에 가지고 있어야 하는 은 코드. 없으면 0이다.
        /// 은 코드와 고유 코드는 언제나 0을 돌려준다 — 사다리 밖이다.
        /// </summary>
        public static int RequiredCodeIdFor(int codeId, Unit probeOwner)
        {
            EnsureLadder(probeOwner);
            return Prerequisites.TryGetValue(codeId, out int required) ? required : 0;
        }

        /// <summary>
        /// 공용 구간을 한 번 훑어 은금 사다리의 역색인을 만든다.
        /// 없는 ID는 <c>CodeFactory</c>가 null을 돌려주므로 조용히 건너뛴다.
        /// </summary>
        private static void EnsureLadder(Unit probeOwner)
        {
            if (_ladderBuilt) return;
            _ladderBuilt = true;

            for (int codeId = MinSharedId; codeId <= MaxSharedId; codeId++)
            {
                Entry entry = Get(codeId, probeOwner);
                if (!entry.Exists || entry.SupersededByCodeId <= 0) continue;

                // 같은 금 코드를 여럿이 가리키면 먼저 선언된 쪽을 선행으로 삼는다.
                if (!Prerequisites.ContainsKey(entry.SupersededByCodeId))
                {
                    Prerequisites[entry.SupersededByCodeId] = codeId;
                }
            }
        }

        private static Entry Probe(int codeId, Unit probeOwner)
        {
            PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext { Caster = probeOwner });
            if (code == null) return default;

            return new Entry(codeId, string.IsNullOrWhiteSpace(code.CodeName) ? $"패시브 #{codeId}" : code.CodeName,
                code.Grade, code.Transferable, code.IsUniquePassive, code.SupersededByCodeId);
        }

        /// <summary>씬을 갈아엎을 때 색인을 버린다. 코드 정의는 런타임에 바뀌지 않으므로 평소엔 쓰지 않는다.</summary>
        public static void Invalidate()
        {
            Entries.Clear();
            Prerequisites.Clear();
            _ladderBuilt = false;
        }
    }
}
