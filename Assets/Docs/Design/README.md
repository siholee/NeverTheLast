# NeverTheLast 현행 기획서

최종 갱신: **2026-10-14** · 개발 기준: **main**

이 폴더는 현재 게임의 설계 기준이다. `legacy`와 [문서 보관소](../../../Docs/Archive/README.md)는 과거 자료다.
작업 보고서나 채택 전 초안을 현재 사양으로 읽지 않는다. 구현 여부는 실행 코드·YAML·씬 직렬화와 대조한다.

## 읽는 순서

1. [게임 방향](GDD_Main.md): 어떤 선택과 경험을 만드는가.
2. [공통 용어](GDD_Sub_Concepts.md): 진행·행동·자원·상태를 어떻게 구분하는가.
3. 아래 상세 사양: 수치, 판정, 구현 위치와 데이터 원본.
4. [남은 결정과 검증](Design_Backlog.md): 미구현 제안 및 아직 완료되지 않은 검수.

## 상세 사양

| 문서 | 범위 | 데이터·구현 기준 |
| --- | --- | --- |
| [01 진행](Detail_01_Progression.md) | 8라운드 테마·초반 범용 구간·24/48층 고정 보스·정산 | RoundManager, 70/80_stages, 60_enemies |
| [02 전투](Detail_02_Combat.md) | AV·피해·치명타·효과 명중/저항·상태·강인도 | ActionScheduler, Unit, DamageMitigation |
| [03 캐릭터](Detail_03_Character.md) | 8스탯·레벨·초월 자원·소환·ID | UnitStats, 10_units |
| [05 경제](Detail_05_Economy.md) | 골드·상점·공용 보급·휴식·유료 추가 보상 | RewardManager, InventoryManager, 40_items |
| [06 사건](Detail_06_Events.md) | 서사 슬롯·선택·VN·복구 사건 | EventScheduler, 00_intro, 80_stages |
| [07 UI·기술](Detail_07_UI_Tech.md) | 화면 정보 구조·미술/연출 규약·저장·검증 | UITheme, CardView, VerifyHarnessRunner |
| [08 아군](Detail_08_Confirmed_Characters.md) | 현재 구현된 캐릭터의 상세 전투 사양 | 10_units, 20_codes, CodeFactory |
| [09 적](Detail_09_Enemy_Catalog.md) | 운영 적과 복구 테마의 8스탯 이관 | 60_enemies, LegacyEnemyCodes |
| [10 보스](Detail_10_Boss_Catalog.md) | 테마 엘리트·보스·24/48층 스펙 시험 | 60_enemies, 80_stages |
| [11 고유 코드](Detail_11_Code_Catalog.md) | 아군 슬롯별 코드·적 코드 색인 | 20_codes, CodeFactory |
| [12 코드 슬롯](Detail_12_Code_Weapon_Catalog.md) | 고유 슬롯·보상 활성/보관·교환 | Unit, RewardCodeCatalog |
| [13 보상](Detail_13_Reward_Catalog.md) | 생성·티어 분포·즉시 보상 | 90_rewards, RewardManager |
| [15 편성](Detail_15_Party_Synergy.md) | 역할·기믹 축·추천 조합 | 30_synergies, SynergyCatalog |
| [16 원소](Detail_16_Elements.md) | 속성과 부착·반응 행렬·지속피해 | ElementalReactions, ActionDot |
| [17 보상 코드](Detail_17_Reward_Codes.md) | 현행 전체 코드·소모품·축전기·경제 판정 | 20_codes, 40_items, RewardCodeFactory |
| [18 재보](Detail_18_Treasury.md) | 아군 공용 자원·쿠베라·만사 무사·이슈타르 | PartyTreasury, TreasuryActionContext |

04 훈련·14 장비는 폐지된 문서이므로 보관소로 이동했다. 기존 번호는 이력과의 대조를 위해 재배정하지 않는다.
12의 파일명은 기존 참조 호환을 위해 유지하지만 현재 내용은 무기 체계가 아닌 코드 슬롯이다.

## 서사와 미구현 제안

- [서사 설정](../Narrative/README.md): 세이·프랑스 인물 설정과 첫 인사 원고. 원고는 런타임 구현과 구분한다.
- [미구현 캐릭터](../Proposals/Unimplemented_Characters.md): 브라다만테·아스톨포·라부아지에·아스클레피아. 🔸 제안이며 플레이 가능 목록에 포함하지 않는다.
- [과거 문서](../../../Docs/Archive/README.md): 이전 기획, AI 검토 메모리, UI/VFX 검수 기록과 변경 전 사양.

## 유지 규칙

[48층 체크포인트](Boss_48_Checkpoint.md)는 구현된 군세장·호위의 전투 사양이다. 정확한 스탯은 Detail_09에 둔다.

현행 규칙은 해당 상세 문서 한 곳에 두고 다른 문서는 링크한다. 채택되면 제안의 상태를 바꾸고 백로그에서 해결된 항목을 제거한다.
미구현은 `🔸`, 구현되었으나 실측이 남은 것은 `🔸 검증 필요`로 구분한다. 날짜별 작업 보고를 본문에 누적하지 않는다.
문서 수정 시 갱신일과 실제 코드/데이터의 경로를 맞춘다. 이번 정리는 게임 규칙·수치·저장 데이터를 변경하지 않는다.

부분 기능 검사나 특정 화면 PASS를 게임 전체 QA·성능·상용 미술 승인으로 확대하지 않는다. 남은 검수는 백로그에서 확인한다.
