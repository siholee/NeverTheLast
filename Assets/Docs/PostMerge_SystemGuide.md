# Main + Main2 병합 이후 시스템 가이드

이 문서는 main과 main2를 합친 이후 구현된 핵심 시스템과, 이후 개발자가 신규 유닛/테마/적/코드(스킬)를 추가할 때 따라야 할 절차를 정리한다.

## 핵심 구조

### 전장 셀 구조

- 각 진영은 2 x 4 구조를 가진다.
- 아군 영역은 전열/후열 각각 최대 4칸이며, 총 5명의 아군 유닛을 배치할 수 있다.
- 적 영역도 같은 방식으로 전열/후열을 사용한다.
- 적 배치는 `RoundManager.PlaceEnemies()`에서 `archetype`을 기준으로 전열/후열을 결정한다.

현재 적 분류별 배치 규칙:

- 전열: 7, 8, 9
- 후열: 10, 11, 12, 13, 14

관련 파일:

- `Assets/Scripts/Managers/GridManager.cs`
- `Assets/Scripts/Managers/RoundManager.cs`
- `Assets/Scripts/Entities/Cell.cs`

### 게임 모드

현재 런은 크게 육성 모드와 무한 모드를 기준으로 설계되어 있다.

육성 모드:

- 시작 시 메인 캐릭터 1명과 서포트 캐릭터 4명을 선택한다.
- 중복 캐릭터 편성은 허용하지 않는다.
- 최대 100스테이지까지 진행한다.
- 100스테이지 클리어 이후 런이 종료되고, 육성이 완료된 캐릭터는 저장 데이터에 기록된다.
- 스테이지 사이의 육성 페이즈에서 메인 캐릭터를 집중 훈련한다(아래 "육성 페이즈" 참고).

무한 모드:

- 육성이 완료된 캐릭터만 사용할 수 있다.
- 총 5명의 캐릭터를 배치한다.
- 최대 스테이지 제한이 없다.
- 적은 스테이지 증가에 따라 계속 강해진다.
- 보상 확률은 10라운드 이후에도 10라운드 확률을 유지한다.

관련 파일:

- `Assets/Scripts/Managers/RunManager.cs`
- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/CharacterSelectionManager.cs`
- `Assets/Scripts/Core/SaveData.cs`
- `Assets/Scripts/Core/SaveSystem.cs`
- `Assets/Scripts/Core/GameStartIntent.cs`

### 진행 플로우

전투 이후 공통 흐름:

```text
스테이지 전투 -> 보상 선택 -> 다음 스테이지
```

육성 모드에서는 보상 이후 임시 육성 페이즈를 거쳐 다음 스테이지로 넘어간다.

상태 흐름은 `BaseEnums.GameState`에 정의되어 있다.

주요 상태:

- `CharacterSelection`
- `Preparation`
- `RoundInProgress`
- `RoundEnd`
- `RewardSelection`
- `EventStage`
- `TrainingPhase`
- `RunComplete`
- `GameOver`

관련 파일:

- `Assets/Scripts/BaseClasses/Enums.cs`
- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/RunManager.cs`
- `Assets/Scripts/Managers/UIManager.cs`

## 라운드와 스테이지

### 라운드 정의

- 1라운드는 10스테이지로 구성된다.
- 라운드는 1부터 시작한다.
- 현재 전투 스테이지 기준 라운드 계산식은 `((stage - 1) / 10) + 1`이다.
- 각 라운드는 하나의 테마를 가진다.
- 현재 구현은 `80_stages.yaml`의 `stageThemes`를 라운드 순서에 따라 순환 선택한다.

### 특수 스테이지

각 라운드의 10스테이지 안에서 다음 규칙이 적용된다.

- 5스테이지: 이벤트 스테이지
- 8스테이지: 테마 중간 보스
- 10스테이지: 테마 보스
- 25, 50, 75, 100스테이지: 테마와 관계없는 고정 보스
- 고정 보스가 등장하는 라운드에서는 해당 라운드의 테마 보스가 9스테이지에 등장한다.

