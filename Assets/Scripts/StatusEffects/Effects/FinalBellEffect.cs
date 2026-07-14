using BaseClasses;
using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    public class FinalBellEffect : StatusEffect, ITemporalEffect
    {
        public const string StatusIdentifier = "FinalBell";
        public bool IsActive => Duration > 0f;

        public FinalBellEffect(Unit grantor) : base(grantor, StatusIdentifier)
        {
            Stack = 1;
            Duration = 8f;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit == null || unit != Grantor || stat != BaseEnums.PrimaryStat.DEX || !IsActive)
            {
                return 0;
            }

            return unit.Level * 2;
        }

        public void OnUpdate(EventContext context)
        {
            Duration -= context.FloatParam;
            if (Duration <= 0f && context.Grantee != null)
            {
                context.Grantee.RemoveStatusEffect(StatusIdentifier);
            }
        }

        public void UpdateDuration(float duration)
        {
            Duration += duration;
        }

        public int IsTriggered(float deltaTime)
        {
            return 0;
        }
    }
}
