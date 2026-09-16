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
        public List<int> pendingCharacterUnlockIds = new();
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

    /// <summary>걸려 있는 강화제 한 병. 런 범위 상태라 유닛이 아니라 런 세이브에 실린다.</summary>
    [Serializable]
    public class PartyTonicSaveData
    {
        public int stat;
        public int flat;
        public float multiplier = 1f;
        public int remainingBattles;
        public string sourceName;
    }

    /// <summary>받아 둔 스킬 힌트 하나. 아직 배우지 않은 후보라 런 세이브에 실린다.</summary>
    [Serializable]
    public class SkillHintSaveData
    {
        public int codeId;
        public int level = 1;
        public int stage = 1;
        public string sourceName;
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

        // 이번 훈련 턴의 서포트 배치. 화면을 다시 열 때마다 자리가 바뀌면 안 되므로
        // 굴린 결과를 그대로 들고 있는다. 아직 굴리지 않았으면 placementReady가 false다.
        public bool placementReady;
        public List<TrainingPlacementSaveData> placements = new();
    }

    [Serializable]
    public class TrainingPlacementSaveData
    {
        public int unitId;
        public int stat;
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
        // v5: 유닛 ID를 소속별 블록으로 재배치.
        // v6: 코드 ID를 계열별 구간으로 재배치. ownedPassiveCodes에 담긴 codeId의 뜻이 바뀌었고,
        //     스카디의 주·부 스탯과 수르트의 궁극기 자원 종류도 함께 바뀌었다.
        // v7: 테마 순환이 라운드 번호 계산에서 무작위 추첨으로 바뀌었다. 테마를 저장하지 않으면
        //     불러올 때마다 주사위가 다시 굴러 라운드 도중에 테마가 갈린다.
        // v8: 귀중품 주머니와 상점 매대 장비가 들어왔다. 주머니를 저장하지 않으면
        //     불러올 때 주운 물건이 사라진다.
        public const int CurrentVersion = 8;
        public int gameMode;
        public int currentStage;
        public int currentRound;
        public int life;
        public int killCount;
        public int rerollTicketCount;
        public int gold;
        public bool preparationActionUsed;

        /// <summary>이번 준비 페이즈에 상점에서 산 횟수. 한도는 GameManager.MaxShopPurchases다.</summary>
        public int shopPurchaseCount;
        public List<TokenSaveData> tokens = new();
        public List<int> storedItemIds = new();
        public List<SupportBondSaveData> supportBonds = new();

        /// <summary>걸려 있는 강화제. v7 이전 저장본에는 없던 필드라 비어 있는 채로 채워진다.</summary>
        public List<PartyTonicSaveData> partyTonics = new();

        /// <summary>아직 스킬 Pt를 치르지 않은 힌트. 배우고 나면 목록에서 빠진다.</summary>
        public List<SkillHintSaveData> skillHints = new();

        // 훈련 상태. v4 저장본에는 없던 필드라 기본값으로 채워진다(버전은 올리지 않는다).
        public TrainingSaveData training = new();
        public List<UnitSaveData> heroUnits = new();
        public List<string> triggeredEventIds = new();

        /// <summary>이번 라운드의 테마. 0이면 불러온 뒤 새로 뽑는다(v6 이하 저장본).</summary>
        public int currentThemeId;

        /// <summary>아직 발생하지 않은 예약 사건의 ID. 순서를 그대로 보존한다.</summary>
        public List<string> pendingEventIds = new();

        /// <summary>귀중품 주머니. 값은 주울 때 확정된 것을 그대로 들고 간다.</summary>
        public List<Managers.ValuableHolding> valuables = new();
    }
}
