# THESEUS · SD 14차 q1

2026-11-06 · 상태: **검수 대기**

- 키트: Characters/theseus.md·20_codes.yaml(P221/N21/U21).
- N: 단일 적·비접촉·베기. 검 준비/전방 베기 승인 포즈 유지.
- U: 단일 적·비접촉·베기+자기 치유. P: 일반행동 뒤 적 전체·비접촉·파장 주문.
- 아호게·낱머리 제외. 안경·녹안·크림/군청 의상·검 각도 유지. 추가 소품/키트 차이 없음.
- 전 IDLE: crownY=380, chinY=710, (1220-crown)/(chin-crown)=2.5455. 상단 아호게와 낱머리 제외
- 목표 배율 근거: 동일 crown~발 길이에서 머리 0.9498배(-5.02%) → 2.68/37.3%. 프롬프트로만 비율 수정; 부위별 기하 변형 없음.
- 후 IDLE: crownY=420, chinY=715, 머리 295px, 2.7119등신 / 머리 비중 36.88% (내부 2.62~2.74). 수동 판독 ±8px.
- 머리 √(w·h), IDLE 대비: IDLE 335.3px(0.00%) / WINDUP 334.6px(-0.20%) / STRIKE 335.2px(-0.02%) / HIT 335.8px(+0.17%) / DOWN 337.1px(+0.56%)
- 재생성 3회(최초 생성 제외): IDLE 0 / WINDUP 1 / STRIKE 1 / HIT 1 / DOWN 0. 모든 포즈 총 생성 3회 이하.
- 후처리: PowerShell System.Drawing 전체 균등 배율·LockBits 정수 이동만. 머리 면적 편차를 기준으로 포즈 이미지 전체 배율만 보정; 원시 생성본은 _work에 보존.
- 실측 5장: 1024×1280, PNG 8bit RGBA, 네 모서리 alpha 0, alpha≥129 최하단 모두 y1220, x30..994 내. 우향·배경/VFX/글자/흰 외곽 없음 자체 육안 확인.
- 실제 프롬프트: _work_THESEUS/generation_all.json (내장 image_gen, transparent background, 해당 포즈 등록본+새 IDLE 참조; 고유 이름 없이 외형·2.68등신·머리 37.3% 명시).
- 산출: 루트 5 PNG·registered 사본·sheets 5종·measure_THESEUS.png·lineup_q1.png. 측정 ROI/변환/해시는 _work_THESEUS/final_measure.json.
- 남은 결함/검수점: 승인 포즈 유지로 IDLE/HIT 외곽 실루엣이 유사함(가슴 손짓 차이). 망토 금장 잎 문양의 밀도 미세차.
- 등록·Unity 실행/Play 검증은 미실행. 최종 승인 아님. Assets 등록본 25장 해시 변화 0; 다른 대상·이전 회차 수정/삭제/커밋 없음.
