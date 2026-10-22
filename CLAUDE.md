# CLAUDE.md

NeverTheLast의 Claude CLI 작업 안내. 최종 갱신: **2026-10-15**.

공통 프로젝트 규칙은 [AGENTS.md](AGENTS.md), 설계 입구는 [현행 기획서](Assets/Docs/Design/README.md)다.
작업 전 두 문서를 읽는다. 이 파일에 수치·로스터·복제된 설계·검수 이력을 다시 관리하지 않는다.

## 기준 브랜치·문서

- **main**은 현재 Rework 개발 브랜치다. 이전 rework/core-stats의 작업을 이어간다.
- **legacy**는 개편 전 main의 보관본이다. 개발 대상으로 수정하지 않는다.
- 실제 Unity `Application.dataPath`와 Git 상태로 올바른 체크아웃인지 확인한다.
- 현행 Design / 서사 Narrative / 미구현 Proposals / 과거 Docs/Archive를 구분한다.
- 캐릭터별 세부 사양은 `Assets/Docs/Design/Characters/`이며 목록은 Detail_08이다.
- 오래된 작업 일지·코드 주석·Archive를 현재 규칙보다 우선하지 않는다. 실행 코드·YAML·씬 값으로 확인한다.

## 요청별 실행

사용자의 최신 승인 범위가 우선한다. 구현·검수·릴리스 역할을 임의 확대하지 않는다.
전체 게임 QA와 미술 검수는 Claude Opus 5.5를 명시한다. Sonnet 대체나 제한을 합격으로 해석하지 않는다.
미술 검수는 효과군/캐릭터/화면의 작은 범위 하나씩 새 세션으로 수행한다.
해당 원본·필요 프레임·짧은 기존 결함 요약·관련 소스만 받고 PASS/HOLD/FAIL 및 구체적인 결함을 짧게 남긴다.
전체 원본/장문 이전 세션을 매번 다시 넣지 않는다. 구현은 Root가 직접 맡는 사용자 지시를 따른다.

Root가 보고된 결함을 실제 소스/화면과 대조하고 보정·Unity 회귀 후 바뀐 범위만 재검수한다.
미변경 PASS는 동일 소스 근거로 재사용하며 ledger에 범위·판정·제한을 기록한다.
정지 화면·부품 PASS를 영상/4배속/성능/게임 전체 합격으로 확대하지 않는다.

## 검증·보존

현행 VerifyHarnessRunner의 실제 Unity Play Mode와 Console, 필요 시 격리된 Windows Player를 사용한다.
사용자 저장·기존 수정·브랜치/인덱스·음성·구매팩/GUID·행동 종료까지의 행동서열 확장을 보존한다.
다른 작업의 변경을 재구현/되돌리거나 실제 임팩트 콜백·타수·상태/치유/자원 판정을 미술 작업에서 바꾸지 않는다.
작업 결과는 로컬 Logs에 기록하고 날짜별 보고를 현행 기획서 본문에 붙이지 않는다.
전체 검수 재개 시 Design_Backlog 및 `Logs/WholeGameReview20261015/`의 최신 인계·coverage·manifest를 먼저 확인한다.
현재 전체 최종 기술·상용 미술 승인은 미완료다. main 승격과 문서 정리는 QA 승인이나 신규 릴리스가 아니다.
