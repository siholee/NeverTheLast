# ASTOLFO · SD 비율 v13 p3 · 2026-11-06
상태: **검수 대기**
N162: 적 전체 / 비접촉 / 무피해 도발·피해 감소·행동 가속. P362: 자신 / 비접촉 / 소모품 보존·우선도 감소.
U162: 아군 전체 / 비접촉 / 추가행동·반격 치피 지원·치유. SP 없음.
12차 승인 WINDUP(깃창 뒤로 당겨 감기)·STRIKE(몸 무게 전진) 유지. N 비공격과 찌르기 그림 차이는 사용자 승인 예외.
전 IDLE: crownY=280, chinY=592, feetY=1220, 3.0128등신.
목표2.68: 고정 신장 가정 머리 배율 1.1242(+12.42%); 실제는 머리 확대·몸통/다리 단축 재작화.
후 IDLE: crownY=261, chinY=611, head=350px, 2.7400등신(합격2.60~2.76). 뜬 머리·아호게·무기 제외, 수동 경계 오차 ±8px.
생성 I/W/S/H/D=1/1/1/1/1; 재생성 총0회, 포즈당 최대3회 준수.
방식/프롬프트: 내장 image_gen, transparent_background=true. same pose/outfit/face, larger head + shorter torso/legs, 2.68-head chibi; 외형 묘사만 사용.
전체 프롬프트·생성 경로: ../_work_ASTOLFO/prompts.json. 각 등록 포즈 + 새 IDLE 참조; IDLE은 등록 IDLE 참조. 재생성은 이전 후보도 참조.
후처리: PowerShell System.Drawing; 전체 균등 배율 후 LockBits 정수 행 복사로 발 정렬. 부위별 변형·합성·알파 삭제 없음.
머리 ROI 폭×높이→√(w×h): IDLE:486x351→413.02 / WINDUP:488x349→412.69 / STRIKE:477x357→412.66 / HIT:491x347→412.77 / DOWN:468x364→412.74
IDLE 대비 머리 편차: IDLE:0.00% / WINDUP:-0.08% / STRIKE:-0.09% / HIT:-0.06% / DOWN:-0.07%
규격 실측 I/W/S/H/D: 발1220/1220/1220/1220/1220; 5장1024×1280 8bit RGBA·모서리alpha0·alpha≥129 x30..994.
산출: ../measure_ASTOLFO.png, ../sheets/ASTOLFO_{poses,silhouette,face,battle250,before_after}.png, ../lineup_p3.png.
남은 결함/검토: 깃발 끝·금장 문양 미세 차이. N 비공격/찌르기 그림 차이는 승인 예외이며 런타임 판정 변경 없음.
직접 확인: 측정선·머리 ROI·포즈·실루엣·얼굴·전후·250px lineup. 수치 근거 ../_work_ASTOLFO/{before_measure,after_measure,metrics}.json.
검증 범위: 정지 PNG/시트 자체 검토. 최종 Root 승인·등록·Unity Play 검증 미실시. Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음.
