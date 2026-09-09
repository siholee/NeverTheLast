# 상세 기획서 07 — UI와 기술 구조

> **3계층 문서.** UI 화면 구성, 매니저 구조, 데이터 파일, 저장 시스템, 아트/사운드 규약.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 7장을 본다.

최종 갱신: 2026-08-30
관련 코드: `Assets/Scripts/Managers/`, `Assets/Scripts/Core/`, `Assets/Scripts/Data/`

---

## 1. UI 화면 구성

### 1.1 화면 목록

| 화면 | 스크립트 | 역할 |
| --- | --- | --- |
| 메인 메뉴 | `UI/MainMenuUI.cs` | 새 여정 / 이어하기 / 설정 |
| 캐릭터 선택 | `UI/Screens/CharacterSelectScreen.cs` | 철권식 2단계 셀렉트. 아래 §1.6 |
| 준비 | `UI/Screens/PreparationScreen.cs` | 행동 선택, 덱 구성, 장비 |
| 보상 | `UI/Screens/RewardScreen.cs` | 3택 1 |
| 육성 | `UI/Screens/TrainingScreen.cs` | 5스탯 집중 버튼, 서포트 등장/우정 표시 |
| 사건 | `UI/Screens/EventScreen.cs` + `EventVisualNovelScreen.cs` | VN 연출 |
| 도감(TAB) | `UI/Screens/CodexScreen.cs` | 발더스 게이트 3 인벤토리 레이아웃. 아래 §1.5 |
| 모달 | `UI/Screens/ModalScreen.cs` | 확인/경고 |
| 설정 | `UI/SettingsUI.cs` | Music / Sfx 볼륨 등 |

### 1.5 TAB 화면 (CodexScreen)

발더스 게이트 3 인벤토리 화면을 레퍼런스로 삼는다.

```
┌───────────────────────────────────────────────────────────┐
│  캐릭터명                              중량            ✕   │
├──────────┬──────────────────────────────┬─────────────────┤
│ 파티 목록 │ [장비][코드][시트][추천 조합] │  선택 항목 상세  │
│          ├──────────────────────────────┤                 │
│  캐릭터1  │ STR  DEX  CON  INT  LUK      │  아이템 설명     │
│  캐릭터2  │  14   24★  13   10   22◆     │  또는           │
│  캐릭터3  │──────────────────────────────│  캐릭터 요약     │
│          │  소유 아이템 격자 / 시트      │                 │
└──────────┴──────────────────────────────┴─────────────────┘
```

**항상 열 수 있다** — `BattleHud.Update`가 매 프레임 TAB을 검사하며, 캔버스 정렬 순서가 120이라
보상·사건 같은 모달(80)보다 위에 뜬다. 어느 페이즈에서든 아이템 보유자와 장착을 지정할 수 있다.

**주스탯 스트립** — 레퍼런스의 `근접 +N / 명중 보너스 / 피해` 줄 자리에 **5주스탯**을 놓는다.
주스탯은 ★, 부스탯은 ◆로 표시하고 앰버로 강조한다. 소유 아이템 격자 바로 위에 항상 보인다.

**캐릭터 시트 탭** — 네 구획을 순서대로 쌓는다.

| 순서 | 내용 |
| --- | --- |
| 1. 기초 스탯 | STR / DEX / CON / INT / LUK |
| 2. 상세 스탯 | 체력, 방어력, 내구, 위력 100 기준 피해, 치명타(확률·배율), 행동 속도, 마나 효율 |
| 3. 육성 | 육성 Lv, 중량 `현재 / 한도`, 보유 코드 수 |
| 4. 상태 | 지금 걸려 있는 상태를 전투 카드와 **같은 아이콘**으로. 없으면 구획 자체가 빠진다 |

**코드는 별도 탭이다.** 고유 패시브·일반행동·궁극기와 해금·전수·장비 코드를 한 목록에 놓고
등급(은·금·보라)으로 색을 준다. **코드 용량 한도는 폐지됐으므로 `보유 / 최대` 표기는 없다.**

