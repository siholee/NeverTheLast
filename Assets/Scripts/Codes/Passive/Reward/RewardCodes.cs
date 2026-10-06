using BaseClasses;
using Codes.Base;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// 기믹이 있는 보상 코드의 ID → 클래스 표. 데이터 구동 코드는 여기 없다(<see cref="RewardStatCode"/>).
    /// 클래스는 티어별 파일(<c>RewardCodesT2.cs</c> 등)에 있다.
    /// </summary>
    public static partial class RewardCodes
    {
        public static PassiveCode Create(int codeId, PassiveCodeContext context)
        {
            PassiveCode code = null;
            CreateT2(codeId, context, ref code);
            CreateT3(codeId, context, ref code);
            CreateT4(codeId, context, ref code);
            CreateT5(codeId, context, ref code);
            return code;
        }

        static partial void CreateT2(int codeId, PassiveCodeContext context, ref PassiveCode code);
        static partial void CreateT3(int codeId, PassiveCodeContext context, ref PassiveCode code);
        static partial void CreateT4(int codeId, PassiveCodeContext context, ref PassiveCode code);
        static partial void CreateT5(int codeId, PassiveCodeContext context, ref PassiveCode code);
    }
}
