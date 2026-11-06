# SET · SD v12 H1 · 2026-11-05
상태: **검수 대기**
사용자 요청 반영: STRIKE는 뒤로 빠지지 않고 앞무릎 위로 체중을 싣고 어깨·허리를 회전해 곡도로 할퀴듯 베기.
N123: 적 단일 / 접촉 / 곡도 베기·출혈. P323: 직접 공격자 / 접촉 / 반격 베기, 자신 / 비접촉 / 흡혈.
U123: 적 전체 / 비접촉 / 출혈; 자신 / 비접촉 / 치유·반격 예산 갱신. SP 없음.
동작 근거: 승인 11차 바스테트 등록 STRIKE는 몸통 회전 참고만 사용. 한손 곡도·낮은 빈손·낮은 남성형 자세로 양손 발톱 실루엣과 구분.
프롬프트 요약: wolf-pelt bearded tan man, black hair/red-black-gold clothes; forward shoulder/waist rotation, one hooked sword raking slash right, low rear empty hand, no backward lean.
생성: STRIKE 최초1+재생성1(총2회); 후보1 머리 작음 → 후보2 머리 덩어리 확대·가로 범위 압축. 등록 IDLE은 외형, 등록 바스테트 STRIKE는 동작 참조.
유지: IDLE/WINDUP/HIT/DOWN 등록본 바이트 사본.
남은 검토: 곡도 끝 곡률·가슴 장식·망토 주름 세부차. 바스테트 대비 회전 강도와 전진감은 Root 최종 확인 필요.
방식: 내장 image_gen, transparent_background=true(바루나 제외). 이미지 프롬프트에 고유명 없이 외형·동작만 기재.
변환(전체 등방 배율): IDLE=1.000000, WINDUP=1.000000, STRIKE=0.840463, HIT=1.000000, DOWN=1.000000. 생성 캔버스 규격화/머리 보정 후 발 정렬은 LockBits 정수 행 복사만 사용.
머리 수동 ROI 폭×높이→√(w×h), 각 최종 IDLE 대비: IDLE 357.4×348.6→352.96(0.00%) / WINDUP 355.1×350.9→352.97(0.00%) / STRIKE 348.8×357.1→352.92(-0.01%) / HIT 367.2×339.3→353.01(+0.01%) / DOWN 364.9×341.6→353.05(+0.02%)
측정 근거: ../_work_SET/metrics.json 및 head_roi.png. 귀·장발·장식 제외, 가죽 투구는 보이는 셸~턱. 수동 경계 오차 포함·독립 최종 승인 아님.
규격: I/W/S/H/D 발 y(alpha≥129)=1220/1220/1220/1220/1220; 5장 1024×1280·8bit RGBA·모서리alpha0·x30..994.
후처리: 흰 스티커 외곽이 육안상 보이지 않아 경계 제거 미실시. 생성 알파 보존.
시트: sheets/SET_{poses,silhouette,face,battle250,before_after}.png. 250px는 IDLE 두개부 상단~발 기준 공통 배율.
자체 확인: 포즈·실루엣·원화 얼굴·250px·전후 시트. Assets/Library 쓰기·Unity 실행·게임 검증·커밋·파일 삭제 없음.