관련 파일:

- `Assets/Scripts/Managers/RoundManager.cs`
- `Assets/Resources/Data/70_rounds.yaml`
- `Assets/Resources/Data/80_stages.yaml`

### 테마 데이터

테마는 `Assets/Resources/Data/80_stages.yaml`에 정의한다.

```yaml
stageThemes:
  - id: 1
    name: 노르드
    enemyThemeId: 3
    description: 노르드 테마의 적들이 등장합니다.
    midBossId: 2001
    bossId: 3001
    uniqueRewardIds:
      - nordic_fury

fixedBossStages:
  - stage: 25
    bossId: 3001
```

필드 의미:

- `id`: 테마 ID
- `name`: 표시 이름
- `enemyThemeId`: 해당 테마에서 일반 적을 뽑을 때 사용할 테마 필터 ID
- `description`: 설명
- `midBossId`: 라운드 8스테이지에 등장할 중간 보스 ID
- `bossId`: 라운드 10스테이지에 등장할 보스 ID
- `uniqueRewardIds`: 이 테마에서만 보상 풀에 추가되는 보상 ID
- `fixedBossStages`: 특정 전투 스테이지에 강제로 등장할 고정 보스

## 보상 시스템

전투 종료 후 3개의 보상 선택지를 획득한다.

보상 데이터는 `Assets/Resources/Data/90_rewards.yaml`에서 관리한다.

```yaml
rewards:
  - id: nordic_fury
    displayName: 노르드의 격노
    description: 노르드 테마 전용 보상. 선택 유닛 STR 강화 +2
    tier: 3
    themeIds: [1]
    atkBonus: 2

rewardTierOdds:
  - round: 1
    tierWeights: [100, 0, 0, 0, 0]
```

보상 등급 확률:

- `rewardTierOdds`는 라운드별 1~5티어 보상 가중치를 의미한다.
- 육성 모드는 현재 라운드에 맞는 확률을 사용한다.
- 무한 모드는 10라운드 이후에도 10라운드 확률을 유지한다.
- 테마 전용 보상은 `themeIds`가 현재 테마 ID와 맞을 때만 등장한다.

현재 보상 필드:

- `healAmount`: 전체 아군 고정 회복
- `fullHealParty`: 전체 아군 완전 회복
- `atkBonus`: 선택 유닛 STR 강화
- `defBonus`: 선택 유닛 CON 강화
- `hpBonus`: 선택 유닛 CON 강화
- `intBonus`: 선택 유닛 INT 강화
- `critChanceBonus`: 선택 유닛 LUK 강화
- `codeAccelerationBonus`: 현재는 DEX 강화 계열 보상으로 사용
- `rerollTicketBonus`: 리롤 티켓 증가
- `randomTokenAmount`: 무작위 토큰 지급

관련 파일:

- `Assets/Scripts/Managers/RewardManager.cs`
- `Assets/Scripts/Managers/UIManager.cs`
- `Assets/Resources/Data/90_rewards.yaml`

## 육성(트레이닝) 시스템

육성 모드에서 스테이지 사이마다 육성 페이즈가 실행되어 메인 캐릭터를 성장시킨다.

동작:

