using StatusEffects.Base;
using Unity.VisualScripting;
using Unit = Entities.Unit;

namespace StatusEffects.SynergyEffects
{
    public class AkashaSynergy: SynergyEffect
    {
        public AkashaSynergy(string name, string description) : base(null, name, description)
        {
            Stack = 1;
            Duration = 0f;
        }
        public override float AtkMultiplicativeModifier(Unit unit)
        {
            return Stack * 0.2f;
        }
    }
}