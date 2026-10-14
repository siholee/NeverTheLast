using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
namespace Codes.Ultimate
{
    public sealed class SuryaNoEscape : BaseUltimateCode
    {
        public SuryaNoEscape(UltimateCodeContext context) : base(context) => CodeName = "도망칠 곳은 없어";
        protected override IEnumerator Perform()
        {
            var passive = Caster.ActivePassiveCodes.OfType<SuryaExecution>().FirstOrDefault();
            foreach (var enemy in AliveEnemies()) passive?.Mark(enemy, 2);
            var buff = BuffStatus.Create(70672, "surya_sunset", "석양의 집행", Caster, Caster,
                new StatModifierEffect(BaseEnums.StatType.Attack, .25f, percent: true), duration: 2, isBeneficial: true);
            buff.NormalActionDuration = true;
            // 실제 버프 부여 경로가 흉부의 태양 인장을 한 번 소유한다. 얼굴 위 범용 버프광은 쓰지 않는다.
            buff.AffectedVfxPartId = "affected.buff.sunset";
            Caster.AddStatus(buff);
            Effects.Vfx.VfxComposer.PlayScreen("surya.ultimate", Caster, Caster);
            Managers.AudioManager.PlayCue("surya.ultimate.resolve");
            yield return new UnityEngine.WaitForSeconds(.3f);
        }
    }
}