**추천 조합 탭** — `30_synergies.yaml`을 `SynergyCatalog`이 읽어 유닛 칸마다 채운다.
기획 원본은 [Detail_15](Detail_15_Party_Synergy.md)다.

| 순서 | 내용 |
| --- | --- |
| 1. 역할 | 역할군 ID를 한국어 이름으로 풀어 나열 |
| 2. 기여 | 전투·육성 기여를 ●○ 눈금 5칸으로. 권장 열(전열/후열) 병기 |
| 3. 강점 | `provides[].text` 를 줄바꿈 되는 문단으로 |
| 4. 주의 | `caution`. 붉은색 |
| 5. 편성 | **메인 가능**이면 고점·대체 두 벌을 아키타입 이름과 함께. **서포트 전용**이면 이 캐릭터를 부르는 메인 목록 |

**아직 손에 넣지 못한 이름은 흐리게 찍는다**(`SynergyCatalog.IsAvailable`).
Support 유형은 언제나 편성 가능하므로 항상 밝게 나오고, Starter·Locked는 해금·육성 기록을 본다.
"지금 짤 수 있는 조합"과 "다 모으면 되는 조합"이 같은 화면에서 갈린다.

### 1.6 캐릭터 선택 화면

철권 캐릭터 셀렉트를 레퍼런스로 삼는다. 후보를 **초상화 타일 격자**로 늘어놓고,
커서를 올린 캐릭터의 **큰 초상화**를 좌측에 띄운다.

**그림은 초상화(Portrait)만 쓰고 타일·미리보기 모두 정사각형으로 고정한다.**
초상화 원본이 정사각형이므로 칸이 정사각형이면 여백 없이 딱 맞는다.
스탠딩은 세로로 길어 같은 칸에 섞으면 비율이 무너지므로 이 화면에서는 쓰지 않는다.

칸 크기는 `SquareGridSizer`가 격자 영역의 실제 픽셀 크기에서 매번 다시 계산한다.
캔버스 스케일은 첫 프레임에 확정되지 않고 창 크기에 따라 또 바뀌므로 한 번만 재면 어긋난다.

```
┌──────────────┬──────────────────────────────────────┐
│              │  1단계: 메인 캐릭터                   │
│   큰 초상화   │  ┌────┬────┬────┬────┬────┬────┐     │
│              │  │ 초상│ 초상│ 초상│ 초상│ 초상│ 초상│     │
│   캐릭터명    │  │ 이름│ 이름│ 이름│ 이름│ 이름│ 이름│     │
│   원소·주/부  │  └────┴────┴────┴────┴────┴────┘     │
│   분류 설명   │  메인  수르트 / 서포터 —              │
│              │        [← 뒤로]        [다음 →]       │
└──────────────┴──────────────────────────────────────┘
```

**모드별 흐름**

| 모드 | 단계 |
| --- | --- |
| 육성 | 1단계 **메인 1명** → `다음` → 2단계 **서포터 카드 4명** → `여정 시작` |
| 무한 | 메인 단계 없이 **서포터 카드 5장**을 바로 고르고 `여정 시작` |

- 1단계 후보는 `canStartAsMain` 또는 해금된 캐릭터, 2단계 후보는 `canStartAsSupport`만 보여준다
- 메인 단계에서 다른 타일을 누르면 기존 선택을 갈아 끼운다(항상 1명)
- 이미 고른 타일을 다시 누르면 해제된다
- `뒤로`는 2단계에서만 보이며 누르면 편성을 비우고 1단계로 돌아간다

### 1.7 사건(VN) 진행 조작

| 버튼 | 동작 |
| --- | --- |
| `계속 ▸` | 대사 한 줄 진행 |
| `자동` | 켜면 일정 시간마다 스스로 다음 대사로 넘어간다. 다시 누르면 해제 |
| `건너뛰기 ▶▶` | 남은 대사를 전부 건너뛰고 선택지로(없으면 사건 종료로) 간다 |

