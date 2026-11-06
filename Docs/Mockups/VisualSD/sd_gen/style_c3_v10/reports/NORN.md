# NORN
상태: 검수 대기
수정일: 2026-11-05
근거: themeId117, 60_enemies.yaml codes/reworkKit + 20_codes.yaml (N/U=11123, P=21123).
N 아군 최저 체력비 대상·비접촉·시전자 최대HP 2.5% 치유; U 같은 대상 5.5% 치유; P 동료 수에 따른 가하는 피해 증가(공격 동작으로 해석하지 않음).
SP/소환수: 별도 SP 및 실제 소환 개체 없음.
키트 대응/기존 SD 구분: 적갈색 후드·모피·작은 세로 결정 케이지. 소품은 가슴에 두고 빈손을 높이 들어 준비→한 팔을 좌측으로 여는 치유.
콘셉트 차이: 키트 N/U는 아군 치유이므로 공격 없음. 양손 고리 전달형과 달리 한 손 개방/반대 손 소품 유지.
생성: 내장 image_gen, transparent background. IDLE 자체 확정 뒤 동일 IDLE 참조로 4포즈, 전부 좌향. 시각 특징만 프롬프트에 사용.
프롬프트 원문: _work_NORN/all_prompts.json 및 WINDUP_r1_prompt.txt
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/1/1/0/0
원본 정렬: 생성 픽셀을 재샘플링하지 않고 LockBits 정수 행 복사/캔버스 크롭·패딩. 원본·registered 5장 모두 발y1220,1024×1280,8bit RGBA,모서리alpha0,x30..994.
보이는 머리 수동 ROI 원본(px): IDLE=132×171,√150.2(0.0%); WINDUP=144×191,√165.8(+10.4%); STRIKE=89×109,√98.5(-34.4%); HIT=135×152,√143.2(-4.7%); DOWN=133×151,√141.7(-5.7%)
보이는 머리 수동 ROI registered(px): IDLE=87×112,√98.7(0.0%); WINDUP=86×113,√98.6(-0.1%); STRIKE=89×109,√98.5(-0.2%); HIT=93×105,√98.8(+0.1%); DOWN=92×105,√98.3(-0.4%)
머리는 가면/투구 셸 또는 노출된 머리 덩어리로 지정, 후드·긴 땋은머리·왕관 제외. 노출 수염 얼굴은 주 수염까지 보이는 덩어리로 측정. ROI/배율: measurements.json.
§5 보정: 원본 보존, registered 사본만 발바닥 중심 고정 균등 축소 후 정수 y이동. 표시 시트는 registered 기준, battle250은 IDLE 전체 alpha 높이250px 공통배율.
시트: poses/silhouette/face/battle250 + head_roi. 흰 스티커 외곽은 회색 배경 확대에서 미관찰, 조건부 alpha제거 미실시.
남은 결함: 포즈별 후드 접힘·결정 케이지/목걸이 세부 변동. 원본 STRIKE 머리가 작아 registered 사본 사용 필요.
검수 범위: PNG/정지 시트 자체 확인. 최종 승인·Assets 등록·Unity/모션/실기기 검수 미실시.
