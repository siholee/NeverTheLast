# SURYA · SD 비율 v13 p3 · 2026-11-06
상태: **검수 대기**
N67: 적 단일 / 비접촉 / 리볼버 사격. P267: 적 단일 / 비접촉 / 종언 표식.
SP67: 적 전체 / 비접촉 / 사격. U67: 적 전체 표식·자신 공격 강화 / 비접촉 / 무피해 준비.
기존 모자 챙 준비·한 손 사격·피격·옆으로 앉는 DOWN 유지.
전 IDLE: crownY=170, chinY=510, feetY=1220, 3.0882등신.
목표2.68: 고정 신장 가정 머리 배율 1.1523(+15.23%); 실제는 머리 확대·몸통/다리 단축 재작화.
후 IDLE: crownY=219, chinY=596, head=377px, 2.6552등신(합격2.60~2.76). 모자로 가려진 두개부 윤곽 추정, 오차 ±12px.
생성 I/W/S/H/D=3/1/1/1/1; 재생성 총2회, 포즈당 최대3회 준수.
방식/프롬프트: 내장 image_gen, transparent_background=true. same pose/outfit/face, larger head + shorter torso/legs, 2.68-head chibi; 외형 묘사만 사용.
전체 프롬프트·생성 경로: ../_work_SURYA/prompts.json. 각 등록 포즈 + 새 IDLE 참조; IDLE은 등록 IDLE 참조. 재생성은 이전 후보도 참조.
후처리: PowerShell System.Drawing; 전체 균등 배율 후 LockBits 정수 행 복사로 발 정렬. 부위별 변형·합성·알파 삭제 없음.
머리 ROI 폭×높이→√(w×h): IDLE:510x377→438.49 / WINDUP:518x371→438.38 / STRIKE:514x374→438.45 / HIT:523x367→438.11 / DOWN:523x368→438.71
IDLE 대비 머리 편차: IDLE:0.00% / WINDUP:-0.02% / STRIKE:-0.01% / HIT:-0.09% / DOWN:0.05%
규격 실측 I/W/S/H/D: 발1220/1220/1220/1220/1220; 5장1024×1280 8bit RGBA·모서리alpha0·alpha≥129 x30..994.
산출: ../measure_SURYA.png, ../sheets/SURYA_{poses,silhouette,face,battle250,before_after}.png, ../lineup_p3.png.
남은 결함/검토: 자수·체인·브로치 세부 재해석. HIT 실루엣이 IDLE에 가까운 점은 기존 승인 동작 유지 결과.
직접 확인: 측정선·머리 ROI·포즈·실루엣·얼굴·전후·250px lineup. 수치 근거 ../_work_SURYA/{before_measure,after_measure,metrics}.json.
검증 범위: 정지 PNG/시트 자체 검토. 최종 Root 승인·등록·Unity Play 검증 미실시. Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음.
