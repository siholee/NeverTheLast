using Entities;
using StatusEffects.Base;

namespace StatusEffects.SynergyEffects
{
    public class AkashaSynergy: SynergyEffect
    {
        public override float AtkMultiplicativeModifier(Unit unit)
        {
            return Stack * 0.2f;
        }
    }
}