자동 대기 시간 = `1.2초 + 글자 수 × 0.045초` (상한 6초).
긴 대사일수록 오래 머물러 읽을 시간이 남는다.

**선택지·결과 화면에서는 자동이 꺼지고 세 버튼이 모두 숨는다.** 선택은 플레이어가 해야 한다.

> 화면은 MonoBehaviour가 아니므로 타이머는 `UIManager.Update()`가 매 프레임 넘겨 준다.

### 1.8 전장 초상화 크기 정규화

초상화 원본 해상도가 제각각이다 — 아군 1254px, 적 512px, 일부 1024px.
`Cell`의 스프라이트 렌더러 배율을 고정해 두면 **아군은 칸을 넘치고 적은 칸의 절반만 채운다.**

`Cell.SetPortrait`가 긴 변을 `PortraitFitSize`(9.6, 칸 테두리 10.16보다 약간 안쪽)에 맞춰
균일 배율로 줄인다. 해상도와 무관하게 항상 칸에 꼭 맞고 비율도 유지된다.
드래그 미리보기도 같은 배율을 따른다.

### 1.9 전장 카메라 프레이밍

배치 상수를 바꿀 때마다 씬의 카메라를 손으로 옮기면 금방 어긋난다.
`GridManager.FrameCamera()`가 **실제로 만들어진 셀 좌표에서 경계를 구해** 그 중심에 카메라를 두고
`orthographicSize`를 잡는다. 전열/후열 간격이나 대기석 위치를 바꿔도 프레이밍이 따라온다.

HUD가 판을 덮지 않도록 위아래로 여유를 더 준다.

| 여유 | 값 | 이유 |
| --- | --- | --- |
| `CameraTopHudFraction` | 0.07 | 상단 상태바가 최상단 행을 가린다 |
| `CameraBottomHudFraction` | 0.16 | 준비 페이즈 바가 대기석을 가려 유닛을 내려놓을 수 없다 |

아래 여유는 **준비 단계에만** 준다. 전투 중에는 그 바가 사라진다.

**전투 중에는 대기석을 숨긴다.** 유닛을 옮길 수 없어 자리만 차지하기 때문이다.
`SetBenchVisible(false)`가 대기석을 끄고 카메라 계산에서도 빼므로 전장이 그만큼 확대된다.
`OnRoundStart`에서 끄고 `OnRoundEnd`에서 되돌린다.

> 🔸 **씬에 저장된 범위가 구조를 깨뜨릴 수 있다.**
> `CalculateFieldCellPosition`은 |x|=1을 전열, 그 외를 후열로 보므로 x가 ±2를 넘으면
> 여러 x가 같은 좌표로 계산되어 셀이 겹쳐 쌓인다. 실제로 씬에 구 레이아웃 값
> (`xMin -3 / xMax 3 / yMax 3 / benchSize 9`)이 남아 x=−3과 −2가 포개져 있었다.
> `EnforceLayoutBounds()`가 진영당 2열 × 4행, 대기석 5칸으로 바로잡고 경고를 남긴다.

## 1.10 투사체 궤적

공격 이펙트는 Hovl Studio `AAA Projectiles Vol 1`을 쓰고, 비행은 `HS_ProjectileCustomMover`가 굴린다.
`SfxManager.FireSingleProjectile`이 풀에서 꺼내 시전자→대상 좌표와 궤적을 넘긴다.

**궤적은 두 가지만 쓴다.**

| 궤적 | 대상 | 계산 |
| --- | --- | --- |
| **직선형** `Linear` | 마법·특수 투사체, 접촉(근접) 물리 | `Lerp(start, end, t)` |
| **곡선형** `ParabolicArc` | 화살·투척 등 **원거리 물리** | `Lerp` + `4h·t(1−t)`, 최고 높이 5 |

