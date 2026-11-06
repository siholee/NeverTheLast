# JEAN · 잔 · SD v11 F1
수정일: 2026-11-05 · 상태: **검수 대기**
사용자 요청 반영: IDLE은 권투 가드를 없애고 허리 아래에 손을 포갠 평범한 선 자세. STRIKE는 오른어깨·골반을 앞으로 돌리고 오른주먹을 크게 뻗음.
N7: 단일 적 / 접촉 / 맨주먹 타격.
P207: 직접 공격자 / 접촉 / 선제 반격 타격.
U7: 자신 / 비접촉 / 도발·강화·경감. SP 없음.
키트 차이/해석: 사용자 대기 자세가 기존 권투 가드보다 우선. 공격 포즈는 N/P에 대응.
생성: 내장 image_gen, transparent_background=true, 등록본 IDLE을 외형 마스터로 사용.
실제 프롬프트 요약: silver-white straight-haired adult nun; calm lowered hands / anatomical right cross punch with hip and shoulder rotation; black-white habit, ice-blue motifs, articulated gauntlets; screen right, transparent, no VFX.
재생성: IDLE 1회(후보2), STRIKE 0회(후보1); 총 생성3회.
유지 포즈: 등록본을 _work_JEAN/registered_*.png에 원본 복사. 최종 5장에는 전체 등방 크기 보정만 적용(아스톨포 유지 4장은 원본 그대로).
변환: I/W/S/H/D 배율 0.864629 / 0.933056 / 0.904714 / 0.893333 / 0.949007; 배율 처리 뒤 발 정렬은 LockBits 정수 행 복사, 발 정렬 재샘플링 없음.
머리 √(폭×높이) px 및 IDLE 대비: I 267.96(+0.00%) / W 267.90(-0.02%) / S 267.95(-0.01%) / H 267.98(+0.01%) / D 268.06(+0.04%)
측정: 귀·장발·베일·장식 제외, 투구는 보이는 셸~턱. 수동 ROI이므로 정밀 해부학 확정 아님; _work_JEAN/metrics.json·head_roi_final.png 참조.
발 y(alpha≥129): I/W/S/H/D=1220/1220/1220/1220/1220. 5장 1024×1280 RGBA·모서리 alpha0·x30..994 확인.
후처리: 생성 포즈에 경계 안쪽6px·휘도>185·채도<0.18 조건으로만 alpha0 처리, 제거 픽셀 I/W/S/H/D=1618/0/3313/0/0; sheets/JEAN_fringe_2x.png에 비교.
시트: sheets/JEAN_{poses,silhouette,face,battle250,before_after}.png. battle250은 IDLE 두개부 상단~발250px의 공통 배율이며 포즈별 확대 없음.
남은 결함: 새 IDLE의 베일 길이·치마 주름, STRIKE의 건틀릿/장식 세부가 유지 포즈와 미세하게 다름. 정숙 대기와 공격 간 체형 인상은 Root 재확인.
인계: 정지 시트 자체 확인만 수행. Assets 등록·Unity 실행·게임 검증·커밋·파일 삭제 없음. Root 독립 실측·최종 승인 대기.
