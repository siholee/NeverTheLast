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

### 사건(이벤트) 연출 — 비주얼 노벨 화면

사건 스테이지는 블루 아카이브 스토리 화면을 참고한 비주얼 노벨 연출로 표시된다.
연출은 `Assets/Scripts/Managers/UI/Screens/EventVisualNovelScreen.cs`가 전담하고,
`UIManager`는 진입점(`ShowEventStagePanel`/`ShowEventResolution`/`HideEventStagePanel`)만 위임한다.
흐름 제어(대사 진행, 선택 처리)는 그대로 `GameManager`가 담당한다.

화면 구성(블루 아카이브 스타일):

- 불투명 대사 상자 대신 **하단 그라데이션 스크림** 위에 대사를 직접 얹는다(장면이 계속 보인다).
- **기울어진 평행사변형 이름표**(하늘색 #3FA9F5) — 블루 아카이브 UI의 시그니처 형태.
  `UISpriteFactory.Parallelogram()`이 런타임에 생성하며, 9-슬라이스라 가로로 늘려도 기울기가 유지된다.
- 화자 초상화는 화면 오른쪽에 크게 세운다(첫 등장 시 떠오르는 연출, 같은 화자가 이어 말하면 재연출 없음).
- 제목은 좌상단에 하늘색 강조 바와 함께 표시한다.
- 본문/제목 텍스트에는 그림자를 넣어 장면 위에서도 읽히게 한다.
- 선택지는 **흰 카드 + 남색 글씨 + 좌측 하늘색 스트라이프**. 호버 시 밝아진다.
- 우상단 **AUTO / SKIP** 필 버튼: AUTO는 대사 완료 후 자동 진행(약 1.4초 간격),
  SKIP은 남은 대사를 건너뛰고 선택지로 이동한다. 선택지·결과 화면에서는 숨겨진다.
- 대사 진행: 화면 아무 곳이나 클릭. 타자기 진행 중 클릭하면 즉시 전체 출력.
- 선택지 카드에 골드 비용/보유 골드, 전투 발생, 동료·아이템 획득 가능 여부를 자동 표기.

색상/도형은 `UISpriteFactory.Palette`와 `RoundedRect`/`Parallelogram`/`VerticalGradient`가 담당하므로
아트 에셋 없이 스타일을 바꿀 수 있다(다른 UI 화면에서도 재사용 가능).

초상화 지정 규칙(`dialogue` 항목):

1. `portrait`를 직접 지정하면 그 키를 사용한다(`Resources/Sprite/Portraits`의 분류 폴더·확장자 제외).
2. 지정하지 않으면 `speaker` 이름을 `10_units.yaml`의 유닛 `name`과 대조해 자동 해석한다.
3. 둘 다 해당 없으면 초상화 없이 내레이션처럼 표시된다(예: 화자가 `일행`인 경우).

```yaml
    dialogue:
      - speaker: 일행          # 유닛이 아니므로 초상화 없음(내레이션)
        text: 눈보라 속에서 이정표가 희미하게 빛난다.
      - speaker: 케찰코아틀     # 유닛 이름과 일치 → 해당 유닛 초상화 자동 사용
        text: 너희가 지닌 의지의 무게를 보여라.
      - speaker: 룬술사
        portrait: SEI_PORTRAIT # 유닛이 아닌 화자는 초상화를 직접 지정
        text: 룬을 건드리지 않는 편이 좋겠군.
```

`randomSpeakers`를 쓰는 사건은 `{deity}` 치환이 `title`/`speaker`/`text`/`portrait`에 모두 적용되므로
`portrait: "{deity}_PORTRAIT"` 형태로 화자별 초상화를 지정할 수 있다.
(대사 필드 복사/치환은 `StageEventDialogueData.CloneWithReplacement()`가 담당한다.
**대사 필드를 추가할 때 이 메서드도 함께 갱신할 것** — 누락하면 해당 연출이 조용히 사라진다.)

#### 대사 연출 필드

각 `dialogue` 항목에 아래 필드를 선택적으로 붙여 연출을 지정한다.

| 필드 | 설명 |
| --- | --- |
| `portrait` | 초상화 키(`Resources/Sprite/Portraits`의 분류 폴더·확장자 제외) |
| `emotion` | 표정. `{portrait}_{emotion}` 파일을 우선 사용하고, 없으면 기본 초상화로 폴백 |
| `effect` | `shake`(흔들림) / `bounce`(톡 튀기) / `flash`(화면 번쩍) / `none` |
| `sfx` | 1회 재생할 효과음 (`Resources/Audio/SFX/{sfx}`) |
| `bgm` | 이 대사부터 재생할 BGM (`Resources/Audio/BGM/{bgm}`). `stop`이면 정지. 같은 곡이면 재시작하지 않음 |
| `textSpeed` | 타자기 속도 배율(1 = 기본, 0.5 = 느리게, 2 = 빠르게) |

표정이 바뀌면(같은 화자라도 초상화 이미지가 달라지면) 자동으로 살짝 튀는 연출이 들어간다.

오디오는 `Assets/Scripts/Managers/AudioManager.cs`가 담당한다.
**오디오/표정 에셋이 없어도 조용히 무시**되므로(같은 이름은 1회만 로그) 데이터를 미리 작성해 두고
나중에 파일만 넣으면 그대로 살아난다. 볼륨은 `SettingsManager`의 Music/Sfx 값을 따른다.

에디터에서 연출을 즉시 확인하려면 플레이 모드에서 **F9** 를 누른다
(현재 테마의 사건을 미리보기로 실행하며, 스테이지 진행이나 `oncePerRun` 기록에는 영향을 주지 않는다).

### 인트로 시퀀스

새 여정(New Game) 시작 시 캐릭터 선택 **전에** 인트로가 재생된다(포켓몬식 오프닝).

- 데이터: `Assets/Resources/Data/00_intro.yaml`의 `intros` 목록
- 구조는 사건(`StageEventData`)과 동일하므로 비주얼 노벨 화면이 그대로 재생한다
- `choices`가 없는 사건은 마지막 대사 후 **자동 종료**되고 다음 흐름으로 넘어간다
  (`GameManager.AdvanceEventDialogue`가 처리)
- 인트로 데이터가 없으면 건너뛰고 바로 캐릭터 선택으로 진행하므로, 데이터 누락이 진행을 막지 않는다

새 인트로를 추가하려면 `intros`에 항목을 넣고 `GameManager.PlayIntroThen()`이 찾는 id
(`new_game_intro`)를 맞추거나, 다른 id로 호출부를 지정한다.

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

보상 풀은 `Assets/Resources/Data/40_items.yaml`의 아이템 목록에서 생성된다
(`eventOnly` 아이템 제외, 아이템 `rarity`가 보상 티어로 사용됨).
`90_rewards.yaml`에는 라운드별 티어 확률(`rewardTierOdds`)만 남아 있다.

```yaml
rewardTierOdds:
  - round: 1
    tierWeights: [100, 0, 0, 0, 0]
```

보상 등급 확률:

- `rewardTierOdds`는 라운드별 1~5티어 보상 가중치를 의미한다.
- 육성 모드는 현재 라운드에 맞는 확률을 사용한다.
- 무한 모드는 10라운드 이후에도 10라운드 확률을 유지한다.

`RewardDef`(런타임 DTO, `RewardManager.cs` 정의)가 지원하는 효과 필드
(현재는 아이템 보상 위주로 사용되지만, 이벤트 보상 등에서 활용 가능):

- `itemId`: 아이템 지급
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
- 용도: 일반행동 접촉/비접촉 판정(`ClassCatalog.IsContactCombatClass`), 장비 숙련도, 육성 특기 스탯(`ClassCatalog.GetPrimaryStat`).
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

- STR: 일반행동 공격력, 방어력
- DEX: 속도, 회피율
- CON: 최대 체력, 치유 보너스, 보호막 보너스
- INT: 스킬 발동율, 마나 효율
- LUK: 치명타 확률

현재 코드 기준 대표 계산:

- 최대 체력: `CON * 1000`
- 공격력: YAML `atkBase + atkIncrementLvl * 성장 레벨` (명시적 전투 스탯, STR 파생이 아님)
- 방어력: YAML `defBase + defIncrementLvl * 성장 레벨`
- 치명타 확률: `LUK * 1%`
- 치명타 피해: 기본 150%
- 행동 속도: `1 + 최종 DEX * 1%` (독립 공격속도 스탯 없음)
- 마나 최대값: 마나형 궁극기 자원은 기본 100
- 스택형 궁극기 자원은 `ultimateResourceMax`를 사용

정확한 계산은 `Unit.GetDerived...()` 계열 메서드에서 관리한다.

관련 파일:

- `Assets/Scripts/Entities/Unit.cs`
- `Assets/Scripts/BaseClasses/Enums.cs`
- `Assets/Scripts/Managers/DataManager.cs`

### 스탯 필드 규칙

모든 유닛/적은 5스탯 필드(`strBase`~`lukIncrementUpgrade`)와 전투 스탯(`atkBase`,
`atkIncrementLvl`, `defBase`, `defIncrementLvl`)을 YAML에 직접 작성한다.

레거시 필드(`hpBase`, `critChance*`, `critMultiplier*`, `manaBase`,
`atkIncrementUpgrade`, `defIncrementUpgrade`)와 런타임 변환 로직은 제거되었다.
저장 포맷도 v2로 올라가 구버전 세이브는 로드 시 폐기된다(`RunSaveData.version`).

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
- `NormalCode`: 일반행동/일반 코드
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

- 일반행동/일반 코드는 발동 실패하지 않는다.
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

상태 시스템은 `UnitStatus` + `BaseEffect` 단일 체계다.
(구 `StatusEffect` 딕셔너리 시스템은 제거되었고, 버프류는 `Effects/Buffs/`로 이식됨.)

`UnitStatus`는 다음을 지원한다.

- 상태 ID + 문자열 Key (중복/중첩 판정용 — 시전자별 중첩은 Key에 시전자 ID 포함)
- 이름/설명
- 지속시간 (0 이하 = 무한, 라운드 종료 시 정리)
- 카테고리: Positive, Negative, Neutral
- 중첩 정책: Stack, ExtendDuration, ReplaceIfStronger, Ignore, Replace(무조건 교체)
- 여러 효과 인스턴스 (EffectFactory ID 또는 직접 생성한 BaseEffect 객체)
- IsBeneficial (부여 시 OnBeneficialEffectReceived 이벤트 발행)

`BaseEffect`는 생명주기 훅(OnApply/OnUpdate/OnRemove)과 스탯 질의 훅
(치명타/받는·주는 피해/5스탯 가산·배율/마나 회복/보호막/코드 가속)을 제공한다.

상태 정의 방법 두 가지:

- 정식 상태(맹독/화상 등): `UnitStatus(statusId, ...)` — `LoadStatusData()`의 ID 스위치에 등록
- 코드 정의 버프: `BuffStatus.Create(...)` (`Effects/Buffs/BuffEffects.cs`) —
  `StatusDefinition` 기반, ID 대역은 `BuffStatusIds` 참고 (10~ 공용, 100~199 유닛 고유)

관련 파일:

- `Assets/Scripts/Entities/Status/UnitStatus.cs`
- `Assets/Scripts/Effects/Base/BaseEffect.cs`
- `Assets/Scripts/Effects/Base/EffectFactory.cs`
- `Assets/Scripts/Effects/Buffs/BuffEffects.cs`

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

- `Assets/Scripts/Entities/Unit.cs` (`AddShield`/`SetShield`, `ShieldCurr`/`ShieldMax`)

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
- `portrait`는 `SpriteResource`가 `Resources/Sprite/Portraits`의 아군·일반·엘리트·보스 폴더에서 찾는다.
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
- `element`와 `unitClass`는 유닛 정체성 및 일반행동 접촉/비접촉 판정에 사용된다.
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
```

체크 포인트:

- 테마의 `enemyThemeId`와 같은 `themeId`를 가진 normal 적이 `60_enemies.yaml`에 있어야 한다.
- `midBossId`와 `bossId`는 `60_enemies.yaml`에 존재해야 한다.

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
- 일반행동과 같은 행동 주기에서 사용

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

### 1. 상태 정의

두 가지 방법이 있다.

- 정식 상태(디버프 등 여러 코드가 공유): `UnitStatus.LoadStatusData()`의 ID 스위치에 등록
- 코드 정의 버프(한 코드 전용): `StatusDefinition` + `BuffStatus.Create(...)` 사용,
  ID는 `BuffStatusIds`에 추가 (100~199 = 유닛 고유 버프 대역)

파일:

- `Assets/Scripts/Entities/Status/UnitStatus.cs`
- `Assets/Scripts/Effects/Buffs/BuffEffects.cs`

### 2. 효과 정의

효과는 `BaseEffect`를 상속한다. 생명주기형 효과(DoT 등)는 `EffectFactory`에 ID를 등록하고,
스탯 질의형 버프 효과는 `Effects/Buffs/`에 클래스를 추가해 `status.AddEffect(new ...)`로 직접 첨부한다.

파일:

- `Assets/Scripts/Effects/Base/BaseEffect.cs`
- `Assets/Scripts/Effects/Base/EffectFactory.cs`
- `Assets/Scripts/Effects/Negative`
- `Assets/Scripts/Effects/Neutral`
- `Assets/Scripts/Effects/Buffs`

### 3. 코드에서 상태 부여

예시 흐름:

```csharp
// 정식 상태 (EffectFactory ID 기반)
var status = new UnitStatus(statusId, Caster, target);
status.AddEffect(effectId, coefficient);
target.AddStatus(status);

// 코드 정의 버프 (직접 생성한 효과 객체)
target.AddStatus(BuffStatus.Create(
    BuffStatusIds.MyBuff, "MyBuffKey", "버프 이름",
    Caster, target, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.STR, 4),
    duration: 4f));