선택은 `ProjectileFlight.PathFor(피해 태그)`가 한다 — `Physical`이면서 `ContactAttack`이 아니면 곡선형,
그 외에는 직선형이다. 접촉 물리를 뺀 이유는 붙어서 때리는 연출에 포물선을 씌우면
이펙트가 솟았다 내려와 타격감이 어긋나기 때문이다.

> 🔸 **나머지 경로 타입(Spiral·Wave·Bezier·MultiPoint·DelayedDrop)은 구현만 남기고 쓰지 않는다.**
> 이 게임은 XY 평면을 정사영으로 비추는데, 그 경로들은 오프셋을 **경로에 수직인 z축**으로 준다.
> Wave는 전량이 z라 화면에서 아무 변화가 없었고, Spiral은 절반만 y라 나선이 아니라 위아래 진동으로 보였다.
> 곡선형은 y를 직접 올리므로 2D에서 그대로 보인다.

**파일 위치** — `Scripts/Effects/Projectiles/`
(`HS_ProjectileCustomMover` · `ProjectilePathType` · `ProjectilePathData` · `ProjectileFlight`)
`HS_ProjectileCustomMover`는 이 프로젝트가 작성한 코드인데 벤더 폴더(`Hovl Studio/`)에 있었다.
에셋을 다시 임포트하면 덮어써질 자리라 Scripts로 옮겼다.

### 1.2 전투 HUD

| 컴포넌트 | 표시 |
| --- | --- |
| `HUD/TopStatusBar.cs` | 생명력, 스테이지/라운드, 페이즈 타이머(원형 게이지 + 숫자) |
| `HUD/PartyPanel.cs` | 파티 유닛 상태 |
| `HUD/ActionQueuePanel.cs` | **행동 순서 큐** — 다음에 누가 움직이는지 |
| `HUD/BattleHud.cs` | 전투 화면 총괄 |

> 전투가 자동인 만큼, **"다음에 누가 움직이는가"를 항상 노출**하는 것이 관전 경험의 핵심이다.

### 1.3 UI 테마

| 파일 | 역할 |
| --- | --- |
| `UI/Theme/UITheme.cs` | 색상 팔레트, 타이포 |
| `UI/Theme/UIShapes.cs` | 런타임 도형 생성 (RoundedRect, Parallelogram, VerticalGradient 등) |
| `UI/Core/UIBuild.cs` | 공통 빌더 |

**아트 에셋 없이 코드로 UI를 구성한다.** 스타일 변경이 데이터/코드 한 곳에서 끝난다.

### 1.4 셀 UI 규칙

| 규칙 | 내용 |
| --- | --- |
| 책임 분리 | `Unit.cs`는 게임 로직, `Cell.cs`는 UI 표시 |
| 필드 전용 | 대기석 유닛은 UI를 표시하지 않는다 |
| HP 바 | 체력 100% = scale 9, 위치 이동으로 감소 표현 |
| **z값 통일** | 모든 바의 z = 0 |
| 깊이 제어 | z축이 아닌 **`SpriteRenderer.sortingOrder`** 사용 |

```csharp
// HP 바 스케일 계산
float targetScale = hpRatio * 9.0f;
float xOffset = (9.0f - targetScale) * -0.5f;

// 렌더 순서
currHpRenderer.sortingOrder = maxHpRenderer.sortingOrder + 1;
```

> **교훈** — 2D에서는 z축 위치보다 `sortingOrder`가 안정적이다.
> 과거 HP 바에 임의 z값(-0.1f)을 쓴 결과, 드래그 앤 드롭의 z 좌표계와 충돌해 위치가 왜곡됐다.

## 2. 매니저 구조

모든 매니저는 싱글턴(`Manager.Instance`)이다.

