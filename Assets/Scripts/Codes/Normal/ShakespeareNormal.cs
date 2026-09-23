using System.Collections;
using BaseClasses;
using Codes.Base;

namespace Codes.Normal
{
    /// <summary>셰익스피어 N — 공격 대신 언어 중첩 3을 얻는다.</summary>
    public sealed class ShakespeareInspiration : BaseNormalCode
    {
        public ShakespeareInspiration(NormalCodeContext context) : base(context)
        {
            CodeName = "영감의 기록";
            Power = 0;
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast) { StopCode(); yield break; }
            Caster.SetCombatResourceMaximum(Codes.Passive.ShakespeareCombat.LanguageResource,
                Codes.Passive.ShakespeareCombat.LanguageMaximum);
            Caster.AddCombatResource(Codes.Passive.ShakespeareCombat.LanguageResource, 3);
            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }
}
