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
        /// 어느 적을 처치하든 보상 후보에 오르는 장비·귀중품 ID. 기본 장비·범용 T2·고유 장비·범용 귀중품·캐릭터 전용 장비가 여기 있다.
        /// </summary>
        public List<int> commonDropItemIds;
    }
}
