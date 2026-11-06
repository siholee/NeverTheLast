# 벨리테스 (VELITES / 11035) · SD 12차 H2 HOLD 보정
상태: **검수 대기** · 2026-11-05 · root_review_pm.md h2 HOLD 반영
사용자 요청: 볼트 얹기는 250px에서 장전으로 읽히지 않음. 석궁 앞을 땅으로 숙이고 한 발로 등자를 밟으며 두 손으로 시위를 위로 끌어올리는 큰 장전.
반영: 좌측 앞발로 석궁 앞등자를 누름. 석궁을 아래로 세우고 양손으로 시위 중앙을 가슴 아래까지 끌어올림. 넓게 벌린 팔꿈치·길게 선 V자 시위로 세로 실루엣 구성.
N 일반행동: 적 1명 / 비접촉 / 석궁 사격 (20_codes·60_enemies 대조).
U 일제 투창: 적 1명 / 비접촉 / 사격 · P 산병 지원: 자신 / 비접촉 / 동료 수 비례 상시 강화. SP 없음.
키트 해석: U 명칭은 투창이나 일반행동 50003·원화·등록본 석궁을 우선. 사용자 요청에 따라 장전용 앞등자를 명확히 그림. 스콜피오의 수평 뒤로 당김과 구분.
유지: IDLE·STRIKE·HIT·DOWN 바이트 보존. PASS인 다른 5명의 PNG·시트·보고서 수정 없음(보정 전후 해시 대조). 좌향·눈 완전 차폐·회청 의상 유지.
생성: 이번 보정 내장 image_gen 1회/최대 2회, transparent background, 등록본 IDLE 참조. 추가 재생성 0회.
프롬프트 전문: _work_VELITES/fix_root_hold1/generation_request.json. 인물/작품명 없이 발 등자·양손 수직 당김·의상 고정·좌향·무배경을 지시.
처리: 원본 1122×1402 보존, 균등 배율 0.888로 머리/캔버스 보정 후 LockBits 정수 행복사 dy=+15. 발 정렬 재샘플링 없음. 흰 외곽 삭제 미적용.
규격 실측: 5장 1024×1280·8bit RGBA·모서리 alpha 0·alpha≥129 x=30..994 이내. 새 WINDUP bounds=(191,318)..(891,1220).
발 y(alpha≥129): IDLE 1220 · WINDUP 1220 · STRIKE 1220 · HIT 1220 · DOWN 1220.
머리 ROI w×h(px): IDLE 333.8×343.6 · WINDUP 316.0×354.0 · STRIKE 336.0×336.0 · HIT 338.6×338.6 · DOWN 326.9×350.8.
머리 √(w×h) px / IDLE 편차: IDLE 338.65(0%) · WINDUP 334.46(-1.24%) · STRIKE 336.00(-0.78%) · HIT 338.65(0%) · DOWN 338.65(0%).
측정: 보이는 투구 셸~턱, 돌기·뺨/목가리개 끝 제외. measurements.json·head_roi_2x.png·fix_root_hold1/head_grid1.png 보존. 이전 HOLD본은 _superseded_hold1/에 보존.
갱신 시트: poses·silhouette·face·battle250·before_after. before_after는 등록본→V12 HOLD→보정본 3열. battle250은 동일 IDLE 배율의 정적 비교.
남은 확인: 등자·시위 중앙과 금속 세부의 생성 차이, 0.14초 WINDUP의 큰 장전 읽힘/동작 연결은 Root 최종 검수 필요. Assets/Library 쓰기·Unity 실행·커밋·삭제 없음.
