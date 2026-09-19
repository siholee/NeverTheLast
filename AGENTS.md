# AGENTS.md

이 저장소에서 작업하는 Codex를 위한 지침이다.
최종 갱신: **2026-09-18** — 현재 작업 트리의 코드·YAML·씬 설정 기준.

## 프로젝트 개요

NeverTheLast는 **Unity 6000.4.9f1 / C#**으로 만드는 신화 기반 로그라이트 RPG다.
메인 캐릭터와 서포터로 파티를 구성하고, 문명별 테마를 진행하며 메인 캐릭터를 육성한다.
전투는 **턴제 자동 전투**이며, 플레이어는 전투 전에 편성·배치·장비·훈련·스킬을 결정한다.
게임 모드는 `Training`과 `Infinite`이며, 육성 완주 기록과 서포트 카드는 이후 플레이에 이어진다.

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
| `Detail_02_Combat.md` | 전장·행동치·피해·보호막·상태·이벤트 |
| `Detail_03_Character.md` | 스탯·코드·패시브 해금·ID 규칙 |
| `Detail_04_Training.md` | 훈련·우정도·힌트·전수·계승 |
| `Detail_05_Economy.md`, `Detail_06_Events.md` | 재화·보상·장비·사건 |
| `Detail_07_UI_Tech.md` | UI·매니저·저장·검증 도구 |
| `Detail_08_Confirmed_Characters.md` | 캐릭터별 전투 사양 |
| `Detail_09_Enemy_Catalog.md`, `Detail_10_Boss_Catalog.md` | 적 편성·보스 |
| `Detail_11_Code_Catalog.md`, `Detail_12_Code_Weapon_Catalog.md` | 코드 효과 색인·슬롯·숙련 |
| `Detail_13_Reward_Catalog.md`, `Detail_14_Equipment_Catalog.md` | 보상 풀·장비 목록 |
| `Detail_15_Party_Synergy.md`, `Detail_16_Elements.md` | 추천 편성·원소 반응·전장 상태 |
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

독립적인 테스트 프로젝트·린터 대신 **실제 게임을 실행하는 커스텀 검증 도구**가 있다.
변경 범위에 맞는 도구를 선택하고, 실행하지 않은 검증을 통과했다고 보고하지 않는다.

| `Tools/NeverTheLast/` 메뉴 | 용도 |
| --- | --- |
| `Run Debug Verification` | 기본 시스템 검증 (`Ctrl+F6`) |
| `Run Integration Verification` | 통합 검증 (`Ctrl+F7`) |
| `Run Campaign Verification` | 캠페인 진행 검증 (`Ctrl+F8`) |
| `Run Lavoisier Verification` | 라부아지에 전용 검증 |
| `Run Balance Sim (5 runs)` | 자동 플레이 밸런스 시뮬레이션 |

- 구현: `Managers/UI/DevTools/DebugVerification*.cs`, `BalanceSim.cs`와 `Tools/Editor/`의 메뉴 스크립트.
  검증 보고서는 `Logs/`, 밸런스 시뮬레이션 결과는 `Logs/BalanceSim/`에 기록된다.
- 시뮬레이션의 배치 진입점은 `BalanceSimMenu.RunBatch`다. `-simRuns`, `-simStage`, `-simSeed`,
  `-simScale`, `-simLineup`, `-simFront`를 지원한다. Play Mode에서 비동기로 진행한 뒤 스스로 종료하므로
  일반 빌드의 `-quit` 옵션을 그대로 붙이지 않는다. 실행 시 이전 시뮬레이션 결과가 삭제된다.
