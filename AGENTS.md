# AGENTS.md

이 저장소에서 작업하는 Codex를 위한 지침이다.
최종 갱신: **2026-10-06** — 보상 코드(패시브 500~999, 94종, 유닛당 활성 7칸 + 개인 보관함) · 영구 중첩 · 소환수 · 공격 형태 태그(50000번대) · 로카팔라 묶음(인드라 초월기가 유일한 개방자) · 인드라/바유/쿠베라/아누비스/우치 구현 · 저장 v13/v12 이관 · 준비 중 무료 드래그(게임 측면 — 연출은 아직). 직전 갱신(2026-10-03) — 8스탯·공용 인벤토리·휴식 전면 개편, 밝은 분위기 안 2 및 손그림 시트 래퍼·전역 VFX 방향/다중 관통/앞 레이어·96_vfx 부품 조합·상태 부여 연출·시/사바흐/세이/찬드라/히미코/피그말리온/바스테트/잔 기본 Handpaint·Legacy 비교·공허 적 전원/궁극기 비투사체 VFX·행동서열 삽입 슬롯/재배치 모션·P11 하네스 메타데이터. 개편 전 상태는 커밋 `a6194c9`.

## 프로젝트 개요

NeverTheLast는 **Unity 6000.4.9f1 / C#**으로 만드는 신화 기반 로그라이트 RPG다.
해금한 캐릭터 1~5명으로 파티를 구성하고, 문명별 테마가 이어지는 끝없는 런을 진행한다.
전투는 **턴제 자동 전투**이며, 플레이어는 전투 전에 편성·배치·덱 구성을 결정한다.
게임 모드는 **무한 모드 하나**다(`GameMode` enum 없음). **훈련 · 서포트 카드 · 계승 · 메인/서포트 구분은 2026-09 개편으로 사라졌다.**
캐릭터는 `characterType: Unlocked | Locked`의 해금/미해금으로만 나뉘고, 해금 판정은 `CharacterSelectionManager.IsUnlocked` 하나다.
로스터는 한 명씩 다시 만드는 중이며 현행 캐릭터 목록과 전투 사양은 `Assets/Docs/Design/Detail_08_Confirmed_Characters.md`, 등록 데이터는 `10_units.yaml`을 기준으로 한다. 테마는 **공허의 시험장**이다.

- 작업 언어는 한국어다. 설명·주석·로그는 주변 코드의 표현과 밀도에 맞춘다.
- 에디터 버전의 기준은 `ProjectSettings/ProjectVersion.txt`다.
- 기존 작업 트리에 수정·삭제·미추적 파일이 있을 수 있다. 요청과 무관한 변경을 덮어쓰거나 되돌리지 않는다.

## 문서와 구현의 기준

기획 문서의 입구는 `Assets/Docs/Design/README.md`다. 게임 규칙을 바꾸기 전에 관련 상세
기획서를 읽고, 변경 시 코드·데이터·해당 문서를 함께 맞춘다.

| 문서 (`Assets/Docs/Design/` 기준) | 확인할 내용 |
| --- | --- |
| `GDD_Main.md`, `GDD_Sub_Concepts.md` | 게임 방향과 개념 정의 |
| `Detail_01_Progression.md` | 런·스테이지·테마·진행 흐름 |
| `Detail_02_Combat.md` | 전장·행동치·피해·경감·효과 명중·보호막·제어·상태·이벤트·카드 연출 |
| `Detail_03_Character.md` | 8스탯·선형 성장·마나·코드·집중·ID 규칙 |
| `Detail_04_Training.md` | 폐지된 훈련 체계의 목록 |
| `Detail_05_Economy.md`, `Detail_06_Events.md` | 재화·보상·공용 아이템·식량·사건 |
| `Detail_07_UI_Tech.md` | UI·매니저·저장·검증 도구 |
| `Detail_08_Confirmed_Characters.md` | 캐릭터별 전투 사양 |
| `Detail_09_Enemy_Catalog.md`, `Detail_10_Boss_Catalog.md` | 적 편성·보스 |
| `Detail_11_Code_Catalog.md`, `Detail_12_Code_Weapon_Catalog.md` | 코드 효과 색인·슬롯 |
| `Detail_13_Reward_Catalog.md`, `Detail_14_Equipment_Catalog.md` | 보상 풀·폐지된 장비 규칙의 이력 |
| `Detail_15_Party_Synergy.md`, `Detail_16_Elements.md` | 추천 편성·원소 반응·전장 상태 |
| `Detail_17_Reward_Codes.md` | 보상 코드(패시브 500~999, T1~T5)·활성/보관/무료 드래그/상위 대체·공격 형태 태그·비약/인장/두루마리·보상 풀/상점·구현 위치 |
| `Design_Backlog.md` | 아직 결정되지 않은 기획 |

- 수치의 원본은 해당 상세 기획서에 둔다. 이 지침에 캐릭터 수·콘텐츠 목록·밸런스 수치를 중복 관리하지 않는다.
- 미구현 기획은 구현된 기능과 구분하고 `🔸`로 표시한다. 문서 수정 시 최종 갱신일을 남긴다.
- `CLAUDE.md`에도 구조·콘텐츠 추가 안내가 있다. 다만 안내 문서와 코드 주석에도 과거 규칙이 남을 수 있으므로,
  **현재 동작은 실행 코드·YAML·씬의 직렬화 값으로 확인한다.** 불일치를 이유로 기획을 임의 확정하지 않는다.
- 삭제된 `Assets/Instructions/`와 과거 구현 보고서를 현행 설계 근거로 사용하지 않는다. 필요하면 git 이력을 참고한다.

## 개발·빌드·검증

- 시작 씬: `Assets/Scenes/MainMenu.unity` → `Assets/Scenes/Game.unity`.
  실제 빌드 씬은 `ProjectSettings/EditorBuildSettings.asset`을 확인한다.