| 매니저 | 책임 |
| --- | --- |
| `GameManager` | 게임 상태 머신, 페이즈 타이머, 생명력, 사건 흐름, 준비 행동 |
| `RunManager` | 런 수명(시작/저장/복원/완료), 우정도, 사건 발생 이력, **스테이지 전진 단일 진입점** |
| `RoundManager` | 스테이지/라운드/테마 결정, 적 편성·배치, 보스 슬롯 판정 |
| `GridManager` | 셀 생성, 유닛 스폰/활성화, 타겟팅, `heroList` / `enemyList` |
| `ActionScheduler` | 행동치 기반 행동 예약·직렬 실행 (MonoBehaviour 아님, `GameManager`가 소유) |
| `TrainingManager` | 집중 훈련, 서포트 판정, 패시브 전수 (**정적 클래스**) |
| `RewardManager` | 보상 풀 생성 및 적용 |
| `CharacterSelectionManager` | 편성 규칙 검증 (최대 5, 메인 1, 중복 불가) |
| `InventoryManager` | 골드/토큰/티켓/보관 아이템, 장비 착용 검증 |
| `DataManager` | YAML 제네릭 로더 (`Load<T>`) |
| `UIManager` | 화면 전환 위임 |
| `AudioManager` | BGM/SFX 재생, 볼륨 |
| `SfxManager` | 전투 이펙트 |
| `DragAndDropManager` | 유닛 배치 조작 |
| `EventScheduler` | 사건 예약 큐. 임의 지점에 사건을 끼워 넣는다 (MonoBehaviour 아님) |

`SettingsManager`는 `Assets/Scripts/Core/`에 있고 위 목록의 싱글턴 규칙과 별개다.
| `EventScheduler` | 사건 예약 큐 |

### 2.1 단일 진입점 원칙

`RunManager.AdvanceToNextStage()` — 보상/사건/육성 어느 흐름에서 오든 여기를 통과한다.

```
if (육성 모드) {
    if (Stage >= MaxTrainingStage || !TryLoadNextRound())  → CompleteTrainingRun()
    else                                                    → 세이브 + 다음 스테이지
} else {
    if (!TryLoadNextRound())  → RunComplete + 세이브 삭제 + 메인 메뉴
    else                       → 세이브 + 다음 스테이지
}
```

> **흐름이 갈라져도 종료 판정은 하나로 유지된다.** 새 진행 경로를 추가할 때 이 메서드를 호출한다.

### 2.2 씬 구성

| 씬 | 상수 |
| --- | --- |
| 메인 메뉴 | `SceneNames.MainMenu` |
| 전투 | `SceneNames.Game` |

씬 전환 시 `CleanupRunContext()`로 런 컨텍스트를 정리한다.
`RunManager`는 `DontDestroyOnLoad`로 씬을 건너뛴다.

## 3. 데이터 파일

`Assets/Resources/Data/` — YamlDotNet으로 역직렬화한다.

| 파일 | 내용 | DTO |
| --- | --- | --- |
| `00_intro.yaml` | 인트로 시퀀스 | `IntroData.cs` |
| `10_units.yaml` | 아군 유닛 | `UnitData.cs` |
| `20_codes.yaml` | 코드 표시 데이터 (패시브/일반/궁극기) | — |
| `30_synergies.yaml` | 역할군·아키타입·메인별 추천 편성 ([Detail_15](Detail_15_Party_Synergy.md)) | `SynergyData.cs` |
| `40_items.yaml` | 아이템 (= 전투 보상 풀) | `ItemData.cs` |
| `50_tokens.yaml` | 토큰 정의 | `TokenData.cs` |
| `60_enemies.yaml` | 적 (normal/elite/boss) | `EnemyData.cs` |
| `70_rounds.yaml` | 라운드 타입/편성 패턴 (폴백 전용) | `RoundData.cs` |
| `80_stages.yaml` | 테마, 사건, 고정 보스 | `StageData.cs` |
| `90_rewards.yaml` | 라운드별 보상 티어 확률 | `RewardData.cs` |

**로딩** — `DataManager.Load<T>()` 제네릭 로더. `IgnoreUnmatchedProperties`가 적용되어
YAML에 남아 있는 폐기 필드(`classLevels`, `subclasses` 등)는 무시된다.

