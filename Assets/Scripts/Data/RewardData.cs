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

        /// <summary>보상 카드 한 장의 분류 비율(Detail_17 §5.1). 키: code · consumable · drop.</summary>
        public Dictionary<string, float> rewardCategoryWeights;

        /// <summary>코드 후보의 태그 가중치. 키: match(파티 태그와 겹침) · universal(범용) · other(겹치지 않음).</summary>
        public Dictionary<string, float> codeTagWeights;

        /// <summary>파티 누군가 하위를 가진 상위 코드의 가중치 배율.</summary>
        public float upperCodeWeight = 1.5f;
    }
}