- 기본 실행·컴파일 확인은 Unity Editor의 Play Mode와 Console에서 한다.
- **CLI 빌드 진입점이 있다.** `Assets/Scripts/Tools/Editor/DemoBuild.cs`의
  `DemoBuild.BuildWindows` / `DemoBuild.BuildMac`을 사용한다.
  에디터 메뉴는 `Tools/NeverTheLast/Build Windows Demo` / `Build Mac Demo`다.

```text
Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod DemoBuild.BuildWindows -logFile Logs/DemoBuild.log
Unity.exe -batchmode -quit -projectPath . -buildTarget OSXUniversal -executeMethod DemoBuild.BuildMac -logFile Logs/DemoBuildMac.log
```

`Unity.exe`는 설치된 해당 버전 실행 파일로 지정한다. 대상 플랫폼의 Build Support 모듈이 필요하다.
빌드 스크립트는 해당 `Builds/NeverTheLast_Demo_*` 출력 디렉터리를 지우고 다시 생성한다.

### 검증 도구

옛 커스텀 검증 도구(Debug · Integration · Campaign · Lavoisier Verification, Balance Sim)는 옛 5스탯 · 옛 로스터를
전제로 해 **2026-09 개편 때 모두 지웠다.** 지금은 F1 디버그 패널과 Play Mode 육안 확인이 전부다.
실행하지 않은 검증을 통과했다고 보고하지 않는다.

- 개발용 디버그 기능은 `Core/DebugMode.cs`, `Managers/UI/DevTools/DebugOverlay.cs`를 본다.
  디버그 조작은 `DebugMode.BeginSession()`으로 사용자 저장 쓰기를 격리한다.
- 생성된 `Assembly-CSharp.csproj`가 있으면 임시 복사본으로 `dotnet build` 컴파일 점검을 할 수 있다.
  새 소스의 `Compile Include` 누락과 로컬 Unity 참조를 확인하고, 출력은 임시 경로로 분리한다.
  이는 Unity Play Mode 검증을 대체하지 않는다. 생성된 프로젝트 파일을 직접 수정해 유지하지 않는다.
- Unity 에셋의 `.meta`는 에디터가 생성하도록 하고 기존 GUID를 보존한다.

## 구조와 런 진행

아래 경로는 별도 표시가 없으면 `Assets/Scripts/` 기준이다.
모든 매니저가 싱글턴 MonoBehaviour이거나 같은 수명을 가지는 것은 아니다.

| 구성 요소 | 책임 |
| --- | --- |
| `Managers/GameManager` | 상태 전환·전투 시작/정산·준비 행동·사건 흐름, 스케줄러 소유 |
| `Managers/RunManager` | 런 시작/복원/종료, 사건 이력과 저장 |
| `Managers/RoundManager` | 일반 C# 클래스. 스테이지·테마·적 편성과 배치 |
| `Managers/ActionScheduler` | 일반 C# 클래스. 행동 예약·우선순위·턴 진행 |
| `Managers/EventScheduler` | 일반 C# 클래스. 예약 사건과 체크포인트 |
| `Managers/GridManager` | Game 씬의 셀·유닛·필드/대기석, `heroList` / `enemyList` |
| `Managers/CharacterSelectionManager` | 1~5인 편성, 해금·중복 검사. 첫 자리가 대표(`FindLeadUnit`) |
| `Managers/RewardManager`, `InventoryManager` | 보상 카드(티어 → 분류 코드 55/소모품 30/드랍 15 → 태그 가중 추첨, 코드 1장 보장, 리롤)·상점·재화·리롤 티켓(시작 0)·공용 인벤토리·식량·비약·코드 도구 |
| `Managers/DataManager`, `SynergyCatalog` | YAML 로드 / 역할·추천 편성 조회 |
| `Managers/UIManager` | 화면 라우팅. UI 구현은 `Managers/UI/{Core,Theme,HUD,Screens,DevTools}` |
| `Managers/AudioManager` | BGM과 YAML 논리 SFX 큐, 소스 풀·배속 병합·코드 믹서 그룹·궁극기 더킹·하네스 로그. `SfxManager`는 씬 참조 때문에 남긴 빈 껍데기이고, 전투 연출은 `Effects/Vfx/`가 맡는다 |
| `Core/SaveSystem`, `SaveData`, `SettingsManager` | PlayerPrefs JSON 저장·DTO·설정 |

- 상태 정의는 `BaseClasses/Enums.cs`의 `BaseEnums.GameState`다.
  기본 전투 흐름은 `CharacterSelection → Preparation → RoundInProgress → RoundEnd → RewardSelection`이다.
  `EventStage`가 진행 중 끼어들고, `RunComplete` 또는 `GameOver`로 끝난다.
- 전환은 `NextGameState()`만으로 끝나지 않는다. 준비 화면·보상·사건 콜백과
  `EnterNextStageAfterLoad()`를 함께 확인한다. **스테이지 전진은 `RunManager.AdvanceToNextStage()`를 거친다.**
- 준비 페이즈에서는 **상점 · 휴식 · 공용 인벤토리 · 덱 구성**만 할 수 있다. 추가 전투는 없다.
  휴식은 짧은 휴식과 식량을 쓰는 긴 휴식으로 나뉘며 `GameManager`가 사용 여부와 짧은 휴식 횟수를 관리한다.
- 테마·보스·편성은 `RoundManager`와 `80_stages.yaml`을 확인한다. `enabled: false`인 테마는 회전에서 제외되며,
  `stagePatterns`가 개별 보스 ID보다 우선한다. 테마 치환·연작은 `rotationRedirectThemeId` / `chainNextThemeId`를 사용한다.
- 사용자 저장 가능 시점은 `RunManager.CanSaveNow`를 확인한다. 전투·보상 선택 중 상태를 무조건 저장하지 않는다.
  ID나 저장 구조의 호환성을 깨는 변경은 `RunSaveData.CurrentVersion`과 복원 코드를 함께 검토한다.
  현재 저장 버전은 **v13**이며 v12를 자동 이관한다 — `UnitSaveData.activeRewardCodeSlots`(빈 칸 포함 7칸)·`storedRewardCodeIds`·`grantedPassiveCodeIds`(활성·사건 패시브)·`permanentStacks`(영구 중첩, 소수 몫 포함),
  `RunSaveData.pendingTonicItemId`. 유닛 런 상태는 `Unit.WriteRunState` / `RestoreRunState` 한 쌍만 읽고 쓴다.

