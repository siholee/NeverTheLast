# SURTR · SD v12 H1 · 2026-11-05
상태: **검수 대기**
사용자 요청 반영: 5포즈 모두 원화의 느슨한 S형 머리 결·적당한 정수리 볼륨·작은 아호게 복원. 촘촘한 고리 곱슬은 피함.
N81: 적 단일 / 접촉 / 양손 대검 베기. U81: 적 전체 / 비접촉 / 베기, 자신 체력 비용.
P281: 실제 직접 공격 피해 후 아군 전체 / 비접촉 / 보호막. SP 없음.
행동: IDLE 어깨 대검·WINDUP 뒤로 올리기·STRIKE 우측 내려베기·HIT 움찔·DOWN 대검을 짚고 무릎. 원소/VFX는 그리지 않음.
프롬프트 요약: change red hair only to loose broad shallow S-waves, modest airy crown, small cowlick, no ringlets; preserve pose/black-red-gold coat/giant sword, painted detail/right-facing.
생성: 5포즈 각 최초1회, 재생성0회(총5회). 등록 IDLE+각 등록 포즈+Portrait 참조, 새 IDLE을 나머지4장 머리 가이드로 추가.
유지: 5포즈 모두 생성본. 의상·무기·동작은 등록본을 기준으로 유지.
남은 검토: 원화보다 장발의 웨이브 선이 선명함. 머리 결/볼륨의 최종 수용 여부, 검 끝·옷 금장 세부 원근 차이는 Root 확인 필요.
방식: 내장 image_gen, transparent_background=true(바루나 제외). 이미지 프롬프트에 고유명 없이 외형·동작만 기재.
변환(전체 등방 배율): IDLE=0.845811, WINDUP=0.852050, STRIKE=0.852050, HIT=0.852050, DOWN=0.849376. 생성 캔버스 규격화/머리 보정 후 발 정렬은 LockBits 정수 행 복사만 사용.
머리 수동 ROI 폭×높이→√(w×h), 각 최종 IDLE 대비: IDLE 335.5×304.8→319.80(0.00%) / WINDUP 330.5×309.7→319.92(+0.04%) / STRIKE 332.4×307.8→319.86(+0.02%) / HIT 340.8×300.4→319.93(+0.04%) / DOWN 315.5×324.5→319.95(+0.05%)
측정 근거: ../_work_SURTR/metrics.json 및 head_roi.png. 귀·장발·장식 제외, 가죽 투구는 보이는 셸~턱. 수동 경계 오차 포함·독립 최종 승인 아님.
규격: I/W/S/H/D 발 y(alpha≥129)=1220/1220/1220/1220/1220; 5장 1024×1280·8bit RGBA·모서리alpha0·x30..994.
후처리: 흰 스티커 외곽이 육안상 보이지 않아 경계 제거 미실시. 생성 알파 보존.
시트: sheets/SURTR_{poses,silhouette,face,battle250,before_after}.png. 250px는 IDLE 두개부 상단~발 기준 공통 배율.
자체 확인: 포즈·실루엣·원화 얼굴·250px·전후 시트. Assets/Library 쓰기·Unity 실행·게임 검증·커밋·파일 삭제 없음.
