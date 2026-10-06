using BaseClasses;
using Codes.Base;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// 보상 코드 ID(500~999) → 클래스. 데이터 구동 코드(<see cref="RewardStatCode"/>)는 표만 보고 만들고,
    /// 기믹이 있는 코드는 여기 스위치에 한 줄씩 잇는다. 구현이 없으면 null — 보상 풀이 후보에서 뺀다.
    /// </summary>
    public static class RewardCodeFactory
    {
        public static PassiveCode Create(int codeId, PassiveCodeContext context)
        {
            CodeCatalog.Entry entry = RewardCodeCatalog.Find(codeId);
            if (entry == null) return null;

            PassiveCode special = CreateSpecific(codeId, context);
            if (special != null) return special;

            bool dataDriven = entry.flatStats != null || entry.percentStats != null ||
                              (entry.damageTag > 0 && entry.damageTagBonus > 0f);
            return dataDriven ? new RewardStatCode(context, codeId) : null;
        }

        /// <summary>구현된 보상 코드인가. 보상 풀 · 상점이 미구현 코드를 내밀지 않게 묻는다.</summary>
        public static bool IsImplemented(int codeId) => Create(codeId, new PassiveCodeContext()) != null;

        private static PassiveCode CreateSpecific(int codeId, PassiveCodeContext context)
            => RewardCodes.Create(codeId, context);
    }
}