```

상태를 부여하는 코드가 궁극기 자원 스택을 올려야 한다면, 부여 성공 시점에 `AddUltimateResource(1)`를 호출하는 식으로 연결한다.

## 신규 보상 추가 절차

전투 보상 풀은 `40_items.yaml`의 아이템에서 자동 생성된다
(`RewardManager.GenerateRewards`, `eventOnly` 제외, `rarity`=티어).

- 새 전투 보상을 추가하려면 `40_items.yaml`에 아이템을 추가한다.
- 라운드별 티어 확률을 조정하려면 `90_rewards.yaml`의 `rewardTierOdds`를 수정한다.
- 아이템이 아닌 효과(회복, 스탯 강화 등)가 필요하면 `RewardDef`와 `RewardManager.ApplyReward()`를 함께 확장한다.

관련 파일:

- `Assets/Scripts/Managers/RewardManager.cs`
- `Assets/Resources/Data/40_items.yaml`
- `Assets/Resources/Data/90_rewards.yaml`

## 데이터 로딩 파일 목록

주요 YAML:

- `10_units.yaml`: 아군 유닛
- `20_codes.yaml`: 코드 표시 데이터
- `50_tokens.yaml`: 토큰 데이터
- `60_enemies.yaml`: 적 데이터
- `70_rounds.yaml`: 라운드 패턴
- `80_stages.yaml`: 테마/고정 보스
- `90_rewards.yaml`: 보상 등급 확률(`rewardTierOdds`)

로딩 담당:

- `Assets/Scripts/Managers/DataManager.cs` (제네릭 `Load<T>` 로더)
- DTO 클래스: `Assets/Scripts/Data/` (패밀리별 파일)

## 구현 후 확인 체크리스트

신규 콘텐츠를 추가한 뒤 최소한 다음을 확인한다.

1. YAML 들여쓰기가 깨지지 않았는가
2. 새 ID가 기존 ID와 중복되지 않는가
3. 유닛/적의 코드 ID가 `20_codes.yaml`과 `CodeFactory`에 모두 존재하는가
4. 테마 `enemyThemeId`와 적 `themeId`가 매칭되는가
5. 라운드 패턴의 `archetypes`와 적 `archetype`이 매칭되는가
6. 보스/중간 보스 ID가 `60_enemies.yaml`에 존재하는가
7. 궁극기 자원 타입이 `Mana` 또는 `Stack`으로 지정되었는가
8. 일반 코드와 궁극기 코드에 INT 실패 판정을 직접 넣지 않았는가
9. 패시브 코드에 영구 효과를 등록했다면 제거 조건도 설계했는가
10. `dotnet build Assembly-CSharp.csproj`가 성공하는가
11. `dotnet build Assembly-CSharp-Editor.csproj`가 성공하는가

## 현재 과도기/주의 사항

- 스탯 모델은 5스탯 + 명시적 atk/def로 단일화되었다. 레거시 필드/변환 로직은 제거됨
  (자세한 규칙은 "스탯 필드 규칙" 절 참고).
- `AtkCurr`, `DefCurr`, `HpMax`, `ManaCurr` 같은 파생 전투 값은 UI와 전투 계산 호환을 위해 유지된다.
- 상태 시스템은 `UnitStatus` + `BaseEffect` 단일 체계다 (구 `StatusEffect` 딕셔너리 제거됨).
- Unity가 `.csproj`를 재생성할 수 있으므로, 파일 삭제 후 IDE 빌드에서 삭제된 `.cs`를 찾는 오류가 나오면 프로젝트 파일 재생성 또는 Compile 항목 정리를 확인한다.
