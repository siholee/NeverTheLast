using StatusEffects.SynergyEffects;

namespace StatusEffects.Base
{
    public class SynergyEffectFactory
    {
        public static SynergyEffect CreateSynergyEffect(int synergyId)
        {
            return synergyId switch
            {
                1 => new AkashaSynergy(),
                2 => new LeaderSynergy(),
                3 => new SniperSynergy(),
                4 => new SentinelSynergy(),
                _ => null,
            };
        }
    }
}