## 전장과 전투 규칙

### 배치·스탯

- 기본 전장은 `x = -2, -1, 1, 2`, `y = 1..4`이며 진영별 8칸이다.
  아군 전열/후열은 `-1/-2`, 적은 `1/2`다. 코드에서는 `GetFrontColumn(isEnemy)` / `GetRearColumn(isEnemy)`를 쓴다.
- 대기석은 별도 배열이며 현재 `GridManager.benchSize`와 `Game.unity` 값은 **5**다.
  초기 파티는 1~5명이다. 슬롯 수와 파티 선택 제한을 혼동하지 않는다.
- 전장 판정은 `Unit.IsOnField`를 사용한다. 소환수는 `Combat/Summons.cs`가 만든다 — 칸이 없고 `GridManager.summonList`에만 있으며,
  소환자의 고유 특성이 `ISummonerPolicy`(방식 `Swarm`/`Companion`·상한·`SummonSpec`·넘침 처리)를 구현해 `Summons.Register`로 건다.
  생성 경로는 `Summons.Create` 하나(`Summons.AnySummonCreated`), 소멸은 `Unit.Die`를 지나는 사망 판정이고 라운드 종료 정리는 사망이 아니다.
  고정 속도 소환수는 `ActionScheduler`에 참가하고 추월 제한 밖이며, 피해는 소환자 명의 + `DamageTag.SummonAttack`이다(`Detail_03 §4.7`).
- 스탯은 `BaseEnums.StatType`의 **여덟** — 최대 체력 · 공격력 · 특수공격력 · 방어력 · 특수방어력 · 속도 · 치명타 확률 · 치명타 피해.
  `Entities/UnitStats.cs`가 기초값 + 성장치 × (레벨 − 1)을 들고, 파생 스탯은 없다. 피해는 코드가 `Code.StatDamage`로
  (대부분 공격력 계수) 만들고, 경감 스탯은 `Combat.DamageMitigation`이 고른다(일반 → 방어력, 지속 · 반응 → 특수방어력,
  고정 피해 → 없음). 기준값 K는 **공격자 레벨**을 따른다. 규칙 원본은 `Detail_03` · `Detail_02 §3`.
- 스탯·상태가 바뀌면 최종 스탯도 갱신해야 한다. 내부 계산은 `Unit.AttributesUpdate()`,
  외부에서 동적 효과 변경을 알릴 때는 `RefreshAttributes()`를 사용한다. 재계산은 현재 HP 비율을 보존한다.

### 행동 스케줄러

- `ActionScheduler`가 이산적인 행동치(AV)로 순서를 정하고 한 번에 한 행동을 실행한다.
  현재 구현에는 속도 비율에 따른 추월 제한과 `AdvanceAction()`의 앞당김 보정도 있다.
  속도 변경의 결과를 단순한 실시간 공격 주기로 가정하지 않는다.
- 현재 우선순위는 `PriorityAdditional → Additional → Coordinated(미사용) → Special → Ultimate → Normal`이다.
  같은 유닛·종류·키의 행동을 중복 예약하지 않는다.
- 패시브·효과·이벤트 훅은 행동을 예약하거나 값을 바꾸는 트리거다.
  대체행동은 일반행동의 슬롯을 사용하며, 공격하지 않아도 `BaseNormalCode.NotifyActionResolved()`를 호출한다.
- `PriorityAdditional`과 `Additional`만 `IsAdditional()`에 포함된다.
  협동행동은 `OnCoordinatedActivates`, 특수행동은 `OnSpecialActivates`를 사용하므로 추가행동으로 세지 않는다.
  특수행동은 전 유닛 공용 체계가 아니라 `special` 슬롯을 가진 유닛만 특정 트리거로 예약한다.
  묶음은 `Combat.SpecialAction.OpenGate(opener, gateTag)`가 유닛 태그로 연다 — 로카팔라 묶음을 여는 것은 **인드라의 초월기뿐**이고
  `SpecialCode.OpenOrder` 순서는 바유 -100 → 쿠베라 0 → 인드라 +50 → 찬드라 +100이다. 행동 종료는 `ActionScheduler.AnyActionFinished`.
- 자동 시전 궁극기는 자원 충전 시 예약되며 턴을 소비하거나 일반행동 AV를 초기화하지 않는다.
  `UltimateCode.IsAutoCast`와 `ConsumesResourceOnResolve`를 따르는 특수 구현도 확인한다.
- 실제 행동 시작과 패시브 효과 발생은 `Combat.ActionAnnouncer`로 좌상단 행동서열의 맨 위 확장 슬롯에 끼워 넣는다.
  종류는 일반·대체·추가·협동·궁극기(초월기)·특수·패시브(특성)이며, 패시브는 조건 검사 때가 아니라
  자원 획득·상태 부여·후속 공격처럼 효과가 실제 적용된 지점에서만 호출한다. 슬롯은 실제 시간 1.5초를 유지하고 최대 3줄이다.
  별도 배너와 좌측 `I` 막대는 쓰지 않는다. 예약 삽입·행동치 변경·사망 제거 때 기존 슬롯을 재사용해 보간 이동하고,
  `AdvanceAction` 대상에는 작은 `↑` 표식을 붙인다.
- 일반행동·궁극기에 옛 스킬 쿨다운을 다시 도입하지 않는다. 지속시간·주기 발동·내부 발동 제한은
  `Unit.TurnCount`, `BaseEffect.OnOwnerTurn()`, `PeriodicTurnPassive`, `Combat.TurnCooldown` / `TargetTurnCooldown`을 쓴다.
  전투 판정에 `Time.time`이나 프레임 수를 쓰지 않는다. 마나는 행동(+20) · 피격(+5) · 처치(+10)로 차고 시간으로는 차지 않는다.
  집중(`Combat.Concentration`)은 시전자의 턴으로 세고 시전자가 제어 · 사망하면 끝난다.

