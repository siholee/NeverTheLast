# 적 소환 시스템 개편 요약

## 핵심 변경사항

### 이전 시스템 ❌
- 대기열(Queue) 방식으로 적을 순차적으로 소환
- 셀이 비면 대기열에서 적을 꺼내 소환
- `70_rounds.yaml`에 셀별로 적 ID 리스트 정의

### 새로운 시스템 ✅
- 라운드 시작 시 모든 적을 즉시 소환
- 스테이지 테마가 적의 진영 결정
- 라운드 타입이 소환될 직업 결정
- 직업에 따라 자동으로 행(Row) 배치

---

## 시스템 구조

```
스테이지 (1~∞)
└── 스테이지 테마 (랜덤 선택, 예: 노르드)
    └── 8개 라운드
        ├── 1~3 라운드: 일반
        ├── 4 라운드: 엘리트
        ├── 5~6 라운드: 일반
        ├── 7 라운드: 일반 (8스테이지 이후: 엘리트)
        └── 8 라운드: 엘리트 (8스테이지 이후: 보스)
```

---

## 데이터 파일

### 1. `15_enemies.yaml` - 적 데이터
```yaml
enemies:
  - id: 1001          # 일반 적: 1000번대
    name: 발키리
    faction: 3        # 노르드 (소속)
    class: 9          # 처형자 (직업)
    tier: normal      # 일반/엘리트/보스
    # ... 스탯 정보
  
  - id: 2001          # 엘리트: 2000번대
    name: 헤임달
    tier: elite
  
  - id: 3001          # 보스: 3000번대
    name: 오딘
    tier: boss
```

### 2. `75_stage_themes.yaml` - 스테이지 테마
```yaml
stageThemes:
  - id: 1
    name: 노르드
    faction: 3        # 이 스테이지에서 노르드 진영 적만 등장
```

### 3. `80_round_types.yaml` - 라운드 구성
```yaml
roundTypes:
  - id: 1
    name: 혼합 라운드 1
    classes: [7, 8, 10]     # 파수꾼, 투사, 사수
    enemyCount: 3           # 3마리 소환

  - id: 10
    name: 엘리트 라운드 1
    isElite: true
    eliteIds: [2001]        # 헤임달
    classes: [8, 10]        # + 투사, 사수
    enemyCount: 2

  - id: 20
    name: 보스 라운드
    isBoss: true
    bossId: 3001            # 오딘
    classes: [7, 8]         # + 호위 (파수꾼, 투사)
    enemyCount: 2

stages:
  - stageNumber: 1
    rounds: [1, 2, 3, 10, 5, 6, 7, 11]  # 각 라운드의 타입 ID
```

---

## 노르드 진영 적 목록

### 일반 적 (ID: 1001~1009)

**1열 (전사형)**
- 1001: 발키리 (처형자)
- 1002: 광전사 (투사)
- 1003: 후스카를 (투사)
- 1004: 욤스비킹 (파수꾼)

**2열 (지원형)**
- 1007: 흘리드스캴프 (책략가)
- 1008: 세이드 (메카닉)
- 1009: 바니르 사제 (지원가)

**3열 (원거리)**
- 1005: 애시르 사수 (사수)
- 1006: 감반테인 (마법사)

### 엘리트 (ID: 2001~2002)
- 2001: 헤임달 (파수꾼) - 4라운드 등장
- 2002: 토르 (투사) - 8라운드 등장

### 보스 (ID: 3001)
- 3001: 오딘 (마법사) - 8스테이지 이후 8라운드

---

## 배치 규칙

### 직업별 행(Row) 자동 배치
- **1행**: 처형자(9), 투사(8), 파수꾼(7)
- **2행**: 책략가(12), 지원가(14), 메카닉(13)
- **3행**: 사수(10), 마법사(11)

### 예시
```
라운드 타입: [파수꾼, 투사, 사수] 3마리 소환

배치 결과:
    3열    2열    1열
3행 [사수]
2행
1행 [파수꾼][투사]

※ 아래부터 채우고, 같은 행이면 왼쪽부터 배치
```

---

## 코드 변경사항

### DataManager.cs
```csharp
// 새로운 데이터 로드 메서드
public EnemyDataList FetchEnemyDataList()
public StageThemeDataList FetchStageThemeDataList()
public RoundTypeDataList FetchRoundTypeDataList()

// 새로운 데이터 클래스
public class EnemyData { ... }
public class StageThemeData { ... }
public class RoundTypeData { ... }
```

### RoundManager.cs
```csharp
// 스테이지 시스템 추가
public int Stage { get; private set; }
public void InitializeStage(int stageNumber)  // 스테이지 초기화

// 라운드 시작 시 즉시 소환
public void LoadRound(int roundNumber)
    └── SpawnEnemiesForRound()    // 라운드 타입에 따라 소환
        └── PlaceEnemies()        // 직업별 행 배치

// 대기열 시스템 제거
❌ private Dictionary<int, Queue<int>> _spawnQueues
❌ private bool TrySpawnEnemy()
❌ public void NotifyCellAvailable()
```

### Unit.cs
```csharp
protected virtual void LoadData(bool _isEnemy, int _id)
{
    if (_isEnemy)
    {
        // EnemyData에서 로드
        EnemyDataList enemyDataList = ...;
        EnemyData enemyData = enemyDataList.enemies.Find(...);
        // 소속 = faction, 직업 = class
        Synergies = new List<int> { enemyData.faction, enemyData.@class };
    }
    else
    {
        // 기존 UnitData에서 로드 (영웅)
    }
}
```

### GameManager.cs
```csharp
void Start()
{
    // ...
    _roundManager = new RoundManager(dataManager);
    _roundManager.InitializeStage(1);  // 추가됨
    _roundManager.LoadRound(1);
}
```

---

## 사용 방법

### 새로운 진영 추가하기
1. `15_enemies.yaml`에 적 데이터 추가 (새 faction ID 사용)
2. `75_stage_themes.yaml`에 스테이지 테마 추가
3. 완료! 자동으로 랜덤 선택됨

### 라운드 구성 변경하기
1. `80_round_types.yaml`의 `stages` 섹션 수정
2. 원하는 라운드 타입 ID를 순서대로 배치

### 적 밸런싱
- `15_enemies.yaml`에서 적 스탯 조정
- 티어에 따라 능력치 차별화

---

## 테스트 체크리스트

- [ ] 스테이지 시작 시 테마 로그 확인
- [ ] 각 라운드마다 올바른 직업의 적 소환
- [ ] 적이 정확한 행에 배치
- [ ] 4라운드에 엘리트 등장
- [ ] 8라운드에 엘리트 등장 (8스테이지까지)
- [ ] 8스테이지 이후 7라운드 엘리트, 8라운드 보스
- [ ] 8라운드 완료 후 다음 스테이지 진행

---

## 문제 해결

### 에러: "Enemy data not found"
→ `15_enemies.yaml` 파일 확인, 적 ID 범위 확인

### 에러: "Stage theme data not found"
→ `75_stage_themes.yaml` 파일 확인

### 에러: "RoundType not found"
→ `80_round_types.yaml` 파일 확인, 스테이지 구성 확인

### 적이 소환되지 않음
→ 콘솔 로그 확인, 코드 구현 여부 확인, 초상화 이미지 확인

---

**작성일**: 2025-10-27
