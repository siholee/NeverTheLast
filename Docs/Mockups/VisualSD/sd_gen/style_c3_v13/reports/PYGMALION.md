# PYGMALION · p4 추가 수정 · 2026-11-06
상태: **검수 대기** — IDLE 목표 2.66~2.72 충족, Root 최종 판정 전.
이전 HOLD1 2.7557 / HOLD2 2.7816 → 새 IDLE **2.7013등신**.
등록본 재측정 기준은 crown137/chin507, 2.9270등신. 최종 새 IDLE은 crownY180/chinY565, 두개부 높이385px, 정수리~발1040px.
접근: 머리·넓은 웨이브를 유지하고 hip-to-sole 한 머리 길이·작은 발을 명시해 가늘고 긴 레깅스 다리를 단축.
IDLE 먼저 확정 후 나머지4장을 그 IDLE+각 등록 포즈 참조로 생성. 서 있는 IDLE/WINDUP/STRIKE 정수리~발=1040/1044/1040px(최대 차이0.38%).
HIT 990px·DOWN 895px는 원래의 무릎 굽힘·앉은 자세를 유지한 결과. 높이를 억지로 늘리는 부분 변형 없음.
N20: 적 단일 / 비접촉 / 빈손 원격 주문. 준비의 올린 손·정점의 낮게 펼친 손 유지.
U20: 자신 / 집중·보호 및 접촉 공격 반응 / 가시·맹독 반사. P220: 아군·소환수 피해 오라. SP·별도 소환수 그림 없음.
생성: 내장 image_gen, transparent background. 인물 이름 없이 외형·포즈·짧은 다리·동일 머리 및 웨이브 유지 지시.
실제 전문 프롬프트는 _work_PYGMALION/prompts.txt. 이번 추가 시도 IDLE/WINDUP/STRIKE/HIT/DOWN=3/1/1/1/1, 총7회·재생성2회(IDLE).
머리 w×h / √면적 / IDLE 대비: IDLE470×385 /425.38 /0%; WINDUP486×373 /425.77 /+0.09%.
STRIKE460×388 /422.47 /−0.68%; HIT470×373 /418.70 /−1.57%; DOWN460×403 /430.56 /+1.22%.
5장 모두 1024×1280 8bit RGBA, 모서리 alpha0, alpha≥129 최하단 y1220, 가로30..994 안. 측정 CSV·두개부 ROI 시트 보존.
후처리: PowerShell System.Drawing 전체 균등 배율1회·LockBits 정수 이동만. 몸 부위별 기하 변형·합성 없음.
직접 확인: poses·silhouette·face·battle250·before_after·measure·lineup. 서 있는 세 포즈의 몸 높이와 짧은 다리, 각 행동·얼굴·의상·굵은 웨이브 유지 확인.
남은 결함: 아이라인·소매주름·스트랩·머리 끝 세부 미세 차이. 중대 잘림·흰 외곽·곱슬 증가 미발견. 두개부 수동 측정 불확도±10px.
HOLD1 보존 폴더 _superseded_hold1/ 유지. HOLD2 결과·작업46개는 _work_PYGMALION/_superseded_hold2/로 이동 보존. 탈락본 포함 삭제 없음.
THOTH·SHAKESPEARE·NEITH 관련107개 파일 및 아군 등록본5장의 SHA256 불변 확인. 수정은 PYGMALION 산출물과 묶음 lineup뿐.
Assets/·Library/ 쓰기·Unity 실행·커밋 없음. 최종 미술 승인·게임 등록은 수행하지 않음.
