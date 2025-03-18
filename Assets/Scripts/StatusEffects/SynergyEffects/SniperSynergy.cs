using Entities;
using StatusEffects.Base;

namespace StatusEffects.SynergyEffects
{
    public class SniperSynergy: SynergyEffect
    {
        public override float AtkMultiplicativeModifier(Unit unit)
        {
            return Stack * 0.15f;
        }

        public override float CodeAccelerationMultiplicativeModifier(Unit unit)
        {
            if (unit.Synergies.Contains(3)) return Stack * 0.2f;
            return 0f;
        }
    }
}