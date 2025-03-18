using Entities;
using StatusEffects.Base;

namespace StatusEffects.SynergyEffects
{
    public class LeaderSynergy: SynergyEffect
    {
        public override float AtkMultiplicativeModifier(Unit unit)
        {
            return Stack * 0.1f;
        }

        public override float DefMultiplicativeModifier(Unit unit)
        {
            return Stack * 0.1f;
        }

        public override float CritChanceAdditiveModifier(Unit unit)
        {
            return Stack * 0.05f;
        }

        public override float CritMultiplierAdditiveModifier(Unit unit)
        {
            return Stack * 0.15f;
        }
    }
}