using System.Collections.Generic;
using System.Linq;

namespace Codes.Base
{
    /// <summary>
    /// 보상 코드(패시브 500~999)의 표 조회. 원본은 `20_codes.yaml`의 패시브 항목이고 여기는 그 위의 규칙이다.
    ///
    /// 티어 · 태그 · 상위 사다리 · 상점 고정 · 후보 조건을 한곳에서 답해, 보상 풀 · 상점 · 캐릭터 창 · 승급의 인장이
    /// 같은 판정을 쓰게 한다(Detail_17).
    /// </summary>
    public static class RewardCodeCatalog
    {
        public const int MinId = 500;
        public const int MaxId = 999;

        /// <summary>T1 깡 스탯 → 대응 T2 퍼센트(청동 승급의 인장 전용, Detail_17 §2.3). 조화의 문장(512)은 고르게 한다.</summary>
        public static readonly IReadOnlyDictionary<int, int> BronzeSealTargets = new Dictionary<int, int>
        {
            { 501, 601 }, { 502, 602 }, { 503, 603 }, { 504, 604 }, { 505, 605 }, { 506, 606 },
            { 507, 607 }, { 508, 608 }, { 509, 601 }, { 510, 603 }, { 511, 607 },
        };

        /// <summary>조화의 문장(512)이 청동 인장으로 바뀔 수 있는 후보.</summary>
        public static readonly IReadOnlyList<int> HarmonySealChoices = new[] { 601, 602, 603, 604, 605 };

        public static CodeCatalog.Entry Find(int codeId)
        {
            if (codeId < MinId || codeId > MaxId) return null;
            CodeCatalog.Entry entry = CodeCatalog.Find(CodeCatalog.Slot.Passive, codeId);
            return entry != null && entry.tier > 0 ? entry : null;
        }

        public static bool IsRewardCode(int codeId) => Find(codeId) != null;

        public static int TierOf(int codeId) => Find(codeId)?.tier ?? 0;

        public static IEnumerable<CodeCatalog.Entry> All()
            => CodeCatalog.All(CodeCatalog.Slot.Passive).Where(entry => entry != null && entry.tier > 0 &&
                                                                       entry.id >= MinId && entry.id <= MaxId);

        /// <summary>이 코드의 바로 위 상위. 없으면 0.</summary>
        public static int UpperOf(int codeId) => Find(codeId)?.supersededBy ?? 0;

        /// <summary><paramref name="upper"/>가 <paramref name="lower"/>의 사다리 위쪽(한 단계 이상)인가.</summary>
        public static bool IsUpperOf(int upper, int lower)
        {
            if (upper <= 0 || lower <= 0 || upper == lower) return false;
            int current = UpperOf(lower);
            for (int guard = 0; current > 0 && guard < 8; guard++)
            {
                if (current == upper) return true;
                current = UpperOf(current);
            }
            return false;
        }

        /// <summary>이 코드를 상위로 가리키는 하위 코드들(사다리 한 단계 아래).</summary>
        public static IEnumerable<int> LowersOf(int codeId) => All().Where(entry => entry.supersededBy == codeId).Select(entry => entry.id);

        /// <summary>
        /// 승급의 인장이 이 코드를 올릴 대상. 청동 인장 전용 T1 → T2 대응을 포함한다. 없으면 0.
        /// </summary>
        public static int SealTargetOf(int codeId)
        {
            if (BronzeSealTargets.TryGetValue(codeId, out int bronze)) return bronze;
            return UpperOf(codeId);
        }

        public static IReadOnlyList<string> TagsOf(int codeId)
            => Find(codeId)?.codeTags ?? (IReadOnlyList<string>)System.Array.Empty<string>();
    }
}
