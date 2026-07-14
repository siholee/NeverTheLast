# 아즈텍 테마 및 케찰코아틀 구현 기록

- 기록일: 2026-07-13
- 대상 프로젝트: NeverTheLast
- 범위: 5대 스탯 개편, 스테이지/이벤트/보상 분리, 아즈텍 테마, 케찰코아틀, 마카나, 관련 전투 기반 기능

## 1. 스테이지 구조

### 기본 계산

- 전투 스테이지: `Stage`
- 보상 라운드: `Round = ((Stage - 1) / 10) + 1`
- 라운드 내 순번: `StageInRound = ((Stage - 1) % 10) + 1`
- 적 레벨: `EnemyLevel = max(0, Stage - 1)`

테마는 보상 라운드마다 데이터 등록 순서대로 교대한다.

| 전투 스테이지 | 보상 라운드 | 테마 |
|---|---:|---|
| 1~10 | 1 | 노르드 |
| 11~20 | 2 | 아즈텍 |
| 21~30 | 3 | 노르드 |
| 31~40 | 4 | 아즈텍 |

이후에도 같은 순서로 반복된다. 따라서 최초 아즈텍 구간은 11~20 스테이지이며, 케찰코아틀 이벤트는 15 스테이지에서 처음 등장한다.

> 테마 식별자는 두 종류다. `stageThemes[].id`(노르드 1, 아즈텍 2)는 테마 자체의 ID이며 이벤트(`events[].themeId`)가 이 값을 참조한다. `stageThemes[].enemyThemeId`(노르드 3, 아즈텍 4)는 적 데이터(`enemies[].themeId`)를 필터링할 때 쓰는 별도 ID다. 따라서 아즈텍 이벤트는 `themeId: 2`, 아즈텍 적은 `themeId: 4`로 등록되어 있으며 둘은 서로 다른 축이다.

### 라운드 내 주요 위치

| `StageInRound` | 역할 |
|---:|---|
| 1~4 | 일반 전투. 테마 전용 패턴이 있으면 해당 패턴 우선 |
| 5 | 테마 이벤트 |
| 6~7 | 일반 전투. 테마 전용 패턴이 있으면 해당 패턴 우선 |
| 8 | 테마 중간 보스 |
| 9 | 일반 전투. 고정 보스가 포함된 10스테이지 구간에서는 테마 보스 |
| 10 | 테마 보스. 해당 구간에 고정 보스가 있으면 일반 전투 타입으로 대체 |

고정 보스는 현재 25, 50, 75, 100 스테이지에 등록되어 있다. 처리 우선순위상 명시적인 테마 이벤트가 먼저 판정되므로, `StageInRound == 5`인 25/75 스테이지는 현재 이벤트가 고정 보스보다 우선한다. 50/100 스테이지는 등록된 고정 보스가 등장한다.

### 아즈텍 적 구성

| 역할 | ID | 이름 | 아키타입 |
|---|---:|---|---:|
| 전열 | 1010 | 재규어전사 | 7 |
| 전열 | 1011 | 독수리전사 | 8 |
| 후열 | 1012 | 피필틴 | 10 |
| 중간 보스 | 2003 | 틀랄록 | - |
| 테마 보스 | 3002 | 테스카틀리포카 | - |
| 시련 이벤트 보스 | 3201 | 케찰코아틀 | - |
| 적대 이벤트 보스 | 3202 | 격노한 케찰코아틀 | - |

아즈텍 테마에는 라운드 내 1, 2, 3, 4, 6, 7, 9번 위치의 전용 가중치 패턴이 등록되어 있다. 패턴은 아키타입 7/8/10을 조합하며, 실제 소환 시 같은 테마와 아키타입을 가진 적 ID를 선택한다.

## 2. 5대 스탯

### STR

- 효과: 장비 보유 중량 한도 증가
- 공식: `CarryWeightMax = 5 + max(0, STR)`
- 현재 장비 중량은 장착 중인 모든 장비의 `weight` 합계다.
- 장착 결과가 한도를 넘으면 장착을 거부한다.
- STR은 더 이상 공격력이나 물리 방어력을 올리지 않는다.

