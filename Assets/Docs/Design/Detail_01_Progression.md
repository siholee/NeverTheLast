# 상세 기획서 01 — 진행 구조

> **3계층 문서.** 런 진행, 스테이지·라운드·테마, 적 편성과 스케일링, 생명력, 페이즈 타이머.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 1장을 본다.

최종 갱신: 2026-08-10
관련 코드: `GameManager.cs`, `RunManager.cs`, `RoundManager.cs`, `CharacterSelectionManager.cs`
관련 데이터: `70_rounds.yaml`, `80_stages.yaml`, `60_enemies.yaml`

---

## 1. 게임 상태 머신

`BaseEnums.GameState`

| 상태 | 진입 조건 | 이탈 |
| --- | --- | --- |
| `CharacterSelection` | 새 여정 시작(인트로 종료 후) | 편성 확정 |
| `Preparation` | 스테이지 진입 | 전투 시작 / 사건 발생 |
| `RoundInProgress` | `StartBattle()` | 적 전멸 / 아군 전멸 / 타임아웃 |
| `RoundEnd` | 전투 종료 | 정산 후 보상 |
| `RewardSelection` | 정산 완료 | 보상 선택 |
| `EventStage` | 사건 슬롯 도달 또는 사건 예약 | 선택지 결과 확정 |
| `TrainingPhase` | 보상 이후(육성 모드) / 준비 페이즈 행동 | 집중 스탯 선택 |
| `RunComplete` | 최대 스테이지 도달 또는 라운드 소진 | 메인 메뉴 (세이브 삭제) |
| `GameOver` | 생명력 0 이하 | 메인 메뉴 (세이브 삭제) |

## 2. 페이즈 타이머

| 항목 | 값 | 코드 |
| --- | --- | --- |
| 준비 시간 | **30초** | `GameManager.preparationTime` |
| 전투 제한 시간 | **60초** | `GameManager.roundProgressTime` |

- HUD의 원형 게이지는 `PhaseProgressRatio`(1 → 0), 숫자는 `PhaseRemainingSeconds`(올림)를 읽는다.
- 타이머가 돌지 않는 페이즈에서는 비율 `1`, 남은 초 `0`을 반환한다.

## 3. 준비 페이즈 행동

**스테이지당 1회만** 사용할 수 있다 (`preparationActionUsed`).

| 행동 | 효과 | 메서드 |
| --- | --- | --- |
| 육성 | 육성 페이즈로 진입해 집중 훈련 1회 | `OpenTrainingFromPreparation()` |
| 휴식 | 모든 활성 아군 체력 회복 | `RestFromPreparation()` |
| 추가 전투 | 즉시 전투 시작 (보상 기회 추가) | `BeginAdditionalBattleFromPreparation()` |

**행동을 소모하지 않는 것**: 덱 구성(`OpenDeckSetupFromPreparation`), 장비 창(`OpenEquipmentFromPreparation`)

**행동 잠금** — `IsPreparationLimitedToDeck()` 가 `true`면 덱 구성만 허용된다.
조건: **육성 모드 + 현재 스테이지가 보스 스테이지**

## 4. 스테이지와 라운드

| 값 | 계산식 |
| --- | --- |
| 라운드 | `Round = ((Stage - 1) / 10) + 1` |
| 라운드 내 위치 | `StageInRound = ((Stage - 1) % 10) + 1` |
| 적 레벨 | `EnemyLevel = Stage` (1스테이지 = Lv.1 = 성장분 0) |

**1라운드 = 10스테이지 = 테마 1개.** 테마는 `stageThemes` 목록을 라운드 순서대로 순환 선택한다.

## 5. 스테이지 슬롯 배치

| StageInRound | 콘텐츠 | 판정 |
| --- | --- | --- |
| 1, 2, 3, 4, 6, 7 | 일반 전투 | 테마 `stagePatterns` |
| **5** | **사건 스테이지** | `StageInRound == 5 && !IsFixedBossStage(Stage)` |
| **8** | 테마 중간 보스 | `_currentStageTheme.midBossId` |
| 9 | 일반 전투 — 단, **고정 보스 라운드에서는 테마 보스** | `StageInRound == 9 && CurrentRoundHasFixedBoss()` |
| **10** | 테마 보스 | `StageInRound == 10 && !CurrentRoundHasFixedBoss()` |

### 고정 보스 (`fixedBossStages`)

**현재 비어 있다 (`fixedBossStages: []`).**

테마가 콜로세움 하나뿐이라 10스테이지마다 스파르타쿠스가 반복 등장하므로,
25/50/75/100 지점의 고정 보스는 테마가 늘어난 뒤에 다시 설계한다.
(개편 전에는 네 지점 모두 오딘(3001)이었으나, 노르드 테마 제거와 함께 사라졌다.)

