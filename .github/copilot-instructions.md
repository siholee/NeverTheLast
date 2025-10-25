# GitHub Copilot Instructions for NeverTheLast

## Project Overview
**NeverTheLast**는 Unity 기반의 오토체스/타워디펜스 하이브리드 게임입니다. C#으로 작성되었으며, 플레이어는 그리드에 영웅 유닛을 전략적으로 배치하여 적의 웨이브와 싸우고, 자원을 관리하며, 시너지와 유닛 업그레이드를 통해 라운드를 진행합니다.

## Core Technologies
- **Engine**: Unity 6000.2+
- **Language**: C#
- **Platform**: Windows
- **Data Format**: YAML (YamlDotNet)
- **Package ID**: `com.solid.autochess`

## Code Style & Conventions

### Naming Conventions
- **Classes/Structs**: PascalCase (예: `GameManager`, `UnitStatus`)
- **Methods**: PascalCase (예: `SpawnUnit`, `TakeDamage`)
- **Fields (private)**: camelCase with underscore prefix (예: `_eventDict`, `_unitList`)
- **Fields (serialized)**: camelCase (예: `hpCurr`, `manaCurr`)
- **Properties**: PascalCase (예: `HpMax`, `IsEnemy`)
- **Constants**: PascalCase (예: `MaxBenchSlots`)
- **Enums**: PascalCase for type and values (예: `UnitEventType.OnSpawn`)

### Architecture Patterns
1. **Singleton Pattern**: 모든 Manager 클래스는 싱글톤으로 구현
   ```csharp
   public class XManager : MonoBehaviour
   {
       public static XManager Instance { get; private set; }
       
       private void Awake()
       {
           if (Instance != null && Instance != this)
           {
               Destroy(gameObject);
               return;
           }
           Instance = this;
       }
   }
   ```

2. **Event-Driven Architecture**: Unit 클래스는 이벤트 기반 시스템 사용
   - `Dictionary<BaseEnums.UnitEventType, Delegate>` 패턴으로 이벤트 관리
   - 이벤트 타입: `OnSpawn`, `OnDeath`, `OnTakingDamage`, `OnRoundStart` 등

3. **State Machine**: GameManager는 게임 상태 관리
   - 상태: `Preparation` → `RoundInProgress` → `RoundEnd` → `GameOver`

### Serialization
- Unity Inspector에 노출할 필드는 `[SerializeField]` 사용
- Public property와 private serialized field 패턴:
  ```csharp
  [SerializeField] private int hpMax;
  public int HpMax { get => hpMax; protected set => hpMax = value; }
  ```

## Project Structure

### Key Directories
- `Assets/Scripts/Managers/`: 싱글톤 Manager 클래스들
- `Assets/Scripts/Entities/`: Unit, Cell 등 게임 엔티티
- `Assets/Scripts/BaseClasses/`: 기본 클래스와 Enum 정의
- `Assets/Scripts/Codes/`: 유닛 스킬/능력 코드
- `Assets/Scripts/StatusEffects/`: 상태효과 및 시너지 효과
- `Assets/Scripts/Helpers/`: 유틸리티 헬퍼 클래스
- `Assets/Resources/Data/`: YAML 데이터 파일
  - `10_units.yaml`: 유닛 스탯, 코드, 시너지
  - `20_codes.yaml`: 유닛 스킬 매핑
  - `30_status.yaml`: 상태효과 정의
  - `40_synergies.yaml`: 시너지 정의
  - `50_tokens.yaml`: 인게임 재화 정의
  - `70_rounds.yaml`: 라운드 웨이브 정의

### Key Managers
1. **GameManager**: 중앙 게임 상태 컨트롤러, 라운드 전환, 생명 시스템
2. **GridManager**: 배틀 그리드(-3~3, 0~3) 및 벤치(9슬롯) 관리, 유닛 스폰/타겟팅
3. **DataManager**: YAML 데이터 로딩 (유닛, 시너지)
4. **UIManager**: UI 업데이트 및 상호작용
5. **ShopManager**: 상점 로직 및 유닛 구매/판매
6. **SynergyManager**: 시너지 계산 및 효과 적용
7. **BenchManager**: 벤치 유닛 관리
8. **InventoryManager**: 플레이어 인벤토리 관리

