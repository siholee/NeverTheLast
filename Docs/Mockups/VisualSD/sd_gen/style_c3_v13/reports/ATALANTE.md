# ATALANTE · SD 비율 v13 p3 · 2026-11-06
상태: **검수 대기**
N165: 적 단일 / 비접촉 / 활 사격·맹독. P365: 아군 지속피해 증폭·적 단일 비접촉 추격.
U165: 적 전체 / 비접촉 / 활 사격·맹독. SP 없음.
기존 짧은 리커브 조준·방출·피격·주저앉는 포즈 유지.
전 IDLE: crownY=127, chinY=480, feetY=1220, 3.0963등신.
목표2.68: 고정 신장 가정 머리 배율 1.1553(+15.53%); 실제는 머리 확대·몸통/다리 단축 재작화.
후 IDLE: crownY=204, chinY=584, head=380px, 2.6737등신(합격2.60~2.76). 뜬 머리·아호게·무기 제외, 수동 경계 오차 ±8px.
생성 I/W/S/H/D=2/1/1/1/1; 재생성 총1회, 포즈당 최대3회 준수.
방식/프롬프트: 내장 image_gen, transparent_background=true. same pose/outfit/face, larger head + shorter torso/legs, 2.68-head chibi; 외형 묘사만 사용.
전체 프롬프트·생성 경로: ../_work_ATALANTE/prompts.json. 각 등록 포즈 + 새 IDLE 참조; IDLE은 등록 IDLE 참조. 재생성은 이전 후보도 참조.
후처리: PowerShell System.Drawing; 전체 균등 배율 후 LockBits 정수 행 복사로 발 정렬. 부위별 변형·합성·알파 삭제 없음.
머리 ROI 폭×높이→√(w×h): IDLE:450x382→414.61 / WINDUP:438x393→414.89 / STRIKE:451x382→415.07 / HIT:454x379→414.81 / DOWN:448x384→414.77
IDLE 대비 머리 편차: IDLE:0.00% / WINDUP:0.07% / STRIKE:0.11% / HIT:0.05% / DOWN:0.04%
규격 실측 I/W/S/H/D: 발1220/1220/1220/1220/1220; 5장1024×1280 8bit RGBA·모서리alpha0·alpha≥129 x30..994.
산출: ../measure_ATALANTE.png, ../sheets/ATALANTE_{poses,silhouette,face,battle250,before_after}.png, ../lineup_p3.png.
남은 결함/검토: 땋은 머리 묶음·부츠 끈·가방 세부가 미세 재해석됨. 치명 결함은 자체 정지화면 검토에서 미발견.
직접 확인: 측정선·머리 ROI·포즈·실루엣·얼굴·전후·250px lineup. 수치 근거 ../_work_ATALANTE/{before_measure,after_measure,metrics}.json.
검증 범위: 정지 PNG/시트 자체 검토. 최종 Root 승인·등록·Unity Play 검증 미실시. Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음.
