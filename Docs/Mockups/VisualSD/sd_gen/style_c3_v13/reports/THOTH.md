# THOTH · p4 · 2026-11-06
상태: **검수 대기**
IDLE 전: crownY 300 / chinY 614 / 2.9299등신. 픽셀 격자 재확인, 아호게 제외.
목표 계산: 고정 높이 920px / 2.68 = 머리 343.28px, 기존 314px 대비 ×1.0933.
판단: 머리 확대와 몸통·다리 단축. STRIKE 날개 가로폭 때문에 최종 IDLE 포함 공통 머리 크기를 전체 균등 축소로 조율.
IDLE 후: crownY **363** / chinY **681** / 머리 높이 318px / **2.6950등신**.
N124: 아군 전체·소환수 / 비접촉 / 치유. 서판·첨필 지원 자세 유지.
U124: 자신 / 비접촉 / 서기관 소환·수명 갱신. 별도 서기관 그림은 변경하지 않음.
P324: 아군·소환수 / 비접촉 / 소환 피해 증폭·피해 감소 오라. SP 없음.
생성: 내장 image_gen, 포즈별 등록본이 편집 대상. 나머지 4포즈는 새 IDLE도 비율 참조.
프롬프트: same pose/outfit/face; larger skull, shorter torso/legs; 2.68 heads; transparent. 실제 전문은 _work_THOTH/prompts.txt.
시도 IDLE/WINDUP/STRIKE/HIT/DOWN = 3/1/1/1/1, 재생성 2회(IDLE).
머리 w×h / √면적 / IDLE 대비: IDLE 373×318 / 344.40 / 0%; WINDUP 367×323 / 344.30 / −0.03%.
STRIKE 356×320 / 337.52 / −2.00%; HIT 375×316 / 344.24 / −0.05%; DOWN 361×328 / 344.10 / −0.09%.
후처리: PowerShell System.Drawing 전체 균등 배율 1회 + LockBits 정수 이동. 부위 변형·합성 없음.
5장 모두 1024×1280, PNG 8bit RGBA, 모서리 alpha 0, alpha≥129 최하단 y1220; x 범위 32..992 안.
직접 확인: poses·silhouette·face·battle250·before_after·lineup 및 측정선. 250px에서 머리 크기 일관, 우향·원래 포즈 보존.
남은 결함: 흰 앞머리 결·깃털·금장 미세 차이. 중대 잘림·흰 테두리 미발견. 두개부 수동 측정 불확도 약 ±5px.
산출: measure_THOTH.png, sheets/THOTH_*.png, _work_THOTH/measurements.csv·head_measurements.png.
Assets/·Library/ 쓰기, Unity 실행, 커밋, 삭제 없음. Root 최종 검수·등록 전 단계.