### DEX

- 효과: 일반 공격 속도 증가
- 공식: `AttackSpeed = 1 + max(0, DEX) * 0.01`
- 일반 공격 재사용 대기시간 감소에 적용한다.
- 기존 회피율 효과는 제거했으며 파생 회피율은 `0`이다.

### CON

- 기존 효과를 유지한다.
- 최대 체력: `max(1, CON * 1000)`
- 치유량 보너스: `clamp(max(0, CON - 10) * 0.01, 0, 2)`
- 보호막 보너스: 치유량 보너스와 같은 공식

### INT

- 효과 1: 마나 회복량 증가
- 공식: `ManaRecoveryMultiplier = 1 + max(0, INT) * 0.02`
- 효과 2: 최대 스킬 보유 수 증가
- 공식: `MaxCodeCount = max(3, INT)`
- 기본 보유 코드 수는 일반 코드와 궁극기 2개에 패시브 수를 더해 계산한다.
- 기존 코드 발동 확률 효과는 제거했으며 발동 확률은 항상 `1`이다.

### LUK

- 효과: 크리티컬 확률 증가
- 공식: `CriticalChance = clamp01(LUK * 0.01)`
- 기본 크리티컬 피해 배율은 `1.5`다.

### 공격력과 방어력 호환

- 공격력은 기존 `AtkBase/AtkIncrementLvl/AtkIncrementUpgrade` 값을 사용한다.
- 공격력 데이터가 없으면 기본값 `100`을 사용한다.
- 방어력은 기존 `DefBase/DefIncrementLvl/DefIncrementUpgrade` 값을 사용한다.
- 피해 계산은 현재 방어력 `DefCurr`을 참조한다.

## 3. 장비와 보상

### 장비 중량

| 아이템 ID | 아이템 | 중량 |
|---:|---|---:|
| 4001 | 로브 | 2 |
| 4002 | 완드 | 2 |
| 4003 | 활 | 4 |
| 4004 | 갑옷 | 6 |
| 4005 | 방패 | 3 |
| 4010 | 마카나 | 3 |

준비 단계 장비 UI에서 보관 아이템을 페이지 단위로 확인하고 장착할 수 있다. 교체로 해제된 아이템은 보관함에 되돌린다.

### 일반 보상

- 일반 전투 승리 후 아이템 3개를 제시하고 하나를 선택한다.
- 아이템 희귀도를 기존 보상 티어 확률에 대응시켜 후보를 생성한다.
- `eventOnly: true`인 아이템은 일반 보상 후보에서 제외한다.
- 기존 `90_rewards.yaml`의 보상 정의와 확률표는 하위 호환 및 티어 확률 계산을 위해 유지한다.

### 이벤트와 보상의 구분

- 이벤트: 캐릭터 대화 후 선택지를 표시하고, 선택에 따라 골드 지불이나 특수 전투를 수행한다.
- 보상: 대화 없이 전투 승리 직후 표시하는 아이템 선택 화면이다.
- 이벤트 전투 승리 시 선택지에 지정된 유닛 또는 아이템만 즉시 지급한다.
- 이벤트 해결 후에는 일반 아이템 보상 화면을 추가로 열지 않는다.

## 4. 골드

- 런 시작 골드: `500`
- 적 처치 골드: `25 * 현재 Stage`
- 골드 지불은 `TrySpendGold`를 통해 부족 여부를 검증한다.
- 현재 골드와 보관 아이템 ID 목록은 런 저장 데이터에 포함한다.
- 현재 골드는 상단 HUD에 표시한다.

## 5. 이벤트 시스템

### 데이터 구조

`80_stages.yaml`에 다음 구조를 추가했다.

- `StageEventData`: 이벤트 ID, 테마, 라운드 내 위치, 제목, 대화, 선택지
- 대화: 화자와 대사
- 선택지: 행동 종류, 스테이지당 골드 비용, 전투 적, 지급 유닛/아이템, 성공/실패 문구

