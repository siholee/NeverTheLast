# 상세 기획서 06 — 사건(이벤트)과 비주얼 노벨

> **3계층 문서.** 사건 데이터 스키마, 선택지 액션, VN 연출 필드, 인트로 시퀀스, 현재 사건 목록.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 6장을 본다.

최종 갱신: 2026-08-30
관련 코드: `GameManager.cs`, `EventScheduler.cs`, `EventVisualNovelScreen.cs`, `UIManager.cs`, `AudioManager.cs`
관련 데이터: `80_stages.yaml` (`events`), `00_intro.yaml`

---

## 1. 발생 규칙

| 항목 | 값 |
| --- | --- |
| 고정 슬롯 | 라운드 내 **5스테이지** |
| 판정 | `StageInRound == 5 && !IsFixedBossStage(Stage)` |
| 매칭 | 사건의 `themeId` + `stageInRound`가 현재 스테이지와 일치 |
| 임의 삽입 | `EventScheduler`로 페이즈 사이 어디든 예약 가능 |
| 폴백 | 조건에 맞는 사건이 없으면 `BuildFallbackEvent()`가 "{테마}의 갈림길"을 생성 |

### 1.1 발생 제한

| 필드 | 동작 |
| --- | --- |
| `oncePerRun: true` | 런당 1회. `RunManager._triggeredEventIds`로 추적, 세이브에 기록 |
| `blockedUnitIds: [24]` | 해당 유닛을 이미 보유 중이면 사건 자체가 발생하지 않음 |

### 1.2 흐름 제어

- 흐름(대사 진행, 선택 처리)은 **`GameManager`**가 담당한다
- 화면 연출은 **`EventVisualNovelScreen`**이 전담한다
- `UIManager`는 진입점(`ShowEventStagePanel` / `ShowEventResolution` / `HideEventStagePanel`)만 위임한다
- 사건 종료 후에는 `_eventResumeAction`(연속 동작)이 있으면 그것을, 없으면 `AdvanceToNextStage()`를 실행한다

## 2. 사건 데이터 스키마

```yaml
events:
  - id: example_recruit_event     # 고유 ID (oncePerRun 추적 키)
    themeId: 4                    # 실재하는 테마 ID
    stageInRound: 5
    oncePerRun: true
    blockedUnitIds: [24]          # 이미 보유 중이면 사건 자체가 발생하지 않음
    title: 사건 제목
    dialogue:
      - speaker: 일행              # 유닛 아님 → 초상화 없이 내레이션
        text: 상황을 서술하는 문장.
      - speaker: 수르트            # 유닛 이름과 일치 → 초상화 자동 해석
        text: 화자의 대사.
    choices:
      - id: recruit_surtr
        text: 동행을 청한다.
        action: recruit
        grantUnitId: 24
        successText: 수르트가 일행에 합류했다.
```

## 3. 선택지 액션

| `action` | 동작 | 필요 필드 |
| --- | --- | --- |
| `continue` | 대사 종료 후 진행 | `successText` |
| `recruit` | 캐릭터 즉시 영입 | `grantUnitId` |
| `battle_recruit` | 전투 → 승리 시 영입 | `battleEnemyId`, `grantUnitId`, `failureText` |
| `battle_item` | 전투 → 승리 시 아이템 획득 | `battleEnemyId`, `grantItemId`, `failureText` |
| `pay_gold` | 골드 지불 | `goldCostPerStage` (× 현재 스테이지) |
| `grant_passive` | 패시브 부여 | `grantPassiveCodeId`, `grantPassiveToAll` |

### 3.1 선택지 카드 자동 표기

선택지 UI에는 다음이 자동으로 붙는다.

- 골드 비용 / 현재 보유 골드
- 전투 발생 여부
- 동료 획득 가능 여부
- 아이템 획득 가능 여부

플레이어가 카드만 보고 위험과 대가를 판단할 수 있어야 한다.

### 3.2 텍스트 치환

| 토큰 | 치환 대상 |
| --- | --- |
| `{cost}` | 계산된 골드 비용 |
| `{deity}` | `randomSpeakers` 사용 시 선택된 화자 이름 |

`{deity}`는 `title` / `speaker` / `text` / `portrait` 모두에 적용된다.
따라서 `portrait: "{deity}_PORTRAIT"` 형태로 화자별 초상화를 지정할 수 있다.

> 치환은 `StageEventDialogueData.CloneWithReplacement()`가 담당한다.
> **대사 필드를 추가할 때 이 메서드도 반드시 함께 갱신한다** — 누락하면 그 연출이 조용히 사라진다.

## 4. 비주얼 노벨 연출

블루 아카이브 스토리 화면을 참고한 구성.

### 4.1 화면 구성