## 4. 저장 시스템

`Assets/Scripts/Core/SaveSystem.cs`, `SaveData.cs`

### 4.1 진행 저장 (`RunSaveData`, version 6)

| 필드 | 내용 |
| --- | --- |
| `currentStage` / `currentRound` | 진행 위치 |
| `gameMode` | 육성 / 무한 |
| `life` / `killCount` | 생명력, 처치 수 |
| `gold` / `tokens` / `rerollTicketCount` / `storedItemIds` | 인벤토리 |
| `preparationActionUsed` | 준비 행동 소모 여부 |
| `supportBonds` | 서포트별 우정도 |
| `training` | 훈련 상태(기력·스킬 포인트·컨디션·집중 배치). v4 저장본에는 없어 기본값으로 채워진다 |
| `heroUnits` | 아군 유닛 상태 (아래 4.2) |
| `triggeredEventIds` | 이미 발생한 사건 ID |

**버전 이력** — v2 레거시 스탯 필드 제거 · v3 공격력/방어력 폐지와 아군 EXP 레벨업 도입 ·
v4 유닛별 휴대 인벤토리와 3단계 중량 · v5 유닛 ID 소속별 재배치 ·
**v6 코드 ID 계열별 재배치**(스카디 주·부 스탯, 수르트 궁극기 자원 종류 변경 포함).

### 4.2 유닛 저장 (`UnitSaveData`)

`unitId`, `currentHP`, `xPos` / `yPos` / `isBench`, `level` / `exp`, `trainingLevel`,
`strUpgrade` ~ `lukUpgrade`, `codeAccelerationBonus`, `equippedItemIds`, `carriedItemIds`, `grantedPassiveCodeIds`

### 4.3 영구 저장 (`TrainedCharacterRecord`)

