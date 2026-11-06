# SHI · SD 비율 13차 p1

- 상태: 검수 대기 · 2026-11-06
- 키트: N2 적 단일·비접촉·베기(발도); 처치 시 특수 재행동.
- 키트: U2 상시형·적 단일/전체·비접촉·만종 후속 피해; P202 처치각 특수 멸참.
- 키트: 별도 능동 SP 없음. STRIKE 12차 칼끝-칼집 입구의 발도 순간 유지; DOWN 페이드는 런타임 효과.
- 기준: 현행 해당 포즈 등록본·Standings/Portraits·캐릭터 상세·20_codes.yaml. 포즈/의상/얼굴 동일, 비율만 변경 요청.
- 전 IDLE 재측정: crownY=208, chinY=618, feetY=1220, (1220-208)/(618-208)=2.4683등신. 두개부 윤곽은 12차 값과 일치(수동 오차 ±10px).
- 목표 2.68: 몸 길이 고정 시 머리 배율 (전 등신−1)/1.68=0.8740; 전체 높이 고정 비교값 전/2.68=0.9210. 머리 축소 우선; 부위 기하 합성 없음.
- 후 IDLE: crownY=291, chinY=638, feetY=1220; (1220−291)/(638−291)=2.6772등신. 합격 범위 2.60~2.76.
- 생성: 내장 image_gen, transparent background. 실제 외형 묘사·same pose/outfit·smaller head·2.68-head; 고유명 없는 프롬프트 전문 *_prompt.txt.
- 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/1/2/2/1; 재생성 0/0/1/1/0, 합계 2회.
- 후처리: PowerShell System.Drawing 전체 균등 배율 1회 + LockBits 정수 이동. 발 y 실측 IDLE/WINDUP/STRIKE/HIT/DOWN = 1220/1220/1220/1220/1220.
- 파일 실측 5장: 1024×1280, PNG 8bit RGBA(type6), 네 모서리 alpha0, alpha≥129 가로 30..994 안. p1_pixel_checks.csv 참조.
- 머리 ROI √(w·h) 실측(px): IDLE:403x347=373.95; WINDUP:402x348=374.03; STRIKE:399x352=374.76; HIT:397x353=374.35; DOWN:387x362=374.29
- IDLE 대비 머리 수치 편차 최대 0.217%; 좌표 반올림값 기준, 수동 경계 오차 ±10px 별도. final_measure.csv·measure_KEY.png에 근거.
- 직접 확인: poses/silhouette/face/battle250/before_after·정수리 정규화 lineup. 승인·등록 판단은 별도 최종 검수.
- 남은 결함: STRIKE와 WINDUP이 IDLE보다 조금 길게 읽힘. 발도 접점·칼 길이와 머리 장식 최종 검수 필요. HIT 2번으로 몸 길이 개선.
- 파일 등록/Assets·Library 쓰기/Unity 실행/커밋 없음. 작업 소유 파일 삭제 없음.