| 요소 | 사양 |
| --- | --- |
| 대사 영역 | 불투명 상자 대신 **하단 그라데이션 스크림** 위에 직접 얹는다 (장면이 계속 보인다) |
| 이름표 | **기울어진 평행사변형**, 하늘색 `#3FA9F5`. `UISpriteFactory.Parallelogram()`이 런타임 생성, 9-슬라이스라 늘려도 기울기 유지 |
| 화자 초상화 | 화면 오른쪽에 크게. 첫 등장 시 떠오르는 연출, 같은 화자가 이어 말하면 재연출 없음 |
| 제목 | 좌상단, 하늘색 강조 바와 함께 |
| 텍스트 | 본문/제목 모두 그림자 처리 (장면 위에서도 읽히도록) |
| 선택지 | **흰 카드 + 남색 글씨 + 좌측 하늘색 스트라이프**, 호버 시 밝아짐 |

색상/도형은 `UISpriteFactory.Palette`와 `RoundedRect` / `Parallelogram` / `VerticalGradient`가 담당한다.
**아트 에셋 없이 스타일을 바꿀 수 있고, 다른 UI 화면에서도 재사용 가능하다.**

### 4.2 조작

| 조작 | 동작 |
| --- | --- |
| 화면 클릭 | 다음 대사. 타자기 진행 중이면 즉시 전체 출력 |
| **AUTO** (우상단 필 버튼) | 대사 완료 후 약 **1.4초** 간격 자동 진행 |
| **SKIP** (우상단 필 버튼) | 남은 대사를 건너뛰고 선택지로 이동 |

AUTO / SKIP은 선택지·결과 화면에서 숨겨진다.

### 4.3 대사 연출 필드

각 `dialogue` 항목에 선택적으로 붙인다.

| 필드 | 설명 |
| --- | --- |
| `portrait` | 초상화 키 (`Resources/Sprite/Portraits`의 분류 폴더·확장자 제외) |
| `standing` | 스탠딩 CG 파일명 |
| `emotion` | 표정. `{portrait}_{emotion}` 파일 우선, 없으면 기본 초상화로 폴백 |
| `effect` | `shake`(흔들림) / `bounce`(톡 튀기) / `flash`(화면 번쩍) / `none` |
| `sfx` | 1회 재생 효과음 (`Resources/Audio/SFX/{sfx}`) |
| `bgm` | 이 대사부터 재생할 BGM (`Resources/Audio/BGM/{bgm}`). `stop`이면 정지, 같은 곡이면 재시작하지 않음 |
| `textSpeed` | 타자기 속도 배율 (1 = 기본, 0.5 = 느리게, 2 = 빠르게) |

**표정이 바뀌면(같은 화자라도 초상화 이미지가 달라지면) 자동으로 살짝 튀는 연출이 들어간다.**

### 4.4 초상화 해석 규칙

우선순위 순으로 판정한다.

1. `portrait`를 직접 지정하면 그 값을 사용
2. 미지정 시 `speaker` 이름을 `10_units.yaml`의 유닛 `name`과 대조해 자동 해석
3. 둘 다 해당 없으면 **초상화 없이 내레이션처럼 표시** (예: 화자가 `일행`)

```yaml
dialogue:
  - speaker: 일행          # 유닛 아님 → 초상화 없음 (내레이션)
    text: 눈보라 속에서 이정표가 희미하게 빛난다.
  - speaker: 케찰코아틀     # 유닛 이름 일치 → 초상화 자동 해석
    text: 너희가 지닌 의지의 무게를 보여라.
  - speaker: 룬술사
    portrait: SEI_PORTRAIT # 유닛 아닌 화자는 직접 지정
    text: 룬을 건드리지 않는 편이 좋겠군.
```

### 4.5 에셋 누락 처리

**오디오·표정 에셋이 없어도 조용히 무시된다** (같은 이름은 1회만 로그).
데이터를 먼저 작성해 두고 나중에 파일만 넣으면 그대로 살아난다.

볼륨은 `SettingsManager`의 Music / Sfx 값을 따른다. 오디오는 `AudioManager`가 담당한다.

### 4.6 에디터 미리보기

플레이 모드에서 **F9** — 현재 테마의 사건을 미리보기로 실행한다.
스테이지 진행이나 `oncePerRun` 기록에는 영향을 주지 않는다.

## 5. 인트로 시퀀스

새 여정(New Game) 시작 시 **캐릭터 선택 전에** 재생된다 (포켓몬식 오프닝).

| 항목 | 내용 |
| --- | --- |
| 데이터 | `Assets/Resources/Data/00_intro.yaml`의 `intros` 목록 |
| 구조 | 사건(`StageEventData`)과 **동일** — VN 화면이 그대로 재생한다 |
| 종료 | `choices`가 없으면 마지막 대사 후 **자동 종료** (`GameManager.AdvanceEventDialogue`) |
| 호출 | `GameManager.PlayIntroThen()`이 id `new_game_intro`를 찾는다 |
| 누락 시 | 인트로 데이터가 없으면 건너뛰고 캐릭터 선택으로 진행 — **데이터 누락이 진행을 막지 않는다** |

