# BASTET · 바스테트 · SD v11 F1
수정일: 2026-11-05 · 상태: **검수 대기**
사용자 요청 반영: 잔 STRIKE를 몸통 회전 기준으로 참조. 오른손 발톱을 어깨 높이로 가로질러 뻗고 반대 손은 낮게 회수. 세트의 낮은 베기와 윤곽 분리.
N122: 단일 적 / 접촉 / 짧은 발톱 타격.
P322: 경호 자격 아군·최종 대상 / 비접촉 / 대상 교체.
U122: 반격당한 단일 적 / 접촉 / 추가 타격; 반격 요원 / 비접촉 / 치유. SP 없음.
키트 차이/해석: 사용자 할퀴기 요청을 발톱의 방향과 팔 신전으로 표현. 별도 궤적 VFX 없음.
생성: 내장 image_gen, transparent_background=true, 등록본 IDLE을 외형 마스터로 사용.
실제 프롬프트 요약: dark-tan adult woman, black-gold cat helmet, long black hair, amber eyes; right shoulder/hip driven forward, high right-hand crescent claw rake; left blade low; compact painterly chibi, screen right, no trails.
재생성: STRIKE 0회(후보1); 총 생성1회.
유지 포즈: 등록본을 _work_BASTET/registered_*.png에 원본 복사. 최종 5장에는 전체 등방 크기 보정만 적용(아스톨포 유지 4장은 원본 그대로).
변환: I/W/S/H/D 배율 0.910765 / 0.904329 / 0.888913 / 0.930932 / 0.977194; 배율 처리 뒤 발 정렬은 LockBits 정수 행 복사, 발 정렬 재샘플링 없음.
머리 √(폭×높이) px 및 IDLE 대비: I 300.09(+0.00%) / W 300.05(-0.01%) / S 299.91(-0.06%) / H 300.01(-0.03%) / D 300.08(-0.00%)
측정: 귀·장발·베일·장식 제외, 투구는 보이는 셸~턱. 수동 ROI이므로 정밀 해부학 확정 아님; _work_BASTET/metrics.json·head_roi_final.png 참조.
발 y(alpha≥129): I/W/S/H/D=1220/1220/1220/1220/1220. 5장 1024×1280 RGBA·모서리 alpha0·x30..994 확인.
후처리: 생성 포즈에 경계 안쪽6px·휘도>185·채도<0.18 조건으로만 alpha0 처리, 제거 픽셀 I/W/S/H/D=0/0/428/0/0; sheets/BASTET_fringe_2x.png에 비교.
시트: sheets/BASTET_{poses,silhouette,face,battle250,before_after}.png. battle250은 IDLE 두개부 상단~발250px의 공통 배율이며 포즈별 확대 없음.
남은 결함: 전방 발톱 날 수·곡률과 술 장식이 등록본과 미세하게 다름. 회전감·전방 손목 각도 최종 검수 필요.
인계: 정지 시트 자체 확인만 수행. Assets 등록·Unity 실행·게임 검증·커밋·파일 삭제 없음. Root 독립 실측·최종 승인 대기.
