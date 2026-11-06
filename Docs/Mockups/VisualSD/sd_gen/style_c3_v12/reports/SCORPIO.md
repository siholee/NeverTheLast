# 스콜피오 (SCORPIO / 11036) · SD 12차 H2
상태: **검수 대기** · 2026-11-05
사용자 요청: WINDUP을 키트 무기의 장전 동작으로 변경.
반영: 석궁을 낮게 대각선으로 내리고 왼손으로 받침. 오른손으로 V자 활시위를 후방 걸쇠 쪽으로 당기며 앞무릎을 굽힘. 크랭크 회전 제거.
N 일반행동: 적 1명 / 비접촉 / 석궁 사격 (20_codes·60_enemies 대조).
U 관통 사격: 적 1명 / 비접촉 / 사격 · P 조준 사격: 자신 / 비접촉 / 동료 수 비례 상시 강화. SP 없음.
키트 해석: 일반행동 50003 비접촉 사격·방어력20% 무시. 그림에는 피해/VFX를 넣지 않음.
유지 포즈: IDLE·STRIKE·HIT·DOWN는 Assets 등록본 바이트 그대로 복사(해시 대조). 5장 좌향·눈 완전 차폐 유지.
생성: 내장 image_gen·transparent background, 수정 포즈 1장 생성·품질 재생성 0회(별도 첫 요청은 안전 필터 차단 1회, 이미지 미생성). IDLE 참조, 추가 소환체 없음.
실제 프롬프트: _work_SCORPIO/generation_request.json. 이름 없이 시각 특징·동작·원본 의상 고정·좌향·무배경·무VFX를 지시.
처리: 생성 원본 1122×1402는 _work에 보존. 균등 배율 0.83로 1024×1280 캔버스/머리 크기 보정 후 LockBits 정수 행복사 dy=33 (발 정렬 재샘플링 없음). 흰 외곽 삭제 미적용.
규격 실측: 5장 1024×1280·8bit RGBA·모서리 alpha=0·alpha≥129 x=30..994 이내.
발 y(alpha≥129): IDLE 1220 · WINDUP 1220 · STRIKE 1220 · HIT 1220 · DOWN 1220.
머리 ROI w×h(px): IDLE 267.9×296.8 · WINDUP 257.0×302.0 · STRIKE 275.3×288.7 · HIT 276.3×287.7 · DOWN 278.8×285.1.
머리 √(w×h) px / IDLE 편차: IDLE 281.94(+0.00%) · WINDUP 278.59(-1.19%) · STRIKE 281.94(-0.00%) · HIT 281.94(-0.00%) · DOWN 281.94(-0.00%).
측정: 투구 셸~턱, 돌기·뺨/목가리개 끝 제외. 변경 포즈 직접 측정/2배 대조; 복사본 ROI는 기존 실측과 현재 소스 해시 일치 확인. measurements.json·head_roi_2x.png 보존.
시트: sheets/SCORPIO_{poses|silhouette|face|battle250|before_after}.png. battle250은 IDLE 투구 셸~발=250px의 동일 배율 정적 비교.
남은 확인: 250px의 손/활시위 교차부가 작게 보임. 석궁 금속부 세부 차이와 0.14초 장전 인지는 Root 최종 검수 필요. Assets/Library 쓰기·Unity 실행·커밋·삭제 없음. 최종 미술 승인/Play 검증 아님.
