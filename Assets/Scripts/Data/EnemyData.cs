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
        public string tagline; // 호버 시 전투 성격을 소개하는 한 문장
        public int themeId;     // 스테이지 테마 필터 ID
        public int archetype;   // 스폰/배치용 적 분류 ID
        public string tier;     // normal, elite, boss

        /// <summary>
        /// 이 적을 <b>처치했을 때</b> 보상 후보에 오르는 공용 아이템 ID.
        /// 한 전투에서 처치한 적들의 드랍 + 공용 드랍 + 소모품이 한 풀로 합쳐진 뒤 티어 확률로 3장이 뽑힌다.
        /// </summary>
        public List<int> drops;
        public RestoredEnemyKitData reworkKit;
    }

    /// <summary>오리지널 적의 전투 역할을 현행 스탯·행동·효과 명중으로 옮긴 명시 사양.</summary>
    [Serializable]
    public class RestoredEnemyKitData
    {
        public int originalId;
        public string normalName, ultimateName, passiveName;
        public string normalMode = "Single", ultimateMode = "Single", passiveMode = "None";
        public int attackForm = 50004;
        public bool contact;
        public float normalRatio = 1f, ultimateRatio = 1.8f;
        public int normalTargets = 1, ultimateTargets = 1, ultimateHits = 1;
        public string normalElement = "None", ultimateElement = "None";
        public string normalDot, ultimateDot;
        public float armorIgnore, healingReduction;
        public float shieldRatio, lifesteal;
        public int wardHits;
    }

    [Serializable]
    public class EnemyDataList
    {
        public List<EnemyData> enemies;
    }
}
