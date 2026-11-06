# TSUKUYOMI · SD 14차 q1

2026-11-06 · 상태: **검수 대기**

- 키트: Characters/tsukuyomi.md·20_codes.yaml(P245/N45/U45).
- N: 단일 적·비접촉·주문/저주. 작은 손짓 준비/전방 가리킴 승인 포즈 유지.
- U: 아군 전체·비접촉·보호막/특공 버프+적 전체 저주. P: 아군 전체·비접촉·지속피해 증폭/개전 보호막.
- 장발·낱머리 제외. 초월자 정숙 톤, HIT 감은 눈·절제된 반동, 단정한 DOWN 유지. 추가 소품 없음.
- 전 IDLE: crownY=191, chinY=592, (1220-crown)/(chin-crown)=2.5661. 장발·낱머리 제외, 초월자 정숙 톤
- 목표 배율 근거: 동일 crown~발 길이에서 머리 0.9575배(-4.25%) → 2.68/37.3%. 프롬프트로만 비율 수정; 부위별 기하 변형 없음.
- 후 IDLE: crownY=223, chinY=592, 머리 369px, 2.7019등신 / 머리 비중 37.01% (내부 2.62~2.74). 수동 판독 ±8px.
- 머리 √(w·h), IDLE 대비: IDLE 423.0px(0.00%) / WINDUP 423.0px(0.00%) / STRIKE 424.2px(+0.26%) / HIT 423.2px(+0.03%) / DOWN 422.8px(-0.05%)
- 재생성 6회(최초 생성 제외): IDLE 2 / WINDUP 1 / STRIKE 1 / HIT 1 / DOWN 1. 모든 포즈 총 생성 3회 이하.
- 후처리: PowerShell System.Drawing 전체 균등 배율·LockBits 정수 이동만. 머리 면적 편차를 기준으로 포즈 이미지 전체 배율만 보정; 원시 생성본은 _work에 보존.
- 실측 5장: 1024×1280, PNG 8bit RGBA, 네 모서리 alpha 0, alpha≥129 최하단 모두 y1220, x30..994 내. 우향·배경/VFX/글자/흰 외곽 없음 자체 육안 확인.
- 실제 프롬프트: _work_TSUKUYOMI/generation_all.json (내장 image_gen, transparent background, 해당 포즈 등록본+새 IDLE 참조; 고유 이름 없이 외형·2.68등신·머리 37.3% 명시).
- 산출: 루트 5 PNG·registered 사본·sheets 5종·measure_TSUKUYOMI.png·lineup_q1.png. 측정 ROI/변환/해시는 _work_TSUKUYOMI/final_measure.json.
- 남은 결함/검수점: 승인 포즈 유지로 IDLE/WINDUP/HIT 외곽 차이가 작음. 머리카락 내부 결·소매 금장 미세차. 정숙 톤/감은 HIT 눈은 유지.
- 등록·Unity 실행/Play 검증은 미실행. 최종 승인 아님. Assets 등록본 25장 해시 변화 0; 다른 대상·이전 회차 수정/삭제/커밋 없음.