### 코드

- 구현은 `Codes/{Base,Passive,Normal,Special,Ultimate}/`, 생성은 `CodeFactory`, 표시 데이터는
  `20_codes.yaml`과 `Codes/Base/CodeCatalog.cs`가 담당한다. 같은 숫자 ID라도 슬롯이 다르면 별개다.
- 유닛은 기본적으로 **고유 특성 · 일반행동 · 초월기** 셋을 가지며, 일부만 트리거형 **특수행동**을 추가로 가진다.
  해금 패시브 · 코드 용량은 없다. 대신 유닛마다 **보상 코드 7칸**(`Unit.RewardCodeSlotCount`)이 따로 있다 — 코드 ID 규칙:
  패시브 **500~999**가 보상 코드(T1 500번대 … T5 900번대, 최초 티어를 따르고 재밸런싱해도 ID는 안 바꾼다), **일반 500~**은 소환수의 일반행동
  (우치 분신 일격 N 500), 소환수 정의 ID는 900번대. 보상 코드는 준비 중 무료 드래그로 활성/보관·아군 이동·점유 칸 교환한다. 전수의 두루마리는 신규 보상에서 제외된 저장 호환 데이터다.
- 보상 코드의 규칙은 `Unit`에 있다: `CanReceiveRewardCode` / `TryGrantRewardCode`(가득 차면 보관, 상위를 받으면 활성 하위를 보관하고 같은 칸에서 교체,
  상위 보유 시 하위 거부)·`TryReplaceRewardCode`(승급의 인장)·`RemoveRewardCode`. 티어·태그·상위 사다리는 `20_codes.yaml`을 읽는
  `Codes/Base/RewardCodeCatalog.cs`가 답한다. 구현은 `Codes/Passive/Reward/` — 데이터 구동 30종(`RewardStatCode`: `flatStats`/`percentStats`/`damageTag`)과
  클래스 64종(`RewardCodesT2~T5.cs`, `RewardPassiveCode` 뼈대, `IRewardAuraSource`+`RewardAura.Best`로 "필드에 있으면 높은 하나만" 오라).
  스탯 보정은 `PassiveCode.PermanentStatFlatModifier`/`PermanentStatPercentModifier`(전투 밖 캐릭터 창에도 보임), 승리 정산은
  `OnBattleSettled`·`SelfExpBonus`·`PartyExpAuraBonus`·`PartyGoldAuraBonus`다. 보상·상점·인벤토리의 "누구에게/어느 코드" 선택은 `RewardCodeFlow`/`CodeChoicePicker`가 공유한다.
- **영구 중첩**은 런 내내 남는 유닛별 카운터다(아누비스의 영혼). 올리는 입구는 `Unit.GainPermanentStacks(key, amount)` 하나이고 `amount`는 소수일 수 있으며
  보유 코드가 `PermanentStackGainMultiplier`로 배율을 건다(성장 보상 코드). v12에서 도입됐고 현재 v13에서도 보존한다.
- 공격하는 모든 코드는 `Code.AttackForm`(피해 태그 50000번대)을 선언한다. `Combat/AttackForms.cs`가 보상 풀에 "파티에 그 형태 공격이 있는가"를 답한다.
- 일반행동은 `BaseNormalCode`(접촉이면 카드 돌진, 아니면 카드 정중앙에서 투사체), 초월기는 `BaseUltimateCode.Perform()`을 채운다.
- 장비·장착 슬롯·장비 숙련은 없다. 아이템은 파티 공용 인벤토리에만 들어가며 유닛 스탯을 바꾸지 않는다.

### 이벤트·피해·상태·원소

- `Unit.AddListener<T>()` / `Invoke<T>()`는 이벤트별로 같은 컨텍스트 타입을 사용해야 한다.
  튜플을 임의로 만들지 말고 `BaseClasses/Contexts.cs`와 실제 발행부를 확인한다.
  피해 전/중/후 이벤트는 `EventContext`, 공격자의 `OnDamageDealt`는 `DamageResolvedContext`를 사용한다.
- 피해 입구는 `Unit.TakeDamage(DamageContext)`다.
  `OnBeforeDamageTaken → OnTakingDamage → OnAfterDamageTaken` 중 기본 피해 처리에서
  경감·보호막·HP·사망·실제 피해량을 처리한다. 경감 전에 정적 이벤트 `Unit.AnyDamageIncoming`이 나간다. 개별 스킬에서 공통 피해 처리를 복제하지 않는다.
- 태그는 `BaseClasses/DamageTags.cs`를 따른다. 지속피해(`CodeType.Effect`)는 빈 태그 목록을 사용하므로
  접촉 공격 여부는 `ContactAttack`의 존재로 검사한다. **50000번대는 무기가 아니라 공격 형태**(`Slash` 베기 · `Pierce` 찌르기 · `Shot` 사격 ·
  `Strike` 타격 · `Spell` 주문)이고 공격 하나는 형태 하나다. **`SummonAttack`(20006)**은 소환수가 가한 피해 표지다.
  치명타는 `Code.RollCritical(target, out mult)`가 `BaseEffect.CritChanceBonusAgainst`/`CritDamageBonusAgainst`/`ForcesCriticalAgainst`/
  `CritOverflowToDamageRatio`를 모으고, 제어 지속은 `ControlDurationModifier`(최소 1턴), 시전자가 거는 상태의 지속은 `OutgoingStatusDurationModifier`가 보정한다.
  정적 이벤트에 `Unit.AnyHealed`·`AnyShieldGranted`·`AnyPermanentStackGained`가 있다.
- 현행 상태 모델은 `Entities/Status/UnitStatus` + `Entities/UnitStatusController` + `Effects/Base/BaseEffect`다.
  상태 식별·중첩은 `Key`와 `StatusStackPolicy`를 따른다. 상태 추가/제거는 `Unit.AddStatus()` 등의 API를 거친다.
