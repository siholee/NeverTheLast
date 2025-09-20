using StatusEffects.SynergyEffects;

namespace StatusEffects.Base
{
    public class SynergyEffectFactory
    {
        public static SynergyEffect CreateSynergyEffect(int synergyId)
        {
            return synergyId switch
            {
                1 => new AkashaSynergy("아카샤", "아카샤설명"),
                2 => new LeaderSynergy("리더", "리더설명"),
                3 => new SniperSynergy("스나이퍼", "스나이퍼설명"),
                4 => new SentinelSynergy("센티넬", "센티넬설명"),
                _ => null,
            };
        }
    }
}