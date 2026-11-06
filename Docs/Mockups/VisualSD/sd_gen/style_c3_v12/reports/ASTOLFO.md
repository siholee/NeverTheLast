# ASTOLFO · SD v12 H1 · 2026-11-05
상태: **검수 대기**
사용자 요청 반영: WINDUP은 깃창을 뒤로 당기고 허리를 감은 낮은 준비; STRIKE는 양손으로 찌르며 앞무릎·상체를 함께 조금 전진.
N162: 적 전체 / 비접촉 / 무피해 도발·피해 감소·행동 가속.
P362: 자신 / 비접촉 / 타겟 우선도 감소·소모품 보존. U162: 아군 전체 / 비접촉 / 추가행동·반격 치피 지원·치유. SP 없음.
키트 차이: 사용자 최우선 요청에 따라 찌르기 그림 채택. 실제 N은 비공격이며 피해·판정은 변경하지 않음.
프롬프트 요약: ivory hair/cyan streak/braid/eyes, silver-blue-gold armor, white scarf; low coiled two-handed banner spear anticipation / forward-weighted thrust, full right-facing chibi, no effects.
생성: WINDUP·STRIKE 각 최초1회, 재생성0회(총2회). 등록본 IDLE 참조.
유지: IDLE/HIT/DOWN 등록본 바이트 사본.
남은 검토: 창이 가로 캔버스에 맞춰 짧아지고, STRIKE 깃발의 흐름·끝 모양이 IDLE과 달라짐. 상체 전진 강도는 Root 확인 필요.
방식: 내장 image_gen, transparent_background=true(바루나 제외). 이미지 프롬프트에 고유명 없이 외형·동작만 기재.
변환(전체 등방 배율): IDLE=1.000000, WINDUP=0.883244, STRIKE=0.883244, HIT=1.000000, DOWN=1.000000. 생성 캔버스 규격화/머리 보정 후 발 정렬은 LockBits 정수 행 복사만 사용.
머리 수동 ROI 폭×높이→√(w×h), 각 최종 IDLE 대비: IDLE 438.7×323.9→376.95(0.00%) / WINDUP 421.0×337.5→376.96(0.00%) / STRIKE 425.8×333.7→376.95(0.00%) / HIT 449.8×315.6→376.77(-0.05%) / DOWN 433.4×328.1→377.09(+0.04%)
측정 근거: ../_work_ASTOLFO/metrics.json 및 head_roi.png. 귀·장발·장식 제외, 가죽 투구는 보이는 셸~턱. 수동 경계 오차 포함·독립 최종 승인 아님.
규격: I/W/S/H/D 발 y(alpha≥129)=1220/1220/1220/1220/1220; 5장 1024×1280·8bit RGBA·모서리alpha0·x30..994.
후처리: 흰 스티커 외곽이 육안상 보이지 않아 경계 제거 미실시. 생성 알파 보존.
시트: sheets/ASTOLFO_{poses,silhouette,face,battle250,before_after}.png. 250px는 IDLE 두개부 상단~발 기준 공통 배율.
자체 확인: 포즈·실루엣·원화 얼굴·250px·전후 시트. Assets/Library 쓰기·Unity 실행·게임 검증·커밋·파일 삭제 없음.
