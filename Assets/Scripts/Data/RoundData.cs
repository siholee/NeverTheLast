using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class RoundPattern
    {
        public List<int> archetypes;    // 일반 라운드용 적 분류 리스트
        public List<int> eliteIds;      // 엘리트 라운드용 엘리트 ID 리스트
        public int bossId;              // 보스 라운드용 보스 ID
        public int weight;              // 이 패턴이 선택될 확률 가중치
    }

    [Serializable]
    public class RoundTypeData
    {
        public int id;
        public string name;
        public bool isElite;
        public bool isBoss;
        public List<RoundPattern> patterns;  // 여러 패턴 중 랜덤 선택
    }

    [Serializable]
    public class StageData
    {
        public int stageNumber;
        public List<int> rounds;    // 각 라운드의 roundType ID
    }

    [Serializable]
    public class RoundTypeDataList
    {
        public List<RoundTypeData> roundTypes;
        public List<StageData> stages;
    }
}