## 6. 적 편성

### 6.1 편성 방식 두 가지

테마의 `stagePatterns[].patterns[]`는 두 형태 중 하나를 쓴다.

**명시 편성** — 적 ID를 직접 나열

```yaml
- stageInRound: 2
  patterns:
    - { enemyIds: [1010, 1012, 1011], weight: 50 }
    - { enemyIds: [1010, 1013, 1015], weight: 50 }
```

**분류 편성** — 분류(archetype)만 지정, 해당 `themeId`의 적 중에서 무작위 선택

```yaml
- stageInRound: 3
  patterns:
    - { archetypes: [7, 7, 10], weight: 50 }
    - { archetypes: [8, 8, 10], weight: 50 }
```

패턴은 `weight` 가중 추첨으로 뽑는다.

| 테마 | 사용 방식 |
| --- | --- |
| 콜로세움 (4) | 명시 편성 (엘리트를 일반 슬롯에 섞음) |

현재 유일한 테마가 `stagePatterns`를 모두 정의하므로, `70_rounds.yaml`의 라운드 타입은 사실상 폴백으로만 남아 있다.

### 6.2 분류(archetype)별 배치 열

| archetype | 배치 | 현재 해당 적 |
| --- | --- | --- |
| 7 | **전열** | 무르밀로, 마르켈루스 |
| 8 | **전열** | 호플로마쿠스, 디마카이루스, 스파르타쿠스 |
| 9 | **전열** | 트라엑스, 세쿠토르 |
| 10 | **후열** | 레티아리우스, 사비나 |
| 11~14 | 후열 | **해당 적 없음** (테마 정리로 소멸) |

새 분류 ID를 추가하면 `RoundManager.GetColumnForArchetype()`도 함께 갱신한다.

### 6.3 라운드 타입 (`70_rounds.yaml`)

테마가 `stagePatterns`를 정의하면 그쪽이 우선하므로, 현재는 폴백 역할만 한다.
사용 가능한 archetype이 7~10으로 줄어 전 항목이 그 범위로 재작성되었다.

| ID | 성격 | 편성 |
| --- | --- | --- |
| 1, 3, 9 | 일반 | 전열 중심 2~5기 |
| 2, 5, 6 | 일반 | 후열 혼합 2~4기 |
| 7 | 일반 | 전열 집중 3~5기 |
| 4 | **엘리트** | `2004` 또는 `2004+2005` |
| 8 | **엘리트** | `2004+2005` 또는 `2004+2004+2005` |
| 10 | **보스** | `3003` 스파르타쿠스 |

## 7. 테마 데이터 스키마

```yaml
stageThemes:
  - id: 4
    name: 콜로세움
    tags: [Colosseum, Rome, Festival]
    enemyThemeId: 5          # 일반 적 검색 필터 (적의 themeId와 일치해야 함)
    description: ...
    midBossId: 2005          # 8스테이지
    bossId: 3003             # 10스테이지
    uniqueRewardIds: [...]   # 이 테마에서만 보상 풀에 추가 (선택)
    stagePatterns: [...]     # 생략 시 라운드 타입 사용
```

### 현재 테마

| ID | 이름 | enemyThemeId | 중간 보스 | 보스 |
| --- | --- | --- | --- | --- |
| 4 | 콜로세움 | 5 | 2005 베스티아리우스 사비나 | 3003 스파르타쿠스 |

**테마가 하나뿐이므로 10스테이지마다 같은 테마가 반복된다.**
노르드·아즈텍·일본 테마는 전용 적 스프라이트가 없어(전부 `SEI_PORTRAIT` 임시 사용) 제거되었다.

## 8. 적 데이터 현황

| 등급 | 수 | 목록 |
| --- | --- | --- |
| 일반 | 6 | 무르밀로, 호플로마쿠스, 트라엑스, 레티아리우스, 세쿠토르, 디마카이루스 |
| 엘리트 | 2 | 프리무스 팔루스 마르켈루스(2004), 베스티아리우스 사비나(2005) |
| 보스 | 1 | 스파르타쿠스(3003) |
| **합계** | **9** | **전원 전용 초상화 + 스탠딩 보유** |

### 8.1 적 스탯 (Lv.1 기준)

| 적 | 주/부 | 위력 | 체력 | 방어 | 등급 |
| --- | --- | --- | --- | --- | --- |
| 무르밀로 | STR/CON | 140 | 2,900 | 14 | normal |
| 호플로마쿠스 | STR/DEX | 190 | 1,800 | 9 | normal |
| 트라엑스 | STR/LUK | 230 | 1,400 | 7 | normal |
| 레티아리우스 | DEX/INT | 200 | 1,200 | 6 | normal |
| 세쿠토르 | STR/DEX | 220 | 1,600 | 8 | normal |
| 디마카이루스 | DEX/LUK | 240 | 1,200 | 6 | normal |
| 마르켈루스 | STR/CON | 310 | 5,700 | 28 | elite |
| 사비나 | DEX/LUK | 290 | 5,000 | 25 | elite |
| 스파르타쿠스 | STR/LUK | 500 | 7,800 | 39 | boss |