## Coding Guidelines

### Manager Access
- 항상 `Manager.Instance`를 통해 접근:
  ```csharp
  GameManager.Instance.CurrentLife
  GridManager.Instance.SpawnUnit(unitId, cell, isEnemy)
  ```

### Grid System
- **배틀 그리드**: x: -3~3, y: 0~3 (y=0인 벤치는 전투 불참)
- **벤치**: 별도의 9개 슬롯, 배틀 필드와 분리
- 유닛은 18개 셀에 비활성 상태로 대기, 필요시 활성화하여 재사용

### Unit Lifecycle
1. 스폰: `GridManager.SpawnUnit()` → 비활성 유닛 활성화 및 초기화
2. 전투: 자동 전투 로직 실행
3. 사망: 유닛 비활성화 → 2초 대기 (즉시 재소환 방지)
4. 재사용: 풀링 패턴으로 오브젝트 재활용

### Event System
- 이벤트 구독/발행 시 적절한 `UnitEventType` 사용
- 이벤트 핸들러는 반드시 정리 (메모리 누수 방지)
- 주요 이벤트:
  - `OnSpawn`: 유닛 스폰 시
  - `OnDeath`: 유닛 사망 시
  - `OnTakingDamage`: 피해를 받을 때
  - `OnRoundStart`: 라운드 시작 시
  - `OnRoundEnd`: 라운드 종료 시

### Status Effects & Synergies
- 상태 효과는 `StatusEffect` 베이스 클래스 상속
- 시너지 효과는 `SynergyEffect` 베이스 클래스 상속
- 인터페이스 구현:
  - `ITemporalEffect`: 시간 제한 효과
  - `IStackEffect`: 중첩 가능 효과
  - `IHpChangeEffect`: HP 변경 효과

### Data Loading
- YAML 파일은 `Resources/Data/` 경로에서 로드
- DataManager에서 중앙 관리
- 데이터 구조는 `BaseClasses/Info.cs`에서 정의

## Common Patterns

### Adding New Unit
1. YAML 파일에 유닛 데이터 추가 (`10_units.yaml`)
2. 필요시 스킬 코드 작성 (`Scripts/Codes/`)
3. 유닛별 패시브 작성 (`Scripts/StatusEffects/Passive/`)
4. GridManager의 유닛 리스트에 등록

### Adding New Synergy
1. YAML 파일에 시너지 데이터 추가 (`40_synergies.yaml`)
2. `SynergyEffect` 클래스 작성 (`Scripts/StatusEffects/SynergyEffects/`)
3. `SynergyEffectFactory`에 등록
4. SynergyManager에서 효과 적용 로직 구현

### Creating New Manager
```csharp
using UnityEngine;

namespace Managers
{
    public class NewManager : MonoBehaviour
    {
        public static NewManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Manager logic here
    }
}
```

## Testing & Debugging
- Unity Editor의 Play Mode에서 테스트
- Inspector에서 직렬화된 필드 확인 가능
- Debug 로그는 `debuglog.txt`에 기록
- 주요 씬: `Assets/Scenes/SampleScene.unity`

## Performance Considerations
- 유닛 풀링을 통한 메모리 최적화
- 이벤트 구독 해제로 메모리 누수 방지
- 타겟팅 로직 최적화 (프레임당 과도한 연산 방지)

## Additional Notes
- 한국어 주석과 변수명 혼용 가능
- 벤치 유닛(y=0)은 전투에 참여하지 않음
- 라운드 종료 시 남은 적의 수만큼 생명력 감소
- 준비 시간 후 자동으로 라운드 시작
- 모든 시스템은 GameManager를 중심으로 연결

## When Generating Code
- 항상 네임스페이스 포함 (예: `using Managers;`, `using Entities;`)
- Unity 생명주기 메서드 순서: `Awake` → `Start` → `Update`
- Null 체크를 통한 방어적 프로그래밍
- 에러 발생 시 적절한 로그 출력
- 성능을 고려한 코드 작성 (캐싱, 불필요한 연산 제거)