- 개발용 디버그 기능은 `Core/DebugMode.cs`, `Managers/UI/DevTools/DebugOverlay.cs`를 본다.
  검증·시뮬레이션은 `DebugMode.BeginSession()`으로 사용자 저장 쓰기를 격리한다.
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
| `Managers/RunManager` | 런 시작/복원/종료, 육성·우정도·힌트·강화제·사건 이력과 저장 |
| `Managers/RoundManager` | 일반 C# 클래스. 스테이지·테마·적 편성과 배치 |
| `Managers/ActionScheduler` | 일반 C# 클래스. 행동 예약·우선순위·턴 진행 |
| `Managers/EventScheduler` | 일반 C# 클래스. 예약 사건과 체크포인트 |
| `Managers/GridManager` | Game 씬의 셀·유닛·필드/대기석, `heroList` / `enemyList` |
| `Managers/TrainingManager` | 정적 클래스. 훈련·서포트 배치·우정도·스킬 힌트/전수 |
| `Managers/CharacterSelectionManager` | 메인·서포터 선택, 중복·시작 자격 검사 |
| `Managers/RewardManager`, `InventoryManager` | 보상·상점·재화·장비 |
| `Managers/DataManager`, `SynergyCatalog` | YAML 로드 / 역할·추천 편성 조회 |
| `Managers/UIManager` | 화면 라우팅. UI 구현은 `Managers/UI/{Core,Theme,HUD,Screens,DevTools}` |
| `Managers/AudioManager`, `SfxManager` | 오디오 / 전투 연출 |
| `Core/SaveSystem`, `SaveData`, `SettingsManager` | PlayerPrefs JSON 저장·DTO·설정 |

- 상태 정의는 `BaseClasses/Enums.cs`의 `BaseEnums.GameState`다.
  기본 전투 흐름은 `CharacterSelection → Preparation → RoundInProgress → RoundEnd → RewardSelection`이다.
  `EventStage`, `TrainingPhase`가 진행 중 끼어들고, `RunComplete` 또는 `GameOver`로 끝난다.
- 전환은 `NextGameState()`만으로 끝나지 않는다. 준비 행동·보상·사건 콜백과
  `EnterNextStageAfterLoad()`를 함께 확인한다. **스테이지 전진은 `RunManager.AdvanceToNextStage()`를 거친다.**
- 훈련은 준비 페이즈의 행동으로 진입할 수 있다. 스테이지가 사건으로 대체되거나 보스 준비 행동이 제한되는 경로도 있다.
- 테마·보스·편성은 `RoundManager`와 `80_stages.yaml`을 확인한다. `enabled: false`인 테마는 회전에서 제외되며,
  `stagePatterns`가 개별 보스 ID보다 우선한다. 테마 치환·연작은 `rotationRedirectThemeId` / `chainNextThemeId`를 사용한다.
- 사용자 저장 가능 시점은 `RunManager.CanSaveNow`를 확인한다. 전투·보상 선택 중 상태를 무조건 저장하지 않는다.
  ID나 저장 구조의 호환성을 깨는 변경은 `RunSaveData.CurrentVersion`과 복원 코드를 함께 검토한다.

## 전장과 전투 규칙

### 배치·스탯

- 기본 전장은 `x = -2, -1, 1, 2`, `y = 1..4`이며 진영별 8칸이다.
  아군 전열/후열은 `-1/-2`, 적은 `1/2`다. 코드에서는 `GetFrontColumn(isEnemy)` / `GetRearColumn(isEnemy)`를 쓴다.
- 대기석은 별도 배열이며 현재 `GridManager.benchSize`와 `Game.unity` 값은 **5**다.
  초기 파티는 메인 1명을 포함해 최대 5명이다. 슬롯 수와 파티 선택 제한을 혼동하지 않는다.
- 소환수는 `currentCell`이 없을 수 있다. 전장 판정은 `Unit.IsOnField`, 소환 규칙은 `Combat/Summons.cs`,
  `SummonSpec.cs`를 사용한다.
- 스탯 원본은 `Entities/UnitStats.cs`의 **STR·DEX·CON·INT·LUK**다.
  별도 성장 공격력/방어력 필드를 추가하지 않는다. 피해는 코드 위력과 주스탯, 체력은 CON,
  방어·중량 한도는 STR, 행동 속도는 DEX, 마나 효율은 INT, 치명타는 LUK에서 파생된다.
- 스탯·장비·상태가 바뀌면 파생 스탯도 갱신해야 한다. 내부 계산은 `Unit.AttributesUpdate()`,
  외부에서 동적 효과 변경을 알릴 때는 `RefreshAttributes()`를 사용한다. 재계산은 현재 HP 비율을 보존한다.

