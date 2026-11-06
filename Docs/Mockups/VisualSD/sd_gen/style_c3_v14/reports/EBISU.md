# EBISU · SD 비율 14차 q2

2026-11-06 · 상태: **검수 대기**

등록본 IDLE 재측정: crownY=346, chinY=660, feetY=1220 → **2.7834등신**. System.Drawing 눈금 확대를 직접 확인했으며 v12 측정과 같은 값이다.
목표 2.68의 머리 비중 37.313%. 초기 머리 확대 지침 = (현재 등신 / 2.68 − 1) ×100 = **+3.86%**. 미세 이탈이므로 머리/하체 비율만 수정하도록 생성했다.
최종 IDLE: crownY=334, chinY=659, feetY=1220 → **2.7262등신 / 머리 비중 36.682%**. 실측 중심값은 재생성 기준 2.62~2.74 안이다. 수동 판독 불확도 ±10px를 남기며 최종 승인이 아니다.

## 키트와 포즈
- N163: 단일 적·비접촉·주문. 낚싯대 준비 → 앞으로 캐스팅.
- U163: 자신 치유 + 생존 아군 전원 정화·비접촉·지원.
- P363: 피해 분담 및 피격 대상 강화. 별도 SP 없음.

## 제작·측정
내장 image_gen / transparent_background=true. 각 포즈는 해당 등록본을 참조했고 나머지 4포즈는 새 IDLE도 크기 참조로 사용했다. 얼굴·방향·의상·소품·동작을 고정하고 머리 비중 수치를 명시했다. 최종 프롬프트는 [prompts_q2.json](../_work_AMATERASU/prompts_q2.json).
재생성: 0 회(최초 생성 제외). 원시 생성본은 _work_EBISU/에 보존. 후처리는 System.Drawing 전체 균등 배율 1회와 LockBits 정수 행 이동뿐이며 부위별 합성·색상 보정·alpha 경계 삭제는 하지 않았다.

| 포즈 | ROI x1,y1→x2,y2 | √(w×h) | IDLE 편차 | 원시본→최종 균등 배율 | 정수 dx,dy |
| --- | --- | ---: | ---: | ---: | --- |
| IDLE | 281,334→689,659 | 364.14 | 0.000% | 0.912656 | 0,-2 |
| WINDUP | 429,336→841,649 | 359.10 | -1.384% | 0.926346 | -8,-20 |
| STRIKE | 250,392→650,706 | 354.40 | -2.675% | 0.912656 | 0,-1 |
| HIT | 250,347→686,653 | 365.26 | 0.307% | 0.912656 | 0,-1 |
| DOWN | 358,478→741,824 | 364.03 | -0.031% | 0.912656 | 0,-2 |

## 검증·잔여 사항
- 5장 1024×1280, PNG 8bit RGBA, 모서리 alpha=0, alpha≥129 최하단 y1220, x30..994 안. 머리 √(w×h)는 IDLE ±3% 안.
- before_after·face·실루엣·250px 시트를 직접 보았다. 우향·주요 동작·표정·소품 유지, 배경·그림자·VFX·글자·흰 외곽은 육안상 보이지 않는다.
- 리본·장발·낚싯대는 ROI에서 제외. STRIKE 머리 면적 지표는 IDLE 대비 -2.675%로 허용 안이지만 작은 쪽 경계다. 소매 스크롤 세부가 약간 달라 별도 육안 확인이 필요하다.
- Unity/Assets 등록/모션/실전 검증은 이번 작업에서 실행하지 않았다. Root 최종 검수와 별도 등록이 남는다.

[측정선](../measure_EBISU.png) · [포즈](../sheets/EBISU_poses.png) · [실루엣](../sheets/EBISU_silhouette.png) · [얼굴](../sheets/EBISU_face.png) · [전투250](../sheets/EBISU_battle250.png) · [전후](../sheets/EBISU_before_after.png) · [q2 lineup](../lineup_q2.png)

수치·해시 원본: [_work_AMATERASU/final_measurements.csv](../_work_AMATERASU/final_measurements.csv), [validation.csv](../_work_AMATERASU/validation.csv). lineup 기준 아군은 현행 97_stage_figures.yaml의 crownY로 정규화했다.
