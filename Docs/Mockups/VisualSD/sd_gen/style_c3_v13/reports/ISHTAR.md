# ISHTAR · SD 비율 13차 p1

- 상태: 검수 대기 · 2026-11-06
- 키트: N220 적 단일·비접촉·볼트액션 장총 사격.
- 키트: U220 적 단일 연사·비접촉·재보 소비; P420 자신 재보 소비에 초월기 강화.
- 키트: SP 없음. 실제 탄·섬광은 이미지에 추가하지 않음.
- 기준: 현행 해당 포즈 등록본·Standings/Portraits·캐릭터 상세·20_codes.yaml. 포즈/의상/얼굴 동일, 비율만 변경 요청.
- 전 IDLE 재측정: crownY=359, chinY=711, feetY=1220, (1220-359)/(711-359)=2.4460등신. 두개부 윤곽은 12차 값과 일치(수동 오차 ±10px).
- 목표 2.68: 몸 길이 고정 시 머리 배율 (전 등신−1)/1.68=0.8607; 전체 높이 고정 비교값 전/2.68=0.9127. 머리 축소 우선; 부위 기하 합성 없음.
- 후 IDLE: crownY=381, chinY=692, feetY=1220; (1220−381)/(692−381)=2.6977등신. 합격 범위 2.60~2.76.
- 생성: 내장 image_gen, transparent background. 실제 외형 묘사·same pose/outfit·smaller head·2.68-head; 고유명 없는 프롬프트 전문 *_prompt.txt.
- 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 3/1/1/1/1; 재생성 2/0/0/0/0, 합계 2회.
- 후처리: PowerShell System.Drawing 전체 균등 배율 1회 + LockBits 정수 이동. 발 y 실측 IDLE/WINDUP/STRIKE/HIT/DOWN = 1220/1220/1220/1220/1220.
- 파일 실측 5장: 1024×1280, PNG 8bit RGBA(type6), 네 모서리 alpha0, alpha≥129 가로 30..994 안. p1_pixel_checks.csv 참조.
- 머리 ROI √(w·h) 실측(px): IDLE:373x311=340.59; WINDUP:380x305=340.44; STRIKE:396x292=340.05; HIT:392x295=340.06; DOWN:349x332=340.39
- IDLE 대비 머리 수치 편차 최대 0.160%; 좌표 반올림값 기준, 수동 경계 오차 ±10px 별도. final_measure.csv·measure_KEY.png에 근거.
- 직접 확인: poses/silhouette/face/battle250/before_after·정수리 정규화 lineup. 승인·등록 판단은 별도 최종 검수.
- 남은 결함: WINDUP과 DOWN이 IDLE보다 길게 읽힘. 자수·장총 소형 장식 미세차 최종 검수 필요.
- 파일 등록/Assets·Library 쓰기/Unity 실행/커밋 없음. 작업 소유 파일 삭제 없음.
