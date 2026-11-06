# SD 9차 지시서 · 2026-11-05

발주: 사용자 → Root(PM) → Codex · 규격·검수는 [SD_Spec.md](SD_Spec.md)가 기준이다(이 문서는 범위와 추가 규칙만).

## 범위

| 구분 | 대상(원화 키) | 방향 |
| --- | --- | --- |
| 아군(SD 미보유) | 테세우스 THESEUS · 마리 테레즈 MARIE · 나폴레옹 NAPOLEON · 호루스 HORUS | 오른쪽 |
| 메히코 적(themeId 107) | 재규어 전사 AZTEC_JAGUAR · 정예 재규어 AZTEC_ELITE_JAGUAR · 독수리 전사 AZTEC_EAGLE · 정예 독수리 AZTEC_ELITE_EAGLE · 뱀 사제 AZTEC_SERPENT_PRIEST · 정예 뱀 사제 AZTEC_ELITE_SERPENT_PRIEST · 케찰코아틀 QUETZALCOATL · 테스카틀리포카 AZTEC_TEZCATLIPOCA | 왼쪽 |

- 키트는 아군 `Assets/Docs/Design/Characters/{id}.md`·`20_codes.yaml`, 적 `60_enemies.yaml`의 codes·reworkKit(normalMode 등)·`20_codes.yaml`에서 읽는다.
- 메히코 일반·정예병은 얼굴을 유지한다(익명 눈 차폐 예외는 로마 군단 일반병에만 적용). 일반과 정예는 같은 계열이되 정예가 장식·체격·무기로 한 단계 위로 보이게 한다.
- 케찰코아틀·테스카틀리포카는 보스급 신격이다. 비인간형 원화면 SD_Spec의 동물형 SD 비율 규칙을 따른다.

## 머리 크기 측정 보강(7·8차 검수에서 확정)

투구·가면·비인간형은 "보이는 머리 덩어리"로 잰다. 두개부를 투구 안쪽으로 추정하지 않는다.
- 투구: 투구 셸 윗선~턱(또는 가면 아래 끝), 볏·깃털·뺨가리개 끝·목가리개 제외.
- 동물·괴수: 주둥이 끝~뒤통수, 뿔·갈기·귀·볏 제외.
- 머리가 없는 개체(결정체 등): 주 코어.
같은 캐릭터 5장의 이 값이 IDLE ±3%여야 하며, 250px 전투 크기 나란히 보기에서 크기가 눈에 띄게 달라 보이면 수치와 상관없이 HOLD다.

## 산출물

`sd_gen/style_c3_v9/{ID}_SD_*.png`, `sheets/{ID}_poses|silhouette|face.png`, `reports/{ID}.md`. 등록은 Root 검수 뒤 별도로 한다.