- 육성 페이즈 UI에서 5스탯(STR/DEX/CON/INT/LUK) 중 하나를 집중 훈련으로 선택한다.
- 선택한 스탯이 강화되고, 메인의 트레이닝 레벨이 +1 오른다.
- 트레이닝 레벨이 오르면 레벨 해금 패시브(#2/#3) 해금을 다시 판정한다.
- 서포트 캐릭터는 훈련 효과를 증폭한다.

집중 스탯 강화량 계산:

- 기본 강화량 `BaseStatGain`(1).
- 서포트 1명당 `SupportStatBonusPerUnit`(1) 추가.
- 집중 스탯이 서포트의 특기 스탯(클래스 주 스탯, `ClassCatalog.GetPrimaryStat`)과 일치하면 서포트 1명당 `AffinityBonusPerUnit`(1) 추가.

트레이닝 레벨은 `Unit.TrainingLevel`로 저장/복원되며, 패시브 해금 판정은 `PassiveUnlockLevel = Level + TrainingLevel`을 사용한다.

관련 파일:

- `Assets/Scripts/Managers/TrainingManager.cs`
- `Assets/Scripts/Managers/GameManager.cs` (`EnterTrainingPhase`, `CompleteTrainingPhaseWithFocus`)
- `Assets/Scripts/Managers/UIManager.cs` (`EnsureTrainingPhasePanel`, 5스탯 집중 버튼)
- `Assets/Scripts/Entities/Unit.cs` (`AddStatUpgrade`, `GainTrainingLevel`, `TrainingLevel`)

## 패시브 시스템 (유닛당 3개)

모든 유닛은 패시브 3개를 가진다: 초기 패시브 1개 + 레벨 해금 패시브 2개.

- 초기 패시브는 `codes.passive`(단일 ID)로 지정하며 항상 활성화된다.
- 레벨 해금 패시브는 `levelPassives` 목록으로 지정한다: `{ codeId, unlockLevel, stage }`.
- `unlockLevel`은 `PassiveUnlockLevel`(= Level + TrainingLevel) 기준으로 판정한다.
- 육성 중에는 메인만 트레이닝 레벨이 오르므로, 서포트의 #2/#3 패시브는 그 유닛이 메인일 때(또는 무한 모드의 육성 완료 유닛일 때) 의미가 있다.

런타임에서는 `Unit.PassiveCodes`(List)가 활성 패시브를, `PendingLevelPassives`가 아직 해금되지 않은 정의를 보관한다.
레벨업 후 `RefreshLevelPassives()`가 해금 대상을 승격한다.

관련 파일:

- `Assets/Scripts/Managers/DataManager.cs` (`LevelPassiveData`, `UnitData.levelPassives`)
- `Assets/Scripts/Entities/Unit.cs` (`PassiveCodes`, `LoadPassiveCodes`, `RefreshLevelPassives`)

## 클래스 시스템 (제거됨)

BG3식 클래스 레벨/서브클래스 육성 시스템은 복잡도 문제로 제거되었다.

- `unitClass`는 단순 식별 문자열 태그로만 유지한다.
- 용도: 일반공격 접촉/비접촉 판정(`ClassCatalog.IsContactCombatClass`), 장비 숙련도, 육성 특기 스탯(`ClassCatalog.GetPrimaryStat`).
- `classLevels`/`subclasses` 필드는 유닛/적 데이터와 저장 데이터에서 제거되었다. YAML에 남아 있어도 `IgnoreUnmatchedProperties`로 무시된다.
- 유닛 성장은 육성(트레이닝) 시스템과 보상/업그레이드로만 이루어진다.

## 스탯 시스템

### 플레이어가 관여하는 5개 스탯

모든 유닛은 다음 5개 주요 스탯을 가진다.

- STR
- DEX
- CON
- INT
- LUK

각 스탯은 base, level increment, upgrade increment 구조를 가진다.

```yaml
strBase: 10
strIncrementLvl: 1
strIncrementUpgrade: 1
dexBase: 10
dexIncrementLvl: 1
dexIncrementUpgrade: 1
conBase: 10
conIncrementLvl: 1
conIncrementUpgrade: 1
intBase: 10
intIncrementLvl: 1
intIncrementUpgrade: 1
lukBase: 10
lukIncrementLvl: 1
lukIncrementUpgrade: 1
```

현재 파생 스탯 계산:

- STR: 일반공격 공격력, 방어력
- DEX: 속도, 회피율
- CON: 최대 체력, 치유 보너스, 보호막 보너스
- INT: 스킬 발동율, 마나 효율
- LUK: 치명타 확률

현재 코드 기준 대표 계산:

- 최대 체력: `CON * 1000`
- 공격력: `STR * 10`
- 방어력: `STR * 3`
- 치명타 확률: `LUK * 1%`
- 치명타 피해: 기본 150%
- 마나 최대값: 마나형 궁극기 자원은 기본 100
- 스택형 궁극기 자원은 `ultimateResourceMax` 또는 기존 `manaBase`를 사용

정확한 계산은 `Unit.GetDerived...()` 계열 메서드에서 관리한다.

관련 파일:

- `Assets/Scripts/Entities/Unit.cs`
- `Assets/Scripts/BaseClasses/Enums.cs`
- `Assets/Scripts/Managers/DataManager.cs`

### 레거시 스탯 호환

기존 데이터에는 `hpBase`, `atkBase`, `defBase`, `critChance`, `manaBase` 등이 남아 있다.

현재 `strBase` 등 5스탯이 YAML에 없으면 기존 레거시 스탯을 임시로 5스탯으로 변환한다. 이는 기존 저장/유닛 데이터와 충돌을 줄이기 위한 과도기 처리이다.

새로운 데이터는 가급적 5스탯 필드를 직접 작성한다.

레거시 modifier 정리:

- `AtkAdditiveModifier`
- `AtkMultiplicativeModifier`
- `DefAdditiveModifier`
- `DefMultiplicativeModifier`
- `HpAdditiveModifier`
- `HpMultiplicativeModifier`
- `CodeAccelerationMultiplicativeModifier`
- `HealingReceivedModifier`

위 계열은 제거되었다. 새 효과는 5스탯, Status 시스템, Code 시스템, DamageContext를 기준으로 구현한다.

유지된 효과 축:

- 치명타 확률 보정
- 치명타 피해 보정
- 받는 피해 보정
- HP 변화 효과
- 보호막
- 피해 태그
- 관통

## 코드(스킬) 시스템

### 코드 분류

코드는 세 종류로 구분한다.

- `PassiveCode`: 패시브 코드
- `NormalCode`: 일반공격/일반 코드
- `UltimateCode`: 궁극기 코드

`CodeActivationType`:

- `Passive`
- `Active`
- `Ultimate`

주의:

- 예전 `Special` 명칭은 사용하지 않는다.
- 궁극기 코드는 `UltimateCode` 및 `CodeActivationType.Ultimate`로 분류한다.

관련 파일:

- `Assets/Scripts/Codes/Base/Code.cs`
- `Assets/Scripts/Codes/Base/PassiveCode.cs`
- `Assets/Scripts/Codes/Base/NormalCode.cs`
- `Assets/Scripts/Codes/Base/UltimateCode.cs`
- `Assets/Scripts/BaseClasses/Enums.cs`

### 발동률 규칙

- 일반공격/일반 코드는 발동 실패하지 않는다.
- 궁극기 코드는 발동 실패하지 않는다.
- INT 기반 발동률은 현재 패시브 코드에만 적용된다.
- 조건부 스킬은 조건을 만족해야 하며, 조건을 만족한 뒤에도 패시브라면 INT 기반 발동 판정을 통과해야 한다.

### 궁극기 자원

기본은 마나이며, 유닛별로 독자 자원을 사용할 수 있다.

유닛/적 YAML 필드:

```yaml
ultimateResourceType: Mana
ultimateResourceName: 마나
ultimateResourceMax: 100
```

스택형 예시:

```yaml
ultimateResourceType: Stack
ultimateResourceName: 종언의 시
ultimateResourceMax: 9
```

동작 규칙:

- `RecoverMana()`는 마나형 자원에만 적용된다.
- 스택형 자원은 코드나 이벤트에서 `AddUltimateResource(amount)`를 직접 호출해 증가시킨다.
- 궁극기 자원이 최대치에 도달해도 즉시 시전하지 않는다.
- 유닛이 행동 가능한 상태일 때만 궁극기 시전을 시도한다.
- 현재 `Unit.Update()`에서 `!isControlled && !isCasting` 조건 안에서 궁극기 발동을 검사한다.

예시 설계:

```text
종언의 시:
아군이 적에게 디버프를 부여할 때마다 AddUltimateResource(1).
9스택이 되면 다음 행동 가능 타이밍에 궁극기 코드 발동.
```

## 상태, 피해, 보호막

### Status 시스템

현재 두 종류의 상태 시스템이 공존한다.

- 기존 `StatusEffect` 딕셔너리 기반 시스템
- 신규 `UnitStatus` 리스트 기반 시스템

신규 개발은 가능하면 `UnitStatus` 기반으로 작성한다.

`UnitStatus`는 다음을 지원한다.

- 상태 ID
- 이름/설명
- 지속시간
- 카테고리: Positive, Negative, Neutral
- 중첩 정책: Stack, ExtendDuration, ReplaceIfStronger, Ignore
- 여러 효과 인스턴스

관련 파일:

- `Assets/Scripts/Entities/Status/UnitStatus.cs`
- `Assets/Scripts/Effects/Base/BaseEffect.cs`
- `Assets/Scripts/Effects/Base/EffectFactory.cs`
- `Assets/Resources/Data/30_status.yaml`

### DamageContext

공격 처리에는 여전히 피해 태그와 관통이 필요하다.

피해 처리 흐름:

```text
OnBeforeDamageTaken -> OnTakingDamage -> OnAfterDamageTaken
```

`DamageContext`는 공격자, 피해량, 치명타 여부, 코드 타입, 피해 태그, 관통 정보를 담는다.

관련 파일:

- `Assets/Scripts/BaseClasses/Contexts.cs`
- `Assets/Scripts/BaseClasses/Enums.cs`
- `Assets/Scripts/Entities/Unit.cs`

### 치유 보너스와 보호막 보너스

CON은 최대 체력과 치유 보너스에 더해 보호막 보너스에도 연동된다.

- 치유 보너스: 회복량 계산에 사용
- 보호막 보너스: `AddShield`, `SetShield`에서 보호막 부여량을 증폭

관련 파일:

- `Assets/Scripts/Entities/Unit.cs`
- `Assets/Scripts/StatusEffects/Effects/ShieldEffect.cs`

## 신규 유닛 추가 절차

### 1. 유닛 YAML 추가

파일:

- `Assets/Resources/Data/10_units.yaml`

예시:

```yaml
  - id: 101
    name: 신규 캐릭터
    element: None
    unitClass: Ranger
    strBase: 10
    strIncrementLvl: 1
    strIncrementUpgrade: 1
    dexBase: 10
    dexIncrementLvl: 1
    dexIncrementUpgrade: 1
    conBase: 10
    conIncrementLvl: 1
    conIncrementUpgrade: 1
    intBase: 10
    intIncrementLvl: 1
    intIncrementUpgrade: 1
    lukBase: 10
    lukIncrementLvl: 1
    lukIncrementUpgrade: 1
    ultimateResourceType: Mana
    ultimateResourceName: 마나
    ultimateResourceMax: 100
    codes:
      passive: 10      # 초기 패시브(#1, 항상 활성)
      normal: 10
      ultimate: 10
    codeStages:
      passive: 1
      normal: 1
      ultimate: 1
    # 레벨 해금 패시브(#2, #3). 모든 유닛은 초기 패시브 1 + 레벨 해금 패시브 2 = 총 3개.
    # unlockLevel은 PassiveUnlockLevel(= Level + TrainingLevel) 기준으로 판정한다.
    levelPassives:
      - { codeId: 20, unlockLevel: 5,  stage: 1 }
      - { codeId: 21, unlockLevel: 10, stage: 1 }
    portrait: NEW_CHARACTER_PORTRAIT
    cost: [1, 2]
    costAmount: 6
    tier: 1
```

체크 포인트:

- `id`는 기존 유닛과 겹치면 안 된다.
- `element`는 `None`, `Fire`, `Water`, `Grass`, `Wind`, `Electric`, `Ice`, `Rock` 중 하나를 사용한다.
- `unitClass`는 현재 문자열 기반으로 관리하며 BG3식 클래스 이름을 사용한다. 예: `Ranger`, `Wizard`, `Cleric`, `Fighter`.
- `codes`는 `20_codes.yaml`과 `CodeFactory`에 모두 연결되어야 한다.
- `portrait`는 `Resources/Sprite/Portraits/{portrait}` 경로에서 로드된다.
- 새 데이터는 5스탯 필드를 직접 작성한다.

### 2. 선택 UI/저장 조건 확인

캐릭터 선택, 육성 완료 저장, 무한 모드 사용 가능 여부는 `CharacterSelectionManager`, `RunManager`, `SaveData` 흐름을 따른다.

신규 유닛이 선택 목록에 보이지 않으면 다음을 확인한다.

- `10_units.yaml`에 정상 등록되었는가
- DataManager가 YAML을 정상 로드하는가
- 선택 UI가 해당 유닛 ID를 필터링하고 있지 않은가
- 무한 모드라면 해당 유닛이 육성 완료 데이터에 포함되어 있는가

## 신규 적 추가 절차

### 1. 적 YAML 추가

파일:

- `Assets/Resources/Data/60_enemies.yaml`

예시:

```yaml
  - id: 1101
    name: 신규 전사
    themeId: 3
    archetype: 8
    element: Fire
    unitClass: Fighter
    tier: normal
    strBase: 10
    strIncrementLvl: 1
    strIncrementUpgrade: 1
    dexBase: 8
    dexIncrementLvl: 1
    dexIncrementUpgrade: 1
    conBase: 12
    conIncrementLvl: 1
    conIncrementUpgrade: 1
    intBase: 5
    intIncrementLvl: 0
    intIncrementUpgrade: 0
    lukBase: 5
    lukIncrementLvl: 0
    lukIncrementUpgrade: 0
    ultimateResourceType: Mana
    ultimateResourceName: 마나
    ultimateResourceMax: 100
    codes:
      passive: 100
      normal: 100
      ultimate: 100
    portrait: SEI_PORTRAIT
```

체크 포인트:

- `id`는 기존 적과 겹치면 안 된다.
- `themeId`는 테마의 `enemyThemeId`와 매칭된다.
- `archetype`은 라운드 패턴의 `archetypes`와 매칭된다.
- `element`와 `unitClass`는 유닛 정체성 및 일반공격 접촉/비접촉 판정에 사용된다.
- `tier`는 일반 적 검색에서 `"normal"`을 사용한다.
- 중간 보스/보스는 테마에서 직접 ID로 지정한다.

### 2. 배치 규칙 확인

일반 적은 `RoundManager.GetRandomEnemyByThemeAndArchetype()`로 선택된다.

배치 열은 `archetype`에 따라 정해진다.

- 7, 8, 9: 전열
- 그 외: 후열

새 적 분류 ID를 추가한다면 `RoundManager.GetColumnForArchetype()`도 같이 갱신한다.

## 신규 테마 추가 절차

### 1. 테마 등록

파일:

- `Assets/Resources/Data/80_stages.yaml`

예시:

```yaml
stageThemes:
  - id: 2
    name: 신규 테마
    enemyThemeId: 4
    description: 신규 테마 적들이 등장합니다.
    midBossId: 2101
    bossId: 3101
    uniqueRewardIds:
      - new_theme_reward
```

체크 포인트:

- 테마의 `enemyThemeId`와 같은 `themeId`를 가진 normal 적이 `60_enemies.yaml`에 있어야 한다.
- `midBossId`와 `bossId`는 `60_enemies.yaml`에 존재해야 한다.
- `uniqueRewardIds`를 사용한다면 같은 ID의 보상이 `90_rewards.yaml`에 있어야 한다.

### 2. 테마 전용 보상 추가

파일:

- `Assets/Resources/Data/90_rewards.yaml`

예시:

```yaml
  - id: new_theme_reward
    displayName: 신규 테마 보상
    description: 신규 테마 전용 보상
    tier: 3
    themeIds: [2]
    atkBonus: 2
```

## 신규 코드(스킬) 추가 절차

코드는 YAML 등록과 C# 클래스, `CodeFactory` 연결이 모두 필요하다.

### 1. 코드 종류 선택

패시브:

- `Assets/Scripts/Codes/Passive`
- `PassiveCode` 상속
- INT 기반 발동률 체크 대상

일반:

- `Assets/Scripts/Codes/Normal`
- `NormalCode` 상속
- 발동 실패하지 않음
- 일반공격과 같은 행동 주기에서 사용

궁극기:

- `Assets/Scripts/Codes/Ultimate`
- `UltimateCode` 상속
- 발동 실패하지 않음
- 궁극기 자원이 최대치이고 행동 가능한 상태일 때 발동

### 2. C# 클래스 작성

예시:

```csharp
using Codes.Base;

namespace Codes.Ultimate
{
    public class NewUltimate : UltimateCode
    {
        public NewUltimate(UltimateCodeContext context) : base(context)
        {
            CodeName = "신규 궁극기";
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive;
        }

        public override void CastCode()
        {
            base.CastCode();
            // 효과 구현
        }
    }
}
```

실제 구현 시 기존 코드들을 먼저 참고한다.

- 일반 코드 예시: `Assets/Scripts/Codes/Normal/a005_NAtlanta.cs`
- 궁극기 예시: `Assets/Scripts/Codes/Ultimate/a005_U_Moonfall.cs`
- 보호막 예시: `Assets/Scripts/Codes/Test/GiveShield.cs`
- 상태 부여 예시: `Assets/Scripts/Codes/Normal/a005_NAtlanta.cs`

### 3. CodeFactory 연결

파일:

- `Assets/Scripts/Codes/Base/CodeFactory.cs`

예시:

```csharp
public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
{
    return codeId switch
    {
        10 => new NewUltimate(context),
        _ => null,
    };
}
```

패시브/일반/궁극기 종류에 맞는 팩토리 메서드에 추가한다.

### 4. 코드 YAML 등록

파일:

- `Assets/Resources/Data/20_codes.yaml`

예시:

```yaml
codes:
  ultimate:
    - id: 10
      verbalName: 신규 궁극기
      codeName: NewUltimate
```

주의:

- `id`는 같은 코드 종류 안에서 겹치면 안 된다.
- `codeName`은 문서/데이터 식별용으로 사용되며, 실제 인스턴스 생성은 `CodeFactory`가 담당한다.
- 유닛의 `codes.ultimate` 값은 여기 등록한 ID 및 `CodeFactory` ID와 맞아야 한다.

### 5. 유닛에 코드 연결

파일:

- `Assets/Resources/Data/10_units.yaml`
- `Assets/Resources/Data/60_enemies.yaml`

예시:

```yaml
codes:
  passive: 10
  normal: 10
  ultimate: 10
codeStages:
  passive: 1
  normal: 1
  ultimate: 1
```

`codeStages`는 1단계 또는 3단계 코드 설계를 위해 사용한다.

## 신규 상태/효과 추가 절차

신규 상태는 가능하면 신규 `UnitStatus` 시스템으로 작성한다.

### 1. 상태 정의

현재 상태 기본값은 `UnitStatus.LoadStatusData()`에 하드코딩된 부분이 있다. 상태 데이터 YAML 연동을 확장할 경우 `30_status.yaml`과 `DataManager` 로딩 구조도 함께 정리한다.

파일:

- `Assets/Scripts/Entities/Status/UnitStatus.cs`
- `Assets/Resources/Data/30_status.yaml`

### 2. 효과 정의

효과는 `BaseEffect`를 상속하고 `EffectFactory`에 연결한다.

파일:

- `Assets/Scripts/Effects/Base/BaseEffect.cs`
- `Assets/Scripts/Effects/Base/EffectFactory.cs`
- `Assets/Scripts/Effects/Positive`
- `Assets/Scripts/Effects/Negative`
- `Assets/Scripts/Effects/Neutral`

### 3. 코드에서 상태 부여

예시 흐름:

```csharp
var status = new UnitStatus(statusId, Caster, target);
status.AddEffect(effectId, duration);
target.AddStatus(status);
```

상태를 부여하는 코드가 궁극기 자원 스택을 올려야 한다면, 부여 성공 시점에 `AddUltimateResource(1)`를 호출하는 식으로 연결한다.

## 신규 보상 추가 절차

파일:

- `Assets/Resources/Data/90_rewards.yaml`

예시:

```yaml
  - id: str_training
    displayName: STR 훈련
    description: 선택 유닛 STR 강화 +1
    tier: 1
    atkBonus: 1
```

체크 포인트:

- `id`는 고유해야 한다.
- `tier`는 1~5를 사용한다.
- 테마 전용이면 `themeIds`를 지정한다.
- 새 효과 필드가 필요하면 `RewardDef`와 `RewardManager.ApplyReward()`를 함께 확장한다.

관련 파일:

- `Assets/Scripts/Managers/RewardManager.cs`
- `Assets/Resources/Data/90_rewards.yaml`

## 데이터 로딩 파일 목록

주요 YAML:

- `10_units.yaml`: 아군 유닛
- `20_codes.yaml`: 코드 표시 데이터
- `30_status.yaml`: 상태/효과 데이터
- `50_tokens.yaml`: 토큰 데이터
- `60_enemies.yaml`: 적 데이터
- `70_rounds.yaml`: 라운드 패턴
- `80_stages.yaml`: 테마/고정 보스
- `90_rewards.yaml`: 보상/등급 확률

로딩 담당:

- `Assets/Scripts/Managers/DataManager.cs`

## 구현 후 확인 체크리스트

신규 콘텐츠를 추가한 뒤 최소한 다음을 확인한다.

1. YAML 들여쓰기가 깨지지 않았는가
2. 새 ID가 기존 ID와 중복되지 않는가
3. 유닛/적의 코드 ID가 `20_codes.yaml`과 `CodeFactory`에 모두 존재하는가
4. 테마 `enemyThemeId`와 적 `themeId`가 매칭되는가
5. 라운드 패턴의 `archetypes`와 적 `archetype`이 매칭되는가
6. 보스/중간 보스 ID가 `60_enemies.yaml`에 존재하는가
7. 테마 전용 보상 ID가 `90_rewards.yaml`에 존재하는가
8. 궁극기 자원 타입이 `Mana` 또는 `Stack`으로 지정되었는가
9. 일반 코드와 궁극기 코드에 INT 실패 판정을 직접 넣지 않았는가
10. 패시브 코드에 영구 효과를 등록했다면 제거 조건도 설계했는가
11. `dotnet build Assembly-CSharp.csproj`가 성공하는가
12. `dotnet build Assembly-CSharp-Editor.csproj`가 성공하는가

## 현재 과도기/주의 사항

- 기존 문서나 일부 YAML에는 `hpBase`, `atkBase`, `defBase`, `manaBase` 같은 레거시 필드가 남아 있다.
- 새 유닛/적은 5스탯 필드를 우선 작성한다.
- `AtkCurr`, `DefCurr`, `HpMax`, `ManaCurr` 같은 파생 전투 값은 UI와 전투 계산 호환을 위해 유지된다.
- `StatusEffect` 기반 레거시 상태와 신규 `UnitStatus` 기반 상태가 공존한다.
- 새 개발은 가능하면 `UnitStatus`와 `BaseEffect` 기반으로 진행한다.
- Unity가 `.csproj`를 재생성할 수 있으므로, 파일 삭제 후 IDE 빌드에서 삭제된 `.cs`를 찾는 오류가 나오면 프로젝트 파일 재생성 또는 Compile 항목 정리를 확인한다.
