# 트리아리 (TRIARII / 11032) · SD 12차 H2
상태: **검수 대기** · 2026-11-05
사용자 요청: STRIKE 전진 찌르기, 하스타티와 실루엣 구분.
반영: 앞발 디딤·상체 좌측 전진. 가장 낮은 압축 자세·허리 높이 수평 창·짧은 황토 망토·금속 정강이 보호구.
N 일반행동: 적 1명 / 접촉 / 창 찌르기 (20_codes·60_enemies 대조).
U 최후의 창: 적 1명 / 접촉 / 찌르기 · P 최후의 방벽: 자신 / 비접촉 / 시작 보호막. SP 없음.
키트 해석: 일반행동 접촉 찌르기 우선. U의 바위 부착을 그림 VFX로 추가하지 않음.
유지 포즈: IDLE·WINDUP·HIT·DOWN는 Assets 등록본 바이트 그대로 복사(해시 대조). 5장 좌향·눈 완전 차폐 유지.
생성: 내장 image_gen·transparent background, 수정 포즈 1장 생성·품질 재생성 0회. IDLE 참조, 추가 소환체 없음.
실제 프롬프트: _work_TRIARII/generation_request.json. 이름 없이 시각 특징·동작·원본 의상 고정·좌향·무배경·무VFX를 지시.
처리: 생성 원본 1122×1402는 _work에 보존. 균등 배율 0.86로 1024×1280 캔버스/머리 크기 보정 후 LockBits 정수 행복사 dy=55 (발 정렬 재샘플링 없음). 흰 외곽 삭제 미적용.
규격 실측: 5장 1024×1280·8bit RGBA·모서리 alpha=0·alpha≥129 x=30..994 이내.
발 y(alpha≥129): IDLE 1220 · WINDUP 1220 · STRIKE 1220 · HIT 1220 · DOWN 1220.
머리 ROI w×h(px): IDLE 244.5×271.0 · WINDUP 261.2×253.7 · STRIKE 254.9×263.2 · HIT 248.0×256.0 · DOWN 246.8×268.4.
머리 √(w×h) px / IDLE 편차: IDLE 257.39(+0.00%) · WINDUP 257.39(+0.00%) · STRIKE 259.00(+0.62%) · HIT 251.97(-2.11%) · DOWN 257.39(+0.00%).
측정: 투구 셸~턱, 돌기·뺨/목가리개 끝 제외. 변경 포즈 직접 측정/2배 대조; 복사본 ROI는 기존 실측과 현재 소스 해시 일치 확인. measurements.json·head_roi_2x.png 보존.
시트: sheets/TRIARII_{poses|silhouette|face|battle250|before_after}.png. battle250은 IDLE 투구 셸~발=250px의 동일 배율 정적 비교.
남은 확인: 기존 HIT 머리 -2.11%는 허용 범위이며 그대로 복사. 투구 턱선·금속띠 세부 차이와 실제 동작 연결은 Root 검수 필요. Assets/Library 쓰기·Unity 실행·커밋·삭제 없음. 최종 미술 승인/Play 검증 아님.
