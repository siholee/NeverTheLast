# INDRA · SD 14차 q1

2026-11-06 · 상태: **검수 대기**

- 키트: Characters/indra.md·20_codes.yaml(P263/N63/SP63/U63).
- N: 단일 적·비접촉·번개 주문. 바즈라 준비/전방 지시 승인 포즈 유지.
- SP: 적 전체·비접촉·주문. U: 아군 참여자 특수행동 소집·비접촉·피해 없음. P: 아군 전체 특수 피해 버프/자기 마나.
- 관 장식은 crown 제외. 은자색 머리·자안·관/금장·홀·소매/시선 유지. 추가 소품/키트 차이 없음.
- 전 IDLE: crownY=433, chinY=737, (1220-crown)/(chin-crown)=2.5888. 관 뿔과 보석 제외
- 목표 배율 근거: 동일 crown~발 길이에서 머리 0.9660배(-3.40%) → 2.68/37.3%. 프롬프트로만 비율 수정; 부위별 기하 변형 없음.
- 후 IDLE: crownY=466, chinY=742, 머리 276px, 2.7319등신 / 머리 비중 36.60% (내부 2.62~2.74). 수동 판독 ±8px.
- 머리 √(w·h), IDLE 대비: IDLE 313.5px(0.00%) / WINDUP 312.9px(-0.18%) / STRIKE 313.3px(-0.04%) / HIT 313.0px(-0.14%) / DOWN 313.5px(+0.01%)
- 재생성 3회(최초 생성 제외): IDLE 0 / WINDUP 1 / STRIKE 1 / HIT 1 / DOWN 0. 모든 포즈 총 생성 3회 이하.
- 후처리: PowerShell System.Drawing 전체 균등 배율·LockBits 정수 이동만. 머리 면적 편차를 기준으로 포즈 이미지 전체 배율만 보정; 원시 생성본은 _work에 보존.
- 실측 5장: 1024×1280, PNG 8bit RGBA, 네 모서리 alpha 0, alpha≥129 최하단 모두 y1220, x30..994 내. 우향·배경/VFX/글자/흰 외곽 없음 자체 육안 확인.
- 실제 프롬프트: _work_INDRA/generation_all.json (내장 image_gen, transparent background, 해당 포즈 등록본+새 IDLE 참조; 고유 이름 없이 외형·2.68등신·머리 37.3% 명시).
- 산출: 루트 5 PNG·registered 사본·sheets 5종·measure_INDRA.png·lineup_q1.png. 측정 ROI/변환/해시는 _work_INDRA/final_measure.json.
- 남은 결함/검수점: IDLE 2.732로 내부 상한 2.74에 가까움. 관 가림 때문에 crown 수동 판독 오차 존재. 소매/어깨 금장 세부 미세차.
- 등록·Unity 실행/Play 검증은 미실행. 최종 승인 아님. Assets 등록본 25장 해시 변화 0; 다른 대상·이전 회차 수정/삭제/커밋 없음.
