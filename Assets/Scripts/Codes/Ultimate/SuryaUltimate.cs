using BaseClasses;
using Combat;

namespace Codes.Ultimate
{
    /// <summary>
    /// 수리야 U — 디바카르. 전장을 <b>햇빛</b>으로 바꾼다. 수리야 기준 3턴.
    ///
    /// 피해를 내지 않는다. 햇빛은 수리야의 고유 패시브 <c>사비타</c>가 도는 조건이자
    /// 불 편성 전체의 배율이라, 이 궁극기 자체가 편성의 스위치다.
    /// </summary>
    public sealed class SuryaDivakara : SimpleUltimate
    {
        private const int FieldTurns = 3;

        public SuryaDivakara(UltimateCodeContext context)
            : base(context, "디바카르", 4, 0.4f) { }

        protected override void Resolve() => Battlefield.Set(FieldKind.Sunlight, Caster, FieldTurns);
    }
}
