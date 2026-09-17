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
    triggerBossId: 0              # >0이면 그 보스 격파 직후 예약(슬롯 추첨을 타지 않음)
    triggerThemeId: 0             # >0이면 이 테마의 triggerStageInRound 슬롯 승리 직후 예약(슬롯 추첨을 타지 않음)
    triggerStageInRound: 0        # triggerThemeId와 함께 쓰는 내용 슬롯(1~10)
    requiresRunEventIds: []       # 이번 런에 이 중 하나라도 본 뒤에만 뜬다(체인)
    allowUnlockedRecruit: false   # true면 영구 해금한 유닛도 이번 런에 없으면 다시 합류를 제안
    requiresUnitInParty: 0        # >0이면 그 유닛이 일행에 있을 때만 뜬다(동행 대사 갈래)
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
        rosterFullText: 자리가 없다. 맞이하려면 누군가가 떠나야 할 것 같다.
        successText: 수르트가 일행에 합류했다.
        failureText: 자리를 비우지 못했다. 합류는 없던 일이 되었다.
```

## 3. 선택지 액션

| `action` | 동작 | 필요 필드 |
| --- | --- | --- |
| `continue` | 대사 종료 후 진행 | `successText` |
| `recruit` | 캐릭터 즉시 영입 | `grantUnitId`, `rosterFullText`(자리 없을 때) |
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

### 3.2 자리가 없을 때 — 떠나보낼 사람 고르기

`grantUnitId`가 붙은 선택지는 고르는 즉시 합류시키지 않는다. **먼저 자리를 묻는다**
(`GridManager.HasAvailableAllySlot()` — 대기석 5칸, 없으면 아군 필드 8칸).

자리가 하나도 없으면 합류 대신 **떠나보낼 사람을 고르는 화면**이 열린다.

| 요소 | 내용 |
| --- | --- |
| 문구 | 선택지의 `rosterFullText`. 비우면 범용 기본 문구 |
| 후보 | 현재 아군 전원 — **메인 캐릭터와 소환수는 뺀다** |
| 고르면 | 그 유닛이 일행을 떠나고, 기다리던 합류가 마저 진행된다(`successText`) |
| 포기하면 | 합류는 없던 일이 된다(`failureText`) |

메인을 후보에서 빼는 이유는 **런의 축이기 때문**이다. 한 캐릭터를 키우는 구조에서
메인을 실수로 내보낼 수 있으면 런이 통째로 무의미해진다.

> 이 분기가 없으면 자리가 없는 플레이어는 합류를 골랐는데 아무 일도 일어나지 않는 것을
> 본다 — 예전 `RecruitSupportUnit`은 경고 로그만 남기고 조용히 실패했다.

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

**테마마다 하나씩, 5스테이지 슬롯에 하나씩 있다.** 전부 `oncePerRun`이다.
🔸 콜로세움(테마 4)은 리메이크 대기 중이라 사건이 없다 — 옛 `colosseum_masked_gladiator`는 삭제했고 리메이크 때 새로 쓴다.

| id | 테마 | 제목 | 유형 |
| --- | --- | --- | --- |
| `legion_crossing_the_rubicon` | 5 로마 군단 | 강가의 야영지 | 복선형 |
| `mexica_smoking_mirror` | 6 메히코 | 연기 나는 거울 | 복선형 |
| `aswan_the_first_seal` | 7 아스완 | 첫 번째 봉인 | 복선형 |
| `nord_the_raiders_fire` | 16 노르드 | 꺼지지 않는 불 | 복선형 |
| `nord_north_the_white_silence` | 17 노르드 북부 | 그치지 않는 눈 | **경첩형** |
| `seed_beneath_the_ground` | **0 전 테마** | 땅속의 것 | **분기형** |
| `nord_central_the_kings_road` | 18 노르드 중부 | 왕의 길 | 복선형 |

영입 사건은 여기 세지 않는다(`aswan_bastet_*`, `t4_horus_*`, `t4_set_*`, `nord_loki_after_thor`,
`nord_central_oathbound_pair`, `mexica_quetzalcoatl_after_duel`). 슬롯 승리 예약형 체인(`japan_sky_*`)도 따로 본다(§6.11).
천공 전선(테마 19)과 해안 전선(테마 20)은 **5슬롯 전용 사건을 두지 않는다** — 공용 사건 목록을 탄다.

### 6.0 보스 격파 예약 사건 — `triggerBossId`

슬롯 추첨을 타지 않고 **보스를 넘어선 직후 큐에 들어가는** 사건들이다.
방아쇠는 코드가 아니라 **데이터**가 들고 있다 — 사건에 `triggerBossId`를 적으면 끝이고,
`themeId`/`stageInRound`는 0으로 둔다(이 값이 붙은 사건은 슬롯 풀에서도 빠진다).

| id | 방아쇠(`triggerBossId`) | 영입 |
| --- | --- | --- |
| `aswan_bastet_first_amun_ra_clear` | 아문·라 3012 | 바스테트(122) |
| `nord_loki_after_thor` | 토르 3060 | 로키(82) |
| `nord_central_oathbound_pair` | 시구르드 2051 (중간 보스) | 시구르드(84) · 브륀힐드(85) |
| `mexica_quetzalcoatl_after_duel` | 케찰코아틀 3201 (중간 보스) | 케찰코아틀(140) |
| `japan_coast_susanoo_after_archon` | 심연의 집정관 3080 | 스사노오(42) — §6.12 |

한 보스에 여러 사건을 달아도 된다. 정의 순서대로 전부 예약된다.

**보스 격파 기록은 이 지점에서 남는다.** `SaveSystem.MarkBossDefeated(보스ID)`가
보스 스테이지를 이긴 모든 보스에 대해 호출되므로, `requiresBossDefeatId`는 이제
아문·라뿐 아니라 **아무 보스에나** 걸 수 있다.

> 예전에는 "최초 격파" 자체가 합류 사건의 일회성이라, 사건을 찾은 뒤에야 기록할 수 있었다.
> 지금은 **해금 여부**가 일회성을 맡으므로 기록이 무엇도 소진하지 않는다.

### 6.0.1 만나면 해금된다

**영입 사건을 끝까지 본 것만으로 그 유닛은 영구 해금된다.** 합류시켰든 거절했든 같다
(`GameManager.GrantRecruitUnlock`, `CompleteEventStage`에서 한 번). 육성 모드에서만 남는다.

| 선택 | 이번 런 | 다음 런부터 |
| --- | --- | --- |
| 합류를 환영한다 | 즉시 일행에 들어온다 | 메인·서포터로 편성 가능 |
| 정중하게 거절한다 | 들어오지 않는다 | **메인·서포터로 편성 가능** |
| 자리가 없어 포기 | 들어오지 않는다 | **메인·서포터로 편성 가능** |

**거절이 해금까지 빼앗으면 선택지가 아니라 함정이다.** 자리가 없다거나 지금 조합에
맞지 않는다는 이유로 거절해도, 만났다는 사실은 남아야 한다.

그 결과 **영입 사건은 어느 방식이든 평생 한 번만 뜬다** — `RoundManager.IsEventEligible`과
`GameManager.CanOfferEvent`가 둘 다 `SaveSystem.IsStarterUnlocked`로 거르기 때문이다.
보스 예약형과 T4 슬롯형(`t4_horus_*`, `t4_set_*`)이 같은 규칙을 쓴다.

### 6.2 강가의 야영지 (`legion_crossing_the_rubicon`)

루비콘 도하 직전의 카이사르와 마주친다. 선택지는 둘 다 `continue`이며,
한쪽은 결승 예고, 다른 쪽은 군단 진형을 눈에 담는 서술로 갈린다.

### 6.3 연기 나는 거울 (`mexica_smoking_mirror`)

테스카틀리포카가 흑요석 거울 너머에서 말을 건다. 10스테이지 보스전의 복선이다.

### 6.4 첫 번째 봉인 (`aswan_the_first_seal`)

호루스가 강 상류를 가리키며 사령의 성질(한 번은 되살아난다)을 미리 알려 준다.
**4스테이지에서 적으로 만난 호루스가 5스테이지에서는 말을 거는 구성**이라,
앞 절반(종말의 사도)과 뒤 절반(사령) 사이의 이음매 역할을 한다.

### 6.5 꺼지지 않는 불 (`nord_the_raiders_fire`)

볼바가 눈 위에 불을 얹는 수를 직접 보여 준다. 노르드 1의 융해딜을 대사로 미리 설명하는 복선형.

### 6.6 그치지 않는 눈 (`nord_north_the_white_silence`) — 경첩

**노르드 북부의 편성을 앞뒤로 가르는 사건이다.** 5슬롯 앞에서는 살아 있는 짐승과 프리즘만
나오고, 여기서 눈을 걷어내 금속 껍질을 처음 본 뒤부터 씨앗·기사·사수가 깨어난다.
사건이 연출이 아니라 **편성의 경첩**으로 쓰인 첫 사례다.

### 6.7 맹세한 자들 (`nord_central_oathbound_pair`) — 합류

**중간 보스 격파가 방아쇠인 첫 사건이다.** `triggerBossId: 2051`.

지금까지 `triggerBossId`는 10슬롯 최종 보스만 잡았다(`RoundManager.CurrentBossId`).
노르드 중부의 합류 분기가 **6슬롯 중간 보스 자리**에 있어서,
`CurrentMidBossId`를 새로 두고 방아쇠를 둘로 늘렸다.

**한 사건이 둘을 내민다.** 시구르드를 데려가든 브륀힐드를 데려가든 둘 다 두고 가든,
**양쪽 모두 영구 해금**된다 — 만났다는 사실이 해금 조건이라는 기존 규칙 그대로다.
그래서 `StageEventData.RecruitUnitIds`(복수형)가 새로 생겼고,
`GrantRecruitUnlock`이 대표 한 명이 아니라 제안된 전원을 해금한다.

### 6.8 땅속의 것 (`seed_beneath_the_ground`) — 전 테마

**테마를 가리지 않는 첫 사건이다.** `themeId: 0` · `tier: 1` · `oncePerRun: true`.

| 선택 | 결과 |
| --- | --- |
| 덮어 두고 간다 | **아무것도 주지 않는다.** 되돌아올 수 없다 |
| 껍질을 깬다 | **종말의 씨앗(2032)과 전투.** 이기면 봉인된 향유병(4903) |

**덮어 두는 쪽이 노 리턴인 것이 이 사건의 전부다.** 안전하게 넘어간 것 자체가 값이고,
무엇이 들어 있었는지는 끝내 알 수 없다. 깨는 쪽은 대폭발(16%)을 든 엘리트 씨앗을
혼자 상대하는 대신 귀중품을 가져간다 — 자폭 규모를 아는 플레이어일수록 무게가 다르다.

> `tier`가 붙은 범용 사건은 그 슬롯의 **테마 사건보다 먼저** 잡힌다(`RoundManager`).
> 1로 둔 것은 영입 사건(T4)이 뜰 자리를 빼앗지 않기 위해서다. 한 런에 한 번만 뜨므로
> 테마 사건 하나를 밀어내는 것은 런당 한 번이다.

> 🔴 종말의 씨앗(2032)은 이 사건이 **유일한 등장처**다. 어느 테마 편성에도 없다.

### 6.9 형의 뒤에 서 있던 자 (`nord_loki_after_thor`) — 영입

**노르드(테마 16)의 보스 토르(3060)를 넘어서면 로키가 합류를 청한다.**
전투 승리 직후·보상 표시 직전의 체크포인트에서 뜬다.

| 선택 | 이번 런 | 다음 런부터 |
| --- | --- | --- |
| 합류를 환영한다 | 로키(82)가 일행에 들어온다 | 편성 가능 |
| 정중하게 거절한다 | 들어오지 않는다 | **편성 가능**(§6.0.1) |

자리가 없으면 합류 대신 §3.2의 자리 비우기 화면이 열린다
(`rosterFullText`: "자리가 없다. 로키를 영입하기 위해서는 누군가가 떠나야 할 것 같다.").

> 형을 쓰러뜨린 쪽에 붙는 배신자라는 신화 그대로다. 로키는 거절해도 기분 나빠 하지 않는다 —
> 그래야 거절이 손해가 아니라 취향으로 읽힌다.

### 6.10 거울에서 풀려난 깃털 (`mexica_quetzalcoatl_after_duel`) — 영입

메히코 **9슬롯 중간 보스** 케찰코아틀(3201)을 넘어서면 예약된다. `triggerBossId: 3201`.

10슬롯의 테스카틀리포카가 이 테마의 최종 보스이고 둘은 형제다. 깃털 뱀은 제 뜻으로 길을
막은 것이 아니라 흑요석 거울에 비친 명령을 따르고 있었고, 그 명령이 끊기는 자리가 곧
합류 분기다 — 6.3 「연기 나는 거울」이 깔아 둔 실을 여기서 걷는다.

거절해도 해금된다(6.0.1). 다음 슬롯에서 형을 만나는 순서라, **거절 쪽 대사만 보스전의
복선으로 남는다.**

> **부수 효과**: 이 사건이 생기면서 케찰코아틀(140)은 **데모 즉시 사용 목록에서 빠진다.**
> `CharacterSelectionManager.HasNoUnlockPath`는 "해금 경로가 데이터에 없으면 열어 준다"는
> 규칙이므로, 경로가 생기는 순간 저절로 다시 잠긴다. 의도된 자기 유지 동작이다.

### 6.11 천공 전선 츠쿠요미 체인 (`japan_sky_*`) — 슬롯 승리 예약

**보스가 아니라 슬롯이 방아쇠다.** 천공 1슬롯에는 범용 괴조만 서서 적 ID로 가를 수 없으므로
`triggerThemeId` + `triggerStageInRound`로 잡는다. `GameManager.QueueSlotClearEvents`가 승리 직후,
스테이지가 넘어가기 전에 테마와 내용 슬롯을 읽는다. 사건 안의 임시 전투는 방아쇠가 아니다.
이 두 값이 붙은 사건은 슬롯 풀에서 빠진다.

| id | 방아쇠 | 조건 | 내용 |
| --- | --- | --- | --- |
| `japan_sky_moonlit_reinforcement` | 19 · 1슬롯 | 츠쿠요미가 일행에 없음(`allowUnlockedRecruit`) | **합류** — 츠쿠요미(41). 거절해도 해금 |
| `japan_sky_moonlit_reinforcement_companion` | 19 · 1슬롯 | 츠쿠요미가 일행에 있음(`requiresUnitInParty: 41`) | 동행 대사만. 중복 영입·보상 없음 |
| `japan_sky_sever_the_connection` | 19 · 6슬롯 | 위 둘 중 하나를 봤음(`requiresRunEventIds`) | 고치의 연결과 나비의 인분 예고 |
| `japan_sky_falling_scales` | 19 · 9슬롯 | 같음 | 허기 결정·파열 비늘 예고 |
| `japan_sky_frontline_secured` | 19 · 10슬롯 | 같음 | 방어선 확보. 추가 보상 없음 |

**츠쿠요미 합류는 테마의 안전장치다.** 천공의 적은 다단·지속피해가 없는 파티를 의도적으로 막으므로,
어떤 메인으로 들어왔든 1슬롯 뒤에 넘을 수단을 한 명은 확보하게 한다. 그래서 다른 영입 사건과 달리
**영구 해금한 뒤에도 이번 런에 없으면 다시 제안한다**(`allowUnlockedRecruit`). 런당 한 번이다.

B·C·D는 선택지가 하나뿐인 짧은 후속 대사다. 선택지를 하나 둔 것은 `oncePerRun` 기록이
선택지 처리에서 남기 때문이다 — 선택지 없는 사건은 기록되지 않는다.

> **부수 효과**: 이 체인이 생기면서 츠쿠요미(41)는 **데모 즉시 사용 목록에서 빠진다**(6.10과 같은 동작).
> 영구 해금·육성 기록이 있는 프로필은 그대로다.

### 6.12 해안 전선 예고와 스사노오 합류 (`japan_coast_*`)

세이메이 합류는 슬롯 승리형, 스사노오 합류는 보스 격파형이다. 두 예고는 선행 사건 조건 없이 뜬다 —
세이메이를 거절한 런에서도 갑주·대호흡의 힌트는 받아야 하기 때문이다.

| id | 방아쇠 | 조건 | 내용 |
| --- | --- | --- | --- |
| `japan_coast_seimei_on_the_shore` | 20 · 1슬롯 | 세이메이가 일행에 없음(`allowUnlockedRecruit`) | **합류** — 아베노 세이메이(43). 거절해도 해금 |
| `japan_coast_seimei_companion` | 20 · 1슬롯 | 세이메이가 일행에 있음(`requiresUnitInParty: 43`) | 동행 대사만 |
| `japan_coast_cracked_shells` | 20 · 2슬롯 | — | 서리 앉은 곳부터 갈라진 껍질. 3슬롯에서 처음 나오는 **공허의 갑주를 얼려서 벗긴다**는 예고 |
| `japan_coast_deep_breath` | 20 · 8슬롯 | — | 물이 빠져나가는 바다. **대호흡을 행동불능으로 끊는다**는 예고 |
| `japan_coast_susanoo_after_archon` | 보스 3080 | 스사노오가 일행에 없음(`allowUnlockedRecruit`) | **합류** — 스사노오(42). 거절해도 해금 |
| `japan_coast_susanoo_companion` | 보스 3080 | 스사노오가 일행에 있음(`requiresUnitInParty: 42`) | 동행 대사만 |

**세이메이 합류는 테마의 안전장치다.** 관통이 제어 없는 파티의 탈출구이므로 츠쿠요미처럼 해금 뒤에도 이번 런에 없으면 다시 제안한다.
스사노오도 같은 규칙이다.

> **부수 효과**: 스사노오(42)·세이메이(43)는 해금 경로가 있어 **데모 즉시 사용 목록에 들지 않는다**(6.10과 같은 동작).

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
   (보스 격파형이면 `triggerBossId`를, 슬롯 승리형이면 `triggerThemeId`·`triggerStageInRound`를 적고 `themeId`/`stageInRound`를 0으로 두었는가)
4. `battle_*` 액션의 `battleEnemyId`가 `60_enemies.yaml`에 존재하는가
5. `grantItemId` / `grantUnitId` / `grantPassiveCodeId`가 실재하는가
6. 실패 분기(`failureText`)를 작성했는가
   (영입이면 자리 부족용 `rosterFullText`까지)
7. 거절 분기의 문구가 **해금은 되었다**고 알려 주는가(§6.0.1)
8. 새 대사 필드를 추가했다면 `CloneWithReplacement()`도 갱신했는가

## 8. 현재 공백

| 공백 | 내용 |
| --- | --- |
| 🔴 전력 변화 사건 없음 | 네 사건 모두 `action: continue`뿐이다. `grantUnitId` · `grantItemId` · `grantPassiveCodeId` · `battle_*`가 **스키마에만 있고 데이터에 한 번도 쓰이지 않는다** |
| 🔸 Locked 17인 중 13인 획득 불가 | 영입 경로가 생긴 것은 바스테트(122) · 호루스(120) · 세트(123) · 로키(82)뿐이다. 나머지 `characterType: Locked` 캐릭터는 여전히 편성 불가다 — [Design_Backlog](Design_Backlog.md) 항목 5·21 |
| 🔸 테마당 1개 고정 | 한 테마의 런에서는 늘 같은 사건이 나온다. 테마마다 둘 이상 두고 굴려야 반복이 줄어든다 |
| 🔸 `randomSpeakers` 미사용 | 치환 기능이 구현되어 있으나 실제 데이터에 쓰이지 않는다 |
| 🔸 사건 스케줄러 활용 | 보스 격파 예약과 슬롯 승리 예약(천공 체인·해안 예고)이 쓴다. 전투 도중 삽입 사례는 아직 없다 |
