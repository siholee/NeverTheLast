# SD 13차 비율 통일 지시서 · 2026-11-06

발주: 사용자 "21명 다시 그리기" 승인 → Root(PM) → Codex. 규격·검수는 [SD_Spec.md](SD_Spec.md), 머리 측정은 [9차 지시서](SD_Pose_v9_Brief.md), 크기 기준은 [12차 지시서](SD_Fix_v12_Brief.md) N(정수리 crownY).

목표: 남녀·체형 구분 없이 **아군 전원이 같은 SD 등신**. 등신 = (1220 − crownY) / (chinY − crownY), IDLE 기준. 12차 측정 중앙값 2.71.

- **목표 2.68 ± 0.08 (합격 2.60~2.76)**, 허용 한계 2.55~2.80(벗어나면 HOLD).
- 측정법은 12차 `Logs/SDFixV12_20261105/crown_measure.csv`와 같다(crownY = 두개부 맨 위, 귀·뿔·모자·관·후드·아호게·뜬 머리·무기 제외 / chinY = 턱 끝). 모자·후드로 가려진 경우 두개 윤곽 추정.
- 호루스 제외. 수르트는 12차 새 IDLE이 약 2.58이라 이번 대상에서 뺀다(Root 재측정으로 확인).

## 대상 20명

| 묶음 | 대상(현재 등신) | 방향 |
| --- | --- | --- |
| p1 머리 큼 | LOKI 2.36 · ERESHKIGAL 2.45 · ISHTAR 2.45 · SEI 2.46 · SHI 2.47 · VAYU 2.48 | 머리를 줄이거나 몸을 늘린다(머리 −7~−12%) |
| p2 많이 김 | KUBERA 4.26 · NAPOLEON 3.49 · MARIE 3.33 · GAUDI 3.14 · ORION 3.13 | 머리를 키우고 몸통·다리를 줄인다 |
| p3 김 | ATALANTE 3.10 · SURYA 3.09 · ANUBIS 3.04 · ASTOLFO 3.01 · VARUNA 2.94 | 같음 |
| p4 약간 김 | THOTH 2.93 · PYGMALION 2.93 · SHAKESPEARE 2.91 · NEITH 2.88 | 같음 |

## 유지할 것 (비율만 바꾼다)

- **5포즈 모두 다시 그린다**(머리 크기 IDLE ±3% 유지 규칙 때문). 각 포즈의 동작·실루엣·무기 각도·시선·표정·의상·색·장신구는 현재 등록본 그대로. 사용자가 승인한 포즈다.
- 얼굴(눈 모양·눈색·머리색·머리 결)·성별 표현·체형 개성은 유지. 비율만 SD 공통 비율로.
- 초월자 정숙 톤: SEI·SHI(그리고 범위 밖 츠쿠요미)만. 과장 없이 우아·절도.
- 특수: VARUNA WINDUP = 새 IDLE 복사. SHI STRIKE = 칼끝이 칼집 입구에서 막 빠져나오는 순간, DOWN 흐려짐은 게임 효과. ASTOLFO·SHI·VARUNA는 12차 승인 포즈가 기준. NAPOLEON 돌격소총, SHAKESPEARE 셀카봉, PYGMALION·SHAKESPEARE 웨이브는 곱슬 경계를 넘지 않게.
- 키트 먼저: 무기·접촉 여부·치유 여부는 키트 문서 기준(포즈 변경 금지와 충돌하지 않음).

## 방법

1. 대상마다 현재 IDLE crownY·chinY를 다시 재고 목표 배율을 계산한다(머리를 키울지 몸을 줄일지 판단 근거를 보고서에 적는다).
2. 내장 image_gen(transparent background)으로 **현재 등록본 해당 포즈를 참조 이미지**로 써서 다시 그린다. 프롬프트는 외형 묘사와 "same pose, same outfit, re-proportioned to a 2.7-head chibi: larger head, shorter torso and legs"(머리 큼 묶음은 반대) 식으로. 포즈당 최대 3회.
3. 몸 일부 확대·축소 같은 기하 변형 합성 금지(이음새·선 굵기 불일치). 전체 균등 배율·정수 이동만 허용.
4. 규격: 1024×1280 8bit RGBA, 모서리 alpha 0, alpha≥129 최하단 y1220, 가로 30..994, 오른쪽 방향, 머리 √(w·h) IDLE ±3%, 흰 외곽 없음.
5. 산출: `Docs/Mockups/VisualSD/sd_gen/style_c3_v13/{KEY}_SD_*.png`, sheets(poses·silhouette·face·battle250·before_after — before_after는 현재 등록본과 같은 기준선·같은 정수리 정규화로 나란히), `measure_{KEY}.png`(crownY·chinY 선을 그은 IDLE 확대), reports/{KEY}.md(전·후 등신, 상태 "검수 대기"). 묶음 전체 `lineup_{묶음}.png`: 등록본 기준 아군 몇 명(THESEUS·HIMIKO·JEAN·AGNI·BASTET)과 대상 새 IDLE을 정수리 정규화 250px로 나란히.

## Root 검수

등신 수치 + 육안(250px·실전 크기에서 머리 크기·몸 길이가 기준 5명과 같은 무리로 읽히는지), 포즈 일치(before_after), 얼굴·의상 동일성, §8 검수표. 통과분은 등록(GUID 유지)·crownY/앵커 재실측·Unity Play 검증 후 검토판 갱신.
