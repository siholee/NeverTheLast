using BaseClasses;
using Codes.Base;

namespace Codes.Ultimate
{
    /// <summary>
    /// 라그나로크는 자동 시전하지 않는 상시형 궁극기다.
    /// 불 원소 대상 공격으로 쌓는 전용 자원과 효과는 고유 패시브 SurtrTwilight가 관리한다.
    /// </summary>
    public sealed class SurtrRagnarok : UltimateCode
    {
        public override bool IsAutoCast => false;

        public SurtrRagnarok(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "라그나로크";
            Cooldown = 0f;
            CastingDelay = 0f;
            MaxStage = 1;
        }

        public override bool HasValidTarget() => false;
    }
}
