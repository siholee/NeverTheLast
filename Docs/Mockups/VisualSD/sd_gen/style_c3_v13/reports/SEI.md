# SEI · SD 비율 13차 p1

- 상태: 검수 대기 · 2026-11-06
- 키트: N1 적 단일·비접촉·주문/방어력 감소(손끝 지목).
- 키트: U1 아군 전체·비접촉·보호막/강화/집중; P201 무작위 적 암편/아군 보호막.
- 키트: SP 없음. 초월자 정숙·절도 톤 유지.
- 기준: 현행 해당 포즈 등록본·Standings/Portraits·캐릭터 상세·20_codes.yaml. 포즈/의상/얼굴 동일, 비율만 변경 요청.
- 전 IDLE 재측정: crownY=285, chinY=665, feetY=1220, (1220-285)/(665-285)=2.4605등신. 두개부 윤곽은 12차 값과 일치(수동 오차 ±10px).
- 목표 2.68: 몸 길이 고정 시 머리 배율 (전 등신−1)/1.68=0.8694; 전체 높이 고정 비교값 전/2.68=0.9181. 머리 축소 우선; 부위 기하 합성 없음.
- 후 IDLE: crownY=252, chinY=614, feetY=1220; (1220−252)/(614−252)=2.6740등신. 합격 범위 2.60~2.76.
- 생성: 내장 image_gen, transparent background. 실제 외형 묘사·same pose/outfit·smaller head·2.68-head; 고유명 없는 프롬프트 전문 *_prompt.txt.
- 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/1/1/1/1; 재생성 0/0/0/0/0, 합계 0회.
- 후처리: PowerShell System.Drawing 전체 균등 배율 1회 + LockBits 정수 이동. 발 y 실측 IDLE/WINDUP/STRIKE/HIT/DOWN = 1220/1220/1220/1220/1220.
- 파일 실측 5장: 1024×1280, PNG 8bit RGBA(type6), 네 모서리 alpha0, alpha≥129 가로 30..994 안. p1_pixel_checks.csv 참조.
- 머리 ROI √(w·h) 실측(px): IDLE:434x362=396.37; WINDUP:432x363=396.00; STRIKE:436x361=396.73; HIT:408x386=396.85; DOWN:417x377=396.50
- IDLE 대비 머리 수치 편차 최대 0.121%; 좌표 반올림값 기준, 수동 경계 오차 ±10px 별도. final_measure.csv·measure_KEY.png에 근거.
- 직접 확인: poses/silhouette/face/battle250/before_after·정수리 정규화 lineup. 승인·등록 판단은 별도 최종 검수.
- 남은 결함: 큰 결함 미발견. WINDUP/STRIKE의 몸 길이·소매 자수 미세차, HIT 시선과 정숙 톤 최종 검수 필요.
- 파일 등록/Assets·Library 쓰기/Unity 실행/커밋 없음. 작업 소유 파일 삭제 없음.