새 인트로를 추가하려면 `intros`에 항목을 넣고 id를 맞추거나, 다른 id로 호출부를 지정한다.

## 6. 현재 사건 목록

**테마마다 하나씩, 5스테이지 슬롯에 하나씩 있다.** 전부 `oncePerRun`이며
현재까지는 모두 **선택지 액션이 `continue`뿐**이라 전력이 바뀌지 않는다.

| id | 테마 | 제목 | 유형 |
| --- | --- | --- | --- |
| `colosseum_masked_gladiator` | 4 콜로세움 | 가면 쓴 검투사 | 복선형 |
| `legion_crossing_the_rubicon` | 5 로마 군단 | 강가의 야영지 | 복선형 |
| `mexica_smoking_mirror` | 6 메히코 | 연기 나는 거울 | 복선형 |
| `aswan_the_first_seal` | 7 아스완 | 첫 번째 봉인 | 복선형 |

> 🔸 콜로세움 사건은 테마가 `enabled: false`라 **현재 런에서는 발생하지 않는다.**
> 테마를 되살리면 그대로 함께 돌아온다.

### 6.1 가면 쓴 검투사 (`colosseum_masked_gladiator`)

보스 스파르타쿠스의 정체를 미리 흘리는 복선형. 대사에서 `portrait`와 `standing`을
직접 지정해 정체를 밝히기 전후를 연출한다(`SHI_PORTRAIT` → `SPARTACUS_PORTRAIT`).

### 6.2 강가의 야영지 (`legion_crossing_the_rubicon`)

루비콘 도하 직전의 카이사르와 마주친다. 선택지는 둘 다 `continue`이며,
한쪽은 결승 예고, 다른 쪽은 군단 진형을 눈에 담는 서술로 갈린다.

### 6.3 연기 나는 거울 (`mexica_smoking_mirror`)

테스카틀리포카가 흑요석 거울 너머에서 말을 건다. 10스테이지 보스전의 복선이다.

### 6.4 첫 번째 봉인 (`aswan_the_first_seal`)

호루스가 강 상류를 가리키며 사령의 성질(한 번은 되살아난다)을 미리 알려 준다.
**4스테이지에서 적으로 만난 호루스가 5스테이지에서는 말을 거는 구성**이라,
앞 절반(종말의 사도)과 뒤 절반(사령) 사이의 이음매 역할을 한다.

## 7. 사건 설계 가이드

| 유형 | 역할 | 사례 |
| --- | --- | --- |
| **영입형** | 파티 확장 | 수르트, 츠쿠요미 |
| **분기형** | 위험/비용/보상의 트레이드오프 | 케찰코아틀 |
| **복선형** | 서사 전달, 보스전 기대감 | 스파르타쿠스 |
| **버프형** | 전력의 소폭 상승 | 월신의 가호 (제거됨) |

> **네 사건이 전부 복선형이다.** 최소한 영입형 하나는 복원하거나 새로 만들어야
> 사건이 "전력을 바꾸는 분기"라는 본래 역할을 회복한다.

### 7.1 작성 시 체크리스트

1. `id`가 고유한가 (`oncePerRun` 추적 키다)
2. `themeId`가 실재하는 테마인가
3. 영입 사건이면 `blockedUnitIds`에 해당 유닛을 넣었는가
4. `battle_*` 액션의 `battleEnemyId`가 `60_enemies.yaml`에 존재하는가
5. `grantItemId` / `grantUnitId` / `grantPassiveCodeId`가 실재하는가
6. 실패 분기(`failureText`)를 작성했는가
7. 새 대사 필드를 추가했다면 `CloneWithReplacement()`도 갱신했는가

## 8. 현재 공백

| 공백 | 내용 |
| --- | --- |
| 🔴 전력 변화 사건 없음 | 네 사건 모두 `action: continue`뿐이다. `grantUnitId` · `grantItemId` · `grantPassiveCodeId` · `battle_*`가 **스키마에만 있고 데이터에 한 번도 쓰이지 않는다** |
| 🔴 Locked 17인 획득 불가 | 영입 사건이 없어 `characterType: Locked` 캐릭터 전원이 편성 불가다 — [Design_Backlog](Design_Backlog.md) 항목 5·21 |
| 🔸 테마당 1개 고정 | 한 테마의 런에서는 늘 같은 사건이 나온다. 테마마다 둘 이상 두고 굴려야 반복이 줄어든다 |
| 🔸 `randomSpeakers` 미사용 | 치환 기능이 구현되어 있으나 실제 데이터에 쓰이지 않는다 |
| 🔸 사건 스케줄러 활용 | 임의 지점 삽입 기능이 있으나 5스테이지 고정 슬롯 외 사용 사례가 없다 |
