using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 유닛 세이브 데이터 DTO (JSON 직렬화용)
    /// </summary>
    [Serializable]
    public class UnitSaveData
    {
        public int   unitId;
        public int   currentHP;
        public int   atkBaseBonus;       // 로그라이트 보너스 누적치
        public int   defBaseBonus;
        public float critChanceBonus;
        public float speedBonus;
        public int   hpBaseBonus;
        /// <summary>전열(Frontline) / 후열(Backline) 배치 정보. "Frontline" 또는 "Backline".</summary>
        public string gridLine = "Frontline";
        /// <summary>라인 내 슬롯 인덱스 (0–3)</summary>
        public int slotIndex = 0;
    }

    /// <summary>
    /// 런 세이브 데이터 DTO (JSON 직렬화용)
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public int                currentStage;
        public int                currentEncounter;
        public int                totalBattles;
        public List<UnitSaveData> heroTeam = new();
    }
}
