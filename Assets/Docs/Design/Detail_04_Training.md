# 상세 기획서 04 — 육성과 계승

> **3계층 문서.** 집중 훈련, 서포트 카드, 우정도, 패시브 전수, 육성 완료 기록.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 4장을 본다.

최종 갱신: 2026-08-30
관련 코드: `TrainingManager.cs`, `RunManager.cs`, `SaveData.cs`, `SupportBondState.cs`, `Unit.cs`

---

## 1. 육성 페이즈

육성 모드에서 스테이지 사이마다 실행된다.

**흐름**

```
보상 선택 완료
  → GameManager.EnterTrainingPhase()   [GameState.TrainingPhase]
  → UI에서 5스탯 중 하나 선택
  → CompleteTrainingPhaseWithFocus(focus)
      · TrainingManager.ApplyTraining(focus)
      · preparationActionUsed = true      ← 준비 페이즈 행동을 소모한 것으로 처리
      · 세이브
  → GameState.Preparation
```

준비 페이즈에서 직접 진입할 수도 있다 (`OpenTrainingFromPreparation`).

## 2. 집중 훈련 기본 수치

`TrainingManager` 상수

| 상수 | 값 | 의미 |
| --- | --- | --- |
| `BaseStatGain` | **1** | 집중 스탯 기본 강화량 |
| `SupportStatBonusPerUnit` | **1** | 서포트 카드가 없는 서포트 1명당 추가량 |
| `AffinityBonusPerUnit` | **1** | 카드 없는 서포트의 최고 스탯과 집중 스탯이 일치할 때 추가량 |
| `TrainingLevelGain` | **1** | 육성 페이즈마다 오르는 트레이닝 레벨 |
| `OffSpecialtyAppearanceRate` | **35** | 특기가 아닌 스탯 훈련 시 서포트 등장률(%) |
| `MaxBond` | `SupportBondState.MaxBond` | 우정도 상한 |

**총 강화량 = `BaseStatGain + Σ(등장한 서포트의 보너스)`**

### 2.1 카드 없는 서포트 (육성 미완료 캐릭터)

| 조건 | 보너스 |
| --- | --- |
| 항상 | +1 |
| 집중 스탯 == 서포트의 현재 최고 5스탯 | +1 추가 |

카드가 없으면 **등장 판정 없이 항상 참여**한다.

## 3. 서포트 카드

육성을 완료한 캐릭터가 남기는 훈련 보조 데이터 (`SupportCardSaveData`).

### 3.1 카드 필드와 생성 공식

`RunManager.BuildSupportCard()` — 런 완주 시 1회 생성된다.

| 필드 | 생성 공식 | 범위 |
| --- | --- | --- |
| `specialtyTraining` | 80% 확률로 최고 스탯, 20%는 5스탯 무작위 | STR/DEX/CON/INT/LUK |
| `specialtyRate` | `clamp(rand(35,60) + 트레이닝레벨 / 8, 20, 85)` | 20~85 |
| `specialtyBonus` | `clamp(1 + 특기스탯값 / 35 + rand(0,1), 1, 8)` | 1~8 |
| `trainingBonus` | `clamp(rand(0,2) + sourcePower / 250, 0, 6)` | 0~6 |
| `skillTransferRate` | `clamp(10 + 전수가능패시브수 × 6 + LUK / 8 + rand(0,10), 5, 70)` | 5~70 |
| `initialBond` | `clamp(20 + 트레이닝레벨 / 3 + rand(0,15), 0, 75)` | 0~75 |
| `bondGainRate` | `clamp(1 + rand(0,2) + 트레이닝레벨 / 50, 1, 6)` | 1~6 |
| `friendshipBonus` | `clamp(1 + specialtyBonus / 2 + rand(0,2), 1, 8)` | 1~8 |
| `sourcePower` | `5스탯합 + 트레이닝레벨 × 3 + Σ(패시브 stage × 10)` | — |

**설계 의도**

- `sourcePower`가 `trainingBonus`에 반영되므로 **잘 키운 캐릭터일수록 상시 보너스가 크다**
- `specialtyRate`도 트레이닝 레벨에 비례 → 잘 키울수록 **자주 등장한다**
- 무작위 요소가 섞여 있어 같은 캐릭터를 다시 육성할 이유가 생긴다

