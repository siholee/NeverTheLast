# SHAKESPEARE · p4 · 2026-11-06
상태: **검수 대기**
IDLE 전: crownY 115 / chinY 495 / 2.9079등신. 등록본 픽셀 격자 재확인, 리본·뜬 머리 제외.
목표 계산: 고정 높이 1105px / 2.68 = 머리 412.31px, 기존 380px 대비 ×1.0850.
판단: 머리 확대와 몸통·다리 단축. 몸 높이를 IDLE 기준으로 고정해 각 포즈의 머리를 전체 균등 배율로 맞춤.
IDLE 후: crownY **115** / chinY **522** / 머리 높이 407px / **2.7150등신**.
N10: 자신 / 비접촉 / 언어 축적(공격 없음). 셀카 준비·점프 셀카 동작과 셀카봉 유지.
U10: 아군 소환 개체 / 비접촉 / 소환 피해 증폭·치명상 방지 결계.
P210: 아군 소환 관련 / 비접촉 / 언어 획득·확정 치명 지원. SP 없음.
생성: 내장 image_gen, 각 등록 포즈 편집 대상 + 새 IDLE 비율 참조.
프롬프트: exact pose/outfit/face/selfie-stick; modest larger skull, shorter body; broad waves, no extra curls; transparent. 전문은 _work_SHAKESPEARE/prompts.txt.
시도 IDLE/WINDUP/STRIKE/HIT/DOWN = 1/1/1/1/2, 재생성 1회(DOWN 첫 본 가장자리 잘림 의심).
머리 w×h / √면적 / IDLE 대비: IDLE 440×407 / 423.18 / 0%; WINDUP 447×400 / 422.85 / −0.08%.
STRIKE 441×406 / 423.14 / −0.01%; HIT 445×402 / 422.95 / −0.05%; DOWN 438×408 / 422.73 / −0.11%.
후처리: PowerShell System.Drawing 전체 균등 배율 1회 + LockBits 정수 이동. 부위 변형·합성 없음.
5장 모두 1024×1280, PNG 8bit RGBA, 모서리 alpha 0, alpha≥129 최하단 y1220; x 범위 32..992 안.
직접 확인: 5종 시트·lineup·측정선. 셀카봉·휴대폰 방향과 점프 다리·V손 유지. 웨이브 증가 미발견.
남은 결함: 왕관·하트·체크·머리핀 세부 미세 차이. 선택 DOWN은 잘림 해소. 두개부 수동 측정 불확도 약 ±5px.
산출: measure_SHAKESPEARE.png, sheets/SHAKESPEARE_*.png, _work_SHAKESPEARE/measurements.csv·head_measurements.png.
Assets/·Library/ 쓰기, Unity 실행, 커밋, 삭제 없음. Root 최종 검수·등록 전 단계.