- 효과는 필요한 질의 훅만 재정의하고 `Effects/Buffs/SharedModifierEffects.cs` 등 공통 효과를 우선 재사용한다.
  무한 지속 상태·구독·오라는 `StopCode()`, `OnRemove()`, 사망·라운드 종료에서 정리할 경로를 둔다.
- 원소 속성과 부착은 구분한다. 반응 구현은 `Effects/Negative/ElementalReactions.cs`,
  규격은 `Detail_16_Elements.md`를 확인한다. 전장 전체 효과는 `Combat/Battlefield.cs`가 관리하며 `UnitStatus`와 다르다.
- 옛 `SynergyManager`식 조합 수치 보너스는 없다. 현재 `30_synergies.yaml` / `SynergyCatalog`는
  역할·아키타입·추천 편성 데이터다. 실제 파티 연계는 코드와 전투 효과에서 구현한다.

## 데이터 파일과 콘텐츠 변경

데이터는 `Assets/Resources/Data/`, DTO는 `Assets/Scripts/Data/`에 있다.
`DataManager`가 YamlDotNet으로 로드하며, `20_codes.yaml`은 `CodeCatalog`가 별도로 읽는다.
파일 번호를 자동 로드 순서로 가정하지 말고 각 로더의 호출을 확인한다.

| 파일 | 내용 | DTO / 로더 |
| --- | --- | --- |
| `00_intro.yaml` | 인트로 | `IntroData.cs` |
| `10_units.yaml` | 아군 · 8스탯 기초값/성장치 · 코드 | `UnitData.cs` : `CombatantData` |
| `20_codes.yaml` | 슬롯별 코드 이름·설명. **패시브 500~999가 보상 코드 94종의 원본**(`tier`·`codeTags`·`supersededBy`·`flatStats`/`percentStats`/`damageTag`·`requiresAttackForm`·`requiresGrowth`·`shopFixed`) | `Codes/Base/CodeCatalog.cs`, `RewardCodeCatalog.cs` |
| `30_synergies.yaml` | 역할·추천 편성 | `SynergyData.cs` |
| `40_items.yaml` | 공용 아이템 — 식량(`Food`), 전투 비약(`Tonic` 5101~5106, 준비 화면에서 써서 다음 전투 1회), 코드 도구(`CodeTool`: 승급의 인장 5201~5204·전수의 두루마리 5211). `rewardPool: true`가 보상 풀 소모품 분류의 표지 | `ItemData.cs` |
| `50_tokens.yaml` | 토큰 | `TokenData.cs` |
| `60_enemies.yaml` | 일반 적·정예·보스·드랍 | `EnemyData.cs` : `CombatantData` |
| `70_rounds.yaml` | 라운드 종류·편성 폴백 | `RoundData.cs` |
| `80_stages.yaml` | 테마·스테이지 패턴·사건·보스 | `StageData.cs` |
| `90_rewards.yaml` | 티어 확률·공통 드랍·분류 비율(`rewardCategoryWeights`)·코드 태그/상위 가중치·즉시 소모품(치료·부활·사탕·운명의 주사위·경험의 서·휴식의 향로) | `RewardData.cs` |
| `95_sfx_cues.yaml` | 논리 SFX 큐·후보·음량/피치·동시 재생/간격·더킹 | `SfxCueData.cs` |
| `96_vfx.yaml` | 표준 VFX 부품·스킬 슬롯 조합·상태 부여 바인딩 | `VfxData.cs` / `Effects/Vfx/VfxDefinitionCatalog.cs` |

- `30_status.yaml`, `50_elements.yaml`, `60_elites.yaml`은 현행 데이터 파일이 아니다.
- 새 유닛은 데이터·코드 구현·`CodeFactory` 등록·표시 설명·아트·관련 상세 기획서를 함께 갱신한다.
  연출은 SFX 논리 키와 같은 이름으로 `96_vfx.yaml`의 `prepare/cast/hit/affected/screen` 표준 부품을 조합하며,
  새 스킬 전용 원화를 만들기 전에 색·크기·방향·시간 매개변수와 상태 분류 기본 부품을 재사용한다.
  일반/특성/궁극기의 논리 이름을 `SfxCueRoutes`와 `95_sfx_cues.yaml`에 추가하고 실제 쓰는 짧은 클립만
  `Resources/Audio/Sfx/`에 둔다. 원본 팩 전체나 긴 스킬 믹스를 저장소에 복사하지 않는다.
  ID 배정은 `Detail_03` / `Detail_11`의 규칙과 기존 팩토리 분기를 확인한다.
  적 코드에는 ID 차이를 enum 인덱스로 쓰는 범위 분기가 있으므로 중간 삽입·재번호 부여에 주의한다.
- 새 테마는 적 아트, `60_enemies.yaml`, `80_stages.yaml`의 `enemyThemeId`·패턴·사건·
  Resources 상대 `backgroundKey`(확장자 없음)·`ambientColor`를 연결한다.
- 새 아이템은 공용 인벤토리 기준으로 만들고, 긴 휴식용 식량이면 `foodValue`와 상점 가격을 함께 정한다.
  비약은 `category: Tonic` + `tonic*` 필드, 코드 도구는 `category: CodeTool`(+`sealMaxTier` 또는 `transferScroll`)이며 `GameManager.TryUseTonic`/`UseSeal`/`UseTransferScroll`로 쓴다.
- **새 보상 코드**: `20_codes.yaml` 패시브 500~999에 항목(티어 대역의 ID, `tier`·`codeTags`·`supersededBy`·필요하면 수치 필드)을 더한다. 스탯/태그 피해만이면
  `RewardStatCode`가 읽으므로 끝이고, 기믹이면 해당 티어 `RewardCodesT*.cs`에 `RewardPassiveCode` 클래스와 `CreateT*` 한 줄을 더한다(정적 이벤트 구독은 `OnStop`에서 해제).
  사다리는 하위 코드의 `supersededBy`(+필요하면 `RewardCodeCatalog.BronzeSealTargets`)로 잇는다. `Detail_17`·`Detail_11 §4`를 같이 고치고 하네스 `r12_core`로 확인한다.
