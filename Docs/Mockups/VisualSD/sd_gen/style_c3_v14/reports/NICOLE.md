# NICOLE · SD 14차 q1

2026-11-06 · 상태: **검수 대기**

- 키트: Characters/nicol.md·20_codes.yaml(P368/N168/U168).
- N: 생존 적 사이 6회 바운스·비접촉·번개 주문. 회중시계 준비/전방 제시 포즈 유지.
- U: 적 전체·비접촉·번개 주문/방어 감소. P: 아군 전체·비접촉·피해 증폭/경감 버프.
- 고글은 crown에서 제외. 원래 얼굴·복장·회중시계와 우향 유지. 추가 소품/키트 차이 없음.
- 전 IDLE: crownY=117, chinY=570, (1220-crown)/(chin-crown)=2.4349. 기존 CSV chinY=555와 달리 실제 턱 최하단 570 재측정, 고글 제외
- 목표 배율 근거: 동일 crown~발 길이에서 머리 0.9085배(-9.15%) → 2.68/37.3%. 프롬프트로만 비율 수정; 부위별 기하 변형 없음.
- 후 IDLE: crownY=159, chinY=560, 머리 401px, 2.6459등신 / 머리 비중 37.79% (내부 2.62~2.74). 수동 판독 ±8px.
- 머리 √(w·h), IDLE 대비: IDLE 466.2px(0.00%) / WINDUP 473.9px(+1.64%) / STRIKE 456.2px(-2.15%) / HIT 469.3px(+0.66%) / DOWN 466.2px(0.00%)
- 재생성 2회(최초 생성 제외): IDLE 2 / WINDUP 0 / STRIKE 0 / HIT 0 / DOWN 0. 모든 포즈 총 생성 3회 이하.
- 후처리: PowerShell System.Drawing 전체 균등 배율·LockBits 정수 이동만. 머리 면적 편차를 기준으로 포즈 이미지 전체 배율만 보정; 원시 생성본은 _work에 보존.
- 실측 5장: 1024×1280, PNG 8bit RGBA, 네 모서리 alpha 0, alpha≥129 최하단 모두 y1220, x30..994 내. 우향·배경/VFX/글자/흰 외곽 없음 자체 육안 확인.
- 실제 프롬프트: _work_NICOLE/generation_all.json (내장 image_gen, transparent background, 해당 포즈 등록본+새 IDLE 참조; 고유 이름 없이 외형·2.68등신·머리 37.3% 명시).
- 산출: 루트 5 PNG·registered 사본·sheets 5종·measure_NICOLE.png·lineup_q1.png. 측정 ROI/변환/해시는 _work_NICOLE/final_measure.json.
- 남은 결함/검수점: HIT의 바깥 머리카락 덩어리는 여전히 넓게 보임. 체인·기어 톱니와 금장 세부 미세차. 중대 규격 결함은 자체 정지화면 검사에서 미발견.
- 등록·Unity 실행/Play 검증은 미실행. 최종 승인 아님. Assets 등록본 25장 해시 변화 0; 다른 대상·이전 회차 수정/삭제/커밋 없음.
