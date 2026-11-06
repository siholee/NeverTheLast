# SURTR · SD 14차 q1

2026-11-06 · 상태: **검수 대기**

- 키트: Characters/surtr.md·20_codes.yaml(P281/N81/U81).
- N: 단일 적·접촉·대검 베기. 12차 승인 준비/내려베기·대검 각도 유지.
- U: 적 전체·비접촉·베기/불 부착. P: 실제 공격 피해 뒤 아군 전체·비접촉·보호막.
- 12차 새 등록본을 참조. 아호게·장발 제외, 어깨 대검·붉은 장발·의상/표정 유지. 추가 소품 없음.
- 전 IDLE: crownY=460, chinY=760, (1220-crown)/(chin-crown)=2.5333. 12차 승인 등록본 재측정; 아호게·장발 제외, 기존 약 2.58 대신 2.533 실측
- 목표 배율 근거: 동일 crown~발 길이에서 머리 0.9453배(-5.47%) → 2.68/37.3%. 프롬프트로만 비율 수정; 부위별 기하 변형 없음.
- 후 IDLE: crownY=465, chinY=747, 머리 282px, 2.6773등신 / 머리 비중 37.35% (내부 2.62~2.74). 수동 판독 ±8px.
- 머리 √(w·h), IDLE 대비: IDLE 313.3px(0.00%) / WINDUP 313.3px(+0.01%) / STRIKE 320.6px(+2.34%) / HIT 312.8px(-0.14%) / DOWN 313.2px(-0.02%)
- 재생성 3회(최초 생성 제외): IDLE 0 / WINDUP 1 / STRIKE 0 / HIT 1 / DOWN 1. 모든 포즈 총 생성 3회 이하.
- 후처리: PowerShell System.Drawing 전체 균등 배율·LockBits 정수 이동만. 머리 면적 편차를 기준으로 포즈 이미지 전체 배율만 보정; 원시 생성본은 _work에 보존.
- 실측 5장: 1024×1280, PNG 8bit RGBA, 네 모서리 alpha 0, alpha≥129 최하단 모두 y1220, x30..994 내. 우향·배경/VFX/글자/흰 외곽 없음 자체 육안 확인.
- 실제 프롬프트: _work_SURTR/generation_all.json (내장 image_gen, transparent background, 해당 포즈 등록본+새 IDLE 참조; 고유 이름 없이 외형·2.68등신·머리 37.3% 명시).
- 산출: 루트 5 PNG·registered 사본·sheets 5종·measure_SURTR.png·lineup_q1.png. 측정 ROI/변환/해시는 _work_SURTR/final_measure.json.
- 남은 결함/검수점: STRIKE 머리 면적 기준 +2.34%(허용 내). 붉은 머리 광택·금장 문양 미세차. 12차 승인 포즈의 큰 각도 변화 없음.
- 등록·Unity 실행/Play 검증은 미실행. 최종 승인 아님. Assets 등록본 25장 해시 변화 0; 다른 대상·이전 회차 수정/삭제/커밋 없음.
