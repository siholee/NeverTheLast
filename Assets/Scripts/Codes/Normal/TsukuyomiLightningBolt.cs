using BaseClasses;
using Codes.Passive;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 츠쿠요미 일반행동. 단일 적에게 INT 기반 위력 40의 특수 피해를 입히고 월광 침식(2턴 지속피해)을 남긴다.
    ///
    /// 원소는 붙이지 않는다. 츠쿠요미의 몫은 지속피해를 깔고 키우고 정산하는 것 하나뿐이다.
    /// </summary>
    public sealed class TsukuyomiLightningBolt : Sachi
    {
        public TsukuyomiLightningBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
            => TsukuyomiMoonlight.ApplyMoonrot(Caster, target);
    }
}
