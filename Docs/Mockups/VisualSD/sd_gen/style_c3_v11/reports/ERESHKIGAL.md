# ERESHKIGAL · SD v11 f2 Root HOLD 교정 · 2026-11-05
상태: **재검수 대기**
Root 요청: 전신 축소 대신 몸을 등록본 수준으로 복원하고 머리만 약10% 축소. 허용된 머리 분리·합성 방식으로 교정.
몸: IDLE/HIT/DOWN은 Assets 등록본 몸을 1.00배로 복원. 저장 후 재읽기에서 각각 y850/y850/y950 아래 몸 픽셀 차이0 확인. WINDUP/STRIKE는 기존 등불 동작의 몸만 발 고정 1.03614배로 복원.
머리: 등록본 머리·단발 레이어만 전5포즈 0.90배. IDLE/WINDUP/STRIKE는 같은 IDLE 머리, HIT/DOWN은 각 등록본 머리 사용. 목·깃은 원본 픽셀로 연결하고 작은 알파 틈을 정리.
키트 N167: 적 단일·비접촉·주문/출혈. U167: 아군 전체/자신 치유+적 전체 출혈·비접촉. P367: 아군 전체·비접촉·지속피해 연동 치유. SP 없음.
머리 수동 ROI 폭×높이→√(w×h): IDLE/WINDUP/STRIKE 328×320→323.98px / HIT315×333→323.87px / DOWN335×313→323.81px. IDLE 대비0/0/0/-0.031/-0.050%.
머리/전신 비율(두개부 높이÷두개부 정수리~발, 등록본 기준자세→교정): IDLE/WINDUP/STRIKE 0.436→0.413 / HIT0.432→0.407 / DOWN0.483→0.456. 반동·좌식 투영 차이를 같은 등신 차이로 해석하지 않음.
발~어깨 높이(수동 투영 랜드마크): IDLE/WINDUP/STRIKE430px / HIT510px / DOWN429px. 기본 준비·발동의 어깨y790, 발y1220을 공통 유지.
기술: 전5장 1024×1280·PNG8bit RGBA(type6)·모서리alpha0·alpha≥129 최하단y1220. 우향 유지, 발 맞춤은 LockBits 정수 행 이동.
행동: WINDUP 가슴 높이 준비→STRIKE 턱 아래 등불 들기 유지. 손·등불은 머리 앞에 복원해 소품이 머리에 가려지지 않게 함. 과장된 머리 위 동작 없음.
산출: poses/silhouette/face/battle250/before_after 5종 갱신. before_after는 등록본/HOLD/교정3행 동일배율. _work_ERESHKIGAL/root_fix/seams_2x.png에 목·깃·손·등불 2배 확대 보존.
방식: PowerShell System.Drawing 합성·리샘플·알파 경계 정리만 사용, 이번 image_gen 추가 호출0. 이전본은 root_fix/before와 작업 후보에 보존.
남은 검토: 수동 두개부/어깨 경계의 독립 확인, 목·깃 합성의 확대 최종 판정, IDLE/WINDUP 단색 차이 약함. 전체 미술 승인으로 보고하지 않음.
근거: _work_ERESHKIGAL/root_fix/{compose_fix,sheets_fix,audit_fix}.ps1·metrics.json 및 Logs/SDFixV11_20261105/codex_f2_fix_metrics.json. 이전 회차 수치 대신 이번 근거를 사용.
보존: Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음. 등록본5장 해시 불변. 다른 대상은 수정하지 않음; 동시 진행된 SET STRIKE 변경은 관찰만 하고 보존.
