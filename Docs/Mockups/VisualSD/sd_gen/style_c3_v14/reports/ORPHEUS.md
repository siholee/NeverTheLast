# ORPHEUS · SD 비율 14차 q2

2026-11-06 · 상태: **검수 대기**

등록본 IDLE 재측정: crownY=152, chinY=534, feetY=1220 → **2.7958등신**. System.Drawing 눈금 확대를 직접 확인했으며 v12 측정과 같은 값이다.
목표 2.68의 머리 비중 37.313%. 초기 머리 확대 지침 = (현재 등신 / 2.68 − 1) ×100 = **+4.32%**. 미세 이탈이므로 머리/하체 비율만 수정하도록 생성했다.
최종 IDLE: crownY=141, chinY=536, feetY=1220 → **2.7316등신 / 머리 비중 36.608%**. 실측 중심값은 재생성 기준 2.62~2.74 안이다. 수동 판독 불확도 ±10px를 남기며 최종 승인이 아니다.

## 키트와 포즈
- N25: 단일 적·비접촉·주문·강인도 삭감. 리라 준비 → 연주 정점.
- U25: 생존 필드 아군 전체·비접촉·강인도 삭감 효율 버프.
- P225: 적 강인도 생성·격파 연계. 실제 소환수 없음, 별도 SP 없음.

## 제작·측정
내장 image_gen / transparent_background=true. 각 포즈는 해당 등록본을 참조했고 나머지 4포즈는 새 IDLE도 크기 참조로 사용했다. 얼굴·방향·의상·소품·동작을 고정하고 머리 비중 수치를 명시했다. 최종 프롬프트는 [prompts_q2.json](../_work_AMATERASU/prompts_q2.json).
재생성: 2 회(최초 생성 제외). 원시 생성본은 _work_ORPHEUS/에 보존. 후처리는 System.Drawing 전체 균등 배율 1회와 LockBits 정수 행 이동뿐이며 부위별 합성·색상 보정·alpha 경계 삭제는 하지 않았다.

| 포즈 | ROI x1,y1→x2,y2 | √(w×h) | IDLE 편차 | 원시본→최종 균등 배율 | 정수 dx,dy |
| --- | --- | ---: | ---: | ---: | --- |
| IDLE | 276,141→772,536 | 442.63 | 0.000% | 0.912656 | 0,3 |
| WINDUP | 241,164→741,556 | 442.72 | 0.020% | 0.885630 | 15,34 |
| STRIKE | 319,149→842,524 | 442.86 | 0.052% | 0.908364 | 2,5 |
| HIT | 122,100→637,481 | 442.96 | 0.075% | 0.969469 | -32,-77 |
| DOWN | 401,374→879,785 | 443.24 | 0.137% | 0.866604 | 26,61 |

## 검증·잔여 사항
- 5장 1024×1280, PNG 8bit RGBA, 모서리 alpha=0, alpha≥129 최하단 y1220, x30..994 안. 머리 √(w×h)는 IDLE ±3% 안.
- before_after·face·실루엣·250px 시트를 직접 보았다. 우향·주요 동작·표정·소품 유지, 배경·그림자·VFX·글자·흰 외곽은 육안상 보이지 않는다.
- 아호게·고립된 머리 끝은 crown/ROI에서 제외. IDLE 1차에서 약해진 아호게를 2차 복원, 3차 비율 재조정. 최종 IDLE의 금색/피부 채도가 등록본·나머지 포즈보다 강한 잔여 차이가 있다. 생성 상한 3회를 지켰으며 색 보정은 하지 않았다.
- Unity/Assets 등록/모션/실전 검증은 이번 작업에서 실행하지 않았다. Root 최종 검수와 별도 등록이 남는다.

[측정선](../measure_ORPHEUS.png) · [포즈](../sheets/ORPHEUS_poses.png) · [실루엣](../sheets/ORPHEUS_silhouette.png) · [얼굴](../sheets/ORPHEUS_face.png) · [전투250](../sheets/ORPHEUS_battle250.png) · [전후](../sheets/ORPHEUS_before_after.png) · [q2 lineup](../lineup_q2.png)

수치·해시 원본: [_work_AMATERASU/final_measurements.csv](../_work_AMATERASU/final_measurements.csv), [validation.csv](../_work_AMATERASU/validation.csv). lineup 기준 아군은 현행 97_stage_figures.yaml의 crownY로 정규화했다.
