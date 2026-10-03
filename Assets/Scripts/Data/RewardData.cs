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

        /// <summary>
        /// 어느 적을 처치하든 보상 후보에 오르는 공용 아이템 ID. 식량과 범용 귀중품이 여기 있다.
        /// </summary>
        public List<int> commonDropItemIds;
    }
}
