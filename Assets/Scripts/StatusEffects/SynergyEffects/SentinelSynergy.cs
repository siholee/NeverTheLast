using Entities;
using StatusEffects.Base;

namespace StatusEffects.SynergyEffects
{
    public class SentinelSynergy: SynergyEffect
    {
        public override float DefMultiplicativeModifier(Unit unit)
        {
            if (unit.Synergies.Contains(4)) return Stack * 0.2f;
            return 0f;
        }

        public override int DefAdditiveModifier(Unit unit)
        {
            return 100;
        }
    }
}