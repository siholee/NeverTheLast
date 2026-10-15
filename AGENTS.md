# AGENTS.md

NeverTheLast 저장소의 Codex 작업 지침. 최종 갱신: **2026-10-15**.

## 프로젝트와 브랜치

Unity/C# 신화 기반 서브컬처 로그라이트 자동전투 RPG다. 엔진은 `ProjectSettings/ProjectVersion.txt`가 기준이다.
해금 캐릭터로 파티를 만들고 배치·코드·상점·휴식을 결정한 뒤 턴제 자동 전투를 진행한다. 무한 모드 하나다.

- **main**: 현행 Rework 개발 기준. 이전 `rework/core-stats`의 작업을 이어간다.
- **legacy**: 개편 전 main의 보관본. 새 구현·수치 조정·문서 갱신을 하지 않는다.
- 과거 커밋·release 브랜치/태그는 이력이다. 작업 채팅의 옛 detached main 작업트리를 현행으로 오인하지 않는다.
- 현재 채팅과 실제 Unity 프로젝트 경로가 다르면 `Application.dataPath`·Git branch/status로 실제 프로젝트를 확인한다.
- 작업 언어는 한국어다. 코드·주석·로그는 주변 표현과 밀도에 맞춘다.
- 사용자 변경·저장·인덱스·브랜치·구매팩·음성·기존 GUID를 보존한다. 요청과 무관한 변경을 되돌리지 않는다.

## 설계 문서

유일한 입구는 [Assets/Docs/Design/README.md](Assets/Docs/Design/README.md)다.
규칙을 바꾸기 전 해당 상세 문서를 읽고 코드·데이터·문서를 함께 맞춘다. 수치와 콘텐츠 개수를 이 지침에 중복 관리하지 않는다.

- `GDD_Main`·`GDD_Sub_Concepts`: 방향·용어.
- `Detail_01/02/03`: 진행·전투·스탯/자원/소환.
- `Detail_05/06/07`: 경제·사건·UI/기술/저장/검증.
- `Detail_08`·`Design/Characters/`: 현재 로스터·캐릭터별 상세 사양.
- `Detail_09/10/11/12/13`: 적·보스·고유 코드·슬롯·보상.
- `Detail_15/16/17/18`: 편성·원소/지속피해·보상 코드·재보.
- `Assets/Docs/Narrative/`: 확정 서사와 집필 제안. 원고 채택과 런타임 구현을 구분한다.
- `Assets/Docs/Proposals/`: 미구현 제안. `Design_Backlog`는 남은 결정·실측·검수를 관리한다.
- `Docs/Archive/`와 legacy는 과거 문서다. 현재 규칙의 근거로 사용하지 않는다.

현재 동작은 **실행 코드·YAML·씬의 직렬화 값**으로 확인한다. 문서 불일치를 이유로 규칙을 임의 확정하지 않는다.
미구현은 `🔸`, 구현 후 검증이 남은 것은 `🔸 검증 필요`로 구분하고 수정일을 남긴다.
작업 일지·AI 검토·검수 회차를 현행 사양에 누적하지 않는다. 결과/촬영/manifest는 `Logs/`에 둔다.

## 개발·빌드·검증

시작 씬은 `Assets/Scenes/MainMenu.unity → Game.unity`; 실제 빌드 씬은 EditorBuildSettings를 확인한다.
기본 컴파일/실행 검증은 Unity Editor Play Mode와 Console이다.

Windows/Mac 빌드는 `Assets/Scripts/Tools/Editor/DemoBuild.cs`의 `DemoBuild.BuildWindows` / `BuildMac`.
메뉴는 `Tools/NeverTheLast/Build Windows Demo` / `Build Mac Demo`다. 해당 Build Support가 필요하다.

```text
Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod DemoBuild.BuildWindows -logFile Logs/DemoBuild.log
```

해당 출력 폴더를 지우고 재생성하는 스크립트이므로 경로를 확인한다. 빌드·로그·녹화본은 소스로 커밋하지 않는다.

현행 커스텀 검증은 `Assets/Scripts/Managers/UI/DevTools/VerifyHarnessRunner.cs`와 시나리오별 partial 파일이다.
실제 Play Mode의 기능·경계·저장·진행·입력·연출·성능을 변화 범위에 맞춰 실행한다. 옛 5스탯 검증/BalanceSim 메뉴는 없다.
`DebugMode.BeginSession()`으로 사용자 저장 쓰기를 격리한다. 별도 QA Player productName은 빌드 후 원래 설정 바이트로 복원한다.
임시 csproj 컴파일은 보조이며 Unity 실행을 대신하지 않는다. 생성 csproj를 유지보수 대상으로 바꾸지 않는다.
실행하지 않은 검사를 합격으로 보고하지 않는다. 부분 기능/정지 화면 PASS를 전체 QA·상용 미술·실기기 성능으로 확대하지 않는다.
전체 검수 재개 시 `Design_Backlog`와 최신 `Logs/WholeGameReview20261015/`의 상태·coverage·소스 근거를 읽는다.

## 공통 구현 규칙

경로는 `Assets/Scripts/` 기준이다. 모든 Manager가 MonoBehaviour/싱글턴인 것은 아니다.

