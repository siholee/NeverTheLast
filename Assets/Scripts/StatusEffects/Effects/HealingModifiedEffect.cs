using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    public class HealingModifiedEffect: StatusEffect
    {
        private float _healingModifier;

        public HealingModifiedEffect(float modifier)
        {
            _healingModifier = modifier;
        }

        public override float HealingReceivedModifier(Unit unit)
        {
            return _healingModifier;
        }
    }
}