### 실행 흐름

1. 이벤트 스테이지 진입
2. 대사를 순서대로 표시
3. 대사가 끝나면 선택지를 표시
4. 선택 행동 실행
5. 골드 지불 또는 이벤트 전투 결과를 표시
6. 일반 보상 없이 다음 스테이지로 진행

정의가 없는 테마의 5번째 스테이지에는 진행 가능한 기본 이벤트를 생성한다. 노르드 테마에는 현재 `룬이 새겨진 이정표` 이벤트가 등록되어 있다.

### 페이즈 사이 사건 발생 (스케줄러)

사건은 더 이상 테마 고정 슬롯(`StageInRound == 5`)에만 묶이지 않는다. 포켓로그/붕괴: 스타레일의 '사건'처럼 임의의 페이즈 경계에서도 발생할 수 있도록 다음 구조를 추가했다.

- `EventScheduler`(`Assets/Scripts/Managers/EventScheduler.cs`): 동적으로 예약된 사건 큐. `Enqueue`/`TryDequeue`/`Clear`를 제공한다.
- `GameManager.RequestEvent(StageEventData)`: 런타임에 사건을 예약한다. 다음 페이즈 체크포인트에서 발생한다.
- `GameManager.EnterEvent(stageEvent, onComplete)`: 페이즈 사이 어디서든 호출 가능한 일반 사건 진입점. 사건 종료 후 `onComplete` 연속 동작으로 원래 흐름을 이어간다.
- `GameManager.TryRunEventCheckpoint(continuation)`: 예약 사건이 있으면 발생시키고 종료 후 `continuation`을 잇는다(반환 `true`). 없으면 `false`를 반환해 호출부가 그대로 진행한다.
- `GameManager.RunEventCheckpoint(continuation)`: 위와 같되, 예약 사건이 없으면 `continuation`을 즉시 실행한다. 아래 세 발생 지점에서 사용한다.

현재 동적 사건은 다음 **세 페이즈 경계**에서 발생할 수 있다.

| 발생 지점 | 코드 위치 | 사건 종료 후 연속 동작 |
|---|---|---|
| 육성 종료 후 | `NextGameState`의 `TrainingPhase` 분기 | `AdvanceAfterTrainingPhase` |
| 전투 시작 전 | `StartRound` → `StartBattle` | `StartBattle`(실제 전투 시작) |
| 전투 승리 후·보상 전 | `EndRound` 승리 분기 → `ShowRewardSelection` | `ShowRewardSelection`(보상 화면) |

고정 슬롯 사건(`EnterEventStage`)도 일반 진입점(`EnterEvent`)을 경유하며, 종료 후 연속 동작은 다음 스테이지 진행(`AdvanceAfterEvent`)이다. 다른 페이즈 경계가 필요하면 그 지점에 `RunEventCheckpoint`를 추가하면 된다. 사건-페이즈는 사건이 발생하는 하나의 지점일 뿐, 발생 자체는 위 경계들에서 가능하다.

> 체크포인트는 `EventScheduler` 큐에 예약된 사건을 소비한다. 아직 예약 로직(무작위 확률 등)은 연결하지 않았으므로, 실제로 사건을 띄우려면 원하는 시점에 `GameManager.RequestEvent(stageEvent)`로 큐에 넣어야 한다.

### 케찰코아틀 고정 이벤트

- 이벤트 ID: `aztec_quetzalcoatl`
- 테마: 아즈텍(2)
- 위치: `StageInRound == 5`
- 최초 등장: 15 스테이지

| 선택 | 처리 | 성공 결과 |
|---|---|---|
| 케찰코아틀의 시련을 받는다 | 적 3201과 전투 | 유닛 20이 서포트 유닛으로 합류 |
| 케찰코아틀에게 공물을 바친다 | `200 * 현재 Stage` 골드 지불 | 전투와 일반 보상 없이 통과 |
| 케찰코아틀과 적대한다 | 적 3202와 전투 | 아이템 4010 마카나 획득 |