### 3.2 카드 보유 서포트의 훈련 기여

| 조건 | 보너스 |
| --- | --- |
| 등장 | `+trainingBonus` |
| 등장 + 특기 일치 | `+specialtyBonus` 추가 |
| 등장 + 특기 일치 + **우정도 75 이상** | `+friendshipBonus` 추가 (= **우정 훈련**) |

### 3.3 등장 판정

```
특기 일치  → specialtyRate (%)  로 판정
특기 불일치 → 35% (OffSpecialtyAppearanceRate) 로 판정
```

## 4. 우정도(Bond)

| 항목 | 값 |
| --- | --- |
| 소유 | `RunManager.SupportBonds` — **런 범위 상태** |
| 초기값 | 카드의 `initialBond` (없으면 0) |
| 증가 | 등장 시 `bondGainRate`, 특기 일치면 **+1 추가** |
| 우정 훈련 발동 | **75 이상** |
| 초기화 | 런 시작 시 (`StartRun` → `SupportBonds.Clear()`) |
| 저장/복원 | `RunSaveData.supportBonds` |

> `RunManager`가 없는 상황(에디터 진입 직후 등)을 위한 폴백 인스턴스가 `TrainingManager`에 있다.

## 5. 훈련 1회 처리 순서

`TrainingManager.ApplyTraining(focus)`

```
1. RollSupportTraining(focus) — 서포트별로:
     a. 육성 기록 조회 → 서포트 카드 확인
     b. 특기 일치 여부 판정
     c. 등장 판정 (카드 없으면 무조건 등장)
     d. 등장 시:
          카드 있음 → trainingBonus
                     + (특기일치 ? specialtyBonus : 0)
                     + (특기일치 && 우정 >= 75 ? friendshipBonus : 0)
                     우정도 += bondGainRate + (특기일치 ? 1 : 0)
          카드 없음 → 1 + (최고스탯 일치 ? 1 : 0)

2. 총 강화량 = 1 + Σ(등장 서포트의 보너스)

3. 메인에 적용:
     main.AddStatUpgrade(focus, gain)
     main.GainTrainingLevel(1)
     ApplySkillTransfers(main, rolls)
     main.AddExp(30 + 5 × Stage)        → 레벨업 시 RefreshLevelPassives() 트리거

4. TrainingResult 반환 (UI 표시용)
```

### 5.1 TrainingResult

| 필드 | 내용 |
| --- | --- |
| `Focus` | 선택한 집중 스탯 |
| `StatGain` | 총 강화량 |
| `SupportBonus` | 서포트 기여 합계 |
| `AffinityBonus` | 그중 특기/우정 보너스 몫 |
| `NewTrainingLevel` | 갱신된 트레이닝 레벨 |
| `SupportMessages` | 서포트별 로그 (`이름: 참여, +N, 우정 A->B / 우정 훈련 / 패시브 X 전수`) |
| `TransferredPassiveIds` | 이번에 전수받은 패시브 ID |

## 6. 패시브 전수

### 6.1 발생 조건 (모두 만족)

1. 서포트가 **서포트 카드를 보유**
2. 이번 훈련에 **등장**
3. **특기 일치** (집중 스탯 == 카드 특기)
4. `skillTransferRate` 확률 통과

### 6.2 전수 대상 선정

```
후보 = 서포트의 ownedPassiveCodes 중
         · transferable == true
         · codeId > 0
         · 메인이 아직 모르는 것

후보 중 1개 무작위 선택 → main.LearnTransferredPassive(codeId, stage)
```

서포트 한 명당 훈련 1회에 최대 1개까지 전수된다.

**고유 패시브는 어떤 경로로도 전수되지 않는다.** `UniquePassiveCode`는 생성 시점에
`Transferable = false`이므로 후보 필터에서 바로 걸러진다.
**서포트 카드가 넘겨줄 수 있는 것은 해금 패시브(`levelPassives`)뿐이다.**

> **열화 전수본 제도는 폐지되었고 잔재도 전부 제거했다.** 예전에는 고유 P마다
> `TransferVersionCodeId`를 두고 열화판을 대신 넘겼으나, 그 필드와 사문 코드
> 230·231·233·224를 모두 삭제했다. 232만 이시스의 독립 해금 패시브
> `생명의 물`로 재편입되어 살아 있다.

## 7. 육성 완료와 계승