- YAML 로더는 `IgnoreUnmatchedProperties()`를 사용한다. 오타 필드가 오류 없이 무시될 수 있으므로
  DTO 필드명과 실제 UI/플레이에서의 반영을 확인한다.

## 아트·UI 규칙

- 공허 계열에서 이름이 `공허의`로 시작하는 유닛은 `VOID_MONSTROUS_BIRD`를 시각 기준으로 삼는다.
  큰 결정형 면 분할, 얇고 절제된 외곽선, 백색·먹색·옅은 금색 골격과 원소색 결정 채색을 유지한다.
- 신규·수정 스프라이트는 스탠딩과 초상화를 기준 원화 옆에 놓고 선 처리·질감·채색의 통일성을 확인한다.
- 적의 접미사 없는 기본 스프라이트는 화면 왼쪽을 바라본다. 양방향이 필요하면 같은 키의 `_RIGHT` 변형을 함께 등록한다.
- 초상화·스탠딩은 `Assets/Resources/Sprite/{Portraits,Standings}/`의 기존 분류와
  `Helpers/SpriteResource.cs` 로드 규칙을 따른다.
- 전장과 기능 UI는 밝은 안 2를 쓴다. `BattlefieldBackdrop`은 하늘 성채·진주색 유리 바닥과 은청색 각인을,
  `UITheme`/`UIBuild`는 백색 라이트 글라스·`#253443` 본문·하늘색 선택선을 만든다. 원화 위 플로팅 띠만 스모크 글라스로 남긴다.
- 전투/기본 준비 프레이밍은 실제 점유 카드만 모으고 소수전 카드를 키운다. 덱 구성에서만 빈 2×4 칸을 펼치며,
  사망·등장 때 카메라는 `HoldCamera` 이후 목표 경계를 부드럽게 따라간다. 메인 메뉴도 같은 하늘 성채/라이트 글라스 언어를 쓴다.