전투에 패배하면 이벤트 보상은 지급하지 않고 결과 문구를 표시한 뒤 진행한다.

## 6. 케찰코아틀

### 유닛 정의

- 유닛 ID: `20`
- 원소: 풀(Dendro)
- 주 스탯: INT
- 부 스탯: CON
- 런 시작 선택 후보에서는 제외한다.
- 이벤트 영입 시 서포트/대기 유닛으로 합류하며 저장 데이터에 포함한다.

### 기본 코드

| 종류 | 코드 ID | 이름 | 구현 |
|---|---:|---|---|
| 패시브 | 70 | 요리스틀리 | 최대 수량 저장 및 피해 1회 무효화 |
| 일반 | 70 | 태양의 공 | 최초 단일 피해, 풀 부여, 요리스틀리 수만큼 연쇄 |
| 궁극기 | 70 | 요리스틀리의 맹세 | 즉시 요리스틀리 1 획득 |

요리스틀리는 일반 마나와 분리된 이름 있는 전투 자원이다. 전투 시작마다 초기화하며 패시브 단계에 따라 최대 `[6/9/12]`개를 저장한다.

- 적의 공격으로 피해를 받기 직전 요리스틀리 1개를 소모하면 해당 피해를 취소한다.
- 일반 코드 첫 피해: `INT * 60`
- 첫 대상에게 풀 원소를 부여한다.
- 추가 튕김 횟수: 현재 요리스틀리 수
- 튕김 피해: 각각 `INT * 30`
- 궁극기는 일반 공격을 대체하지 않고 즉시 자원만 획득한다.

### 레벨 패시브

| 해금 레벨 | 코드 ID | 이름 | 효과 |
|---:|---:|---|---|
| 1 | 71 | 재생력 | 1초마다 CON만큼 체력 회복 |
| 6 | 72 | 흡혈 | 실제로 입힌 피해의 20%만큼 회복 |
| 14 | 73 | 숲의 은총 | 풀 원소 보유 시 CON/INT 1.5배 |
| 18 | 74 | 주문 저격수 | 후열에서 적 후열을 공격하면 피해 1.5배 |
| 22 | 75 | 뿌리박기 | 공격 가능한 동안 4초마다 CON/INT +1 |
| 26 | 76 | 베푸는 자 | 입힌 피해의 20%만큼 현재 체력이 가장 낮은 아군 치유 |
| 38 | 77 | 학자 | INT로 얻는 전체 마나 회복 배율 1.5배 |
| 42 | 78 | 초월 | 전투 8초 후 성장 스탯의 1.5배를 추가 획득 |
| 55 | 79 | 개척 | 궁극기 발동 시 모든 아군에게 풀 원소 부여 |

`뿌리박기`와 `초월`의 증가분은 전투 전용이며 전투 종료 시 제거한다.

### 보스 전용 패시브

| 코드 ID | 이름 | 효과 |
|---:|---|---|
| 80 | 케찰코아틀의 부름 | 전열 빈칸에 재규어전사 또는 독수리전사를 소환. 재사용 대기시간 6초 |
| 81 | 수호자의 의지 | 소환된 재규어/독수리전사가 살아 있는 동안 케찰코아틀이 받는 피해 30% 감소 |
| 82 | 시팍틀리를 살해한 자 | 풀 원소를 가진 아군의 모든 1차 스탯 2배 |

시련 보스 3201은 `케찰코아틀의 부름`과 적 레벨에 맞는 해금 패시브를 보유한다. 적대 보스 3202는 레벨과 관계없이 모든 일반/레벨/보스 전용 패시브를 보유한다.

`수호자의 의지`는 전달된 기획에 구체적인 효과가 없어, 현재는 소환수 생존 중 피해 30% 감소로 구현한 임시 확정값이다.

## 7. 마카나

- 아이템 ID: `4010`
- 종류: 한손 검
- 슬롯: 주무기
- 숙련: Sword
- 희귀도: 5
- 중량: 3
- 일반 보상 제외: `eventOnly: true`
- 기본 보정: LUK +2
- 장비 패시브 코드: 90

