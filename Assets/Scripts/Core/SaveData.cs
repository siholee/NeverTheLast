using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 계정 단위 캐릭터 해금 기록. 런이 끝나도 남는다.
    /// 2026-09 개편으로 육성 기록(서포트 카드 · 최종 스탯)은 훈련과 함께 사라졌다.
    /// </summary>
    [Serializable]
    public class TrainedCharacterCollection
    {
        public List<int> unlockedStarterUnitIds = new();
        public List<int> pendingCharacterUnlockIds = new();
    }

    [Serializable]
    public class TokenSaveData
    {
        public int tokenId;
        public int amount;
    }

    [Serializable]
    public class UnitSaveData
    {
        public int unitId;
        public int currentHP;
        public int xPos;
        public int yPos;
        public bool isBench;
        /// <summary>전투 사이와 저장/불러오기에서 유지되는 궁극기 자원.</summary>
        public int ultimateResource;
        // 유닛 레벨과 누적 경험치. 아군 성장의 유일한 축이다(훈련 강화는 없다).
        public int level = 1;
        public int exp;
        /// <summary>보상 코드(와 사건 패시브)의 ID. 칸 순서를 그대로 보존한다.</summary>
        public List<int> grantedPassiveCodeIds = new();
        public List<int> activeRewardCodeSlots = new();
        public List<int> storedRewardCodeIds = new();
        public List<RewardCodeInstanceData> activeRewardCodeInstances = new();
        public List<RewardCodeInstanceData> storedRewardCodeInstances = new();
        public List<PermanentStatSaveData> nectarStats = new();
        /// <summary>런 영구 중첩(아누비스의 영혼 등). 소수 몫까지 함께 옮긴다.</summary>
        public List<PermanentStackSaveData> permanentStacks = new();
    }

    /// <summary>코드 한 장의 고유 저장 상태. 장착 위치·보유자가 바뀌어도 그대로 옮긴다.</summary>
    [Serializable]
    public class RewardCodeInstanceData
    {
        public int codeId;
        public string instanceId;
        public float charge;
        public int acquiredStage = 1;
        public RewardCodeInstanceData Copy() => new() { codeId=codeId, instanceId=instanceId, charge=charge, acquiredStage=acquiredStage };
    }
    [Serializable]
    public class PermanentStatSaveData
    {
        public string stat;
        public float amount;
    }

    [Serializable]
    public class PermanentStackSaveData
    {
        public string key;
        public int value;
        public float fraction;
    }

    [Serializable]
    public class RunSaveData
    {
        public int version = CurrentVersion;
        // v8까지: 5스탯 · 훈련 · 서포트 카드 시대의 저장본.
        // v9: 2026-09 전면 개편. 8스탯 · 훈련 폐지 · 캐릭터 로스터 초기화로 유닛 스냅샷과 ID의 뜻이
        //     모두 바뀌었다. 이전 저장본은 불러오지 않고 폐기한다.
        // v11: 장비 · 개인 인벤토리 폐지, 공용 인벤토리와 짧은/긴 휴식 도입.
        // v12: 보상 코드 7칸 · 영구 중첩 · 비약/코드 도구 소모품. 유닛 스냅샷의 뜻이 바뀌어 v11은 폐기한다.
        // v13: 빈 자리를 포함한 활성 7칸 + 캐릭터별 코드 보관함. v12는 기존 활성 코드를 그대로 이관한다.
        // v14: 코드별 축전 저장량·고유 식별자·취득 스테이지, 넥타르 영구 스탯. v12/v13 보존 이관.
        // v15: 테마당 10라운드를 8라운드로 이관. 코드 인스턴스·축전량·넥타르는 보존한다.
        public const int CurrentVersion = 15;
        public int currentStage;
        public int currentRound;
        public int life;
        public int killCount;
        public int rerollTicketCount;
        public int gold;
        public bool restUsedThisPreparation;
        public int shortRestCharges = 2;

        /// <summary>이번 준비 페이즈에 상점에서 산 횟수. 한도는 GameManager.MaxShopPurchases다.</summary>
        public int shopPurchaseCount;

        /// <summary>준비 화면에서 써 둔 전투 비약. 다음 전투 시작에 걸리고 비워진다. 0이면 없음.</summary>
        public int pendingTonicItemId;

        public List<TokenSaveData> tokens = new();
        public List<int> storedItemIds = new();
        public List<UnitSaveData> heroUnits = new();
        public List<string> triggeredEventIds = new();

        /// <summary>이번 라운드의 테마. 0이면 불러온 뒤 새로 뽑는다.</summary>
        public int currentThemeId;

        /// <summary>아직 발생하지 않은 예약 사건의 ID. 순서를 그대로 보존한다.</summary>
        public List<string> pendingEventIds = new();

        /// <summary>귀중품 주머니. 값은 주울 때 확정된 것을 그대로 들고 간다.</summary>
        public List<Managers.ValuableHolding> valuables = new();
    }
}
