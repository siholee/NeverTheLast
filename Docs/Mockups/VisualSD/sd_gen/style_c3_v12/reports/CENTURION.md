# 센츄리온 (CENTURION / 11034) · SD 12차 H2
상태: **검수 대기** · 2026-11-05
사용자 요청: STRIKE 전진 찌르기, 하스타티와 실루엣 구분.
반영: 앞발로 체중 이동·상체 회전/전진. 어깨 높이의 높은 창·들린 뒷팔꿈치·세로로 떨어지는 황토 망토·짙은 철갑.
N 일반행동: 적 1명 / 접촉 / 창 찌르기 (20_codes·60_enemies 대조).
U 백인대 돌격: 적 1명 / 접촉 / 찌르기 · P 백인대의 방벽: 자신 / 비접촉 / 시작 보호막. SP 없음.
키트 해석: 현행 tier:normal 익명 병과이므로 눈 차폐 유지. 일반행동 접촉 찌르기 우선.
유지 포즈: IDLE·WINDUP·HIT·DOWN는 Assets 등록본 바이트 그대로 복사(해시 대조). 5장 좌향·눈 완전 차폐 유지.
생성: 내장 image_gen·transparent background, 수정 포즈 1장 생성·품질 재생성 0회. IDLE 참조, 추가 소환체 없음.
실제 프롬프트: _work_CENTURION/generation_request.json. 이름 없이 시각 특징·동작·원본 의상 고정·좌향·무배경·무VFX를 지시.
처리: 생성 원본 1122×1402는 _work에 보존. 균등 배율 0.888로 1024×1280 캔버스/머리 크기 보정 후 LockBits 정수 행복사 dy=32 (발 정렬 재샘플링 없음). 흰 외곽 삭제 미적용.
규격 실측: 5장 1024×1280·8bit RGBA·모서리 alpha=0·alpha≥129 x=30..994 이내.
발 y(alpha≥129): IDLE 1220 · WINDUP 1220 · STRIKE 1220 · HIT 1220 · DOWN 1220.
머리 ROI w×h(px): IDLE 267.2×285.0 · WINDUP 269.3×282.9 · STRIKE 266.4×286.0 · HIT 264.0×280.0 · DOWN 274.2×277.8.
머리 √(w×h) px / IDLE 편차: IDLE 276.00(+0.00%) · WINDUP 276.00(+0.00%) · STRIKE 276.04(+0.01%) · HIT 271.88(-1.49%) · DOWN 276.00(+0.00%).
측정: 투구 셸~턱, 돌기·뺨/목가리개 끝 제외. 변경 포즈 직접 측정/2배 대조; 복사본 ROI는 기존 실측과 현재 소스 해시 일치 확인. measurements.json·head_roi_2x.png 보존.
시트: sheets/CENTURION_{poses|silhouette|face|battle250|before_after}.png. battle250은 IDLE 투구 셸~발=250px의 동일 배율 정적 비교.
남은 확인: 팔꿈치/흉갑 연결과 투구 음각의 미세 차이. 실제 전진 읽힘·동작 연결은 Root 검수 필요. Assets/Library 쓰기·Unity 실행·커밋·삭제 없음. 최종 미술 승인/Play 검증 아님.