### 행동 스케줄러

- `ActionScheduler`가 이산적인 행동치(AV)로 순서를 정하고 한 번에 한 행동을 실행한다.
  현재 구현에는 속도 비율에 따른 추월 제한과 `AdvanceAction()`의 앞당김 보정도 있다.
  DEX 변경의 결과를 단순한 실시간 공격 주기로 가정하지 않는다.
- 현재 우선순위는 `PriorityAdditional → Special → Additional → Coordinated → Ultimate → Normal`이다.
  같은 유닛·종류·키의 행동을 중복 예약하지 않는다.
- 패시브·효과·이벤트 훅은 행동을 예약하거나 값을 바꾸는 트리거다.
  대체행동은 일반행동의 슬롯을 사용하며, 공격하지 않아도 `BaseNormalCode.NotifyActionResolved()`를 호출한다.
- `PriorityAdditional`과 `Additional`만 `IsAdditional()`에 포함된다.
  특수행동과 협동행동은 각각 `OnSpecialActivates`, `OnCoordinatedActivates`를 사용하므로 추가행동으로 세지 않는다.
- 특수행동은 인드라 궁극기가 로카팔라에게 여는 경로다. `Combat.SpecialAction.OpenGate()`를 사용한다.
- 자동 시전 궁극기는 자원 충전 시 예약되며 턴을 소비하거나 일반행동 AV를 초기화하지 않는다.
  `UltimateCode.IsAutoCast`와 `ConsumesResourceOnResolve`를 따르는 특수 구현도 확인한다.
- 일반행동·궁극기에 옛 스킬 쿨다운을 다시 도입하지 않는다. 지속시간·주기 발동·내부 발동 제한은
  `Unit.TurnCount`, `BaseEffect.OnOwnerTurn()`, `PeriodicTurnPassive`, `Combat.TurnCooldown` / `TargetTurnCooldown`을 쓴다.
  전투 판정에 `Time.time`이나 프레임 수를 쓰지 않는다. AV의 초 환산은 궁극기 자원 충전에 사용하며,
  벽시계는 연출·대기 감시·준비 타이머 등에 쓰인다.

### 코드·해금·육성

- 구현은 `Codes/{Base,Passive,Normal,Ultimate,Special}/`, 생성은 `CodeFactory`, 표시 데이터는
  `20_codes.yaml`과 `Codes/Base/CodeCatalog.cs`가 담당한다. 같은 숫자 ID라도 슬롯이 다르면 별개다.
- 기본 슬롯은 고유 패시브·일반행동·궁극기다. `levelPassives`는 조건을 만족한 만큼 배운다.
  **패시브 3개 제한이나 INT 기반 코드 용량 제한은 없다.**
- 현재 `PassiveUnlockLevel => Level`이다. `Level + TrainingLevel`로 계산하지 않는다.
  레벨 변경 시 `RefreshLevelPassives()`가 해금·해제를 처리하며, 먼저 배운 코드를 중복 등록하지 않는다.
- `UniquePassiveCode`는 전수 불가다. 은색 `Normal` / 금색 `Enhanced`의 대체 관계는
  `SupersededByCodeId`와 `Unit.TryCastPassiveCode()`로 처리한다. 보라색 `Unique`는 그 관계 밖에 있다.
- 육성 상태는 `RunManager.Training`, `SupportBonds`, `SkillHints`에 둔다.
  훈련은 서포트 배치·우정도·체력·컨디션·실패·스킬 Pt를 반영한다.
  일부 훈련은 `TrainingManager.SecondaryShares()`에 따라 부 스탯도 올린다.
  힌트와 스킬 Pt를 통한 습득, 완주 후 서포트 카드 계승을 함께 고려한다.
- 장비 효과 적용은 `Unit.CanUseEquipmentEffects()`로 숙련을 검사한다. 숙련이 없으면 중량만 적용된다.

### 이벤트·피해·상태·원소

