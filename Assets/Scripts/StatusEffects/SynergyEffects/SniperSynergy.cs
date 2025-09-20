using Entities;
using StatusEffects.Base;

namespace StatusEffects.SynergyEffects
{
    public class SniperSynergy: SynergyEffect
    {
        public SniperSynergy(string name, string description) : base(null, name, description)
        {
            Stack = 1;
            Duration = 0f;
        }
        
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