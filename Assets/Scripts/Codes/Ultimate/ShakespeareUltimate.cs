using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;

namespace Codes.Ultimate
{
    /// <summary>셰익스피어 U — 3턴간 소환수 피해와 1회 부활을 제공하는 결계.</summary>
    public sealed class ShakespeareQuestion : UltimateCode
    {
        public ShakespeareQuestion(UltimateCodeContext context) : base(context)
        {
            CodeName = "사느냐, 죽느냐, 그것이 문제로다";
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Resolve());
        }

        private IEnumerator Resolve()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (cast)
            {
                Caster.AddStatus(BuffStatus.Create(
                    ShakespeareCombat.FieldStatusId, "shakespeare_summon_field", CodeName,
                    Caster, Caster, new ShakespeareSummonFieldEffect(),
                    duration: 3,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "3턴간 모든 아군 소환수가 가하는 피해 +25%. 결계 동안 각 소환수는 처음 쓰러질 때 한 번 부활합니다."));
            }
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
        public override void StopCode() { if (Caster != null) Caster.isCasting = false; }
    }
}