- `Unit.AddListener<T>()` / `Invoke<T>()`는 이벤트별로 같은 컨텍스트 타입을 사용해야 한다.
  튜플을 임의로 만들지 말고 `BaseClasses/Contexts.cs`와 실제 발행부를 확인한다.
  피해 전/중/후 이벤트는 `EventContext`, 공격자의 `OnDamageDealt`는 `DamageResolvedContext`를 사용한다.
- 피해 입구는 `Unit.TakeDamage(DamageContext)`다.
  `OnBeforeDamageTaken → OnTakingDamage → OnAfterDamageTaken` 중 기본 피해 처리에서
  무효화·경감·보호막·HP·사망·실제 피해량을 처리한다. 개별 스킬에서 공통 피해 처리를 복제하지 않는다.
- 태그는 `BaseClasses/DamageTags.cs`를 따른다. 지속피해(`CodeType.Effect`)는 빈 태그 목록을 사용하므로
  접촉 공격 여부는 `ContactAttack`의 존재로 검사한다.
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
| `10_units.yaml` | 아군·기본 스탯·코드·해금·숙련 | `UnitData.cs` |
| `20_codes.yaml` | 슬롯별 코드 이름·설명 | `Codes/Base/CodeCatalog.cs` |
| `30_synergies.yaml` | 역할·추천 편성 | `SynergyData.cs` |
| `40_items.yaml` | 장비 | `ItemData.cs` |
| `50_tokens.yaml` | 토큰 | `TokenData.cs` |
| `60_enemies.yaml` | 일반 적·정예·보스·드랍 | `EnemyData.cs` |
| `70_rounds.yaml` | 라운드 종류·편성 폴백 | `RoundData.cs` |
| `80_stages.yaml` | 테마·스테이지 패턴·사건·보스 | `StageData.cs` |
| `90_rewards.yaml` | 티어 확률·공통 드랍·소모품 | `RewardData.cs` |

- `30_status.yaml`, `50_elements.yaml`, `60_elites.yaml`은 현행 데이터 파일이 아니다.
- 새 유닛은 데이터·코드 구현·`CodeFactory` 등록·표시 설명·아트·관련 상세 기획서를 함께 갱신한다.
  ID 배정은 `Detail_03` / `Detail_11`의 규칙과 기존 팩토리 분기를 확인한다.
  적 코드에는 ID 차이를 enum 인덱스로 쓰는 범위 분기가 있으므로 중간 삽입·재번호 부여에 주의한다.
- 새 테마는 적 아트, `60_enemies.yaml`, `80_stages.yaml`의 `enemyThemeId`·패턴·사건·배경색을 연결한다.
- 새 장비는 `Detail_14`의 티어·중량·숙련·부여 코드 규칙을 따른다.
- YAML 로더는 `IgnoreUnmatchedProperties()`를 사용한다. 오타 필드가 오류 없이 무시될 수 있으므로
  DTO 필드명과 실제 UI/플레이에서의 반영을 확인한다.

## 아트·UI 규칙

- 공허 계열에서 이름이 `공허의`로 시작하는 유닛은 `VOID_MONSTROUS_BIRD`를 시각 기준으로 삼는다.
  큰 결정형 면 분할, 얇고 절제된 외곽선, 백색·먹색·옅은 금색 골격과 원소색 결정 채색을 유지한다.
- 신규·수정 스프라이트는 스탠딩과 초상화를 기준 원화 옆에 놓고 선 처리·질감·채색의 통일성을 확인한다.
- 적의 접미사 없는 기본 스프라이트는 화면 왼쪽을 바라본다. 양방향이 필요하면 같은 키의 `_RIGHT` 변형을 함께 등록한다.
- 초상화·스탠딩은 `Assets/Resources/Sprite/{Portraits,Standings}/`의 기존 분류와
  `Helpers/SpriteResource.cs` 로드 규칙을 따른다. 페이퍼돌은 `Assets/Docs/Character_PaperDoll_Spec.md`를 본다.
- UI 수정은 `UITheme`, `UIBuild`, `CodeText`, 공통 툴팁 등 기존 도우미를 재사용하고 실제 화면에서 확인한다.
