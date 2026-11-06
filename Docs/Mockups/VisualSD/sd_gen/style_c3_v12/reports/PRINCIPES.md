# 프린키페스 (PRINCIPES / 11031) · SD 12차 H2
상태: **검수 대기** · 2026-11-05
사용자 요청: STRIKE 전진 찌르기, 하스타티와 실루엣 구분.
반영: 하스타티의 앞발/상체 전진 원리 적용. 수평 창·짧은 삼각 베이지 망토·중간 높이 런지.
N 일반행동: 적 1명 / 접촉 / 창 찌르기 (20_codes·60_enemies 대조).
U 전열 돌파: 적 1명 / 접촉 / 찌르기 · P 중열의 방벽: 자신 / 비접촉 / 시작 보호막. SP 없음.
키트 해석: 일반행동을 우선한 접촉 찌르기. 무기·소품 추가 없음.
유지 포즈: IDLE·WINDUP·HIT·DOWN는 Assets 등록본 바이트 그대로 복사(해시 대조). 5장 좌향·눈 완전 차폐 유지.
생성: 내장 image_gen·transparent background, 수정 포즈 1장 생성·품질 재생성 0회. IDLE 참조, 추가 소환체 없음.
실제 프롬프트: _work_PRINCIPES/generation_request.json. 이름 없이 시각 특징·동작·원본 의상 고정·좌향·무배경·무VFX를 지시.
처리: 생성 원본 1122×1402는 _work에 보존. 균등 배율 0.86로 1024×1280 캔버스/머리 크기 보정 후 LockBits 정수 행복사 dy=45 (발 정렬 재샘플링 없음). 흰 외곽 삭제 미적용.
규격 실측: 5장 1024×1280·8bit RGBA·모서리 alpha=0·alpha≥129 x=30..994 이내.
발 y(alpha≥129): IDLE 1220 · WINDUP 1220 · STRIKE 1220 · HIT 1220 · DOWN 1220.
머리 ROI w×h(px): IDLE 261.1×264.0 · WINDUP 267.9×257.3 · STRIKE 259.0×266.4 · HIT 262.5×262.5 · DOWN 257.8×267.3.
머리 √(w×h) px / IDLE 편차: IDLE 262.52(+0.00%) · WINDUP 262.52(-0.00%) · STRIKE 262.69(+0.07%) · HIT 262.52(-0.00%) · DOWN 262.52(-0.00%).
측정: 투구 셸~턱, 돌기·뺨/목가리개 끝 제외. 변경 포즈 직접 측정/2배 대조; 복사본 ROI는 기존 실측과 현재 소스 해시 일치 확인. measurements.json·head_roi_2x.png 보존.
시트: sheets/PRINCIPES_{poses|silhouette|face|battle250|before_after}.png. battle250은 IDLE 투구 셸~발=250px의 동일 배율 정적 비교.
남은 확인: 투구 음각·옷단·가죽띠 세부 차이. 하스타티와 같은 군단병 체형은 유지하며 최종 구분감은 Root 검수 필요. Assets/Library 쓰기·Unity 실행·커밋·삭제 없음. 최종 미술 승인/Play 검증 아님.