### 7.1 완료 조건

육성 모드에서 `Stage >= 100` 또는 다음 라운드 로드 실패 → `CompleteTrainingRun()`

### 7.2 저장되는 것 (`TrainedCharacterRecord`)

| 필드 | 내용 |
| --- | --- |
| `version` | 1 |
| `unitId` / `unitName` | 대상 캐릭터 |
| `finalTrainingLevel` | 최종 트레이닝 레벨 |
| `finalPrimaryStats` | 최종 5스탯 (`GetBase*()` 스냅샷) |
| `ownedPassiveCodes` | 습득 패시브 (`codeId`, `stage`, `transferable`) |
| `titleIds` | 칭호 목록 |
| `supportCard` | 위 §3 공식으로 생성 |
| `createdAtUnixSeconds` | 생성 시각 (서포트 ID 생성에도 사용) |

메인 유닛 인스턴스를 찾지 못하면 ID만 기록된다 (`SaveSystem.AddTrainedCharacter`).

### 7.3 칭호 (`titleIds`)

| 칭호 | 조건 |
| --- | --- |
| `training_complete` | 육성 완료 시 항상 |
| `specialist_{stat}` | 최종 5스탯 중 최고 스탯 (예: `specialist_int`) |
| `veteran_training` | 트레이닝 레벨 **50 이상** |
| `century_training` | 트레이닝 레벨 **90 이상** |
| `skill_collector` | 습득 패시브 **3개 이상** |

### 7.4 계승의 두 갈래

| 경로 | 조건 |
| --- | --- |
| **무한 모드 출전** | `canUseInInfinite: true` + 육성 완료 기록 보유 |
| **서포트로 재등장** | 육성 완료 기록의 `supportCard` 사용 |

## 8. 메인/서포트 판별

| 대상 | 판별 |
| --- | --- |
| 메인 | `CharacterSelectionManager.MainUnitId`와 일치하는 활성 아군. 없으면 **첫 활성 아군** |
| 서포트 | 메인을 제외한 모든 활성 아군 |

> 폴백 규칙(첫 활성 아군)이 있어 메인이 전사해도 육성이 멈추지 않는다.
> 🔸 다만 **의도치 않은 캐릭터가 메인으로 승격될 수 있다.** 메인 사망 처리 정책을 명시할 필요가 있다.

## 9. 육성 밸런스 튜닝 노브

> 캐릭터 정의의 `mainStatTrainingBonus`/`subStatTrainingBonus`는 집중 훈련 1회의 획득량과 별개인
> 최종 스탯 보정 축이다. 일반은 주 +20%/부 +10%, 부스탯 둘이면 각 +5%, 세이·시는 +25%/+15%를 사용한다.

| 노브 | 현재값 | 영향 |
| --- | --- | --- |
| `BaseStatGain` | 1 | 서포트 없이도 보장되는 최소 성장 |
| `SupportStatBonusPerUnit` | 1 | 편성 인원수의 가치 |
| `AffinityBonusPerUnit` | 1 | 편성-훈련 시너지의 가치 |
| `OffSpecialtyAppearanceRate` | 35% | 특기 외 훈련의 기회비용 |
| 우정 훈련 임계값 | 75 | 장기 투자 회수 시점 |
| `specialtyRate` 상한 | 85% | 특기 훈련의 안정성 상한 |
| `skillTransferRate` 상한 | 70% | 패시브 계승 속도 |

### 9.1 현재 밸런스 관찰

- 서포트 4명 전원이 카드 없이 특기 일치 시: `1 + 4×(1+1) = 9`/회. 100스테이지면 최대 **+900**
  (스탯 1점 = 위력 10 / 체력 100이므로, 이 강화량이 곧 후반 전투력의 핵심이다)
- 서포트 4명 전원이 고급 카드 + 우정 훈련 시: `1 + 4×(6+8+8) = 89`/회 — **10배 가까운 격차**
- 🔸 카드 유무에 따른 성장 격차가 매우 크다. 첫 런(카드 없음)과 이후 런의 난이도 체감이 극단적으로 갈릴 수 있다.
  현재 첫 런에서 카드를 얻을 수 있는 캐릭터가 아탈란테뿐이므로, **이 격차가 실제로 관찰되기 전에 캐릭터 확충이 선행되어야 한다.**
