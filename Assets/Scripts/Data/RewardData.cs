using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class RewardTierOddsData
    {
        public int round;
        public List<int> tierWeights;
    }

    [Serializable]
    public class RewardDataList
    {
        public List<RewardTierOddsData> rewardTierOdds;
        public List<RewardDef> rewards;
    }
}
