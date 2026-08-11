using BaseClasses;

namespace Codes.Normal
{
    /// <summary>단일 적에게 INT 40% 특수 피해를 입힌다.</summary>
    public sealed class TsukuyomiLightningBolt : Sachi
    {
        public TsukuyomiLightningBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "일반공격";
        }
    }
}