- 전투 유닛 카드는 자유롭게 선 원화 + 컷코너 윤곽의 `Entities/View/UnitCardView`다. 우상단은 액체형 원소 에너지 오브와
  고유 자원 숫자 배지, 하단은 체력/보호막 · 행동 게이지 · 상태를 담은 리퀴드 글라스 캡슐이다. 생성 HUD PNG는
  `Resources/UI/CardHud/`에서 런타임 스프라이트로 읽는다. 밝은 안 2에서도 카드 패널은 `#26384A` 스모크 A안 한 장과
  흰 글자/글리프를 유지한다. 원화는 넓은 청회색 접지 그림자로 하늘/백색 판에서 분리하고, 확대 블러 실루엣은 스티커 프린지가 되므로 쓰지 않는다. 원화 하단은 색·질감 없는 절차형 알파 그라데이션으로만 패널에 녹인다. 움직임은 `Effects/Vfx/CardMotion`, 투사체 · 충격 · 등장 · 파괴는
  `ProjectileFx` · `ImpactFx` · `EntryFx` · `CardLifecycleFx`를 쓴다. 스킬 플립북은 `SkillFx`/`FlipbookFx`, 원화 침식은
  `ErosionFx`를 쓰며 피해 콜백은 투사체가 없으면 대상 `Hit`의 지정 프레임에 한 번만 실행한다. `VfxArt`는 `Resources/VfxTex/Gen/`의 생성 VFX 원화를,
  `HovlFx`는 `Resources/VfxPrefabs/Hovl/`의 Flash · Projectile · Hit 세트를 풀링하고 원본 이동/충돌을 끈 뒤
  `ProjectileFx`의 중심→중심 이동·단일 착탄 콜백에 결합한다. 등장·보호막·결정 흡수·집중 해제 같은 단발 연출도
  Hovl Flash/Hit을 주인공으로 쓰며 `PlayFlash`/`PlayHit`의 비가산 받침으로 밝은 판의 실루엣을 보존한다. 기본 카드 비율은 Flash 0.30 · Projectile 0.34 · Hit 0.40이고, 바위/공허 발사광은 메시형 `Dragon punch flash` 마법진, 비행·착탄은 18/17 nova를 쓴다. 밝은 판에서 점·세로선으로 풀리는 Sparks/Trail/Glow와 nova 루트 빌보드는 끄고 단발 nova Hit은 발화점 고정·단축 수명으로 바닥 잔류를 막는다. 나머지 기본 매핑은 불 16 · 물 9 · 번개 2 · 풀/바람 1 · 얼음 26 · 무원소 14다. 밝은 안 2에서는
  먼지·외곽·접촉 그림자는 황토/엄버/청회색 알파로 그리고, 좁은 코어·광맥만 가산한다. 규칙은 `Detail_02 §10`.
  구매한 Unity Asset Store `Effect Collection - Handpaint Spritesheet`는 원본 폴더를 수정하지 않고 실제 사용 시트만
  `Resources/VfxTex/Handpaint/`로 복사한다. `Presets/HandpaintFx`가 이름별 행×열·FPS·크기·회전·mirror·tint와
  몸체 알파/휘도 코어 합성을 맡고 저알파 셀 경계는 소프트 컷오프로 걷는다. `VfxStyleSettings`는 캐릭터 기본/Legacy/Handpaint를 F1과 하네스 `vfxStyle`에서
  전환하며 기본은 시·사바흐·세이·찬드라·히미코·피그말리온·바스테트 Handpaint이고 이전 연출은 Legacy로 보존한다.
  행동 중 `96_vfx.yaml`의 `screen.focus.*`는 시전자·현재 대상만 밝게 남기고 나머지를 한 체계로 톤다운한다. 일반/추가·특수/궁극기 강도를 구분하고 4× 이상에서는 강도·지속을 줄이며, 연속 요청은 이어 받아 깜빡임과 이중 암전을 막는다.
  모든 새 연출과 개수 연출은 `VfxSpace.AimFrom(caster, target)`의 시전자→대상 벡터로 회전·좌우 반전을 정한다.
  다중 대상은 대상별 시전을 반복하지 않고 시전자 카드에서 한 번 시전한 뒤 `BuildTargetHitSequence`/`ResolveTargetHits`로
  대상별 피격만 연다. 대상 사이를 잇는 칼날·선·관통 궤적은 만들지 않는다.
  검사·마법사·사수·짐승·적을 포함한 전원은 비행 투사체나 카드 돌진 없이 시전자 시전/사격광 + 대상 피격광으로 구성한다.
  짧은 제자리 반동·후퇴/부분 전진만 허용한다. 표준 부품과 스킬 조합은 `96_vfx.yaml`의
  `prepare/cast/hit/affected/screen` 슬롯으로 정의하고 `VfxDefinitionCatalog`/`VfxComposer`가 재생한다.
  내부 `vfxClass`/`classCastBindings`가 기본 시전을 고른다. caster는 `Fire3` 작은 불꽃을 카드 중앙과 적 방향 끝에,
  archer는 `Muzzle1` 총구광을 쓰고 saber/fighter/lancer/beast는 카드 전진, assassin은 고유 발도를 우선한다.
  상태 부여는 성공한 `UnitStatusController.Add` 뒤 `StatusAffectedVfx`가 명시 바인딩 또는 분류 기본 부품을 단발 재생한다. 모든 VFX 렌더러는
  `VfxSpace.ResolveSortingLayer`가 강제하는 `CombatVfx` 전용 정렬 레이어를 써 카드·이웃 카드·월드 HUD보다 앞에 두되
  화면 고정 상단 바·모달 UI보다 뒤에 둔다. 카드 경계를 벗어난 연출은 자르지 않는다.
  범용 지원 피격은 `hit.support.buff/cleanse/heal/shield` 네 부품을 쓴다. 강화는 머리 위의 `Impact1` 역재생, 보호막은 몸통의 은회색 `Ball1` 회전, 정화는 역재생 `Smoke5`, 치유는 녹백 `Impact1`이다. 강화의 현재 크기는 카드 기준 `0.96×0.84`이며, 요청된 머리 폭 약 75%까지 축소됐다고 보지 않는다. 끝점에서 하단 HUD로 빛점을 보내 상태 아이콘·HP·보호막 표시를 1.0→1.35→1.0으로 팝하고 링 플래시를 연다. 보호막은 은회색, 피그말리온 날카로운 가시는 장미색 고정이며 얼굴을 불투명하게 덮지 않는다.
  `Presets/RadiantVolleyFx`는 Legacy의 0.05초 간격 6발 곡선 연발·카드 안 여섯 지점 분산 착탄·세 번째 탄 정점 단일 콜백을 보관한다. `GeoFx`의 세이 기본 일반행동은 카드에서 적 방향으로 뻗는 금빛 시전광 한 번 뒤 대상 얼굴~상체의 백금 Impact 여섯 번만 연다. 빅뱅은 방향성 대형 시전 한 번 뒤 무작위 대상 위치의 일곱 파쇄와 실제 결과인 아군 보호막 `affected`만 쓰며 선택 후 0.242초 판정을 유지한다. 창세의 노래는 방향성 시전 한 번 뒤 생존 아군 전원의 머리 버프와 몸통 보호막을 동시에 열고 같은 순간 효과를 적용한다. 이전 결정 문장·후속 파편은 Legacy다. 과거 대상 발밑 결정창은
  `Effects/Vfx/Presets/RisingSpikeFx.Play(caster, target, onHit, scale)`로 보관하며 새 캐릭터가 한 줄로 재사용할 수 있다.
  `ChandraFx`의 기본 샤친은 카드에서 적 방향으로 뻗는 은백 시전광 한 번 뒤 대상 얼굴~상체의 Blue/White Impact 여섯 번을 열고 세 번째 정점에서만 판정한다. 소마는 역수렴·돔을 제거하고 방향성 시전 한 번 뒤 선정된 다른 아군에게만 버프 `affected`를 연다. 실제 코드에 보호막이 없으므로 보호막 연출도 없다. 창야는 방향성 시전 한 번 뒤 적 전체 월광 충격을 열고 실제 피해 정산 후 생존 아군 전원의 회복 `affected`를 연다. 달빛 순환도 회복 직후 작은 지원 피격을 쓴다. 옛 치명타 버프 연출은 제거한다. 대상 사이 궤적은 없다. 이전 생성·역수렴·돔은 Legacy다.
  `ShiFx`의 멸참은 `AimFrom`으로 시 카드 위 `Slash1_Blue`를 적 방향으로 긋고 대상 위 팩 화풍 `hit.claw.triple`의 검정·짙은 적색 직선 붓획 세 줄을 시간차 와이프로 연다. 시 전용 `96_vfx.yaml` 정의만 검정 몸체·블러드 레드 외곽·크림슨 코어로 덮어쓰며 공용 부품은 바꾸지 않는다. 작은 Impact/Smoke만 받침으로 쓰며 정점에 한 번만 판정한다. 3/6/9타 만종은 멸참 피격이 끝난 즉시 별도 시전 없이 `Smoke3` 수평 연기 한 장으로 잇는다. 3타는 같은 대상의 작은 연기, 6·9타는 생존 적 진영 중앙에서 한 장이 좌우로 퍼지며 닿는 순서대로 분배 피해를 판정한다. 9타도 크기는 같고 색 대비·코어·화면 리듬만 강화한다. 처치 재행동은 사망 퇴장과 진형 재배치 완료 뒤 시전한다. 옛 만종 버프진·진 귀참은 현행에서 제거하고, 이전 `DrawSlashFx`+`ClawRakeFx` 검붉은 할퀴기는 Legacy로 보존하며 청록 비행 참격은
  `Effects/Vfx/Presets/FlyingSlashFx.Play(caster, target, onHit, scale)`로 보관한다.
  `SabahFx`의 기본 일반행동은 `AimFrom` 방향의 제자리 `Slash1_Purple` 뒤 대상의 방향성 `Lightning2_Purple`+`Impact1_Purple`로
  이어지고 기존 약 0.64초 정점에 대상별 한 번 판정한다. 세 번째 번개 베기는 시전자 카드의 큰 검격 한 번 뒤 최대 3명 위에
  0.09초 간격으로 전격과 판정을 열며, 중간 칼날이나 관통 궤적은 없다. 이전 침식·그림자 이동·할퀴기는 `Presets/ShadowAmbushFx`와 Legacy로
  보존한다. 아즈라엘은 소멸·가는 영혼선을 쓰지 않고 사바흐 카드 위에서 보라색 `Slash5_Blue` 발도를 정확히 한 번 연 뒤 적 진영 중앙에 적당히 큰 보라 벼락 하나, 중앙에서 좌우로 퍼지는 `Smoke3` 수평 연기를 잇는다. 연기가 각 적에 닿을 때 대상별 1회 판정을 열고 공유 치명은 유지한다.
  히미코 일반행동은 `EmberFx`가 부채형 Red 시전광 한 번과 대상 얼굴~상체 중심의 Red Explosion 피격 한 번만 재생한다. 공격으로 맹화가 붙을 때 표식 부여광은 없고, 맹화 3중첩 폭발 때만 표식 위치에서 `Smoke` 한 장이 펑 하고 터진다. 기존 상태·고정 피해 호출 순서는 유지한다. 피그말리온은 발도 없이 짧게 움찔한 뒤
  `Fire3` 시전 뒤 대상 얼굴~상체에 `Explosion2_Red` 화염 피격을 한 번 연다. 적갈·주황 알파 몸체와 노랑~백색 가산 코어를 쓰며
  시전·피격을 `AimFrom`의 대상 방향에 맞추고 단일 판정을 유지한다. 두 캐릭터의 이전 생성 연발/`ClawRakeFx` 경로는 Legacy다.
  바스테트는 내부 `beast` 분류다. 냥냥펀치와 U122 추가행동은 카드 움직임·`hit.claw.triple`·SFX까지 완전히 같은 일반행동을 재생하고 행동서열 종류만 추가행동으로 남긴다. P322 경호는 적 시전 절반에서 정지·포커스 해제·패시브 행동서열 표시를 거친 뒤 경호자가 가로막고, 포커스 복귀 후 최종 경호자에게 원 공격자의 병과·색 피격을 그대로 재생한다.
  `JeanFx`의 잔은 내부 `fighter` 분류다. 신성한 일격은 짧은 카드 전진이 시전을 대신하고, 대상 초상화에 세 번의 진한 남청 `FireAttack` 주먹 잔상·백철색 충격 뒤 같은 충격점에서 뒤로 밀려 퍼지는 청회색 `Smoke3`를 하나의 피격 단계로 연다. 철권제재는 적 시전 절반에서 정지·포커스 해제·패시브 행동서열 표시 뒤 더 빠르고 짧게 재생하며, 공격자가 생존하면 포커스 복귀 후 적 공격을 잇는다. 텐션 오르는데!는 잔 주위 카드 폭 이상 반경의 파랑·은백 `Slash5` 다섯 갈래 시전 하나만 쓴다.
  `EnemyNormalFx`와 U1001~U1004도 `96_vfx.yaml` 조합을 쓴다. 공허 늑대 강타는 같은 3갈래의 굵은 외곽·긴 잔광 강화판, 공허 사수는 진한 외곽의 적색 `Muzzle1`과 하단 중앙 앵커의 수평 `Smoke3`를 쓰며 연사는 짧은 정점 유지 변형으로 시전·피격·SFX를 같은 리듬으로 3회 연다. 공허 선봉은 승인된 얼굴~상체 높이의 확대 `Slash4`, 공허 분쇄자는 `incomingImpact`와 `impactAnchor`를 쓴다. 공허룡은 보스 배율 `Fire3` 법사 시전과 암자주 몸체·백색 코어의 `Explosion1` 피격을 일반·궁극기에 공용한다. 대지 분쇄 기절과 U1004 5회 판정 계약은 유지한다.