장착 유닛이 적을 처치하면 25% 확률로 STR/DEX/CON/INT/LUK 중 하나의 영구 업그레이드가 +1 증가한다. 업그레이드 값은 기존 유닛 저장 데이터에 포함되어 런 저장 후에도 유지된다.

## 8. 전투 기반 기능

케찰코아틀과 마카나 구현을 위해 다음 공통 기능을 추가했다.

- `DamageContext.IsCancelled`: 피해 적용 전 취소 지원
- `DamageResolvedContext`: 체력과 보호막을 포함한 실제 피해량 전달
- `OnDamageDealt`: 실제 피해를 입힌 뒤 발행
- `OnKill`: 처치 확정 시 발행
- 전투 원소 집합: 고유 원소와 전투 중 부여된 원소를 함께 관리
- 이름 있는 전투 자원: 요리스틀리처럼 마나 외 자원 관리
- 1차 스탯 배율, 가하는 피해 배율, 마나 회복 배율용 상태 효과 훅
- 라운드 종료 시 활성 적 정리 및 전투 전용 효과 제거

## 9. 주요 변경 파일

### 코드

- `Assets/Scripts/BaseClasses/Contexts.cs`
- `Assets/Scripts/BaseClasses/Enums.cs`
- `Assets/Scripts/BaseClasses/Equipment.cs`
- `Assets/Scripts/Entities/Unit.cs`
- `Assets/Scripts/Codes/Base/CodeFactory.cs`
- `Assets/Scripts/Codes/Passive/ChandraPassives.cs`
- `Assets/Scripts/Codes/Normal/MagicBolt.cs`
- `Assets/Scripts/Codes/Ultimate/a012_U_Soma.cs`
- `Assets/Scripts/StatusEffects/Base/StatusEffect.cs`
- `Assets/Scripts/StatusEffects/Effects/ChandraEffects.cs`
- `Assets/Scripts/Managers/DataManager.cs`
- `Assets/Scripts/Managers/RoundManager.cs`
- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/RewardManager.cs`
- `Assets/Scripts/Managers/InventoryManager.cs`
- `Assets/Scripts/Managers/RunManager.cs`
- `Assets/Scripts/Managers/GridManager.cs`
- `Assets/Scripts/Managers/UIManager.cs`
- `Assets/Scripts/Managers/UI/InfoTab.cs`
- `Assets/Scripts/Managers/UI/ResourcePanel.cs`
- `Assets/Scripts/Core/SaveData.cs`

### 데이터

- `Assets/Resources/Data/10_units.yaml`
- `Assets/Resources/Data/20_codes.yaml`
- `Assets/Resources/Data/40_items.yaml`
- `Assets/Resources/Data/60_elites.yaml`
- `Assets/Resources/Data/80_stages.yaml`
- `Assets/Resources/Data/90_rewards.yaml`

## 10. 검증 기록

- `dotnet build Assembly-CSharp.csproj --no-restore`: 경고 0, 오류 0
- 모든 `Assets/Resources/Data/*.yaml`: YamlDotNet 구문 분석 성공
- 타입 역직렬화 점검: 아즈텍 테마, 7개 전용 패턴, 케찰코아틀 이벤트의 대화 3개/선택지 3개, 관련 유닛/적/아이템 ID 확인
- 변경 파일 `git diff --check`: 공백 오류 없음
- Unity Editor 플레이 모드 및 실제 UI 조작 검증은 아직 수행하지 않음

## 11. 후속 조정 지점

- STR 중량 기본값 `5`, INT 코드 최대치 공식, 장비별 중량은 밸런스 조정 가능 값이다.
- `수호자의 의지`의 피해 감소율 30%는 임시 기획값이다.
- 아즈텍 캐릭터 초상화는 현재 기존 리소스를 사용하는 플레이스홀더다.
- 25/75 스테이지의 이벤트와 고정 보스 우선순위는 현재 이벤트 우선이며, 의도에 따라 별도 조정이 필요할 수 있다.
