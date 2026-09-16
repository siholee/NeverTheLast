using Codes.Base;
using BaseClasses;
using Entities;

namespace Codes.Passive
{
    public sealed class LavoisierMassConservation : UniquePassiveCode
    {
        public LavoisierMassConservation(PassiveCodeContext context) : base(context)
        {
            CodeName = "질량 보존";
            Caster.Chemistry ??= new LavoisierChemistry(Caster);
        }

        public override void CastCode()
        {
            if (!Caster.Chemistry.Active) Caster.Chemistry.BeginRound();
        }

        public override void StopCode() => Caster?.Chemistry?.EndRound();
    }
}
