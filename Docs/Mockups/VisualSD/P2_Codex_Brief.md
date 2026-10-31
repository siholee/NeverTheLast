# P2 작업 지시서 뼈대 — 확정 방향을 게임에 적용

작성: 2026-10-29 · 발주: 사용자(Root는 뼈대만 작성) · 상태: 🔸 미착수

[SD 디오라마 전장 제안](../../../Assets/Docs/Proposals/Visual_SD_Remake.md)의 “1. 확정한 방향”을 게임에 옮기는 다음 단계다. 화면 기준은 같은 폴더 `index.html`(시안 v1.2)이다.
P1 지시서(`P1_Codex_Brief.md`)의 **공통 금지와 중단 조건**, 단계마다 보고 후 정지, 검증 환경(열린 Unity 에디터의 `Library/NeverTheLastVerify/request.json` IPC)을 그대로 따른다.
작업 전 `AGENTS.md`, `Assets/Docs/Design/README.md`, `Logs/VisualSDP1_20261029/S1_review.md`~`S8b_review.md`를 읽는다.

## 이미 있는 것(P1 결과)

`VisualModeState`·`StageLayout`(S1·S7), 고정 카메라·배경(S2), `StageFigure`·`97_stage_figures.yaml`(S3, `deform` 포함), 발밑 HUD 묶음·강조 연동(S4),
SD 앵커·피격·숫자(S5), `UltimateCutIn`·준비 표식(S6), 컷인 연출 대기 `Effects/Base/ActionPresentation.cs`(S8). 판정 불변 검증용 하네스 시나리오들.

## 단계

### S1 진형 배치

- `StageLayout`의 전투·준비 좌표를 시안 `ROW`·`rowDist`·`laneY`로 바꾼다: 전열 190·후열 540, 레인 y 560~870, 2명 열 t={0.2, 0.8}, 기울기 항 30×(t−0.5), 지그재그 없음.
- 바닥 열 표시(전열·후열 띠): 시안 `rowBandsHTML`. 전투는 옅게, 준비는 진하게 + 이름.
- 시안 대조 20입력(±0.5px), Card 불변, 준비 입력(이동·교환·중복 거절) 재검사.

### S2 양 진영 하단 HUD(스타레일식)

- 발밑 HUD 묶음을 끄고 시안 `hsrBarHTML` 구조의 하단 카드 줄을 만든다: [아군 5] [재보] [적 5], 카드 118×150, 초상화 원화·열 배지·체력판(적은 강인도)·바깥 모서리 초월 원.
- 기존 체력·초월·고유 자원·상태 아이콘 바인딩을 재사용한다. 새 값 계산을 만들지 않는다. 소환수·인원 변화(사망·추가 등장) 때 카드 재배치 규칙을 정해 보고한다.
- 충전 완료·행동 중 카드 상승, SD·행동 순서·피해 집계와 강조 연동. 재보는 현행 공용 SP형 규칙.
- 1920×1080·2560×1080 5:5 촬영, `combat_feedback`·`card_hud_containment`(Card) 회귀.

### S3 C3 SD 포즈 프레임 등록

- `Docs/Mockups/VisualSD/sd_gen/style_c3/{ID}_SD_{IDLE|WINDUP|STRIKE|HIT|DOWN}.png`(시·세이·수르트·스카디·프레이아 25장)를 `Assets/Resources/Sprite/SD/`로 복사한다. 새 GUID는 Unity가 만들게 하고 임포트 정책(`SdTextureImportPolicy`)을 둔다.
- `SdFrameRig`: SD 원화가 있는 유닛은 `StageFigure` 프록시 대신 포즈 프레임을 쓰고, 없으면 프록시로 떨어진다. 캐릭터별 발 좌표·얼굴 x·앵커 보정은 `97_stage_figures.yaml`에 둔다.
- 프레임 교체 시점: 시안 `setPose`(예고 IDLE → WINDUP 0.14s → STRIKE 정점 → IDLE, 피격 HIT 0.42s, 사망 DOWN). 기존 타격 콜백 시각을 구독만 하고 콜백·대기를 소유하지 않는다.
- 사건 동등성(시드 고정, 초월기 포함), 1~5배·일시정지, 프레임 교체 시 발 위치 흔들림 0px 확인.

### S4 D6 비인간형 적 컷인

- `deform:false` 원화에 컷인 전용 얼굴점 `cutinFace`(x, y 비율)를 데이터로 두고, 없으면 원화 전체를 띠 높이에 맞춰 축소한다. 공허 늑대 등 3종 이상 촬영.

### S5 문서 개정(사용자 승인 후에만)

- 제안 문서 “6. 현행 규칙 개정 대상”대로 `Assets/Docs/Design/Detail_07_UI_Tech.md`와 `AGENTS.md`를 고친다. 사용자가 채택을 승인하기 전에는 하지 않는다.

## 범위 밖

나머지 37명 SD 생성(P4), VFX 접점 재조정(P3), 홈·로비 SD(P5), 포즈 보류 3건 재생성(사용자가 기록만 하기로 결정), 기존 맹독 검사 실패(별도 브랜치 `claude/blissful-mclaren-d3fcce`).

## 마무리 검사

P1 마무리와 같다: `five_party`·`combat_1v1`·`speed8`의 Card·Stage 사건 동등성, `prep`, `card_hud_containment`, BalanceSim 변경 전후 동일.
촬영 4장(5:5 전투, 1:1 전투, 준비, 컷인 정점)과 시안 v1.2 나란히 비교. 보고는 `Logs/VisualSDP2_<날짜>/`.