- 진행은 `RunManager.AdvanceToNextStage()`를 통한다. RoundManager 내부 Stage/표시 라운드와 Round/표시 테마를 혼동하지 않는다.
- 배치/필드는 `Unit.IsOnField`·GridManager의 전후열 헬퍼를 사용한다. 칸 없는 소환수는 `Combat/Summons`·summonList 경로를 따른다.
- 스탯은 `UnitStats`의 8스탯과 선형 성장이다. STR/DEX 등 옛 파생 스탯·장비 중량·훈련을 다시 도입하지 않는다.
- 스탯/상태/레벨 변경은 `AttributesUpdate` 또는 `RefreshAttributes`를 사용하며 HP 비율 보존을 확인한다.
- 행동은 ActionScheduler의 AV·우선순위·정확한 행동ID를 사용한다. 턴 규칙에 Time.time/프레임을 쓰지 않는다.
- 일반행동 대체/비공격도 실제 완료에 `NotifyActionResolved()`를 호출한다. 취소와 정상 완료를 구분한다.
- Additional/PriorityAdditional만 추가행동이다. Special은 별도다. 로카팔라 묶음은 인드라 초월기의 `SpecialAction.OpenGate`가 연다.
- 초월기는 자원형/상시형을 구분하며 일반 AV를 임의 초기화하지 않는다. 집중은 `Combat.Concentration`으로 관리한다.
- 직접 공격 대상 교체는 `AttackTargetRouter.Prepare`·`ResolvePreAttack`을 거쳐 개입 후 생존/제어를 다시 확인한다.
- 피해는 `Unit.TakeDamage(DamageContext)`를 통한다. 공통 경감·보호막·사망·실제 피해 처리를 개별 코드에 복제하지 않는다.
- 이벤트 컨텍스트는 `BaseClasses/Contexts.cs`의 발행 타입과 일치시킨다. 임의 튜플로 구독하지 않는다.
- 상태는 UnitStatus/UnitStatusController/BaseEffect이며 AddStatus 등의 API를 사용한다. 기존 SharedModifierEffects를 우선 재사용한다.
- 적대 원소 부착·디버프는 공통 효과 명중/저항을 거친다. 성공한 반응 파생 상태를 중복 재판정하지 않는다.
- 지속피해의 발동과 수명은 별개다. 야마의 즉시 발동은 원 효과/기간을 소모하지 않는다.
- 강인도 격파는 가상 처치다. 실제 사망·골드/EXP/드랍·영혼/축전기 성장 이벤트를 발행하지 않는다.
- 재보는 아군 파티 공용 자원이다. 가용/예약/실제 소비·환불·탄별 판정을 PartyTreasury 경로로 처리한다.
- 소환·오라·무한 상태·정적 이벤트는 StopCode/OnRemove/사망/라운드 종료의 구독·자원 정리를 확인한다.
- 역할/추천 데이터는 SynergyCatalog이며 옛 SynergyManager식 자동 조합 수치 보너스가 아니다.

## 데이터·콘텐츠 추가

`Assets/Resources/Data/`와 `Assets/Scripts/Data/`의 DTO/로더를 함께 확인한다.
00 인트로, 10 아군, 20 코드, 30 역할/편성, 40 아이템, 50 토큰, 60 적, 70 폴백 라운드, 80 테마/사건, 90 보상,
95 SFX 큐, 96 VFX 부품/조합이 현행 파일이다. `30_status`·옛 `40_synergies` 등 없는 파일을 안내하지 않는다.
IgnoreUnmatchedProperties 때문에 오타가 조용히 무시될 수 있으므로 DTO와 실제 반영을 검증한다.

새 유닛은 YAML·고유 코드·CodeFactory·표시 설명·역할/추천·Sprite·SFX/VFX·캐릭터 상세 문서를 함께 연결한다.
새 보상 코드는 RewardCodeFactory·RewardCodeCatalog와 활성/보관/인스턴스/사다리/후보 제한을 확인한다.
ID/저장 호환은 CurrentVersion과 복원 코드를 함께 검토한다. .meta는 Unity가 생성하게 하고 기존 GUID는 보존한다.
보상 코드의 상세 활성/교환/축전/판매 규칙은 Detail_17, 테마/사건 슬롯과 복구 ID는 Detail_01/09를 따른다.

## UI·미술·사운드

아군/적 모두 적 카드 기본 구조를 사용한다. UITheme·UIBuild·CodeText·공통 툴팁을 재사용한다.
군청색 기본 UI·명확한 정보 계층·정렬/여백·세밀한 눌림/선택·두꺼운 테두리 제거·좌측 I 장식선 제거를 유지한다.
이름/원소/한줄소개는 hover, 초월 충전은 원소색 원형 HUD, 상태 알림은 테두리 없는 텍스트다.
행동서열 확장은 행동 종료까지 유지한다. 예고·시전·효과·숫자·종료를 읽을 수 있게 하고 1~5배와 별도 일시정지를 보존한다.
VFX는 구매 Handpaint 기반 공통 부품의 제자리 시전/실제 대상 피격 문법이다. 콜백 수/타이밍과 pause 수명을 보존한다.
SFX는 논리 큐/라우트를 통하며 실제 재생·배속 병합·더킹과 음성을 확인한다. 무음 영상으로 오디오를 승인하지 않는다.
공허 기본형의 결정 면 분할·절제된 선·백색/먹색/금색 골격을 유지하고 원화 비율/alpha/portrait import 정책을 확인한다.
미술·게임 전체 독립 검수는 사용자 지시대로 Claude CLI `--model claude-opus-5-5`; Sonnet으로 전환하지 않는다.
미술은 작은 범위를 새 세션으로 순차 검수하며 Root가 실제 결함을 직접 보정·회귀 검증한다. 최종 미술 PASS 영상만 보여 준다.
구매팩·임시 음성은 .gitignore 공개 소스 규칙을 지키며 강제 추가/삭제하지 않는다. 현재 완료 상태는 백로그와 검수 ledger가 원본이다.
