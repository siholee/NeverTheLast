using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class RoundPattern
    {
        public List<int> enemyIds;      // 테마 전용으로 직접 지정하는 적 ID 리스트

        /// <summary>
        /// 열을 못 박아 배치하는 목록. <see cref="enemyIds"/>는 적의 <c>archetype</c>이 열을
        /// 정하므로 <b>같은 병종을 전열과 후열에 나눠 세울 수 없다</b>. 공허 테마의
        /// 1·2스테이지처럼 프리즘만으로 양 열을 채우는 편성이 이 필드를 쓴다.
        /// 둘 중 하나라도 적혀 있으면 <c>enemyIds</c> 대신 이쪽을 본다.
        /// </summary>
        public List<int> frontIds;
        public List<int> rearIds;

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
