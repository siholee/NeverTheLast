# SHI · SD v12 H1 · 2026-11-05
상태: **검수 대기**
사용자 요청 반영: STRIKE는 검 손잡이를 오른쪽 앞으로 뽑는 발도 중간. 왼쪽 칼집 입구에 칼날의 뾰족한 끝이 닿는 순간, 겨누기·돌진·완료 후 들고 서기 없음.
N2/특수: 적 단일 / 비접촉 / 베기. P202: 처치 가능한 적 단일 / 비접촉 / 특수행동 예약.
U2: 적 단일/전체 / 비접촉 / 추가·분배 피해(상시형).
초월자 톤: 차분한 반개안·제자리 접지·작은 팔 동작, 소매/머리의 과장된 휘날림 억제. 키트 베기는 사용자 요청의 발도 순간으로 표현.
프롬프트 요약: black updo/red flower/tassel, charcoal-red robe/turquoise clasp; pull hilt right, blade point left touching sheath mouth, quiet upright stance, no lunge/effects.
생성: STRIKE 최초1+재생성1(총2회); 후보1 칼끝이 입구에서 각져 보임 → 후보2 뾰족한 칼끝·칼집 길이 보강. 등록 IDLE 참조.
유지: IDLE/WINDUP/HIT/DOWN 등록본 바이트 사본.
남은 검토: 250px에서 칼끝과 입구 접점이 매우 작아 확대 확인 필요. 유지 IDLE/WINDUP/HIT 실루엣 차이가 작음. DOWN 페이드 게임 검증은 본 작업 범위 밖.
방식: 내장 image_gen, transparent_background=true(바루나 제외). 이미지 프롬프트에 고유명 없이 외형·동작만 기재.
변환(전체 등방 배율): IDLE=1.000000, WINDUP=1.000000, STRIKE=0.897504, HIT=1.000000, DOWN=1.000000. 생성 캔버스 규격화/머리 보정 후 발 정렬은 LockBits 정수 행 복사만 사용.
머리 수동 ROI 폭×높이→√(w×h), 각 최종 IDLE 대비: IDLE 490.0×410.0→448.22(0.00%) / WINDUP 490.0×406.0→446.03(-0.49%) / STRIKE 490.0×410.1→448.27(+0.01%) / HIT 487.0×407.0→445.21(-0.67%) / DOWN 497.0×410.0→451.41(+0.71%)
측정 근거: ../_work_SHI/metrics.json 및 head_roi.png. 귀·장발·장식 제외, 가죽 투구는 보이는 셸~턱. 수동 경계 오차 포함·독립 최종 승인 아님.
규격: I/W/S/H/D 발 y(alpha≥129)=1220/1220/1220/1220/1220; 5장 1024×1280·8bit RGBA·모서리alpha0·x30..994.
후처리: 흰 스티커 외곽이 육안상 보이지 않아 경계 제거 미실시. 생성 알파 보존.
시트: sheets/SHI_{poses,silhouette,face,battle250,before_after}.png. 250px는 IDLE 두개부 상단~발 기준 공통 배율.
자체 확인: 포즈·실루엣·원화 얼굴·250px·전후 시트. Assets/Library 쓰기·Unity 실행·게임 검증·커밋·파일 삭제 없음.
