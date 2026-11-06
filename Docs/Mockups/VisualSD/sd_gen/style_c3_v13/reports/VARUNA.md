# VARUNA · SD 비율 v13 p3 · 2026-11-06
상태: **검수 대기**
N66: 적 단일 / 비접촉 / 물 부착 주문. P266: 자신 / 비접촉 / 보호막.
U66: 적 전체·아군 전체 / 비접촉 / 주문·보호막. SP66: 같은 대상 / 비접촉 / 주문·보호막(물 부착 없음).
12차 승인 판결 지시 유지. WINDUP은 새 IDLE 바이트 복사(생성0), 동일 실루엣은 사용자 승인 예외.
전 IDLE: crownY=300, chinY=613, feetY=1220, 2.9393등신.
목표2.68: 고정 신장 가정 머리 배율 1.0968(+9.68%); 실제는 머리 확대·몸통/다리 단축 재작화.
후 IDLE: crownY=166, chinY=558, head=392px, 2.6888등신(합격2.60~2.76). 뜬 머리·아호게·무기 제외, 수동 경계 오차 ±8px.
생성 I/W/S/H/D=3/0/1/1/1; 재생성 총2회, 포즈당 최대3회 준수.
방식/프롬프트: 내장 image_gen, transparent_background=true. same pose/outfit/face, larger head + shorter torso/legs, 2.68-head chibi; 외형 묘사만 사용.
전체 프롬프트·생성 경로: ../_work_VARUNA/prompts.json. 각 등록 포즈 + 새 IDLE 참조; IDLE은 등록 IDLE 참조. 재생성은 이전 후보도 참조.
후처리: PowerShell System.Drawing; 전체 균등 배율 후 LockBits 정수 행 복사로 발 정렬. 부위별 변형·합성·알파 삭제 없음.
머리 ROI 폭×높이→√(w×h): IDLE:513x392→448.44 / WINDUP:513x392→448.44 / STRIKE:526x382→448.25 / HIT:535x376→448.51 / DOWN:475x423→448.25
IDLE 대비 머리 편차: IDLE:0.00% / WINDUP:0.00% / STRIKE:-0.04% / HIT:0.02% / DOWN:-0.04%
규격 실측 I/W/S/H/D: 발1220/1220/1220/1220/1220; 5장1024×1280 8bit RGBA·모서리alpha0·alpha≥129 x30..994.
산출: ../measure_VARUNA.png, ../sheets/VARUNA_{poses,silhouette,face,battle250,before_after}.png, ../lineup_p3.png.
남은 결함/검토: 금장 자수·허리 보주·발목 장식 미세 변화. IDLE=WINDUP은 승인 예외로 유지.
직접 확인: 측정선·머리 ROI·포즈·실루엣·얼굴·전후·250px lineup. 수치 근거 ../_work_VARUNA/{before_measure,after_measure,metrics}.json.
검증 범위: 정지 PNG/시트 자체 검토. 최종 Root 승인·등록·Unity Play 검증 미실시. Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음.
