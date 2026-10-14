# 상세 기획서 04 — 훈련 (폐지)

> **3계층 문서.** 2026-09 전면 개편으로 **훈련 체계 전체를 걷어냈다.**

최종 갱신: 2026-09-25

---

## 무엇이 사라졌나

| 사라진 것 | 대신 |
| --- | --- |
| 집중 훈련 · 훈련 페이즈(`GameState.TrainingPhase`) · 훈련 화면 | 없음. 준비 화면은 상점 · 휴식 · 공용 인벤토리 · 덱 구성만 제공 |
| 스탯 강화 횟수(`incrementUpgrade`) · 주/부 스탯 훈련 보너스 | 롤식 선형 성장 하나 — [Detail_03 §1.1](Detail_03_Character.md) |
| 서포트 카드 · 우정도 · 패시브 전수 | 없음 |
| 스킬 Pt · 스킬 힌트 · 해금 패시브 | 없음. 유닛은 특성 · 일반행동 · 초월기 셋만 가진다 |
| 강장제(파티 토닉) · 훈련 보급품 보상 | 없음 — [Detail_13](Detail_13_Reward_Catalog.md) |
| 훈련 EXP(`90 + 11 × Stage`) | 없음. EXP는 전투(처치 · 클리어)에서만 — [Detail_03 §2](Detail_03_Character.md) |

관련 코드(`TrainingManager`, `TrainingState`, `SupportBondState`, `SkillHintState`, `PartyTonicState`,
훈련 · 스킬 · 서포트 카드 화면)도 모두 지웠고, 저장 포맷을 v9로 올려 옛 저장본을 폐기했다.

옛 설계는 커밋 `a6194c9`의 이 문서에 남아 있다.

> 🔸 아군의 성장이 레벨 하나로 줄면서 후반에 적 레벨을 따라가지 못할 수 있다.
> 메우는 수단은 [Design_Backlog](Design_Backlog.md)의 개편 항목에서 정한다.
