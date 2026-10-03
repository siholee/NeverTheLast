using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>
    /// 적 정의(<c>60_enemies.yaml</c>). 스탯 · 코드 · 초상화는 <see cref="CombatantData"/>에 있다.
    /// 적의 레벨은 스테이지가 정한다 — 같은 성장 공식을 아군과 함께 쓴다.
    /// </summary>
    [Serializable]
    public class EnemyData : CombatantData
    {
        public int themeId;     // 스테이지 테마 필터 ID
        public int archetype;   // 스폰/배치용 적 분류 ID
        public string tier;     // normal, elite, boss

        /// <summary>
        /// 이 적을 <b>처치했을 때</b> 보상 후보에 오르는 공용 아이템 ID.
        /// 한 전투에서 처치한 적들의 드랍 + 공용 드랍 + 소모품이 한 풀로 합쳐진 뒤 티어 확률로 3장이 뽑힌다.
        /// </summary>
        public List<int> drops;
    }

    [Serializable]
    public class EnemyDataList
    {
        public List<EnemyData> enemies;
    }
}
