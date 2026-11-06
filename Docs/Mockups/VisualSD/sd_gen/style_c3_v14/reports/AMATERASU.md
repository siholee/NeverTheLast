# AMATERASU · SD 비율 14차 q2

2026-11-06 · 상태: **검수 대기**

등록본 IDLE 재측정: crownY=300, chinY=632, feetY=1220 → **2.7711등신**. System.Drawing 눈금 확대를 직접 확인했으며 v12 측정과 같은 값이다.
목표 2.68의 머리 비중 37.313%. 초기 머리 확대 지침 = (현재 등신 / 2.68 − 1) ×100 = **+3.40%**. 미세 이탈이므로 머리/하체 비율만 수정하도록 생성했다.
최종 IDLE: crownY=341, chinY=665, feetY=1220 → **2.7130등신 / 머리 비중 36.860%**. 실측 중심값은 재생성 기준 2.62~2.74 안이다. 수동 판독 불확도 ±10px를 남기며 최종 승인이 아니다.

## 키트와 포즈
- N40: 단일 적·비접촉·사격. WINDUP 당김 → STRIKE 발사 후 손 펼침.
- U40: 단일 적을 탄별 선정·비접촉·사격(최대5발).
- P240: 동료 추가공격을 통한 일광 축적. 별도 SP 없음.

## 제작·측정
내장 image_gen / transparent_background=true. 각 포즈는 해당 등록본을 참조했고 나머지 4포즈는 새 IDLE도 크기 참조로 사용했다. 얼굴·방향·의상·소품·동작을 고정하고 머리 비중 수치를 명시했다. 최종 프롬프트는 [prompts_q2.json](../_work_AMATERASU/prompts_q2.json).
재생성: 0 회(최초 생성 제외). 원시 생성본은 _work_AMATERASU/에 보존. 후처리는 System.Drawing 전체 균등 배율 1회와 LockBits 정수 행 이동뿐이며 부위별 합성·색상 보정·alpha 경계 삭제는 하지 않았다.

| 포즈 | ROI x1,y1→x2,y2 | √(w×h) | IDLE 편차 | 원시본→최종 균등 배율 | 정수 dx,dy |
| --- | --- | ---: | ---: | ---: | --- |
| IDLE | 422,341→783,665 | 342.00 | 0.000% | 0.881253 | 18,55 |
| WINDUP | 380,298→742,621 | 341.94 | -0.016% | 0.889209 | 13,30 |
| STRIKE | 440,310→805,631 | 342.29 | 0.086% | 0.906516 | 4,8 |
| HIT | 273,361→635,685 | 342.47 | 0.138% | 0.892671 | 11,25 |
| DOWN | 492,505→844,839 | 342.88 | 0.258% | 0.896736 | 9,22 |

## 검증·잔여 사항
- 5장 1024×1280, PNG 8bit RGBA, 모서리 alpha=0, alpha≥129 최하단 y1220, x30..994 안. 머리 √(w×h)는 IDLE ±3% 안.
- before_after·face·실루엣·250px 시트를 직접 보았다. 우향·주요 동작·표정·소품 유지, 배경·그림자·VFX·글자·흰 외곽은 육안상 보이지 않는다.
- 포니테일·가면·뾰족귀는 crown/ROI에서 제외. 가면 아래 가려진 두개 윤곽은 동일 기준으로 추정했다. 꽃무늬·구슬 간격의 작은 재묘사 차이가 남는다.
- Unity/Assets 등록/모션/실전 검증은 이번 작업에서 실행하지 않았다. Root 최종 검수와 별도 등록이 남는다.

[측정선](../measure_AMATERASU.png) · [포즈](../sheets/AMATERASU_poses.png) · [실루엣](../sheets/AMATERASU_silhouette.png) · [얼굴](../sheets/AMATERASU_face.png) · [전투250](../sheets/AMATERASU_battle250.png) · [전후](../sheets/AMATERASU_before_after.png) · [q2 lineup](../lineup_q2.png)

수치·해시 원본: [_work_AMATERASU/final_measurements.csv](../_work_AMATERASU/final_measurements.csv), [validation.csv](../_work_AMATERASU/validation.csv). lineup 기준 아군은 현행 97_stage_figures.yaml의 crownY로 정규화했다.
