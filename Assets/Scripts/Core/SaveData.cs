using System;
using System.Collections.Generic;
using BaseClasses;

namespace Core
{
    [Serializable]
    public class TrainedCharacterCollection
    {
        public List<int> unitIds = new();
        public List<int> unlockedStarterUnitIds = new();
        public List<TrainedCharacterRecord> records = new();
    }

    [Serializable]
    public class PrimaryStatSaveData
    {
        public int str;
        public int dex;
        public int con;
        public int intStat;
        public int luk;
    }

    [Serializable]
    public class LearnedPassiveSaveData
    {
        public int codeId;
        public int stage = 1;
        public bool transferable = true;
    }

    [Serializable]
    public class SupportCardSaveData
    {
        public string supportId;
        public int sourceUnitId;
        public string sourceUnitName;
        public string specialtyTraining;
        public int specialtyRate;
        public int specialtyBonus;
        public int trainingBonus;
        public int skillTransferRate;
        public int initialBond;
        public int bondGainRate;
        public int friendshipBonus;
        public int sourcePower;
    }

    [Serializable]
    public class TrainedCharacterRecord
    {
        public int version = 1;
        public int unitId;
        public string unitName;
        public int finalTrainingLevel;
        public PrimaryStatSaveData finalPrimaryStats = new();
        public List<string> titleIds = new();
        public List<LearnedPassiveSaveData> ownedPassiveCodes = new();
        public SupportCardSaveData supportCard = new();
        public long createdAtUnixSeconds;
    }

    [Serializable]
    public class TokenSaveData
    {
        public int tokenId;
        public int amount;
    }

    [Serializable]
    public class SupportBondSaveData
    {
        public int unitId;
        public int currentBond;
    }

    /// <summary>훈련 하나의 레벨. 스탯은 <see cref="BaseClasses.BaseEnums.PrimaryStat"/>의 정수값이다.</summary>
    [Serializable]
    public class TrainingLevelSaveData
    {
        public int stat;
        public int level;
    }

    /// <summary>육성 런의 훈련 상태 — 체력 · 훈련 레벨 · 스킬 Pt · 컨디션.</summary>
    [Serializable]
    public class TrainingSaveData
    {
        public int energy = TrainingState.MaxEnergy;
        public int skillPoints;
        public int conditionIndex = TrainingState.NormalConditionIndex;
        public List<TrainingLevelSaveData> levels = new();
    }

    [Serializable]
    public class UnitSaveData
    {
        public int unitId;
        public int currentHP;
        public int xPos;
        public int yPos;
        public bool isBench;
        // 유닛 레벨과 누적 경험치. 아군 성장 곡선의 단일 축이다.
        public int level = 1;
        public int exp;
        public int trainingLevel;
        public int strUpgrade;
        public int dexUpgrade;
        public int conUpgrade;
        public int intUpgrade;
        public int lukUpgrade;
        public float codeAccelerationBonus;
        public List<int> equippedItemIds = new();
        public List<int> carriedItemIds = new();
        public List<int> grantedPassiveCodeIds = new();
    }

    [Serializable]
    public class RunSaveData
    {
        // 저장 포맷 버전. 레거시 스탯 필드 제거(v2) 이전 저장본은 로드하지 않고 폐기한다.
        public int version = CurrentVersion;
        // v3: 공격력/방어력 스탯 폐지 + 아군 EXP 레벨업 도입으로 유닛 스냅샷 구조가 바뀌었다.
        // v4: 유닛별 휴대 인벤토리와 3단계 중량 시스템.
        public const int CurrentVersion = 4;
        public int gameMode;
        public int currentStage;
        public int currentRound;
        public int life;
        public int killCount;
        public int rerollTicketCount;
        public int gold;
        public bool preparationActionUsed;
        public List<TokenSaveData> tokens = new();
        public List<int> storedItemIds = new();
        public List<SupportBondSaveData> supportBonds = new();

        // 훈련 상태. v4 저장본에는 없던 필드라 기본값으로 채워진다(버전은 올리지 않는다).
        public TrainingSaveData training = new();
        public List<UnitSaveData> heroUnits = new();
        public List<string> triggeredEventIds = new();
    }
}