육성 완료 캐릭터. 런과 무관하게 누적된다. → [Detail_04 §7](Detail_04_Training.md#7-육성-완료와-계승)

### 4.4 저장/삭제 시점

| 시점 | 동작 |
| --- | --- |
| 스테이지 전진 | `SaveCurrentRun()` |
| 전투 시작 직전 | `SaveCurrentRun()` |
| 사건 발생 기록 | `MarkEventTriggered()` → 즉시 저장 |
| 앱 일시정지 / 종료 | `OnApplicationPause` / `OnApplicationQuit` |
| 런 시작 | `SaveSystem.DeleteSave()` — 기존 진행 폐기 |
| 게임 오버 / 런 완료 | `SaveSystem.DeleteSave()` |

### 4.5 복원 순서

```
LoadSavedRun()
  1. 모드/생명력/킬 수 복원
  2. 인벤토리 복원 (토큰, 골드, 티켓, 아이템)
  3. 우정도 / 사건 이력 복원
  4. RoundManager.InitializeStage(stage)
  5. 기존 아군 전부 비활성화 → 저장된 아군 스폰 → RestoreRunState()
  6. RoundManager.LoadRound(stage)
  7. UI 갱신 → EnterNextStageAfterLoad() → 준비 행동 상태 복원
```

**구버전 세이브는 로드 시 폐기된다** (`RunSaveData.version`).

## 4.6 디버그 모드 (에디터 · 개발 빌드 전용)

전투가 완전 자동이라 **확인하고 싶은 장면에 도달하는 방법이 정상 플레이뿐이다.**
테마 하나를 보려면 라운드를 그만큼 지나야 하고(테마는 `(Round-1) % 활성테마수`로 정해진다),
Lv.90 해금 코드를 보려면 런을 거의 완주해야 한다. 그래서 상태를 직접 밀어 넣는 조작을 모았다.

`Core/DebugMode.cs`(상태) + `Managers/UI/DevTools/DebugOverlay.cs`(패널).
둘 다 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`으로 감싸 **출시 빌드에는 들어가지 않는다.**
호출부(`Unit.TakeDamage`·`RoundManager.EnsureThemeForCurrentRound`·`GameManager.HandleDebugInput`)도
같은 조건으로 감쌌다.

### 단축키

| 키 | 동작 |
| --- | --- |
| **F1** | 디버그 패널 열기/닫기 |
| F2 | 적 전멸 — 지금 전투를 즉시 이긴다 |
| F3 | 아군 무적 토글 |
| F4 | 배속 순환 1 → 4 → 8배 |
| F9 | 현재 테마의 사건을 즉시 실행 |

### 패널 조작

| 묶음 | 내용 |
| --- | --- |
| 테마 고정 | 라운드 계산을 무시하고 테마를 고정한다. **`enabled: false`인 테마도 고를 수 있다** |
| 스테이지 | ±1 / ±10 이동, 슬롯(1·5사건·6엘리트·9엘리트·10보스) 바로가기 |
| 전투 | 적 전멸(승리) · 아군 전멸(패배) · 아군/적 무적 · 아군 회복 |
| 아군 레벨 | Lv.1 / 30 / 60 / 90 즉시 설정, +10 |
| 배속 | 0.25× / 1× / 4× / 8× |

**강제 설정이 하나라도 켜져 있으면 화면 위에 빨간 배너가 상시로 뜬다.**
무적을 켜 둔 채 밸런스를 재는 사고를 막기 위한 것이다.

### 원칙

- **새 규칙을 만들지 않는다.** 이미 있는 진입점(`RoundManager.LoadRound`, `Unit.Die`,
  `Unit.DebugSetLevel`)만 부르므로 디버그로 만든 상태가 정상 경로와 다르지 않다.
- 무적은 `Unit.TakeDamage` **입구**에서 자른다. 지속피해·고정피해까지 한 곳에서 막기 위해서다.
- 테마 고정은 **다음 스테이지 로드부터** 적용된다.

---

## 5. 아트 규약

| 영역 | 규약 |
| --- | --- |
| 초상화 | `Resources/Sprite/Portraits/{Allies, Enemies/Normal, Enemies/Elite, Enemies/Boss}/{name}` — 데이터에는 폴더·확장자를 제외한 대문자 스네이크 키만 기록 (`TSUKUYOMI_PORTRAIT`) |
| 표정 | `{PORTRAIT}_{emotion}` — 없으면 기본 초상화로 폴백 |
| 스탠딩 | 초상화와 같은 분류 폴더의 `{NAME}_STANDING` |
| 페이퍼돌 | [Character_PaperDoll_Spec.md](../Character_PaperDoll_Spec.md) 참조 |
| 이펙트 | Hovl Studio 투사체 기반 |
| 폰트 | TextMesh Pro |

`SpriteResource`가 위 네 분류 폴더를 먼저 뒤지고 이전 평면 루트를 마지막으로 본다. 따라서 YAML과
코드에는 분류 경로를 넣지 않고 기존 스프라이트 키를 그대로 유지한다. 아군·적이 같은 원화를 공유하는
경우에는 파일을 `Allies`에 한 번만 두고 양쪽에서 같은 키로 불러온다.

> **초상화·스탠딩은 `Resources.Load`를 직접 부르지 않는다. 반드시 `SpriteResource`를 거친다.**
> 데이터가 들고 있는 것은 키(`SEI_PORTRAIT`)이고 실제 파일은 분류 폴더 안에 있어서, 키를 그대로
> `Resources.Load`에 넘기면 **조용히 null이 돌아온다.** 분류 폴더로 옮기던 날 소환수 카드·행동
> 대기열·도감·육성 화면이 한꺼번에 빈 칸이 된 것이 이 때문이다.
> `SpriteResource`는 키와 해석이 끝난 전체 경로를 모두 받으므로 진입점을 하나로 둘 수 있다.
>
> `Unit.PortraitPath`는 **해석에 성공하면 전체 경로, 실패하면 키 그대로**를 담는다. 어느 쪽이든
> 그대로 `SpriteResource.LoadPortrait`에 넘기면 된다.
>
> 아이템 아이콘(`Sprite/Items/`)만은 분류가 없는 평면 폴더라 직접 로드해도 된다.

디버그 검증(F1 → 전체 검증)의 **스프라이트 연결** 단계가 유닛·적·아이템·소환수의 키를 전부 한 번씩
읽어 본다. 자산을 옮기거나 이름을 바꾸면 이 단계가 먼저 깨진다.

### 5.1 투사체 프리팹 제작 절차

1. 원하는 투사체 프리팹을 복사해 `Prefabs/SFX`에 붙여넣기
2. 콜라이더, 리지드바디, 라이트 컴포넌트 삭제
3. `Hovl Studio/HSFiles/Scripts/HS_ProjectileCustomMover` 컴포넌트 추가
4. 기존 `HS_ProjectileMover`의 내용(Hit, Hit PS, Flash, Projectile PS)을 `HS_ProjectileCustomMover`에 동일하게 할당
5. `HS_ProjectileMover` 컴포넌트 삭제
6. 하위 파티클 사이즈 조절 (10배 정도)

> 복사하는 투사체마다 Flash가 없는 등 구성이 조금씩 다르다.

## 6. 사운드 규약

| 항목 | 규약 |
| --- | --- |
| 경로 | `Resources/Audio/BGM/{name}`, `Resources/Audio/SFX/{name}` |
| 담당 | `AudioManager` |
| 볼륨 | `SettingsManager`의 Music / Sfx 값을 따른다 |
| BGM 전환 | 같은 곡이면 재시작하지 않음. `stop`이면 정지 |
| **누락 처리** | 파일이 없어도 **조용히 무시** (같은 이름은 1회만 로그) |

**누락 처리 정책이 핵심이다.** 기획 데이터를 먼저 쓰고 에셋은 나중에 채울 수 있다.

## 7. 빌드와 검증

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity Editor (CLI 빌드 명령 미구성) |
| 메인 씬 | `Assets/Scenes/SampleScene.unity` |
| 플랫폼 | Android, 패키지 `com.solid.autochess` |
| 스크립팅 백엔드 | IL2CPP |
| 입력 | Unity 신규 Input System (active input handler: 2) |

**빌드 검증**

```bash
dotnet build Assembly-CSharp.csproj
```

```bash
dotnet build Assembly-CSharp-Editor.csproj
```

> Unity가 `.csproj`를 재생성할 수 있다. 파일 삭제 후 IDE 빌드에서 삭제된 `.cs`를 찾는 오류가 나면
> 프로젝트 파일 재생성 또는 Compile 항목 정리를 확인한다.

## 8. 디버깅 진입점

| 대상 | 방법 |
| --- | --- |
| 피해 계산 | `Unit.DefaultTakeDamageEvent()`의 로그 활성화 |
| 이벤트 전파 | `GridManager.OnRoundStart()` 로그 |
| 상태 전이/타이머 | `GameManager.Update()` 로그 |
| 유닛 실시간 상태 | 도감(`CodexScreen`) / 파티 패널 |
| 사건 연출 | 플레이 모드에서 **F9** (미리보기) |
| 보호막 | `AddShield` 로그 — `Max A→B, Curr C→D (HP: x/y)` |

## 9. 기술 부채

| 항목 | 내용 |
| --- | --- |
| 🔸 자동 테스트 없음 | 회귀 검증이 전적으로 수동 플레이에 의존한다 |
| 🔸 밸런스 시뮬레이터 없음 | 스테이지별 예상 전투 길이/승률을 계산할 수단이 없다 |
| 🔸 `RewardDef` 필드명 | 옛 스탯 체계 이름이 남아 실제 효과와 어긋난다 |
| 🔸 메인 사망 시 폴백 | 첫 활성 아군이 메인으로 승격된다. 정책 명시 필요 |
