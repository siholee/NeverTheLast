# NeverTheLast 기획서 체계

기획 문서는 **3계층**으로 나뉜다. 아래로 갈수록 구체적이고, 아래로 갈수록 자주 바뀐다.

```
┌─ 메인 기획서 ─────────────────────────────────────┐
│  이 게임이 무엇이고 왜 재미있는가                  │
│  수치 없음. "많다 / 적다"로만 표현                 │
│  → 거의 바뀌지 않는다                              │
└───────────────────────────────────────────────────┘
              ↓ 무엇을 만드는가
┌─ 서브 기획서 ─────────────────────────────────────┐
│  게임에 등장하는 모든 개념의 정의와 관계           │
│  개념의 존재 이유와 다른 개념과의 연결             │
│  → 시스템 구조가 바뀔 때만 바뀐다                  │
└───────────────────────────────────────────────────┘
              ↓ 어떻게 동작하는가
┌─ 상세 기획서 (여러 장) ───────────────────────────┐
│  공식, 수치, 데이터 스키마, 현재 콘텐츠 목록       │
│  → 밸런싱·콘텐츠 추가마다 바뀐다                   │
└───────────────────────────────────────────────────┘
```

---

## 문서 목록

### 1계층 — 메인 기획서

| 문서 | 내용 |
| --- | --- |
| [GDD_Main.md](GDD_Main.md) | 게임 개요, 디자인 필러, 플레이어 경험, 게임 루프, 시스템 지도, 콘텐츠 볼륨 |

### 2계층 — 서브 기획서

| 문서 | 내용 |
| --- | --- |
| [GDD_Sub_Concepts.md](GDD_Sub_Concepts.md) | 전 시스템의 개념 정의와 개념 간 관계도 |

### 3계층 — 상세 기획서

| 문서 | 다루는 범위 |
| --- | --- |
| [Detail_01_Progression.md](Detail_01_Progression.md) | 런 진행, 스테이지·라운드·테마, 적 편성·스케일링, 생명력 |
| [Detail_02_Combat.md](Detail_02_Combat.md) | 전장, 행동치, 피해 공식, 보호막, 원소, 상태, 이벤트 훅 |
| [Detail_03_Character.md](Detail_03_Character.md) | 5스탯과 파생, 코드 3분류, 패시브 해금, 캐릭터 데이터 스키마 |
| [Detail_04_Training.md](Detail_04_Training.md) | 집중 훈련, 서포트 카드, 우정도, 패시브 전수, 육성 완료 계승 |
| [Detail_05_Economy.md](Detail_05_Economy.md) | 골드·토큰·티켓, 장비, 전투 보상 풀과 티어 확률 |
| [Detail_06_Events.md](Detail_06_Events.md) | 사건 데이터 스키마, 선택지 액션, VN 연출 필드, 현재 사건 |
| [Detail_07_UI_Tech.md](Detail_07_UI_Tech.md) | UI 화면 구성, 매니저 구조, 데이터 파일, 저장 시스템 |
| [Detail_08_Confirmed_Characters.md](Detail_08_Confirmed_Characters.md) | 확정 캐릭터의 전투 사양, 해금 패시브, 아트 자산과 남은 작업 |
| [Detail_09_Enemy_Catalog.md](Detail_09_Enemy_Catalog.md) | 적 스탯 규격, 테마별 로스터, 스테이지 편성 |
| [Detail_10_Boss_Catalog.md](Detail_10_Boss_Catalog.md) | 중간 보스와 최종 보스의 사양·공략 축·미결 |
| [Detail_11_Code_Catalog.md](Detail_11_Code_Catalog.md) | **ID별 코드 효과 색인.** 아군·적 전체를 분류별·ID순으로 |
| [Detail_12_Code_Weapon_Catalog.md](Detail_12_Code_Weapon_Catalog.md) | 플레이어 코드 슬롯 규칙과 무기·방어구 숙련의 관리 목록 |
| [Detail_13_Reward_Catalog.md](Detail_13_Reward_Catalog.md) | 보상 풀, 티어 확률, 테마 전용·사건 전용 보상의 관리 목록 |
| [Detail_14_Equipment_Catalog.md](Detail_14_Equipment_Catalog.md) | 장비 ID·숙련·중량과 캐릭터별 시작 무기 목록 |

### 미결 — 기획 백로그

| 문서 | 내용 |
| --- | --- |
| [Design_Backlog.md](Design_Backlog.md) | **결정이 필요한 항목 20개.** 코드·데이터로 해결할 수 없고 설계 판단이 선행되어야 하는 것들 |

> 결정이 끝난 항목은 해당 상세 기획서로 옮기고 백로그에서 지운다.

---

## 관련 문서 (기획서 외)

| 문서 | 역할 |
| --- | --- |
| [../PostMerge_SystemGuide.md](../PostMerge_SystemGuide.md) | 개발자용 구현/콘텐츠 추가 **절차서** |
| [../BattleUI_Overall.md](../BattleUI_Overall.md) | 전투 화면 UI 레이아웃 명세 |
| [../Character_PaperDoll_Spec.md](../Character_PaperDoll_Spec.md) | 캐릭터 페이퍼돌 아트 스펙 |
| [../Aztec_Quetzalcoatl_Implementation.md](../Aztec_Quetzalcoatl_Implementation.md) | 테마 단위 구현 사례 |
| [../StatModifierAudit.md](../StatModifierAudit.md) | 스탯 보정 계열 정리 기록 |
| [../README.md](../README.md) | 스크립트 구조와 이벤트 시스템 설명 |

---

## 작성 규칙

1. **계층을 넘나들지 않는다.** 메인에 수치를 쓰지 않고, 상세에 비전을 쓰지 않는다.
2. **수치는 상세 기획서에만 존재한다.** 같은 수치가 두 문서에 있으면 반드시 하나는 썩는다.
3. **미구현은 🔸로 표기한다.** 기획은 있으나 코드가 없는 것, 코드는 있으나 값이 죽어 있는 것 모두.
4. **상세 기획서를 고칠 때 데이터 파일도 같이 고친다.** 상세 기획서는 `Assets/Resources/Data/*.yaml`과 코드의 거울이다.
5. 문서 상단에 **최종 갱신일**을 남긴다.