- UI 수정은 `UITheme`, `UIBuild`, `CodeText`, 공통 툴팁 등 기존 도우미를 재사용하고 실제 화면에서 확인한다.


### 신규 VFX 모듈 (2026-10-05)

| 모듈 (`Effects/Vfx/`) | 역할 |
| --- | --- |
| `LokapalaFx` | 인드라·바유·쿠베라의 1회 시전→동시 초상화 피격, 금강저/여덟 방향 문양/금화/보고 문, 묶음 HUD 펄스. 피해·상태·자원은 코드의 정점 콜백 한 번만 소유하며 난수·예약 순서를 바꾸지 않는다 |
| `AnubisFx` | 심장의 저울의 짧은 부분 전진과 금·청록 초상화 충격, 사자의 서 자기 강화 문양, 영혼 불꽃·정수/소수 배지 펄스. 기존 피해·회복·영구 중첩 수식과 이벤트 순서를 유지한다 |
| `UchiFx` / `Entities/View/SummonCardView` | 칸 없는 분신의 반투명 카드 세 장을 소환자 모션에 붙인다. 생성·일격·넘침·막기·동시 전진·부적 소멸을 표시하며 셀·배치·카메라에는 참가하지 않는다 |

### UI/코드 보관 2026-10-06

전 화면은 밝은 문서 면·남색 제목/주 행동·하늘색 선택선·사선 모서리로 통일한다. `UIArtDirection.Header`·기존 테마/공통 패널을 재사용한다.
보관 코드는 스탯·상태·오라·정산을 내지 않는다. 무료 드래그는 `Combat.RewardCodeInventory`가 양쪽 결과를 먼저 검증한다.
`ui_overhaul`·`ui_overhaul_menu`는 실제 Unity 검증 경로이며 최신 기록은 `Logs/UIOverhaul20261006/final-review.md`다.