스탯 파생 규칙은 [Detail_03 §2](Detail_03_Character.md#2-파생-스탯)와 동일하다.

## 9. 생명력

| 항목 | 값 |
| --- | --- |
| 시작 생명력 | **20** |
| 차감 조건 | 전투 타임아웃 또는 아군 전멸 |
| 차감량 | **남은 적 수** |
| 0 이하 | `GameOver` → 세이브 삭제 → 메인 메뉴 |

## 10. 라운드 종료 정산

1. 남은 적 수만큼 생명력 차감
2. **아군 필드를 전투 시작 시점 스냅샷으로 복원** (`SaveAllyFieldState` / 복원)
3. 모든 상태 정리 (`StatusController.ClearAll`), 보호막 초기화, 원소 부착 초기화
4. 생명력 판정 → 다음 페이즈 또는 게임 오버

## 11. 게임 모드 규칙

| 항목 | 육성 모드 | 무한 모드 |
| --- | --- | --- |
| enum | `GameMode.Training` | `GameMode.Infinite` |
| 편성 상한 | 5명 (`MaxSelection`) | 5명 |
| 편성 하한 | 1명 (`MinSelection`) | — |
| 메인 수 | 1명 (`MainSelection`) | 메인 개념 없음 |
| 중복 편성 | 불가 | 불가 |
| 편성 자격 | `canStartAsMain` / `canStartAsSupport` | `canUseInInfinite` + 육성 완료 기록 보유 |
| 스테이지 상한 | **100** (`GameManager.MaxTrainingStage`) | 없음 |
| 육성 페이즈 | 있음 | 없음 |
| 보상 확률 | 현재 라운드 확률 | 10라운드 확률 고정 |
| 종료 처리 | 메인을 `TrainedCharacterRecord`로 저장 | 라운드 데이터 소진 시 종료 |

### 스테이지 전진 단일 진입점

`RunManager.AdvanceToNextStage()` — 보상/사건/육성 어느 흐름에서 오든 여기를 통과한다.

```
육성 모드: Stage >= 100 또는 다음 라운드 로드 실패 → CompleteTrainingRun()
무한 모드: 다음 라운드 로드 실패 → RunComplete
그 외:     세이브 → 다음 스테이지 진입
```

## 12. 캐릭터 편성 자격 현황

| 유닛 ID | 이름 | 메인 | 서포트 | 무한 |
| --- | --- | --- | --- | --- |
| 1 | 세이 | — | O | — |
| 2 | 시 | — | — | — |
| 4 | 피그말리온 | — | — | — |
| 5 | 아탈란테 | **O** | — | O |
| 6 | 오리온 | **O** | — | O |
| 7 | 테세우스 | **O** | — | O |
| 12 | 찬드라 | — | O | — |
| 20 | 케찰코아틀 | — | — | — |
| 21 | 츠쿠요미 | — | — | — |
| 24 | 수르트 | **O** | — | O |

메인으로 시작 가능한 캐릭터는 **수르트·아탈란테·오리온·테세우스 4인**이다.

시·케찰코아틀·츠쿠요미·피그말리온은 **획득 경로가 없다**(Locked 보관 상태).
현재 실제로 편성 가능한 캐릭터는 **세이·아탈란테·오리온·테세우스·찬드라·수르트 6인**이다.

## 13. 난이도 곡선 설계

| 축 | 상승 방식 |
| --- | --- |
| 적 강화 | 스테이지당 레벨 +1 (선형, `EnemyLevel = Stage`) |
| 아군 강화 | EXP 레벨업 + 훈련 강화 + 보상 + 장비 + 패시브 해금 |
| 스파이크 | 8스테이지 중간 보스 → 10스테이지 보스 |
| 체크포인트 | 없음 (고정 보스 미설정) |

아군 레벨은 초반에 적보다 약간 앞서다가 후반에 뒤처지도록 EXP 곡선이 잡혀 있다
([Detail_03 §3.3](Detail_03_Character.md#33-결과-곡선)). 그 격차를 메우는 것이 육성 강화와 장비다.

아군의 성장 기울기는 **서포트 편성 품질에 비례**한다. 좋은 서포트 카드를 가진 플레이어는
같은 스테이지에서 더 높은 능력치를 갖는다 — 계승이 곧 난이도 완화 장치다.

실측 전투 길이는 [Detail_02 §10.1](Detail_02_Combat.md#101-현재-밸런스-실측)을 본다.
